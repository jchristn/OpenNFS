namespace Sample.OpenNfsServer.State
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;
    using OpenNFS.Server.Identity;

    internal sealed class SamplePersistentIdentityStore
    {
        private readonly string _defaultOwner;
        private readonly string _defaultOwnerGroup;
        private readonly Dictionary<string, PersistedIdentityEntry> _entriesByPath;
        private readonly string _identityFilePath;
        private readonly object _syncRoot;

        internal SamplePersistentIdentityStore(string identityFilePath, string defaultOwner, string defaultOwnerGroup)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(identityFilePath);
            ArgumentException.ThrowIfNullOrWhiteSpace(defaultOwner);
            ArgumentException.ThrowIfNullOrWhiteSpace(defaultOwnerGroup);

            _defaultOwner = defaultOwner;
            _defaultOwnerGroup = defaultOwnerGroup;
            _entriesByPath = new Dictionary<string, PersistedIdentityEntry>(StringComparer.OrdinalIgnoreCase);
            _identityFilePath = Path.GetFullPath(identityFilePath);
            _syncRoot = new object();

            string? identityDirectory = Path.GetDirectoryName(_identityFilePath);
            if (!string.IsNullOrWhiteSpace(identityDirectory))
            {
                Directory.CreateDirectory(identityDirectory);
            }

            LoadExistingState();
        }

        internal NfsIdentityMapping GetIdentity(string sourcePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

            lock (_syncRoot)
            {
                string normalizedPath = NormalizePath(sourcePath);
                if (_entriesByPath.TryGetValue(normalizedPath, out PersistedIdentityEntry? entry))
                {
                    return new NfsIdentityMapping(
                        entry.Owner ?? _defaultOwner,
                        entry.OwnerGroup ?? _defaultOwnerGroup);
                }

                return new NfsIdentityMapping(_defaultOwner, _defaultOwnerGroup);
            }
        }

        internal NfsIdentityMapping SetIdentity(string sourcePath, string? owner, string? ownerGroup)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

            lock (_syncRoot)
            {
                string normalizedPath = NormalizePath(sourcePath);
                NfsIdentityMapping currentIdentity = GetIdentity(sourcePath);
                PersistedIdentityEntry updatedEntry = new PersistedIdentityEntry
                {
                    Owner = owner ?? currentIdentity.Owner,
                    OwnerGroup = ownerGroup ?? currentIdentity.OwnerGroup,
                };

                _entriesByPath[normalizedPath] = updatedEntry;
                PersistState();
                return new NfsIdentityMapping(updatedEntry.Owner, updatedEntry.OwnerGroup);
            }
        }

        private void LoadExistingState()
        {
            if (!File.Exists(_identityFilePath))
            {
                return;
            }

            using FileStream stream = File.OpenRead(_identityFilePath);
            PersistedIdentityState? persistedState = JsonSerializer.Deserialize<PersistedIdentityState>(
                stream,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                });

            if (persistedState?.EntriesByPath is null)
            {
                return;
            }

            foreach (KeyValuePair<string, PersistedIdentityEntry> pair in persistedState.EntriesByPath)
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value is null)
                {
                    continue;
                }

                _entriesByPath[NormalizePath(pair.Key)] = pair.Value;
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
                _identityFilePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None);

            JsonSerializer.Serialize(
                stream,
                new PersistedIdentityState
                {
                    EntriesByPath = _entriesByPath,
                },
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                });
        }

        private sealed class PersistedIdentityEntry
        {
            public string Owner { get; set; } = string.Empty;

            public string OwnerGroup { get; set; } = string.Empty;
        }

        private sealed class PersistedIdentityState
        {
            public Dictionary<string, PersistedIdentityEntry>? EntriesByPath { get; set; }
        }
    }
}
