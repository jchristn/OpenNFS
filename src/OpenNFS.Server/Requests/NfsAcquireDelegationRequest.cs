#pragma warning disable CS1591
namespace OpenNFS.Server.Requests
{
    using System;
    using System.Threading;
    using OpenNFS.Server.Delegations;

    /// <summary>
    /// Describes a host-facing request to consider granting an NFSv4 delegation for an open file.
    /// </summary>
    public sealed class NfsAcquireDelegationRequest
    {
        public NfsAcquireDelegationRequest(
            string exportPath,
            string sourcePath,
            NfsPathKind pathKind,
            ulong clientId,
            uint shareAccess,
            uint shareDeny,
            CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(exportPath);
            ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

            ExportPath = exportPath;
            SourcePath = sourcePath;
            PathKind = pathKind;
            ClientId = clientId;
            ShareAccess = shareAccess;
            ShareDeny = shareDeny;
            CancellationToken = cancellationToken;
        }

        public CancellationToken CancellationToken { get; }

        public ulong ClientId { get; }

        public string ExportPath { get; }

        public NfsPathKind PathKind { get; }

        public uint ShareAccess { get; }

        public uint ShareDeny { get; }

        public string SourcePath { get; }
    }
}
#pragma warning restore CS1591
