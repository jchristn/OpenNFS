#pragma warning disable CS1591
namespace OpenNFS.Client
{
    using System;

    /// <summary>
    /// NFSv3 file types surfaced by grouped client reply models.
    /// </summary>
    public enum OpenNfsV3FileType
    {
        RegularFile = 1,
        Directory = 2,
        BlockDevice = 3,
        CharacterDevice = 4,
        SymbolicLink = 5,
        Socket = 6,
        Fifo = 7,
    }

    /// <summary>
    /// ACCESS bit flags surfaced by grouped NFSv3 access replies.
    /// </summary>
    [Flags]
    public enum OpenNfsV3AccessMask : uint
    {
        None = 0,
        Read = 1,
        Lookup = 2,
        Modify = 4,
        Extend = 8,
        Delete = 16,
        Execute = 32,
    }

    /// <summary>
    /// FSINFO property bit flags surfaced by grouped NFSv3 filesystem-info replies.
    /// </summary>
    [Flags]
    public enum OpenNfsV3FileSystemProperties : uint
    {
        None = 0,
        SupportsHardLinks = 1,
        SupportsSymbolicLinks = 2,
        Homogeneous = 8,
        CanSetTime = 16,
    }
}
#pragma warning restore CS1591
