namespace Sample.OpenNfsServer.State
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;
    using OpenNFS.Server;

    internal sealed class SamplePersistentAclStore
    {
        private readonly string _aclFilePath;
        private readonly Dictionary<string, PersistedAclEntry[]> _entriesByPath;
        private readonly string _owner;
        private readonly string _ownerGroup;
        private readonly object _syncRoot;

        internal SamplePersistentAclStore(string aclFilePath, string owner, string ownerGroup)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(aclFilePath);
            ArgumentException.ThrowIfNullOrWhiteSpace(owner);
            ArgumentException.ThrowIfNullOrWhiteSpace(ownerGroup);

            _aclFilePath = Path.GetFullPath(aclFilePath);
            _entriesByPath = new Dictionary<string, PersistedAclEntry[]>(StringComparer.OrdinalIgnoreCase);
            _owner = owner;
            _ownerGroup = ownerGroup;
            _syncRoot = new object();

            string? aclDirectory = Path.GetDirectoryName(_aclFilePath);
            if (!string.IsNullOrWhiteSpace(aclDirectory))
            {
                Directory.CreateDirectory(aclDirectory);
            }

            LoadExistingState();
        }

        internal IReadOnlyList<NfsAclEntry> GetEntries(string sourcePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

            lock (_syncRoot)
            {
                string normalizedPath = NormalizePath(sourcePath);
                if (_entriesByPath.TryGetValue(normalizedPath, out PersistedAclEntry[]? persistedEntries))
                {
                    return ConvertEntries(persistedEntries);
                }

                return CreateDefaultEntries();
            }
        }

        internal void SetEntries(string sourcePath, IReadOnlyList<NfsAclEntry> entries)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
            ArgumentNullException.ThrowIfNull(entries);

            lock (_syncRoot)
            {
                string normalizedPath = NormalizePath(sourcePath);
                _entriesByPath[normalizedPath] = ConvertEntries(entries);
                PersistState();
            }
        }

        private static PersistedAclEntry[] ConvertEntries(IReadOnlyList<NfsAclEntry> entries)
        {
            PersistedAclEntry[] converted = new PersistedAclEntry[entries.Count];
            for (int index = 0; index < entries.Count; index++)
            {
                NfsAclEntry entry = entries[index] ?? throw new ArgumentNullException(nameof(entries), "ACL collections cannot contain null entries.");
                converted[index] = new PersistedAclEntry
                {
                    EntryFlags = entry.EntryFlags,
                    EntryType = entry.EntryType,
                    Permissions = entry.Permissions,
                    Who = entry.Who,
                };
            }

            return converted;
        }

        private static NfsAclEntry[] ConvertEntries(IReadOnlyList<PersistedAclEntry> entries)
        {
            NfsAclEntry[] converted = new NfsAclEntry[entries.Count];
            for (int index = 0; index < entries.Count; index++)
            {
                PersistedAclEntry entry = entries[index] ?? throw new ArgumentNullException(nameof(entries), "Persisted ACL collections cannot contain null entries.");
                converted[index] = new NfsAclEntry(entry.EntryType, entry.EntryFlags, entry.Permissions, entry.Who ?? "EVERYONE@");
            }

            return converted;
        }

        private NfsAclEntry[] CreateDefaultEntries()
        {
            return new[]
            {
                new NfsAclEntry(
                    NfsAclEntryType.Allow,
                    NfsAclEntryFlags.FileInherit | NfsAclEntryFlags.DirectoryInherit,
                    NfsAclPermissionMask.GenericRead
                        | NfsAclPermissionMask.GenericWrite
                        | NfsAclPermissionMask.GenericExecute
                        | NfsAclPermissionMask.WriteAcl
                        | NfsAclPermissionMask.WriteOwner
                        | NfsAclPermissionMask.Delete,
                    _owner),
                new NfsAclEntry(
                    NfsAclEntryType.Allow,
                    NfsAclEntryFlags.FileInherit | NfsAclEntryFlags.DirectoryInherit | NfsAclEntryFlags.IdentifierGroup,
                    NfsAclPermissionMask.GenericRead | NfsAclPermissionMask.GenericExecute,
                    _ownerGroup),
            };
        }

        private void LoadExistingState()
        {
            if (!File.Exists(_aclFilePath))
            {
                return;
            }

            using FileStream stream = File.OpenRead(_aclFilePath);
            PersistedAclState? persistedState = JsonSerializer.Deserialize<PersistedAclState>(
                stream,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                });

            if (persistedState?.EntriesByPath is null)
            {
                return;
            }

            foreach (KeyValuePair<string, PersistedAclEntry[]> pair in persistedState.EntriesByPath)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                {
                    continue;
                }

                _entriesByPath[NormalizePath(pair.Key)] = pair.Value ?? Array.Empty<PersistedAclEntry>();
            }
        }

        private static string NormalizePath(string path)
        {
            return Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private void PersistState()
        {
            using FileStream stream = new FileStream(
                _aclFilePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None);

            JsonSerializer.Serialize(
                stream,
                new PersistedAclState
                {
                    EntriesByPath = _entriesByPath,
                },
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                });
        }

        private sealed class PersistedAclEntry
        {
            public NfsAclEntryFlags EntryFlags { get; set; }

            public NfsAclEntryType EntryType { get; set; }

            public NfsAclPermissionMask Permissions { get; set; }

            public string? Who { get; set; }
        }

        private sealed class PersistedAclState
        {
            public Dictionary<string, PersistedAclEntry[]>? EntriesByPath { get; set; }
        }
    }
}
