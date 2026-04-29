#pragma warning disable CS1591
namespace OpenNFS.Client
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// NFSv4.0 directory entry surfaced by grouped client reply models.
    /// </summary>
    public sealed class OpenNfsV40DirectoryEntry
    {
        public OpenNfsV40DirectoryEntry(ulong cookie, string name, OpenNfsV40Attributes? attributes = null)
        {
            ArgumentNullException.ThrowIfNull(name);

            Cookie = cookie;
            Name = name;
            Attributes = attributes;
        }

        public ulong Cookie { get; }

        public string Name { get; }

        public OpenNfsV40Attributes? Attributes { get; }
    }

    /// <summary>
    /// Typed NFSv4.0 LOOKUP result surfaced by the grouped client API.
    /// </summary>
    public sealed class OpenNfsV40LookupResult
    {
        public OpenNfsV40LookupResult(
            OpenNfsV40Status status,
            ReadOnlyMemory<byte> objectFileHandle = default,
            OpenNfsV40Attributes? objectAttributes = null)
        {
            Status = status;
            ObjectFileHandle = objectFileHandle.Length == 0
                ? ReadOnlyMemory<byte>.Empty
                : new ReadOnlyMemory<byte>(objectFileHandle.ToArray());
            ObjectAttributes = objectAttributes;
        }

        public OpenNfsV40Status Status { get; }

        public bool IsSuccess => Status == OpenNfsV40Status.Ok;

        public ReadOnlyMemory<byte> ObjectFileHandle { get; }

        public OpenNfsV40Attributes? ObjectAttributes { get; }
    }

    /// <summary>
    /// Typed NFSv4.0 READDIR result surfaced by the grouped client API.
    /// </summary>
    public sealed class OpenNfsV40ReadDirectoryResult
    {
        public OpenNfsV40ReadDirectoryResult(
            OpenNfsV40Status status,
            ReadOnlyMemory<byte> cookieVerifier = default,
            IReadOnlyList<OpenNfsV40DirectoryEntry>? entries = null,
            bool endOfFile = false)
        {
            Status = status;
            CookieVerifier = cookieVerifier.Length == 0
                ? ReadOnlyMemory<byte>.Empty
                : new ReadOnlyMemory<byte>(cookieVerifier.ToArray());
            Entries = CopyEntries(entries);
            EndOfFile = endOfFile;
        }

        public OpenNfsV40Status Status { get; }

        public bool IsSuccess => Status == OpenNfsV40Status.Ok;

        public ReadOnlyMemory<byte> CookieVerifier { get; }

        public IReadOnlyList<OpenNfsV40DirectoryEntry> Entries { get; }

        public bool EndOfFile { get; }

        private static IReadOnlyList<OpenNfsV40DirectoryEntry> CopyEntries(IReadOnlyList<OpenNfsV40DirectoryEntry>? entries)
        {
            if (entries is null || entries.Count == 0)
            {
                return Array.Empty<OpenNfsV40DirectoryEntry>();
            }

            OpenNfsV40DirectoryEntry[] copy = new OpenNfsV40DirectoryEntry[entries.Count];
            for (int index = 0; index < entries.Count; index++)
            {
                copy[index] = entries[index];
            }

            return copy;
        }
    }
}
#pragma warning restore CS1591
