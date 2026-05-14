namespace OpenNFS.Protocol.V3.Server.Procedures
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Rpc.Xdr;
    using OpenNFS.Server;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

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

            if (HasUnsupportedAttributeUpdates(arguments.new_attributes))
            {
                return CreateFailureResult(nfsstat3.NFS3ERR_NOTSUPP, currentWcc);
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

            if (!setAccessTime && !setModificationTime)
            {
                return CreateFailureResult(nfsstat3.NFS3ERR_NOTSUPP, currentWcc);
            }

            if (!TryApplySupportedTimestampUpdates(
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
                await _server.Settings.FileSystem.GetPathInfoAsync(
                    new NfsGetPathInfoRequest(
                        resolution.Target!.SourcePath,
                        cancellationToken)).ConfigureAwait(false);

            return new SETATTR3res
            {
                status = nfsstat3.NFS3_OK,
                resok = new SETATTR3resok
                {
                    obj_wcc = Nfs3MetadataResolver.CreateWeakCacheConsistencyData(
                        resolution.Target,
                        resolution.PathInfo,
                        afterPathInfoResponse.PathInfo),
                },
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

            if (!TryResolveTimestamp(attributes.atime, out setAccessTime, out accessTimeUtc))
            {
                return false;
            }

            if (!TryResolveTimestamp(attributes.mtime, out setModificationTime, out modificationTimeUtc))
            {
                return false;
            }

            return true;
        }

        private static bool TryResolveTimestamp(
            set_atime? setTime,
            out bool shouldSetTime,
            out DateTimeOffset timestampUtc)
        {
            shouldSetTime = false;
            timestampUtc = DateTimeOffset.UtcNow;

            time_how behavior = setTime?.set_it ?? time_how.DONT_CHANGE;
            switch (behavior)
            {
                case time_how.DONT_CHANGE:
                    return true;

                case time_how.SET_TO_SERVER_TIME:
                    shouldSetTime = true;
                    timestampUtc = DateTimeOffset.UtcNow;
                    return true;

                case time_how.SET_TO_CLIENT_TIME:
                    if (setTime?.atime_value is null)
                    {
                        return false;
                    }

                    shouldSetTime = true;
                    timestampUtc = ConvertWireTimeToUtc(setTime.atime_value);
                    return true;

                default:
                    return false;
            }
        }

        private static bool TryResolveTimestamp(
            set_mtime? setTime,
            out bool shouldSetTime,
            out DateTimeOffset timestampUtc)
        {
            shouldSetTime = false;
            timestampUtc = DateTimeOffset.UtcNow;

            time_how behavior = setTime?.set_it ?? time_how.DONT_CHANGE;
            switch (behavior)
            {
                case time_how.DONT_CHANGE:
                    return true;

                case time_how.SET_TO_SERVER_TIME:
                    shouldSetTime = true;
                    timestampUtc = DateTimeOffset.UtcNow;
                    return true;

                case time_how.SET_TO_CLIENT_TIME:
                    if (setTime?.mtime_value is null)
                    {
                        return false;
                    }

                    shouldSetTime = true;
                    timestampUtc = ConvertWireTimeToUtc(setTime.mtime_value);
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

        private static bool TryApplySupportedTimestampUpdates(
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

        protected override void WriteResult(SETATTR3res result, XdrWriter writer)
        {
            result.WriteTo(writer);
        }
    }
}
