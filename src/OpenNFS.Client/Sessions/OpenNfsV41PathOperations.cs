namespace OpenNFS.Client.Sessions
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using OpenNFS.Protocol.V41.Generated;

    /// <summary>
    /// Path-first COMPOUND-op builders for the NFSv4.1 client surface.
    /// </summary>
    /// <remarks>
    /// These helpers are intentionally server-independent: they only compose <see cref="nfs_argop4"/>
    /// arrays that match RFC 8881 §16 op semantics. Callers feed the result into
    /// <see cref="OpenNfsV41ClientSession.SendCompoundAsync"/> (or
    /// <see cref="OpenNfsV41ClientSession.TrySendCompoundAsync"/>) so the same builder can target any
    /// conforming v4.1 peer (knfsd, nfs-ganesha, OpenNFS.Server once its non-session op surface lands).
    /// <para>
    /// Path resolution mirrors the v3 mounted-session contract: paths are session-scoped, every
    /// COMPOUND re-resolves from <c>PUTROOTFH</c>, and no client-side handle cache is implied. Callers
    /// retain full COMPOUND fidelity — they can prepend or append additional ops to the produced array
    /// when a single round-trip needs to do more than one logical operation.
    /// </para>
    /// </remarks>
    public static class OpenNfsV41PathOperations
    {
        /// <summary>
        /// Returns a <see cref="bitmap4"/> with the supplied FATTR4 identifiers set.
        /// </summary>
        /// <param name="attributeIdentifiers">The FATTR4_* identifiers to include. Must be non-null.</param>
        /// <returns>An RFC 8881 §3.3.6 attribute bitmap.</returns>
        public static bitmap4 BuildAttributeMask(IReadOnlyList<ulong> attributeIdentifiers)
        {
            ArgumentNullException.ThrowIfNull(attributeIdentifiers);

            if (attributeIdentifiers.Count == 0)
            {
                return new bitmap4 { Value = Array.Empty<uint>() };
            }

            ulong highest = 0;
            for (int index = 0; index < attributeIdentifiers.Count; index++)
            {
                if (attributeIdentifiers[index] > highest)
                {
                    highest = attributeIdentifiers[index];
                }
            }

            int wordCount = (int)((highest / 32) + 1);
            uint[] words = new uint[wordCount];
            for (int index = 0; index < attributeIdentifiers.Count; index++)
            {
                ulong identifier = attributeIdentifiers[index];
                int wordIndex = (int)(identifier / 32);
                int bitIndex = (int)(identifier % 32);
                words[wordIndex] |= 1u << bitIndex;
            }

            return new bitmap4 { Value = words };
        }

        /// <summary>
        /// Returns a "stat-like" attribute mask covering the most commonly requested attributes:
        /// <c>FATTR4_TYPE</c>, <c>FATTR4_SIZE</c>, <c>FATTR4_FILEID</c>, <c>FATTR4_MODE</c>,
        /// <c>FATTR4_NUMLINKS</c>, <c>FATTR4_OWNER</c>, <c>FATTR4_OWNER_GROUP</c>,
        /// <c>FATTR4_TIME_ACCESS</c>, <c>FATTR4_TIME_METADATA</c>, and <c>FATTR4_TIME_MODIFY</c>.
        /// </summary>
        /// <returns>The mask.</returns>
        public static bitmap4 BuildStatLikeAttributeMask()
        {
            return BuildAttributeMask(new ulong[]
            {
                Nfs41Constants.FATTR4_TYPE,
                Nfs41Constants.FATTR4_SIZE,
                Nfs41Constants.FATTR4_FILEID,
                Nfs41Constants.FATTR4_MODE,
                Nfs41Constants.FATTR4_NUMLINKS,
                Nfs41Constants.FATTR4_OWNER,
                Nfs41Constants.FATTR4_OWNER_GROUP,
                Nfs41Constants.FATTR4_TIME_ACCESS,
                Nfs41Constants.FATTR4_TIME_METADATA,
                Nfs41Constants.FATTR4_TIME_MODIFY,
            });
        }

        /// <summary>
        /// Splits an NFS path into UTF-8 component bytes.
        /// </summary>
        /// <param name="path">A forward-slash-delimited path. Leading slashes and empty / single-dot
        /// segments are skipped. Double-dot segments are rejected because the v4.1 mounted-session
        /// contract forbids relative navigation.</param>
        /// <returns>Component bytes ready for embedding in <c>LOOKUP4args.objname</c>. An empty path
        /// or root path returns an empty list, indicating "the export root".</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> contains a
        /// <c>..</c> segment.</exception>
        public static IReadOnlyList<byte[]> SplitPathComponents(string path)
        {
            ArgumentNullException.ThrowIfNull(path);

            if (path.Length == 0)
            {
                return Array.Empty<byte[]>();
            }

            string[] rawSegments = path.Split('/');
            List<byte[]> components = new List<byte[]>(rawSegments.Length);
            for (int index = 0; index < rawSegments.Length; index++)
            {
                string segment = rawSegments[index];
                if (segment.Length == 0 || segment == ".")
                {
                    continue;
                }

                if (segment == "..")
                {
                    throw new ArgumentException(
                        "NFSv4.1 path-first operations do not allow '..' navigation.",
                        nameof(path));
                }

                components.Add(Encoding.UTF8.GetBytes(segment));
            }

            return components;
        }

        /// <summary>
        /// Builds a <c>PUTROOTFH</c> + <c>LOOKUP</c>-walk + <c>GETATTR</c> COMPOUND-op sequence for the
        /// supplied path and attribute mask. The result is intended to be passed to
        /// <see cref="OpenNfsV41ClientSession.SendCompoundAsync"/> which auto-injects the leading
        /// <c>SEQUENCE</c> op.
        /// </summary>
        /// <param name="path">The path to query, relative to the export root. An empty or "/" path
        /// queries the export root itself.</param>
        /// <param name="attributeMask">The attribute bitmap to request. Use
        /// <see cref="BuildStatLikeAttributeMask"/> for the common case.</param>
        /// <returns>The op sequence.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="attributeMask"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> contains '..' segments.</exception>
        public static IReadOnlyList<nfs_argop4> BuildGetAttributesOps(string path, bitmap4 attributeMask)
        {
            ArgumentNullException.ThrowIfNull(attributeMask);

            List<nfs_argop4> ops = BuildPathPrefixOps(path ?? string.Empty);
            ops.Add(new nfs_argop4
            {
                argop = nfs_opnum4.OP_GETATTR,
                opgetattr = new GETATTR4args { attr_request = attributeMask },
            });

            return ops;
        }

        /// <summary>
        /// Builds a <c>PUTROOTFH</c> + <c>LOOKUP</c>-walk + <c>READ</c> COMPOUND-op sequence for reading
        /// at the supplied path. The result is intended to be passed to
        /// <see cref="OpenNfsV41ClientSession.SendCompoundAsync"/>.
        /// </summary>
        /// <param name="path">The path to read, relative to the export root.</param>
        /// <param name="stateid">The state id authorizing the read. RFC 8881 §18.22.3 requires a valid
        /// state id (special-zero, special-anonymous, or an open / lock state id).</param>
        /// <param name="offset">The byte offset to begin reading at.</param>
        /// <param name="count">The maximum number of bytes to return.</param>
        /// <returns>The op sequence.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="stateid"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> contains '..' segments.</exception>
        public static IReadOnlyList<nfs_argop4> BuildReadOps(string path, stateid4 stateid, ulong offset, uint count)
        {
            ArgumentNullException.ThrowIfNull(stateid);

            List<nfs_argop4> ops = BuildPathPrefixOps(path ?? string.Empty);
            ops.Add(new nfs_argop4
            {
                argop = nfs_opnum4.OP_READ,
                opread = new READ4args
                {
                    stateid = stateid,
                    offset = new offset4 { Value = offset },
                    count = new count4 { Value = count },
                },
            });

            return ops;
        }

        /// <summary>
        /// Builds a <c>PUTROOTFH</c> + <c>LOOKUP</c>-walk + <c>WRITE</c> COMPOUND-op sequence for
        /// writing to the file at the supplied path. The result is intended to be passed to
        /// <see cref="OpenNfsV41ClientSession.SendCompoundAsync"/>.
        /// </summary>
        /// <param name="path">The file path, relative to the export root.</param>
        /// <param name="stateid">The state id authorizing the write.</param>
        /// <param name="offset">The byte offset to begin writing at.</param>
        /// <param name="stable">The requested write stability.</param>
        /// <param name="data">The bytes to write. The array is defensively copied.</param>
        /// <returns>The op sequence.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="stateid"/> or
        /// <paramref name="data"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> contains '..' segments.</exception>
        public static IReadOnlyList<nfs_argop4> BuildWriteOps(
            string path,
            stateid4 stateid,
            ulong offset,
            stable_how4 stable,
            byte[] data)
        {
            ArgumentNullException.ThrowIfNull(stateid);
            ArgumentNullException.ThrowIfNull(data);

            byte[] dataCopy = new byte[data.Length];
            Buffer.BlockCopy(data, 0, dataCopy, 0, data.Length);

            List<nfs_argop4> ops = BuildPathPrefixOps(path ?? string.Empty);
            ops.Add(new nfs_argop4
            {
                argop = nfs_opnum4.OP_WRITE,
                opwrite = new WRITE4args
                {
                    stateid = stateid,
                    offset = new offset4 { Value = offset },
                    stable = stable,
                    data = dataCopy,
                },
            });

            return ops;
        }

        /// <summary>
        /// Builds a <c>PUTROOTFH</c> + parent <c>LOOKUP</c>-walk + <c>OPEN</c> COMPOUND-op sequence
        /// for opening an existing file.
        /// </summary>
        /// <param name="path">The file path, relative to the export root.</param>
        /// <param name="clientId">The NFSv4.1 client id assigned by the server.</param>
        /// <param name="owner">The caller-stable open-owner identifier.</param>
        /// <param name="sequenceId">The open-owner sequence id.</param>
        /// <param name="shareAccess">The OPEN4_SHARE_ACCESS_* value.</param>
        /// <param name="shareDeny">The OPEN4_SHARE_DENY_* value.</param>
        /// <returns>The op sequence.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is empty, names
        /// only the export root, contains '..' segments, or when <paramref name="owner"/> is empty.</exception>
        public static IReadOnlyList<nfs_argop4> BuildOpenExistingOps(
            string path,
            ulong clientId,
            string owner,
            uint sequenceId,
            uint shareAccess,
            uint shareDeny)
        {
            List<nfs_argop4> ops = BuildParentPathPrefixOps(path, out byte[] finalComponent);
            ops.Add(BuildOpenOp(
                finalComponent,
                clientId,
                owner,
                sequenceId,
                shareAccess,
                shareDeny,
                new openflag4 { opentype = opentype4.OPEN4_NOCREATE }));

            return ops;
        }

        /// <summary>
        /// Builds a <c>PUTROOTFH</c> + parent <c>LOOKUP</c>-walk + create <c>OPEN</c> COMPOUND-op
        /// sequence for creating and opening a file.
        /// </summary>
        /// <param name="path">The file path, relative to the export root.</param>
        /// <param name="clientId">The NFSv4.1 client id assigned by the server.</param>
        /// <param name="owner">The caller-stable open-owner identifier.</param>
        /// <param name="sequenceId">The open-owner sequence id.</param>
        /// <param name="shareAccess">The OPEN4_SHARE_ACCESS_* value.</param>
        /// <param name="shareDeny">The OPEN4_SHARE_DENY_* value.</param>
        /// <param name="createMode">The create mode. Only <c>UNCHECKED4</c> and <c>GUARDED4</c>
        /// are supported by this convenience builder because exclusive create modes require verifier
        /// payloads.</param>
        /// <returns>The op sequence.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is empty, names
        /// only the export root, contains '..' segments, when <paramref name="owner"/> is empty, or
        /// when <paramref name="createMode"/> requires an explicit verifier.</exception>
        public static IReadOnlyList<nfs_argop4> BuildCreateAndOpenOps(
            string path,
            ulong clientId,
            string owner,
            uint sequenceId,
            uint shareAccess,
            uint shareDeny,
            createmode4 createMode = createmode4.GUARDED4)
        {
            if (createMode != createmode4.UNCHECKED4 && createMode != createmode4.GUARDED4)
            {
                throw new ArgumentException(
                    "BuildCreateAndOpenOps only supports UNCHECKED4 and GUARDED4 create modes.",
                    nameof(createMode));
            }

            List<nfs_argop4> ops = BuildParentPathPrefixOps(path, out byte[] finalComponent);
            ops.Add(BuildOpenOp(
                finalComponent,
                clientId,
                owner,
                sequenceId,
                shareAccess,
                shareDeny,
                new openflag4
                {
                    opentype = opentype4.OPEN4_CREATE,
                    how = new createhow4
                    {
                        mode = createMode,
                        createattrs = BuildEmptyAttributes(),
                    },
                }));

            return ops;
        }

        /// <summary>
        /// Builds a <c>PUTROOTFH</c> + <c>LOOKUP</c>-walk + <c>CLOSE</c> COMPOUND-op sequence for the
        /// supplied path and open stateid.
        /// </summary>
        /// <param name="path">The file path, relative to the export root.</param>
        /// <param name="stateid">The open state id to close.</param>
        /// <param name="sequenceId">The open-owner sequence id.</param>
        /// <returns>The op sequence.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="stateid"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> contains '..' segments.</exception>
        public static IReadOnlyList<nfs_argop4> BuildCloseOps(string path, stateid4 stateid, uint sequenceId)
        {
            ArgumentNullException.ThrowIfNull(stateid);

            List<nfs_argop4> ops = BuildPathPrefixOps(path ?? string.Empty);
            ops.Add(new nfs_argop4
            {
                argop = nfs_opnum4.OP_CLOSE,
                opclose = new CLOSE4args
                {
                    seqid = new seqid4 { Value = sequenceId },
                    open_stateid = stateid,
                },
            });

            return ops;
        }

        /// <summary>
        /// Builds a <c>PUTROOTFH</c> + <c>LOOKUP</c>-walk + <c>READDIR</c> COMPOUND-op sequence for
        /// listing the directory at the supplied path. The result is intended to be passed to
        /// <see cref="OpenNfsV41ClientSession.SendCompoundAsync"/>.
        /// </summary>
        /// <param name="path">The directory path, relative to the export root. An empty or "/" path
        /// targets the export root itself.</param>
        /// <param name="cookie">The opaque continuation cookie. Pass 0 for the first call.</param>
        /// <param name="cookieVerifier">The 8-byte cookie verifier. Pass an 8-byte zero array for the
        /// first call; for continuation calls pass the verifier value returned by the previous reply.</param>
        /// <param name="dircount">Maximum bytes the server may return for directory entry names + cookies.</param>
        /// <param name="maxcount">Maximum bytes the server may return overall, including attributes.</param>
        /// <param name="attributeMask">The per-entry attribute bitmap to request. Use
        /// <see cref="BuildStatLikeAttributeMask"/> for the common case.</param>
        /// <returns>The op sequence.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="cookieVerifier"/> or
        /// <paramref name="attributeMask"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="cookieVerifier"/> length is
        /// not exactly 8 bytes, or when <paramref name="path"/> contains '..' segments.</exception>
        public static IReadOnlyList<nfs_argop4> BuildReaddirOps(
            string path,
            ulong cookie,
            byte[] cookieVerifier,
            uint dircount,
            uint maxcount,
            bitmap4 attributeMask)
        {
            ArgumentNullException.ThrowIfNull(cookieVerifier);
            ArgumentNullException.ThrowIfNull(attributeMask);

            if (cookieVerifier.Length != 8)
            {
                throw new ArgumentException(
                    "The READDIR cookie verifier must be exactly 8 bytes per RFC 8881 §18.23.1.",
                    nameof(cookieVerifier));
            }

            byte[] verifierCopy = new byte[8];
            Buffer.BlockCopy(cookieVerifier, 0, verifierCopy, 0, 8);

            List<nfs_argop4> ops = BuildPathPrefixOps(path ?? string.Empty);
            ops.Add(new nfs_argop4
            {
                argop = nfs_opnum4.OP_READDIR,
                opreaddir = new READDIR4args
                {
                    cookie = new nfs_cookie4 { Value = cookie },
                    cookieverf = new verifier4 { Value = verifierCopy },
                    dircount = new count4 { Value = dircount },
                    maxcount = new count4 { Value = maxcount },
                    attr_request = attributeMask,
                },
            });

            return ops;
        }

        /// <summary>
        /// Builds a <c>PUTROOTFH</c> + parent <c>LOOKUP</c>-walk + <c>REMOVE</c> COMPOUND-op sequence
        /// for deleting a directory entry.
        /// </summary>
        /// <param name="path">The file or directory entry path, relative to the export root.</param>
        /// <returns>The op sequence.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is empty, names
        /// only the export root, or contains '..' segments.</exception>
        public static IReadOnlyList<nfs_argop4> BuildRemoveOps(string path)
        {
            IReadOnlyList<byte[]> components = SplitPathComponents(path ?? string.Empty);
            if (components.Count == 0)
            {
                throw new ArgumentException(
                    "NFSv4.1 REMOVE requires a named path under the export root.",
                    nameof(path));
            }

            List<nfs_argop4> ops = BuildPathPrefixOps(JoinPathComponents(components, components.Count - 1));
            ops.Add(new nfs_argop4
            {
                argop = nfs_opnum4.OP_REMOVE,
                opremove = new REMOVE4args
                {
                    target = new component4
                    {
                        Value = new utf8str_cs
                        {
                            Value = new utf8string { Value = components[components.Count - 1] },
                        },
                    },
                },
            });

            return ops;
        }

        private static List<nfs_argop4> BuildPathPrefixOps(string path)
        {
            IReadOnlyList<byte[]> components = SplitPathComponents(path);
            List<nfs_argop4> ops = new List<nfs_argop4>(components.Count + 2)
            {
                new nfs_argop4 { argop = nfs_opnum4.OP_PUTROOTFH },
            };

            for (int index = 0; index < components.Count; index++)
            {
                ops.Add(new nfs_argop4
                {
                    argop = nfs_opnum4.OP_LOOKUP,
                    oplookup = new LOOKUP4args
                    {
                        objname = new component4
                        {
                            Value = new utf8str_cs
                            {
                                Value = new utf8string { Value = components[index] },
                            },
                        },
                    },
                });
            }

            return ops;
        }

        private static List<nfs_argop4> BuildParentPathPrefixOps(string path, out byte[] finalComponent)
        {
            IReadOnlyList<byte[]> components = SplitPathComponents(path ?? string.Empty);
            if (components.Count == 0)
            {
                throw new ArgumentException(
                    "NFSv4.1 OPEN requires a named file path under the export root.",
                    nameof(path));
            }

            finalComponent = components[components.Count - 1];
            return BuildPathPrefixOps(JoinPathComponents(components, components.Count - 1));
        }

        private static nfs_argop4 BuildOpenOp(
            byte[] finalComponent,
            ulong clientId,
            string owner,
            uint sequenceId,
            uint shareAccess,
            uint shareDeny,
            openflag4 openHow)
        {
            ArgumentNullException.ThrowIfNull(owner);
            if (owner.Length == 0)
            {
                throw new ArgumentException("The NFSv4.1 open owner identifier must not be empty.", nameof(owner));
            }

            return new nfs_argop4
            {
                argop = nfs_opnum4.OP_OPEN,
                opopen = new OPEN4args
                {
                    seqid = new seqid4 { Value = sequenceId },
                    share_access = shareAccess,
                    share_deny = shareDeny,
                    owner = new open_owner4
                    {
                        Value = new state_owner4
                        {
                            clientid = new clientid4 { Value = clientId },
                            owner = Encoding.UTF8.GetBytes(owner),
                        },
                    },
                    openhow = openHow,
                    claim = new open_claim4
                    {
                        claim = open_claim_type4.CLAIM_NULL,
                        file = BuildComponent(finalComponent),
                    },
                },
            };
        }

        private static fattr4 BuildEmptyAttributes()
        {
            return new fattr4
            {
                attrmask = new bitmap4 { Value = Array.Empty<uint>() },
                attr_vals = new attrlist4 { Value = Array.Empty<byte>() },
            };
        }

        private static component4 BuildComponent(byte[] value)
        {
            return new component4
            {
                Value = new utf8str_cs
                {
                    Value = new utf8string { Value = value },
                },
            };
        }

        private static string JoinPathComponents(IReadOnlyList<byte[]> components, int count)
        {
            if (count <= 0)
            {
                return string.Empty;
            }

            string[] names = new string[count];
            for (int index = 0; index < count; index++)
            {
                names[index] = Encoding.UTF8.GetString(components[index]);
            }

            return string.Join("/", names);
        }
    }
}
