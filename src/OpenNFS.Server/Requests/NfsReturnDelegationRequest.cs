#pragma warning disable CS1591
namespace OpenNFS.Server.Requests
{
    using System;
    using System.Threading;
    using OpenNFS.Server.Delegations;

    /// <summary>
    /// Describes a host-facing notification that a previously granted delegation was returned.
    /// </summary>
    public sealed class NfsReturnDelegationRequest
    {
        public NfsReturnDelegationRequest(
            string exportPath,
            string sourcePath,
            ulong clientId,
            NfsDelegationKind delegationKind,
            ReadOnlyMemory<byte> stateIdOther,
            CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(exportPath);
            ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

            ExportPath = exportPath;
            SourcePath = sourcePath;
            ClientId = clientId;
            DelegationKind = delegationKind;
            StateIdOther = stateIdOther.Length == 0
                ? ReadOnlyMemory<byte>.Empty
                : new ReadOnlyMemory<byte>(stateIdOther.ToArray());
            CancellationToken = cancellationToken;
        }

        public CancellationToken CancellationToken { get; }

        public ulong ClientId { get; }

        public NfsDelegationKind DelegationKind { get; }

        public string ExportPath { get; }

        public string SourcePath { get; }

        public ReadOnlyMemory<byte> StateIdOther { get; }
    }
}
#pragma warning restore CS1591
