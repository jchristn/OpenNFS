namespace Sample.OpenNfsServer.State
{
    using System;
    using System.Collections.Generic;
    using OpenNFS.Server;
    using OpenNFS.Server.Delegations;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    internal sealed class SampleDelegationManager
    {
        private const uint ShareAccessWrite = 2U;
        private readonly List<string> _recalls = new List<string>();
        private readonly List<string> _returns = new List<string>();
        private readonly object _syncRoot = new object();

        internal NfsAcquireDelegationResponse Acquire(NfsAcquireDelegationRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (request.PathKind != NfsPathKind.File)
            {
                return NfsAcquireDelegationResponse.None;
            }

            if ((request.ShareAccess & ShareAccessWrite) != 0U)
            {
                return NfsAcquireDelegationResponse.None;
            }

            return new NfsAcquireDelegationResponse(NfsDelegationKind.Read);
        }

        internal void RecordRecall(NfsRecallDelegationRequest request)
        {
            lock (_syncRoot)
            {
                _recalls.Add(request.SourcePath);
            }
        }

        internal void RecordReturn(NfsReturnDelegationRequest request)
        {
            lock (_syncRoot)
            {
                _returns.Add(request.SourcePath);
            }
        }
    }
}
