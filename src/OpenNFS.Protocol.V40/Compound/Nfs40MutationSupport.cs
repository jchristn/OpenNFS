namespace OpenNFS.Protocol.V40.Compound
{
    using System;
    using System.IO;
    using System.Text;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Server;

    internal static class Nfs40MutationSupport
    {
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        internal static bitmap4 CreateEmptyAttributeSet()
        {
            return new bitmap4
            {
                Value = Array.Empty<uint>(),
            };
        }

        internal static change_info4 CreateChangeInfo(NfsPathInfo before, NfsPathInfo after)
        {
            ArgumentNullException.ThrowIfNull(before);
            ArgumentNullException.ThrowIfNull(after);

            return new change_info4
            {
                atomic = false,
                before = new changeid4
                {
                    Value = CreateChangeId(before),
                },
                after = new changeid4
                {
                    Value = CreateChangeId(after),
                },
            };
        }

        internal static bool IsDescendantPath(string candidatePath, string parentPath)
        {
            string normalizedCandidatePath = NormalizePath(candidatePath);
            string normalizedParentPath = NormalizePath(parentPath);

            if (!normalizedCandidatePath.StartsWith(normalizedParentPath, StringComparison.OrdinalIgnoreCase)
                || normalizedCandidatePath.Length <= normalizedParentPath.Length)
            {
                return false;
            }

            char separator = normalizedCandidatePath[normalizedParentPath.Length];
            return separator == Path.DirectorySeparatorChar || separator == Path.AltDirectorySeparatorChar;
        }

        internal static bool IsSamePath(string leftPath, string rightPath)
        {
            return string.Equals(
                NormalizePath(leftPath),
                NormalizePath(rightPath),
                StringComparison.OrdinalIgnoreCase);
        }

        internal static nfsstat4 MapCreateException(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            return exception switch
            {
                UnauthorizedAccessException => nfsstat4.NFS4ERR_ACCESS,
                PathTooLongException => nfsstat4.NFS4ERR_NAMETOOLONG,
                DirectoryNotFoundException => nfsstat4.NFS4ERR_NOENT,
                FileNotFoundException => nfsstat4.NFS4ERR_NOENT,
                NotSupportedException => nfsstat4.NFS4ERR_NOTSUPP,
                IOException => nfsstat4.NFS4ERR_IO,
                ArgumentException => nfsstat4.NFS4ERR_INVAL,
                _ => nfsstat4.NFS4ERR_SERVERFAULT,
            };
        }

        internal static nfsstat4 MapDeleteException(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            return exception switch
            {
                UnauthorizedAccessException => nfsstat4.NFS4ERR_ACCESS,
                PathTooLongException => nfsstat4.NFS4ERR_NAMETOOLONG,
                DirectoryNotFoundException => nfsstat4.NFS4ERR_NOENT,
                FileNotFoundException => nfsstat4.NFS4ERR_NOENT,
                NotSupportedException => nfsstat4.NFS4ERR_NOTSUPP,
                IOException => nfsstat4.NFS4ERR_IO,
                _ => nfsstat4.NFS4ERR_SERVERFAULT,
            };
        }

        internal static nfsstat4 MapHardLinkException(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            return exception switch
            {
                UnauthorizedAccessException => nfsstat4.NFS4ERR_ACCESS,
                PathTooLongException => nfsstat4.NFS4ERR_NAMETOOLONG,
                DirectoryNotFoundException => nfsstat4.NFS4ERR_NOENT,
                FileNotFoundException => nfsstat4.NFS4ERR_NOENT,
                NotSupportedException => nfsstat4.NFS4ERR_NOTSUPP,
                IOException => nfsstat4.NFS4ERR_IO,
                _ => nfsstat4.NFS4ERR_SERVERFAULT,
            };
        }

        internal static nfsstat4 MapWriteException(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            return exception switch
            {
                UnauthorizedAccessException => nfsstat4.NFS4ERR_ACCESS,
                PathTooLongException => nfsstat4.NFS4ERR_NAMETOOLONG,
                DirectoryNotFoundException => nfsstat4.NFS4ERR_NOENT,
                FileNotFoundException => nfsstat4.NFS4ERR_NOENT,
                NotSupportedException => nfsstat4.NFS4ERR_NOTSUPP,
                IOException => nfsstat4.NFS4ERR_IO,
                ArgumentException => nfsstat4.NFS4ERR_INVAL,
                OverflowException => nfsstat4.NFS4ERR_FBIG,
                _ => nfsstat4.NFS4ERR_SERVERFAULT,
            };
        }

        internal static nfsstat4 MapCommitException(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            return exception switch
            {
                UnauthorizedAccessException => nfsstat4.NFS4ERR_ACCESS,
                PathTooLongException => nfsstat4.NFS4ERR_NAMETOOLONG,
                DirectoryNotFoundException => nfsstat4.NFS4ERR_NOENT,
                FileNotFoundException => nfsstat4.NFS4ERR_NOENT,
                NotSupportedException => nfsstat4.NFS4ERR_NOTSUPP,
                IOException => nfsstat4.NFS4ERR_IO,
                ArgumentException => nfsstat4.NFS4ERR_INVAL,
                OverflowException => nfsstat4.NFS4ERR_FBIG,
                _ => nfsstat4.NFS4ERR_SERVERFAULT,
            };
        }

        internal static nfsstat4 MapRenameException(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            return exception switch
            {
                UnauthorizedAccessException => nfsstat4.NFS4ERR_ACCESS,
                PathTooLongException => nfsstat4.NFS4ERR_NAMETOOLONG,
                DirectoryNotFoundException => nfsstat4.NFS4ERR_NOENT,
                FileNotFoundException => nfsstat4.NFS4ERR_NOENT,
                NotSupportedException => nfsstat4.NFS4ERR_NOTSUPP,
                IOException => nfsstat4.NFS4ERR_IO,
                _ => nfsstat4.NFS4ERR_SERVERFAULT,
            };
        }

        internal static nfsstat4 MapAclException(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            return exception switch
            {
                UnauthorizedAccessException => nfsstat4.NFS4ERR_ACCESS,
                PathTooLongException => nfsstat4.NFS4ERR_NAMETOOLONG,
                DirectoryNotFoundException => nfsstat4.NFS4ERR_NOENT,
                FileNotFoundException => nfsstat4.NFS4ERR_NOENT,
                NotSupportedException => nfsstat4.NFS4ERR_NOTSUPP,
                IOException => nfsstat4.NFS4ERR_IO,
                ArgumentException => nfsstat4.NFS4ERR_INVAL,
                _ => nfsstat4.NFS4ERR_SERVERFAULT,
            };
        }

        internal static bool TryReadComponent(component4? component, out string componentValue, out nfsstat4 errorStatus)
        {
            return TryReadPathSegment(component?.Value?.Value?.Value, out componentValue, out errorStatus);
        }

        internal static bool TryReadLinkTarget(linktext4? value, out string targetPath, out nfsstat4 errorStatus)
        {
            byte[]? bytes = value?.Value;
            if (bytes is null)
            {
                targetPath = string.Empty;
                errorStatus = nfsstat4.NFS4ERR_BADXDR;
                return false;
            }

            try
            {
                targetPath = StrictUtf8.GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                targetPath = string.Empty;
                errorStatus = nfsstat4.NFS4ERR_INVAL;
                return false;
            }

            if (targetPath.Length < 1)
            {
                errorStatus = nfsstat4.NFS4ERR_INVAL;
                return false;
            }

            errorStatus = nfsstat4.NFS4_OK;
            return true;
        }

        private static ulong CreateChangeId(NfsPathInfo pathInfo)
        {
            DateTimeOffset changeTimeUtc = pathInfo.ChangeTimeUtc
                ?? pathInfo.ModificationTimeUtc
                ?? pathInfo.AccessTimeUtc
                ?? DateTimeOffset.UnixEpoch;
            return unchecked((ulong)changeTimeUtc.UtcTicks);
        }

        private static string NormalizePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static bool TryReadPathSegment(byte[]? bytes, out string componentValue, out nfsstat4 errorStatus)
        {
            if (bytes is null)
            {
                componentValue = string.Empty;
                errorStatus = nfsstat4.NFS4ERR_BADXDR;
                return false;
            }

            try
            {
                componentValue = StrictUtf8.GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                componentValue = string.Empty;
                errorStatus = nfsstat4.NFS4ERR_INVAL;
                return false;
            }

            if (componentValue.Length < 1)
            {
                errorStatus = nfsstat4.NFS4ERR_INVAL;
                return false;
            }

            if (string.Equals(componentValue, ".", StringComparison.Ordinal)
                || string.Equals(componentValue, "..", StringComparison.Ordinal)
                || componentValue.IndexOf(Path.DirectorySeparatorChar) >= 0
                || componentValue.IndexOf(Path.AltDirectorySeparatorChar) >= 0)
            {
                errorStatus = nfsstat4.NFS4ERR_BADNAME;
                return false;
            }

            errorStatus = nfsstat4.NFS4_OK;
            return true;
        }
    }
}
