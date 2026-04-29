#pragma warning disable CS1591
namespace OpenNFS.Client
{
    using System;

    /// <summary>
    /// NFSv4.0 file types surfaced by grouped client reply models.
    /// </summary>
    public enum OpenNfsV40FileType
    {
        RegularFile = 1,
        Directory = 2,
        BlockDevice = 3,
        CharacterDevice = 4,
        SymbolicLink = 5,
        Socket = 6,
        Fifo = 7,
        AttributeDirectory = 8,
        NamedAttribute = 9,
    }

    /// <summary>
    /// ACCESS bit flags surfaced by grouped NFSv4.0 access replies.
    /// </summary>
    [Flags]
    public enum OpenNfsV40AccessMask : uint
    {
        None = 0,
        Read = 1,
        Lookup = 2,
        Modify = 4,
        Extend = 8,
        Delete = 16,
        Execute = 32,
    }
}
#pragma warning restore CS1591
