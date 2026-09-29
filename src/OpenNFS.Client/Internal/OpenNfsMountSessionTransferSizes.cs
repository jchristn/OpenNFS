namespace OpenNFS.Client.Internal
{
    using System;

    /// <summary>
    /// Transfer sizes negotiated from an NFSv3 <c>FSINFO</c> reply for a mounted session.
    /// </summary>
    internal sealed class OpenNfsMountSessionTransferSizes
    {
        internal const int DefaultTransferSize = 64 * 1024;
        internal const int DefaultDirectorySize = 8 * 1024;
        internal const int MaximumTransferSize = 4 * 1024 * 1024;
        internal const int MaximumDatagramTransferSize = 32 * 1024;
        internal const int MaximumDirectorySize = 64 * 1024;

        internal OpenNfsMountSessionTransferSizes(int readSize, int writeSize, int directorySize, bool fromServer)
        {
            if (readSize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(readSize), readSize, "The read transfer size must be positive.");
            }

            if (writeSize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(writeSize), writeSize, "The write transfer size must be positive.");
            }

            if (directorySize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(directorySize), directorySize, "The directory transfer size must be positive.");
            }

            ReadSize = readSize;
            WriteSize = writeSize;
            DirectorySize = directorySize;
            FromServer = fromServer;
        }

        internal int ReadSize { get; }

        internal int WriteSize { get; }

        internal int DirectorySize { get; }

        internal bool FromServer { get; }

        internal static OpenNfsMountSessionTransferSizes CreateDefault(bool datagramCapable)
        {
            int transferSize = datagramCapable ? MaximumDatagramTransferSize : DefaultTransferSize;
            return new OpenNfsMountSessionTransferSizes(transferSize, transferSize, DefaultDirectorySize, fromServer: false);
        }

        internal static OpenNfsMountSessionTransferSizes FromFileSystemInfo(OpenNfsV3FileSystemInfoResult fileSystemInfo, bool datagramCapable)
        {
            ArgumentNullException.ThrowIfNull(fileSystemInfo);

            int ceiling = datagramCapable ? MaximumDatagramTransferSize : MaximumTransferSize;
            int readSize = Select(fileSystemInfo.ReadPreferredBytes, fileSystemInfo.ReadMaxBytes, ceiling);
            int writeSize = Select(fileSystemInfo.WritePreferredBytes, fileSystemInfo.WriteMaxBytes, ceiling);
            int directorySize = fileSystemInfo.DirectoryPreferredBytes > 0
                ? (int)Math.Min(fileSystemInfo.DirectoryPreferredBytes, (uint)MaximumDirectorySize)
                : DefaultDirectorySize;
            return new OpenNfsMountSessionTransferSizes(readSize, writeSize, directorySize, fromServer: true);
        }

        private static int Select(uint preferred, uint maximum, int ceiling)
        {
            long size = preferred > 0 ? preferred : (maximum > 0 ? maximum : DefaultTransferSize);
            if (maximum > 0 && size > maximum)
            {
                size = maximum;
            }

            if (size > ceiling)
            {
                size = ceiling;
            }

            return size < 1 ? DefaultTransferSize : (int)size;
        }
    }
}
