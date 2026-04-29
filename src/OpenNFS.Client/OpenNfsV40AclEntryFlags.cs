#pragma warning disable CS1591
namespace OpenNFS.Client
{
    using System;

    [Flags]
    public enum OpenNfsV40AclEntryFlags : uint
    {
        None = 0,
        FileInherit = 1,
        DirectoryInherit = 2,
        NoPropagateInherit = 4,
        InheritOnly = 8,
        SuccessfulAccess = 16,
        FailedAccess = 32,
        IdentifierGroup = 64,
    }
}
#pragma warning restore CS1591
