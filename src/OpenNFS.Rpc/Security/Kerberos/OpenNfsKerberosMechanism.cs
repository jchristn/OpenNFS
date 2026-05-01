namespace OpenNFS.Rpc.Security.Kerberos
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Net.Security;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Rpc.Security.RpcSecGss;

    /// <summary>
    /// Kerberos v5 implementation of <see cref="IRpcSecGssMechanism"/> built on
    /// <see cref="NegotiateAuthentication"/>.
    /// </summary>
    /// <remarks>
    /// On Windows .NET this delegates to SSPI; on Linux .NET it delegates to GSSAPI. The mechanism
    /// stores per-context-handle <see cref="NegotiateAuthentication"/> instances and routes the
    /// RPCSEC_GSS calls (accept-context / MIC / wrap) through them. Hosts wanting to support
    /// <c>krb5</c> / <c>krb5i</c> / <c>krb5p</c> register an instance of this class with their
    /// RPCSEC_GSS authenticator.
    /// </remarks>
    public sealed class OpenNfsKerberosMechanism : IRpcSecGssMechanism, IDisposable
    {
        private readonly OpenNfsKerberosMechanismOptions options;
        private readonly object gate;
        private readonly Dictionary<string, ContextEntry> contexts;
        private bool isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenNfsKerberosMechanism"/> class.
        /// </summary>
        /// <param name="options">The mechanism options.</param>
        public OpenNfsKerberosMechanism(OpenNfsKerberosMechanismOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            this.options = options;
            gate = new object();
            contexts = new Dictionary<string, ContextEntry>(StringComparer.Ordinal);
        }

        /// <inheritdoc />
        public RpcSecGssMechanismName MechanismName => RpcSecGssMechanismName.KerberosV5;

        /// <inheritdoc />
        public ValueTask<RpcSecGssAcceptResult> AcceptSecurityContextAsync(
            ReadOnlyMemory<byte> contextHandle,
            ReadOnlyMemory<byte> inboundToken,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            byte[] handleBytes;
            ContextEntry entry;
            bool isFreshContext = contextHandle.Length == 0;

            if (isFreshContext)
            {
                handleBytes = AllocateHandle();
                NegotiateAuthentication negotiate = CreateServerAuthentication();
                entry = new ContextEntry(negotiate);
                lock (gate)
                {
                    EvictExpiredContextsLocked();
                    contexts[ToHandleKey(handleBytes)] = entry;
                }
            }
            else
            {
                handleBytes = contextHandle.ToArray();
                string key = ToHandleKey(handleBytes);
                lock (gate)
                {
                    if (!contexts.TryGetValue(key, out ContextEntry? existing))
                    {
                        return ValueTask.FromResult(BuildFailure(handleBytes, RpcSecGssMajorStatus.DefectiveCredential));
                    }

                    entry = existing;
                    entry.Touch();
                }
            }

            byte[]? outboundToken;
            NegotiateAuthenticationStatusCode statusCode;

            try
            {
                outboundToken = entry.Negotiate.GetOutgoingBlob(inboundToken.Span, out statusCode);
            }
            catch (Exception)
            {
                lock (gate)
                {
                    contexts.Remove(ToHandleKey(handleBytes));
                }

                entry.Dispose();
                return ValueTask.FromResult(BuildFailure(handleBytes, RpcSecGssMajorStatus.DefectiveToken));
            }

            switch (statusCode)
            {
                case NegotiateAuthenticationStatusCode.Completed:
                    string? initiator = entry.Negotiate.RemoteIdentity?.Name;
                    return ValueTask.FromResult(new RpcSecGssAcceptResult(
                        contextHandle: handleBytes,
                        majorStatus: RpcSecGssMajorStatus.Complete,
                        minorStatus: 0u,
                        outboundToken: outboundToken ?? Array.Empty<byte>(),
                        isContextEstablished: true,
                        initiatorPrincipal: initiator));

                case NegotiateAuthenticationStatusCode.ContinueNeeded:
                    return ValueTask.FromResult(new RpcSecGssAcceptResult(
                        contextHandle: handleBytes,
                        majorStatus: RpcSecGssMajorStatus.ContinueNeeded,
                        minorStatus: 0u,
                        outboundToken: outboundToken ?? Array.Empty<byte>(),
                        isContextEstablished: false,
                        initiatorPrincipal: null));

                default:
                    lock (gate)
                    {
                        contexts.Remove(ToHandleKey(handleBytes));
                    }

                    entry.Dispose();
                    return ValueTask.FromResult(BuildFailure(handleBytes, MapFailureMajorStatus(statusCode)));
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// On .NET 10+, this delegates to <c>NegotiateAuthentication.VerifyIntegrityCheck</c>. On
        /// .NET 8, that API is not exposed, so the provider surfaces a clear unsupported state. That
        /// means <c>krb5</c> / <c>krb5i</c> work only when this assembly is loaded from its net10.0
        /// build; the net8.0 build supports <c>krb5p</c> (privacy) only.
        /// </remarks>
        public ValueTask<bool> VerifyMicAsync(
            ReadOnlyMemory<byte> contextHandle,
            ReadOnlyMemory<byte> message,
            ReadOnlyMemory<byte> checksum,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            ContextEntry entry = ResolveEstablished(contextHandle);
#if NET10_0_OR_GREATER
            bool ok = entry.Negotiate.VerifyIntegrityCheck(message.Span, checksum.Span);
            return ValueTask.FromResult(ok);
#else
            _ = entry;
            throw new CryptographicException(
                "GSS_VerifyMIC is not exposed by .NET 8 NegotiateAuthentication. Load OpenNFS.Rpc's "
                + "net10.0 build to enable krb5 / krb5i support, or stay on net8.0 with krb5p only.");
#endif
        }

        /// <inheritdoc />
        /// <remarks>
        /// On .NET 10+, this delegates to <c>NegotiateAuthentication.ComputeIntegrityCheck</c>. On
        /// .NET 8, that API is not exposed; see <see cref="VerifyMicAsync"/> for details.
        /// </remarks>
        public ValueTask<byte[]> ComputeMicAsync(
            ReadOnlyMemory<byte> contextHandle,
            ReadOnlyMemory<byte> message,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            ContextEntry entry = ResolveEstablished(contextHandle);
#if NET10_0_OR_GREATER
            ArrayBufferWriter<byte> writer = new ArrayBufferWriter<byte>();
            entry.Negotiate.ComputeIntegrityCheck(message.Span, writer);
            return ValueTask.FromResult(writer.WrittenSpan.ToArray());
#else
            _ = entry;
            throw new CryptographicException(
                "GSS_GetMIC is not exposed by .NET 8 NegotiateAuthentication. Load OpenNFS.Rpc's "
                + "net10.0 build to enable krb5 / krb5i support, or stay on net8.0 with krb5p only.");
#endif
        }

        /// <inheritdoc />
        public ValueTask<byte[]> UnwrapAsync(
            ReadOnlyMemory<byte> contextHandle,
            ReadOnlyMemory<byte> wrappedPayload,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            ContextEntry entry = ResolveEstablished(contextHandle);
            ArrayBufferWriter<byte> writer = new ArrayBufferWriter<byte>();
            NegotiateAuthenticationStatusCode result = entry.Negotiate.Unwrap(wrappedPayload.Span, writer, out _);
            if (result != NegotiateAuthenticationStatusCode.Completed)
            {
                throw new CryptographicException(
                    "Unwrap failed with status '" + result + "' on the established Kerberos context.");
            }

            return ValueTask.FromResult(writer.WrittenSpan.ToArray());
        }

        /// <inheritdoc />
        public ValueTask<byte[]> WrapAsync(
            ReadOnlyMemory<byte> contextHandle,
            ReadOnlyMemory<byte> plaintext,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            ContextEntry entry = ResolveEstablished(contextHandle);
            ArrayBufferWriter<byte> writer = new ArrayBufferWriter<byte>();
            NegotiateAuthenticationStatusCode result = entry.Negotiate.Wrap(plaintext.Span, writer, requestEncryption: true, out _);
            if (result != NegotiateAuthenticationStatusCode.Completed)
            {
                throw new CryptographicException(
                    "Wrap failed with status '" + result + "' on the established Kerberos context.");
            }

            return ValueTask.FromResult(writer.WrittenSpan.ToArray());
        }

        /// <inheritdoc />
        public ValueTask DeleteSecurityContextAsync(
            ReadOnlyMemory<byte> contextHandle,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string key = ToHandleKey(contextHandle.ToArray());
            ContextEntry? removed;
            lock (gate)
            {
                if (contexts.TryGetValue(key, out removed))
                {
                    contexts.Remove(key);
                }
                else
                {
                    removed = null;
                }
            }

            removed?.Dispose();
            return ValueTask.CompletedTask;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            lock (gate)
            {
                foreach (ContextEntry entry in contexts.Values)
                {
                    entry.Dispose();
                }

                contexts.Clear();
            }
        }

        private NegotiateAuthentication CreateServerAuthentication()
        {
            NegotiateAuthenticationServerOptions serverOptions = new NegotiateAuthenticationServerOptions
            {
                Package = "Kerberos",
                RequiredProtectionLevel = options.RequiredProtectionLevel,
            };

            if (options.ServerCredential is not null)
            {
                serverOptions.Credential = options.ServerCredential;
            }

            return new NegotiateAuthentication(serverOptions);
        }

        private ContextEntry ResolveEstablished(ReadOnlyMemory<byte> contextHandle)
        {
            string key = ToHandleKey(contextHandle.ToArray());
            lock (gate)
            {
                if (!contexts.TryGetValue(key, out ContextEntry? entry))
                {
                    throw new InvalidOperationException(
                        "No Kerberos context registered for the supplied handle.");
                }

                if (!entry.Negotiate.IsAuthenticated)
                {
                    throw new InvalidOperationException(
                        "The Kerberos context for the supplied handle has not finished establishment.");
                }

                entry.Touch();
                return entry;
            }
        }

        private RpcSecGssAcceptResult BuildFailure(byte[] handleBytes, RpcSecGssMajorStatus majorStatus)
        {
            return new RpcSecGssAcceptResult(
                contextHandle: handleBytes,
                majorStatus: majorStatus,
                minorStatus: 0u,
                outboundToken: Array.Empty<byte>(),
                isContextEstablished: false,
                initiatorPrincipal: null);
        }

        private void EvictExpiredContextsLocked()
        {
            DateTimeOffset cutoff = DateTimeOffset.UtcNow - options.IdleContextLifetime;
            List<string>? expired = null;
            foreach (KeyValuePair<string, ContextEntry> pair in contexts)
            {
                if (pair.Value.LastTouchedUtc < cutoff)
                {
                    expired ??= new List<string>();
                    expired.Add(pair.Key);
                }
            }

            if (expired is null)
            {
                return;
            }

            foreach (string key in expired)
            {
                if (contexts.TryGetValue(key, out ContextEntry? entry))
                {
                    contexts.Remove(key);
                    entry.Dispose();
                }
            }
        }

        private void ThrowIfDisposed()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(OpenNfsKerberosMechanism));
            }
        }

        private static byte[] AllocateHandle()
        {
            byte[] handle = new byte[16];
            RandomNumberGenerator.Fill(handle);
            return handle;
        }

        private static string ToHandleKey(byte[] handle)
        {
            return Convert.ToHexString(handle);
        }

        private static RpcSecGssMajorStatus MapFailureMajorStatus(NegotiateAuthenticationStatusCode statusCode)
        {
            return statusCode switch
            {
                NegotiateAuthenticationStatusCode.BadBinding => RpcSecGssMajorStatus.DefectiveCredential,
                NegotiateAuthenticationStatusCode.UnknownCredentials => RpcSecGssMajorStatus.DefectiveCredential,
                NegotiateAuthenticationStatusCode.CredentialsExpired => RpcSecGssMajorStatus.DefectiveCredential,
                NegotiateAuthenticationStatusCode.MessageAltered => RpcSecGssMajorStatus.DefectiveToken,
                NegotiateAuthenticationStatusCode.OutOfSequence => RpcSecGssMajorStatus.DefectiveToken,
                NegotiateAuthenticationStatusCode.InvalidToken => RpcSecGssMajorStatus.DefectiveToken,
                NegotiateAuthenticationStatusCode.Unsupported => RpcSecGssMajorStatus.BadMechanism,
                NegotiateAuthenticationStatusCode.QopNotSupported => RpcSecGssMajorStatus.BadMechanism,
                NegotiateAuthenticationStatusCode.TargetUnknown => RpcSecGssMajorStatus.DefectiveCredential,
                NegotiateAuthenticationStatusCode.InvalidCredentials => RpcSecGssMajorStatus.DefectiveCredential,
                NegotiateAuthenticationStatusCode.SecurityQosFailed => RpcSecGssMajorStatus.Failure,
                NegotiateAuthenticationStatusCode.ImpersonationValidationFailed => RpcSecGssMajorStatus.Failure,
                NegotiateAuthenticationStatusCode.ContextExpired => RpcSecGssMajorStatus.Failure,
                _ => RpcSecGssMajorStatus.Failure,
            };
        }

        private sealed class ContextEntry : IDisposable
        {
            private bool isDisposed;

            internal ContextEntry(NegotiateAuthentication negotiate)
            {
                Negotiate = negotiate;
                LastTouchedUtc = DateTimeOffset.UtcNow;
            }

            internal NegotiateAuthentication Negotiate { get; }

            internal DateTimeOffset LastTouchedUtc { get; private set; }

            internal void Touch()
            {
                LastTouchedUtc = DateTimeOffset.UtcNow;
            }

            public void Dispose()
            {
                if (isDisposed)
                {
                    return;
                }

                isDisposed = true;
                Negotiate.Dispose();
            }
        }
    }
}
