namespace OpenNFS.Protocol.V40.Compound
{
    using OpenNFS.Protocol.V40.Generated;

    internal readonly struct TryCreateAttributesResult
    {
        internal TryCreateAttributesResult(fattr4? attributes, nfsstat4 errorStatus)
        {
            Attributes = attributes;
            ErrorStatus = errorStatus;
        }

        internal fattr4? Attributes { get; }

        internal nfsstat4 ErrorStatus { get; }
    }

    internal readonly record struct Nfs40SpaceInfo(
        ulong TotalBytes,
        ulong FreeBytes)
    {
        public ulong AvailableBytes
        {
            get
            {
                return FreeBytes;
            }
        }

        public ulong UsedBytes
        {
            get
            {
                return TotalBytes >= FreeBytes ? TotalBytes - FreeBytes : 0UL;
            }
        }

        public ulong TotalFileSlots
        {
            get
            {
                return TotalBytes / 4096UL;
            }
        }

        public ulong FreeFileSlots
        {
            get
            {
                return FreeBytes / 4096UL;
            }
        }

        public ulong AvailableFileSlots
        {
            get
            {
                return FreeFileSlots;
            }
        }
    }
}
