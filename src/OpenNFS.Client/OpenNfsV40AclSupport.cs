#pragma warning disable CS1591
namespace OpenNFS.Client
{
    using System;

    [Flags]
    public enum OpenNfsV40AclSupport : uint
    {
        None = 0,
        AllowAcl = 1,
        DenyAcl = 2,
        AuditAcl = 4,
        AlarmAcl = 8,
    }
}
#pragma warning restore CS1591
