namespace OpenNFS.Protocol.V3.Server.Procedures
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Rpc.Xdr;
    using OpenNFS.Server;
    using OpenNFS.Server.Abstractions.Capabilities;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    /// <summary>
    /// Handles NFSv3 <c>SETATTR</c> (RFC 1813 section 3.3.2).
    /// </summary>
    /// <remarks>
    /// When the host registers <see cref="INfsAttributeMutation"/>, size, mode, numeric ownership, and timestamp changes are
    /// routed through that seam. Hosts without the capability keep the legacy behavior: timestamp-only changes are applied to
    /// the resolved host path directly, and every other change (including an empty change set) is rejected with
    /// <c>NFS3ERR_NOTSUPP</c>.
    /// </remarks>
    internal sealed class Nfs3SetAttrProcedureHandler : Nfs3ProcedureHandlerBase<SETATTR3args, SETATTR3res>
    {
        private readonly OpenNfsServer _server;

        internal Nfs3SetAttrProcedureHandler(OpenNfsServer server)
            : base((uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_SETATTR, SETATTR3args.ReadFrom)
        {
            ArgumentNullException.ThrowIfNull(server);
            _server = server;
        }

        protected override async Task<SETATTR3res> HandleCoreAsync(SETATTR3args arguments, CancellationToken cancellationToken)
        {
            Nfs3ObjectResolution resolution =
                await Nfs3MetadataResolver.ResolveAsync(_server, arguments.@object, cancellationToken).ConfigureAwait(false);

            if (resolution.Status != nfsstat3.NFS3_OK)
            {
                return CreateFailureResult(
                    resolution.Status,
                    Nfs3MetadataResolver.CreateWeakCacheConsistencyData(null, null, null));
            }

            wcc_data currentWcc = Nfs3MetadataResolver.CreateWeakCacheConsistencyData(
                resolution.Target,
                resolution.PathInfo,
                resolution.PathInfo);

            if (!GuardMatches(arguments.guard, resolution.PathInfo))
            {
                return CreateFailureResult(nfsstat3.NFS3ERR_NOT_SYNC, currentWcc);
            }

            if (!TryResolveTimestampUpdatePlan(
                arguments.new_attributes,
                out bool setAccessTime,
                out DateTimeOffset accessTimeUtc,
                out bool setModificationTime,
                out DateTimeOffset modificationTimeUtc))
            {
                return CreateFailureResult(nfsstat3.NFS3ERR_INVAL, currentWcc);
            }

            INfsAttributeMutation? attributeMutation = _server.Capabilities.TrackedAttributeMutation;
            NfsPathInfo afterPathInfo;

            if (attributeMutation is null)
            {
                if (HasUnsupportedAttributeUpdates(arguments.new_attributes))
                {
                    return CreateFailureResult(nfsstat3.NFS3ERR_NOTSUPP, currentWcc);
                }

                if (!setAccessTime && !setModificationTime)
                {
                    return CreateFailureResult(nfsstat3.NFS3ERR_NOTSUPP, currentWcc);
                }

                if (!TryApplyLegacyTimestampUpdates(
                    resolution.PathInfo,
                    setAccessTime,
                    accessTimeUtc,
                    setModificationTime,
                    modificationTimeUtc,
                    out nfsstat3 failureStatus))
                {
                    return CreateFailureResult(failureStatus, currentWcc);
                }

                NfsGetPathInfoResponse afterPathInfoResponse =
                    await _server.Settings.InstrumentedFileSystem.GetPathInfoAsync(
                        new NfsGetPathInfoRequest(
                            resolution.Target!.SourcePath,
                            cancellationToken)).ConfigureAwait(false);
                afterPathInfo = afterPathInfoResponse.PathInfo;
            }
            else
            {
                sattr3? attributes = arguments.new_attributes;
                ulong? size = attributes?.size?.set_it == true ? attributes.size.size?.Value?.Value ?? 0UL : null;
                uint? mode = attributes?.mode?.set_it == true ? (attributes.mode.mode?.Value?.Value ?? 0U) & 0xFFFU : null;
                uint? userId = attributes?.uid?.set_it == true ? attributes.uid.uid?.Value?.Value ?? 0U : null;
                uint? groupId = attributes?.gid?.set_it == true ? attributes.gid.gid?.Value?.Value ?? 0U : null;

                if (size.HasValue)
                {
                    if (resolution.PathInfo!.Kind == NfsPathKind.Directory)
                    {
                        return CreateFailureResult(nfsstat3.NFS3ERR_ISDIR, currentWcc);
                    }

                    if (resolution.PathInfo.Kind != NfsPathKind.File)
                    {
                        return CreateFailureResult(nfsstat3.NFS3ERR_INVAL, currentWcc);
                    }
                }

                if (!size.HasValue
                    && !mode.HasValue
                    && !userId.HasValue
                    && !groupId.HasValue
                    && !setAccessTime
                    && !setModificationTime)
                {
                    afterPathInfo = resolution.PathInfo!;
                }
                else
                {
                    try
                    {
                        NfsSetAttributesResponse response = await attributeMutation.SetAttributesAsync(
                            new NfsSetAttributesRequest(
                                resolution.Target!.SourcePath,
                                resolution.PathInfo!.Kind,
                                size,
                                mode,
                                userId,
                                groupId,
                                setAccessTime ? accessTimeUtc : null,
                                setModificationTime ? modificationTimeUtc : null,
                                cancellationToken)).ConfigureAwait(false);
                        afterPathInfo = response?.PathInfo
                            ?? throw new InvalidOperationException("The attribute-mutation capability returned a null response.");
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        return CreateFailureResult(MapException(exception), currentWcc);
                    }
                }
            }

            return new SETATTR3res
            {
                status = nfsstat3.NFS3_OK,
                resok = new SETATTR3resok
                {
                    obj_wcc = Nfs3MetadataResolver.CreateWeakCacheConsistencyData(
                        resolution.Target,
                        resolution.PathInfo,
                        afterPathInfo),
                },
            };
        }

        protected override void WriteResult(SETATTR3res result, XdrWriter writer)
        {
            result.WriteTo(writer);
        }

        internal static nfsstat3 MapException(Exception exception)
        {
            return exception switch
            {
                UnauthorizedAccessException => nfsstat3.NFS3ERR_ACCES,
                FileNotFoundException => nfsstat3.NFS3ERR_STALE,
                DirectoryNotFoundException => nfsstat3.NFS3ERR_STALE,
                PathTooLongException => nfsstat3.NFS3ERR_NAMETOOLONG,
                IOException => nfsstat3.NFS3ERR_IO,
                NotSupportedException => nfsstat3.NFS3ERR_NOTSUPP,
                OverflowException => nfsstat3.NFS3ERR_FBIG,
                ArgumentException => nfsstat3.NFS3ERR_INVAL,
                _ => nfsstat3.NFS3ERR_SERVERFAULT,
            };
        }

        private static bool GuardMatches(sattrguard3? guard, NfsPathInfo? pathInfo)
        {
            if (guard is null || !guard.check)
            {
                return true;
            }

            if (pathInfo?.ChangeTimeUtc is null)
            {
                return false;
            }

            nfstime3 currentChangeTime = Nfs3MetadataResolver.CreateTime(pathInfo.ChangeTimeUtc.Value);
            return currentChangeTime.seconds?.Value == guard.obj_ctime?.seconds?.Value
                && currentChangeTime.nseconds?.Value == guard.obj_ctime?.nseconds?.Value;
        }

        private static bool HasUnsupportedAttributeUpdates(sattr3? attributes)
        {
            if (attributes is null)
            {
                return false;
            }

            return (attributes.mode?.set_it ?? false)
                || (attributes.uid?.set_it ?? false)
                || (attributes.gid?.set_it ?? false)
                || (attributes.size?.set_it ?? false);
        }

        private static bool TryResolveTimestampUpdatePlan(
            sattr3? attributes,
            out bool setAccessTime,
            out DateTimeOffset accessTimeUtc,
            out bool setModificationTime,
            out DateTimeOffset modificationTimeUtc)
        {
            setAccessTime = false;
            accessTimeUtc = DateTimeOffset.UtcNow;
            setModificationTime = false;
            modificationTimeUtc = DateTimeOffset.UtcNow;

            if (attributes is null)
            {
                return true;
            }

            if (!TryResolveTimestamp(attributes.atime?.set_it, attributes.atime?.atime_value, out setAccessTime, out accessTimeUtc))
            {
                return false;
            }

            if (!TryResolveTimestamp(attributes.mtime?.set_it, attributes.mtime?.mtime_value, out setModificationTime, out modificationTimeUtc))
            {
                return false;
            }

            return true;
        }

        private static bool TryResolveTimestamp(
            time_how? how,
            nfstime3? clientTime,
            out bool shouldSetTime,
            out DateTimeOffset timestampUtc)
        {
            shouldSetTime = false;
            timestampUtc = DateTimeOffset.UtcNow;

            time_how behavior = how ?? time_how.DONT_CHANGE;
            switch (behavior)
            {
                case time_how.DONT_CHANGE:
                    return true;

                case time_how.SET_TO_SERVER_TIME:
                    shouldSetTime = true;
                    timestampUtc = DateTimeOffset.UtcNow;
                    return true;

                case time_how.SET_TO_CLIENT_TIME:
                    if (clientTime is null)
                    {
                        return false;
                    }

                    shouldSetTime = true;
                    timestampUtc = ConvertWireTimeToUtc(clientTime);
                    return true;

                default:
                    return false;
            }
        }

        private static DateTimeOffset ConvertWireTimeToUtc(nfstime3 timestamp)
        {
            long seconds = timestamp.seconds?.Value ?? 0U;
            uint nanoseconds = timestamp.nseconds?.Value ?? 0U;
            if (nanoseconds > 999_999_999U)
            {
                nanoseconds = 999_999_999U;
            }

            DateTimeOffset baseTime = DateTimeOffset.FromUnixTimeSeconds(seconds);
            long ticks = nanoseconds / 100U;
            return baseTime.AddTicks(ticks);
        }

        private static bool TryApplyLegacyTimestampUpdates(
            NfsPathInfo? pathInfo,
            bool setAccessTime,
            DateTimeOffset accessTimeUtc,
            bool setModificationTime,
            DateTimeOffset modificationTimeUtc,
            out nfsstat3 failureStatus)
        {
            failureStatus = nfsstat3.NFS3_OK;

            if (pathInfo is null
                || !pathInfo.Exists
                || (pathInfo.Kind != NfsPathKind.File && pathInfo.Kind != NfsPathKind.Directory))
            {
                failureStatus = nfsstat3.NFS3ERR_NOTSUPP;
                return false;
            }

            try
            {
                string sourcePath = pathInfo.Path;
                if (pathInfo.Kind == NfsPathKind.Directory)
                {
                    if (!Directory.Exists(sourcePath))
                    {
                        failureStatus = nfsstat3.NFS3ERR_NOTSUPP;
                        return false;
                    }

                    if (setAccessTime)
                    {
                        Directory.SetLastAccessTimeUtc(sourcePath, accessTimeUtc.UtcDateTime);
                    }

                    if (setModificationTime)
                    {
                        Directory.SetLastWriteTimeUtc(sourcePath, modificationTimeUtc.UtcDateTime);
                    }
                }
                else
                {
                    if (!File.Exists(sourcePath))
                    {
                        failureStatus = nfsstat3.NFS3ERR_NOTSUPP;
                        return false;
                    }

                    if (setAccessTime)
                    {
                        File.SetLastAccessTimeUtc(sourcePath, accessTimeUtc.UtcDateTime);
                    }

                    if (setModificationTime)
                    {
                        File.SetLastWriteTimeUtc(sourcePath, modificationTimeUtc.UtcDateTime);
                    }
                }

                return true;
            }
            catch (UnauthorizedAccessException)
            {
                failureStatus = nfsstat3.NFS3ERR_ACCES;
                return false;
            }
            catch (FileNotFoundException)
            {
                failureStatus = nfsstat3.NFS3ERR_STALE;
                return false;
            }
            catch (DirectoryNotFoundException)
            {
                failureStatus = nfsstat3.NFS3ERR_STALE;
                return false;
            }
            catch (IOException)
            {
                failureStatus = nfsstat3.NFS3ERR_IO;
                return false;
            }
            catch (NotSupportedException)
            {
                failureStatus = nfsstat3.NFS3ERR_NOTSUPP;
                return false;
            }
            catch (ArgumentOutOfRangeException)
            {
                failureStatus = nfsstat3.NFS3ERR_INVAL;
                return false;
            }
            catch (ArgumentException)
            {
                failureStatus = nfsstat3.NFS3ERR_INVAL;
                return false;
            }
        }

        private static SETATTR3res CreateFailureResult(nfsstat3 status, wcc_data objectWeakCacheConsistency)
        {
            return new SETATTR3res
            {
                status = status,
                resfail = new SETATTR3resfail
                {
                    obj_wcc = objectWeakCacheConsistency,
                },
            };
        }
    }
}
