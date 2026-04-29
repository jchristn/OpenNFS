#pragma warning disable CS1591
namespace OpenNFS.Client
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Decoded NFSv4.0 attribute envelope surfaced by grouped client reply models.
    /// </summary>
    public sealed class OpenNfsV40Attributes
    {
        public OpenNfsV40Attributes(
            IReadOnlyList<uint>? supportedAttributeMaskWords = null,
            OpenNfsV40FileType? fileType = null,
            OpenNfsV40AclSupport? aclSupport = null,
            IReadOnlyList<OpenNfsV40AclEntry>? aclEntries = null,
            ulong? changeId = null,
            ulong? sizeBytes = null,
            ReadOnlyMemory<byte> fileHandle = default,
            string? owner = null,
            string? ownerGroup = null)
        {
            SupportedAttributeMaskWords = CopyWords(supportedAttributeMaskWords);
            FileType = fileType;
            AclSupport = aclSupport;
            AclEntries = CopyAclEntries(aclEntries);
            ChangeId = changeId;
            SizeBytes = sizeBytes;
            FileHandle = fileHandle.Length == 0
                ? ReadOnlyMemory<byte>.Empty
                : new ReadOnlyMemory<byte>(fileHandle.ToArray());
            Owner = owner;
            OwnerGroup = ownerGroup;
        }

        public IReadOnlyList<uint> SupportedAttributeMaskWords { get; }

        public OpenNfsV40FileType? FileType { get; }

        public OpenNfsV40AclSupport? AclSupport { get; }

        public IReadOnlyList<OpenNfsV40AclEntry> AclEntries { get; }

        public ulong? ChangeId { get; }

        public ulong? SizeBytes { get; }

        public ReadOnlyMemory<byte> FileHandle { get; }

        public string? Owner { get; }

        public string? OwnerGroup { get; }

        internal static IReadOnlyList<uint> CopyWords(IReadOnlyList<uint>? words)
        {
            if (words is null || words.Count == 0)
            {
                return Array.Empty<uint>();
            }

            uint[] copy = new uint[words.Count];
            for (int index = 0; index < words.Count; index++)
            {
                copy[index] = words[index];
            }

            return copy;
        }

        internal static IReadOnlyList<OpenNfsV40AclEntry> CopyAclEntries(IReadOnlyList<OpenNfsV40AclEntry>? entries)
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

    /// <summary>
    /// Typed NFSv4.0 GETATTR result surfaced by the grouped client API.
    /// </summary>
    public sealed class OpenNfsV40GetAttributesResult
    {
        public OpenNfsV40GetAttributesResult(OpenNfsV40Status status, OpenNfsV40Attributes? attributes = null)
        {
            Status = status;
            Attributes = attributes;
        }

        public OpenNfsV40Status Status { get; }

        public bool IsSuccess => Status == OpenNfsV40Status.Ok;

        public OpenNfsV40Attributes? Attributes { get; }
    }

    /// <summary>
    /// Typed NFSv4.0 ACCESS result surfaced by the grouped client API.
    /// </summary>
    public sealed class OpenNfsV40AccessResult
    {
        public OpenNfsV40AccessResult(
            OpenNfsV40Status status,
            OpenNfsV40AccessMask supportedAccess = OpenNfsV40AccessMask.None,
            OpenNfsV40AccessMask grantedAccess = OpenNfsV40AccessMask.None)
        {
            Status = status;
            SupportedAccess = supportedAccess;
            GrantedAccess = grantedAccess;
        }

        public OpenNfsV40Status Status { get; }

        public bool IsSuccess => Status == OpenNfsV40Status.Ok;

        public OpenNfsV40AccessMask SupportedAccess { get; }

        public OpenNfsV40AccessMask GrantedAccess { get; }
    }
}
#pragma warning restore CS1591
