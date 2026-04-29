#pragma warning disable CS1591
namespace OpenNFS.Client
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// NFSv3 directory entry surfaced by grouped client reply models.
    /// </summary>
    public sealed class OpenNfsV3DirectoryEntry
    {
        public OpenNfsV3DirectoryEntry(ulong fileId, string name, ulong cookie)
        {
            ArgumentNullException.ThrowIfNull(name);

            FileId = fileId;
            Name = name;
            Cookie = cookie;
        }

        public ulong FileId { get; }

        public string Name { get; }

        public ulong Cookie { get; }
    }

    /// <summary>
    /// NFSv3 READDIRPLUS entry surfaced by grouped client reply models.
    /// </summary>
    public sealed class OpenNfsV3DirectoryPlusEntry
    {
        public OpenNfsV3DirectoryPlusEntry(
            ulong fileId,
            string name,
            ulong cookie,
            OpenNfsV3Attributes? attributes,
            ReadOnlyMemory<byte> fileHandle = default)
        {
            ArgumentNullException.ThrowIfNull(name);

            FileId = fileId;
            Name = name;
            Cookie = cookie;
            Attributes = attributes;
            FileHandle = fileHandle.Length == 0
                ? ReadOnlyMemory<byte>.Empty
                : new ReadOnlyMemory<byte>(fileHandle.ToArray());
        }

        public ulong FileId { get; }

        public string Name { get; }

        public ulong Cookie { get; }

        public OpenNfsV3Attributes? Attributes { get; }

        public ReadOnlyMemory<byte> FileHandle { get; }
    }

    /// <summary>
    /// Typed NFSv3 LOOKUP result surfaced by the grouped client API.
    /// </summary>
    public sealed class OpenNfsV3LookupResult
    {
        public OpenNfsV3LookupResult(
            OpenNfsV3Status status,
            ReadOnlyMemory<byte> objectFileHandle = default,
            OpenNfsV3Attributes? objectAttributes = null,
            OpenNfsV3Attributes? directoryAttributes = null)
        {
            Status = status;
            ObjectFileHandle = objectFileHandle.Length == 0
                ? ReadOnlyMemory<byte>.Empty
                : new ReadOnlyMemory<byte>(objectFileHandle.ToArray());
            ObjectAttributes = objectAttributes;
            DirectoryAttributes = directoryAttributes;
        }

        public OpenNfsV3Status Status { get; }

        public bool IsSuccess => Status == OpenNfsV3Status.Ok;

        public ReadOnlyMemory<byte> ObjectFileHandle { get; }

        public OpenNfsV3Attributes? ObjectAttributes { get; }

        public OpenNfsV3Attributes? DirectoryAttributes { get; }
    }

    /// <summary>
    /// Typed NFSv3 READDIR result surfaced by the grouped client API.
    /// </summary>
    public sealed class OpenNfsV3ReadDirectoryResult
    {
        public OpenNfsV3ReadDirectoryResult(
            OpenNfsV3Status status,
            OpenNfsV3Attributes? directoryAttributes = null,
            ReadOnlyMemory<byte> cookieVerifier = default,
            IReadOnlyList<OpenNfsV3DirectoryEntry>? entries = null,
            bool endOfFile = false)
        {
            Status = status;
            DirectoryAttributes = directoryAttributes;
            CookieVerifier = cookieVerifier.Length == 0
                ? ReadOnlyMemory<byte>.Empty
                : new ReadOnlyMemory<byte>(cookieVerifier.ToArray());
            Entries = CopyEntries(entries);
            EndOfFile = endOfFile;
        }

        public OpenNfsV3Status Status { get; }

        public bool IsSuccess => Status == OpenNfsV3Status.Ok;

        public OpenNfsV3Attributes? DirectoryAttributes { get; }

        public ReadOnlyMemory<byte> CookieVerifier { get; }

        public IReadOnlyList<OpenNfsV3DirectoryEntry> Entries { get; }

        public bool EndOfFile { get; }

        private static IReadOnlyList<OpenNfsV3DirectoryEntry> CopyEntries(IReadOnlyList<OpenNfsV3DirectoryEntry>? entries)
        {
            if (entries is null || entries.Count == 0)
            {
                return Array.Empty<OpenNfsV3DirectoryEntry>();
            }

            OpenNfsV3DirectoryEntry[] copy = new OpenNfsV3DirectoryEntry[entries.Count];
            for (int index = 0; index < entries.Count; index++)
            {
                copy[index] = entries[index];
            }

            return copy;
        }
    }

    /// <summary>
    /// Typed NFSv3 READDIRPLUS result surfaced by the grouped client API.
    /// </summary>
    public sealed class OpenNfsV3ReadDirectoryPlusResult
    {
        public OpenNfsV3ReadDirectoryPlusResult(
            OpenNfsV3Status status,
            OpenNfsV3Attributes? directoryAttributes = null,
            ReadOnlyMemory<byte> cookieVerifier = default,
            IReadOnlyList<OpenNfsV3DirectoryPlusEntry>? entries = null,
            bool endOfFile = false)
        {
            Status = status;
            DirectoryAttributes = directoryAttributes;
            CookieVerifier = cookieVerifier.Length == 0
                ? ReadOnlyMemory<byte>.Empty
                : new ReadOnlyMemory<byte>(cookieVerifier.ToArray());
            Entries = CopyEntries(entries);
            EndOfFile = endOfFile;
        }

        public OpenNfsV3Status Status { get; }

        public bool IsSuccess => Status == OpenNfsV3Status.Ok;

        public OpenNfsV3Attributes? DirectoryAttributes { get; }

        public ReadOnlyMemory<byte> CookieVerifier { get; }

        public IReadOnlyList<OpenNfsV3DirectoryPlusEntry> Entries { get; }

        public bool EndOfFile { get; }

        private static IReadOnlyList<OpenNfsV3DirectoryPlusEntry> CopyEntries(IReadOnlyList<OpenNfsV3DirectoryPlusEntry>? entries)
        {
            if (entries is null || entries.Count == 0)
            {
                return Array.Empty<OpenNfsV3DirectoryPlusEntry>();
            }

            OpenNfsV3DirectoryPlusEntry[] copy = new OpenNfsV3DirectoryPlusEntry[entries.Count];
            for (int index = 0; index < entries.Count; index++)
            {
                copy[index] = entries[index];
            }

            return copy;
        }
    }
}
#pragma warning restore CS1591
