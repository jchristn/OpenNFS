namespace OpenNFS.Server.FileHandles
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Server.Abstractions;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    /// <summary>
    /// Filehandle provider that persists identity-to-handle mappings across process restarts.
    /// </summary>
    public sealed class PersistentMappingHandleProvider : IFileHandleProvider
    {
        private readonly SemaphoreSlim _Gate = new SemaphoreSlim(1, 1);
        private readonly JsonSerializerOptions _JsonSerializerOptions = new JsonSerializerOptions();
        private PersistentHandleProviderState? _State;

        /// <summary>
        /// Initializes a new instance of the <see cref="PersistentMappingHandleProvider"/> class.
        /// </summary>
        /// <param name="storagePath">Path to the persisted mapping file.</param>
        /// <param name="randomHandleLengthBytes">
        /// Length of the random opaque payload appended after the provider version byte.
        /// Default value: <c>16</c>.
        /// Minimum value: <c>8</c>.
        /// Maximum value: <c>64</c>.
        /// </param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="storagePath"/> is empty or whitespace.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="randomHandleLengthBytes"/> is outside the supported range.</exception>
        public PersistentMappingHandleProvider(string storagePath, int randomHandleLengthBytes = 16)
        {
            if (string.IsNullOrWhiteSpace(storagePath))
            {
                throw new ArgumentException("The persistent filehandle storage path must contain a non-empty value.", nameof(storagePath));
            }

            if (randomHandleLengthBytes < 8 || randomHandleLengthBytes > 64)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(randomHandleLengthBytes),
                    randomHandleLengthBytes,
                    "The persistent filehandle random payload length must be between 8 and 64 bytes.");
            }

            StoragePath = storagePath;
            RandomHandleLengthBytes = randomHandleLengthBytes;
        }

        /// <summary>
        /// Gets the path to the persisted mapping file.
        /// </summary>
        public string StoragePath { get; }

        /// <summary>
        /// Gets the length of the random opaque payload appended after the provider version byte.
        /// </summary>
        public int RandomHandleLengthBytes { get; }

        /// <summary>
        /// Creates or reuses a stable filehandle for a target.
        /// </summary>
        /// <param name="request">Request context for the filehandle-creation operation.</param>
        /// <returns>The created or reused persistent filehandle.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
        public async Task<NfsCreateFileHandleResponse> CreateAsync(NfsCreateFileHandleRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();

            NfsFileHandleTarget target = request.Target;

            await _Gate.WaitAsync(request.CancellationToken).ConfigureAwait(false);

            try
            {
                PersistentHandleProviderState state = await LoadStateAsync(request.CancellationToken).ConfigureAwait(false);
                string identityKey = BuildIdentityKey(target);

                if (state.IdentityToHandle.TryGetValue(identityKey, out string? handleKey))
                {
                    PersistentHandleTargetRecord existingRecord = state.HandleToTarget[handleKey];

                    if (!string.Equals(existingRecord.SourcePath, target.SourcePath, StringComparison.Ordinal)
                        || !string.Equals(existingRecord.ExportPath, target.ExportPath, StringComparison.Ordinal))
                    {
                        state.HandleToTarget[handleKey] = CreateRecord(target);
                        await SaveStateAsync(state, request.CancellationToken).ConfigureAwait(false);
                    }

                    return new NfsCreateFileHandleResponse(new NfsFileHandle(Convert.FromBase64String(handleKey)));
                }

                byte[] randomPayload = new byte[RandomHandleLengthBytes];
                string newHandleKey;

                do
                {
                    RandomNumberGenerator.Fill(randomPayload);
                    byte[] encodedHandle = new byte[randomPayload.Length + 1];
                    encodedHandle[0] = 0x01;
                    Buffer.BlockCopy(randomPayload, 0, encodedHandle, 1, randomPayload.Length);
                    newHandleKey = Convert.ToBase64String(encodedHandle);
                }
                while (state.HandleToTarget.ContainsKey(newHandleKey));

                state.IdentityToHandle[identityKey] = newHandleKey;
                state.HandleToTarget[newHandleKey] = CreateRecord(target);
                await SaveStateAsync(state, request.CancellationToken).ConfigureAwait(false);

                return new NfsCreateFileHandleResponse(new NfsFileHandle(Convert.FromBase64String(newHandleKey)));
            }
            finally
            {
                _Gate.Release();
            }
        }

        /// <summary>
        /// Resolves a previously created filehandle.
        /// </summary>
        /// <param name="request">Request context for the filehandle-resolution operation.</param>
        /// <returns>The resolved filehandle result.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
        public async Task<NfsResolveFileHandleResponse> ResolveAsync(NfsResolveFileHandleRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();

            await _Gate.WaitAsync(request.CancellationToken).ConfigureAwait(false);

            try
            {
                PersistentHandleProviderState state = await LoadStateAsync(request.CancellationToken).ConfigureAwait(false);
                string handleKey = Convert.ToBase64String(request.FileHandle.ToArray());

                if (!state.HandleToTarget.TryGetValue(handleKey, out PersistentHandleTargetRecord? record))
                {
                    return new NfsResolveFileHandleResponse(new NfsFileHandleResolution(found: false));
                }

                NfsFileHandleIdentity? stableIdentity = null;
                if (!string.IsNullOrWhiteSpace(record.IdentityScheme) || !string.IsNullOrWhiteSpace(record.IdentityValue))
                {
                    stableIdentity = new NfsFileHandleIdentity(record.IdentityScheme!, record.IdentityValue!);
                }

                NfsFileHandleTarget target = new NfsFileHandleTarget(
                    exportPath: record.ExportPath,
                    sourcePath: record.SourcePath,
                    stableIdentity: stableIdentity);

                return new NfsResolveFileHandleResponse(new NfsFileHandleResolution(found: true, target: target));
            }
            finally
            {
                _Gate.Release();
            }
        }

        private static string BuildIdentityKey(NfsFileHandleTarget target)
        {
            if (target.StableIdentity is null)
            {
                return string.Concat("path:", target.ExportPath, "\u001f", target.SourcePath);
            }

            return string.Concat(
                "identity:",
                target.ExportPath,
                "\u001f",
                target.StableIdentity.Scheme,
                "\u001f",
                target.StableIdentity.Value);
        }

        private static PersistentHandleTargetRecord CreateRecord(NfsFileHandleTarget target)
        {
            return new PersistentHandleTargetRecord
            {
                ExportPath = target.ExportPath,
                SourcePath = target.SourcePath,
                IdentityScheme = target.StableIdentity?.Scheme,
                IdentityValue = target.StableIdentity?.Value,
            };
        }

        private async Task<PersistentHandleProviderState> LoadStateAsync(CancellationToken cancellationToken)
        {
            if (_State is not null)
            {
                return _State;
            }

            if (!File.Exists(StoragePath))
            {
                _State = new PersistentHandleProviderState();
                return _State;
            }

            string content = await File.ReadAllTextAsync(StoragePath, cancellationToken).ConfigureAwait(false);
            PersistentHandleProviderState? state = JsonSerializer.Deserialize<PersistentHandleProviderState>(content, _JsonSerializerOptions);

            if (state is null)
            {
                throw new InvalidOperationException("The persistent filehandle mapping file '" + StoragePath + "' could not be deserialized.");
            }

            state.EnsureInitialized();
            _State = state;
            return _State;
        }

        private async Task SaveStateAsync(PersistentHandleProviderState state, CancellationToken cancellationToken)
        {
            string? directoryPath = Path.GetDirectoryName(StoragePath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            string content = JsonSerializer.Serialize(state, _JsonSerializerOptions);
            await File.WriteAllTextAsync(StoragePath, content, cancellationToken).ConfigureAwait(false);
        }
    }
}
