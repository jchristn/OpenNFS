#pragma warning disable CS1591
namespace OpenNFS.Server.Requests
{
    using System;
    using System.Threading;
    using OpenNFS.Server.Delegations;

    /// <summary>
    /// Describes a host-facing notification that an existing delegation must be recalled.
    /// </summary>
    public sealed class NfsRecallDelegationRequest
    {
        public NfsRecallDelegationRequest(
            string exportPath,
            string sourcePath,
            ulong clientId,
            NfsDelegationKind delegationKind,
            ReadOnlyMemory<byte> stateIdOther,
            NfsDelegationRecallReason reason,
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
            Reason = reason;
            CancellationToken = cancellationToken;
        }

        public CancellationToken CancellationToken { get; }

        public ulong ClientId { get; }

        public NfsDelegationKind DelegationKind { get; }

        public string ExportPath { get; }

        public NfsDelegationRecallReason Reason { get; }

        public string SourcePath { get; }

        public ReadOnlyMemory<byte> StateIdOther { get; }
    }
}
#pragma warning restore CS1591
