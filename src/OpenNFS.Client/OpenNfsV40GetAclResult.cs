#pragma warning disable CS1591
namespace OpenNFS.Client
{
    using System;
    using System.Collections.Generic;

    public sealed class OpenNfsV40GetAclResult
    {
        private readonly OpenNfsV40AclEntry[] _entries;

        public OpenNfsV40GetAclResult(
            OpenNfsV40Status status,
            OpenNfsV40AclSupport? supportedAcls = null,
            IReadOnlyList<OpenNfsV40AclEntry>? entries = null)
        {
            Status = status;
            SupportedAcls = supportedAcls;
            _entries = CopyEntries(entries);
        }

        public OpenNfsV40Status Status { get; }

        public bool IsSuccess => Status == OpenNfsV40Status.Ok;

        public OpenNfsV40AclSupport? SupportedAcls { get; }

        public IReadOnlyList<OpenNfsV40AclEntry> Entries
        {
            get
            {
                return _entries;
            }
        }

        private static OpenNfsV40AclEntry[] CopyEntries(IReadOnlyList<OpenNfsV40AclEntry>? entries)
        {
            if (entries is null || entries.Count == 0)
            {
                return Array.Empty<OpenNfsV40AclEntry>();
            }

            OpenNfsV40AclEntry[] copy = new OpenNfsV40AclEntry[entries.Count];
            for (int index = 0; index < entries.Count; index++)
            {
                copy[index] = entries[index];
            }

            return copy;
        }
    }
}
#pragma warning restore CS1591
