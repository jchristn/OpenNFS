#pragma warning disable CS1591
namespace OpenNFS.Client
{
    using System;

    [Flags]
    public enum OpenNfsV40AclPermissionMask : uint
    {
        None = 0,
        ReadData = 1,
        WriteData = 2,
        AppendData = 4,
        ReadNamedAttributes = 8,
        WriteNamedAttributes = 16,
        Execute = 32,
        DeleteChild = 64,
        ReadAttributes = 128,
        WriteAttributes = 256,
        Delete = 65536,
        ReadAcl = 131072,
        WriteAcl = 262144,
        WriteOwner = 524288,
        Synchronize = 1048576,
        GenericRead = 1179777,
        GenericWrite = 1442054,
        GenericExecute = 1179808,
    }
}
#pragma warning restore CS1591
