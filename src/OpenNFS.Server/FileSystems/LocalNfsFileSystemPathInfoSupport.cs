namespace OpenNFS.Server.FileSystems
{
    using System;
    using System.IO;
    using System.Text;
    using OpenNFS.Server.Abstractions;

    internal static class LocalNfsFileSystemPathInfoSupport
    {
        internal static NfsPathInfo GetPathInfo(string sourcePath)
        {
            if (TryGetAttributes(sourcePath, out FileAttributes attributes))
            {
                bool isSymbolicLink = (attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint;
                bool isDirectory = (attributes & FileAttributes.Directory) == FileAttributes.Directory;

                if (isSymbolicLink)
                {
                    FileSystemInfo fileSystemInfo = CreateFileSystemInfo(sourcePath, isDirectory);
                    string targetPath = fileSystemInfo.LinkTarget ?? string.Empty;
                    return new NfsPathInfo(
                        sourcePath,
                        NfsPathKind.SymbolicLink,
                        (ulong)Encoding.UTF8.GetByteCount(targetPath),
                        CreateAccessTime(fileSystemInfo),
                        CreateModificationTime(fileSystemInfo),
                        CreateChangeTime(fileSystemInfo));
                }

                if (isDirectory)
                {
                    DirectoryInfo directoryInfo = new DirectoryInfo(sourcePath);
                    return new NfsPathInfo(
                        sourcePath,
                        NfsPathKind.Directory,
                        0,
                        CreateAccessTime(directoryInfo),
                        CreateModificationTime(directoryInfo),
                        CreateChangeTime(directoryInfo));
                }

                FileInfo fileInfo = new FileInfo(sourcePath);
                return new NfsPathInfo(
                    sourcePath,
                    NfsPathKind.File,
                    (ulong)fileInfo.Length,
                    CreateAccessTime(fileInfo),
                    CreateModificationTime(fileInfo),
                    CreateChangeTime(fileInfo));
            }

            return new NfsPathInfo(sourcePath, NfsPathKind.Missing);
        }

        internal static FileSystemInfo CreateFileSystemInfo(string sourcePath)
        {
            bool isDirectory = TryGetAttributes(sourcePath, out FileAttributes attributes)
                && (attributes & FileAttributes.Directory) == FileAttributes.Directory;
            return CreateFileSystemInfo(sourcePath, isDirectory);
        }

        internal static FileSystemInfo CreateFileSystemInfo(string sourcePath, bool isDirectory)
        {
            return isDirectory
                ? new DirectoryInfo(sourcePath)
                : new FileInfo(sourcePath);
        }

        private static bool TryGetAttributes(string sourcePath, out FileAttributes attributes)
        {
            try
            {
                attributes = File.GetAttributes(sourcePath);
                return true;
            }
            catch (IOException)
            {
                attributes = default;
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                attributes = default;
                return false;
            }
        }

        private static DateTimeOffset CreateAccessTime(FileSystemInfo fileSystemInfo)
        {
            return NormalizeTimestamp(fileSystemInfo.LastAccessTimeUtc);
        }

        private static DateTimeOffset CreateModificationTime(FileSystemInfo fileSystemInfo)
        {
            return NormalizeTimestamp(fileSystemInfo.LastWriteTimeUtc);
        }

        private static DateTimeOffset CreateChangeTime(FileSystemInfo fileSystemInfo)
        {
            DateTime latestKnownMetadataTimeUtc =
                fileSystemInfo.LastWriteTimeUtc >= fileSystemInfo.CreationTimeUtc
                    ? fileSystemInfo.LastWriteTimeUtc
                    : fileSystemInfo.CreationTimeUtc;

            return NormalizeTimestamp(latestKnownMetadataTimeUtc);
        }

        private static DateTimeOffset NormalizeTimestamp(DateTime timestampUtc)
        {
            DateTime normalizedTimestampUtc = timestampUtc.Kind == DateTimeKind.Utc
                ? timestampUtc
                : timestampUtc.ToUniversalTime();

            if (normalizedTimestampUtc < DateTime.UnixEpoch)
            {
                normalizedTimestampUtc = DateTime.UnixEpoch;
            }

            return new DateTimeOffset(DateTime.SpecifyKind(normalizedTimestampUtc, DateTimeKind.Utc));
        }
    }
}
