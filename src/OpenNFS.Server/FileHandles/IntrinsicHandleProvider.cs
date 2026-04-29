namespace OpenNFS.Server.FileHandles
{
    using System;
    using System.Text;
    using System.Threading.Tasks;
    using OpenNFS.Server.Abstractions;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    /// <summary>
    /// Stateless filehandle provider that intrinsically encodes the target into the handle payload.
    /// </summary>
    public sealed class IntrinsicHandleProvider : IFileHandleProvider
    {
        private const string Prefix = "ONFSH1";

        /// <summary>
        /// Gets the default intrinsic filehandle provider instance.
        /// </summary>
        public static IntrinsicHandleProvider Default { get; } = new IntrinsicHandleProvider();

        /// <summary>
        /// Initializes a new instance of the <see cref="IntrinsicHandleProvider"/> class.
        /// </summary>
        /// <param name="maximumHandleLengthBytes">
        /// Maximum intrinsic filehandle payload length in bytes.
        /// Default value: <c>1024</c>.
        /// Minimum value: <c>32</c>.
        /// Maximum value: <c>65535</c>.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maximumHandleLengthBytes"/> is outside the supported range.</exception>
        public IntrinsicHandleProvider(int maximumHandleLengthBytes = 1024)
        {
            if (maximumHandleLengthBytes < 32 || maximumHandleLengthBytes > 65535)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumHandleLengthBytes),
                    maximumHandleLengthBytes,
                    "The intrinsic filehandle maximum length must be between 32 and 65535 bytes.");
            }

            MaximumHandleLengthBytes = maximumHandleLengthBytes;
        }

        /// <summary>
        /// Gets the maximum intrinsic filehandle payload length in bytes.
        /// </summary>
        public int MaximumHandleLengthBytes { get; }

        /// <summary>
        /// Creates or reuses a stable filehandle for a target.
        /// </summary>
        /// <param name="request">Request context for the filehandle-creation operation.</param>
        /// <returns>The created intrinsic filehandle.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the encoded intrinsic filehandle would exceed the configured maximum length.</exception>
        public Task<NfsCreateFileHandleResponse> CreateAsync(NfsCreateFileHandleRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();

            NfsFileHandleTarget target = request.Target;

            string stableIdentityScheme = target.StableIdentity?.Scheme ?? string.Empty;
            string stableIdentityValue = target.StableIdentity?.Value ?? string.Empty;
            string payload = string.Concat(
                Prefix,
                "\0",
                target.ExportPath,
                "\0",
                target.SourcePath,
                "\0",
                stableIdentityScheme,
                "\0",
                stableIdentityValue);
            byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);

            if (payloadBytes.Length > MaximumHandleLengthBytes)
            {
                throw new InvalidOperationException(
                    "The intrinsic filehandle for export path '" + target.ExportPath + "' and source path '" + target.SourcePath + "' exceeded the configured maximum length of " + MaximumHandleLengthBytes + " bytes.");
            }

            return Task.FromResult(new NfsCreateFileHandleResponse(new NfsFileHandle(payloadBytes)));
        }

        /// <summary>
        /// Resolves a previously created filehandle.
        /// </summary>
        /// <param name="request">Request context for the filehandle-resolution operation.</param>
        /// <returns>The resolved filehandle result.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
        public Task<NfsResolveFileHandleResponse> ResolveAsync(NfsResolveFileHandleRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();

            string payload = Encoding.UTF8.GetString(request.FileHandle.Bytes.Span);
            string[] components = payload.Split('\0');

            if (components.Length != 5 || !string.Equals(components[0], Prefix, StringComparison.Ordinal))
            {
                return Task.FromResult(new NfsResolveFileHandleResponse(new NfsFileHandleResolution(found: false)));
            }

            NfsFileHandleIdentity? stableIdentity = null;
            if (!string.IsNullOrWhiteSpace(components[3]) || !string.IsNullOrWhiteSpace(components[4]))
            {
                stableIdentity = new NfsFileHandleIdentity(components[3], components[4]);
            }

            NfsFileHandleTarget target = new NfsFileHandleTarget(
                exportPath: components[1],
                sourcePath: components[2],
                stableIdentity: stableIdentity);

            return Task.FromResult(new NfsResolveFileHandleResponse(new NfsFileHandleResolution(found: true, target: target)));
        }
    }
}
