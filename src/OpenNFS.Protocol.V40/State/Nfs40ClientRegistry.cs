namespace OpenNFS.Protocol.V40.State
{
    using System;
    using System.Buffers.Binary;
    using System.Collections.Generic;
    using OpenNFS.Protocol.V40.Generated;

    internal sealed class Nfs40ClientRegistry
    {
        private readonly Nfs40DelegationRegistry _delegations;
        private readonly Nfs40LockRegistry _locks;
        private readonly Nfs40OpenRegistry _opens;
        private readonly TimeSpan _leaseWindow;
        private readonly Dictionary<ulong, ClientRecord> _clientsById =
            new Dictionary<ulong, ClientRecord>();
        private readonly Dictionary<string, ClientRecord> _clientsByIdentity =
            new Dictionary<string, ClientRecord>(StringComparer.Ordinal);
        private ulong _nextClientId = 1UL;
        private ulong _nextConfirmToken = 1UL;

        internal Nfs40ClientRegistry(
            TimeSpan leaseWindow,
            Nfs40OpenRegistry opens,
            Nfs40LockRegistry locks,
            Nfs40DelegationRegistry delegations)
        {
            ArgumentNullException.ThrowIfNull(opens);
            ArgumentNullException.ThrowIfNull(locks);
            ArgumentNullException.ThrowIfNull(delegations);

            _leaseWindow = leaseWindow;
            _opens = opens;
            _locks = locks;
            _delegations = delegations;
        }

        internal void CleanupExpiredClients(DateTimeOffset now)
        {
            if (_clientsById.Count == 0)
            {
                return;
            }

            List<ClientRecord> expiredClients = new List<ClientRecord>();
            foreach (ClientRecord client in _clientsById.Values)
            {
                if (IsExpired(client, now))
                {
                    expiredClients.Add(client);
                }
            }

            for (int index = 0; index < expiredClients.Count; index++)
            {
                RemoveClient(expiredClients[index]);
            }
        }

        internal nfsstat4 ConfirmClient(clientid4? clientId, verifier4? confirmVerifier, DateTimeOffset now)
        {
            if (clientId is null || confirmVerifier?.Value is not { Length: 8 } confirmationBytes)
            {
                return nfsstat4.NFS4ERR_BADXDR;
            }

            if (!_clientsById.TryGetValue(clientId.Value, out ClientRecord? record))
            {
                return nfsstat4.NFS4ERR_STALE_CLIENTID;
            }

            if (IsExpired(record, now))
            {
                RemoveClient(record);
                return nfsstat4.NFS4ERR_EXPIRED;
            }

            if (record.CurrentConfirmVerifier is null
                || !record.CurrentConfirmVerifier.AsSpan().SequenceEqual(confirmationBytes))
            {
                return nfsstat4.NFS4ERR_STALE_CLIENTID;
            }

            record.IsConfirmed = true;
            record.LastRenewUtc = now;
            return nfsstat4.NFS4_OK;
        }

        internal bool IsExpired(ClientRecord client, DateTimeOffset now)
        {
            ArgumentNullException.ThrowIfNull(client);
            return now - client.LastRenewUtc > _leaseWindow;
        }

        internal Nfs40ClientRegistrationResult RegisterClient(
            nfs_client_id4? client,
            cb_client4? callback,
            uint callbackIdent,
            DateTimeOffset now)
        {
            if (client?.verifier?.Value is not { Length: 8 } clientVerifier || client.id is null)
            {
                return new Nfs40ClientRegistrationResult(nfsstat4.NFS4ERR_BADXDR);
            }

            if (client.id.Length == 0)
            {
                return new Nfs40ClientRegistrationResult(nfsstat4.NFS4ERR_INVAL);
            }

            string identityKey = Convert.ToHexString(client.id);
            if (!_clientsByIdentity.TryGetValue(identityKey, out ClientRecord? record))
            {
                record = new ClientRecord(_nextClientId++, identityKey, client.id, now);
                _clientsByIdentity.Add(identityKey, record);
                _clientsById.Add(record.ClientId, record);
            }

            record.ClientVerifier = clientVerifier.AsSpan().ToArray();
            record.CallbackProgram = callback?.cb_program ?? 0U;
            record.CallbackIdent = callbackIdent;
            record.CallbackNetId = callback?.cb_location?.r_netid ?? string.Empty;
            record.CallbackAddress = callback?.cb_location?.r_addr ?? string.Empty;
            record.CurrentConfirmVerifier = CreateVerifier(_nextConfirmToken++);
            record.IsConfirmed = false;
            record.LastRenewUtc = now;

            return new Nfs40ClientRegistrationResult(
                nfsstat4.NFS4_OK,
                record.ClientId,
                record.CurrentConfirmVerifier);
        }

        internal void RemoveClient(ClientRecord client)
        {
            ArgumentNullException.ThrowIfNull(client);

            _delegations.RemoveClientDelegations(client);
            _opens.RemoveClientOpenStates(client);
            _locks.RemoveClientLocks(client);

            _clientsById.Remove(client.ClientId);
            _clientsByIdentity.Remove(client.IdentityKey);
        }

        internal void ResetStateForRecovery(DateTimeOffset now)
        {
            foreach (ClientRecord client in _clientsById.Values)
            {
                client.DelegationStateKeys.Clear();
                client.OpenOwners.Clear();
                client.LockOwners.Clear();
                client.StateKeys.Clear();
                client.LockStateKeys.Clear();
                client.LastRenewUtc = now;
            }
        }

        internal nfsstat4 RenewClient(clientid4? clientId, DateTimeOffset now)
        {
            if (clientId is null)
            {
                return nfsstat4.NFS4ERR_BADXDR;
            }

            if (!_clientsById.TryGetValue(clientId.Value, out ClientRecord? record))
            {
                return nfsstat4.NFS4ERR_STALE_CLIENTID;
            }

            if (IsExpired(record, now))
            {
                RemoveClient(record);
                return nfsstat4.NFS4ERR_EXPIRED;
            }

            if (!record.IsConfirmed)
            {
                return nfsstat4.NFS4ERR_STALE_CLIENTID;
            }

            record.LastRenewUtc = now;
            return nfsstat4.NFS4_OK;
        }

        internal nfsstat4 ValidateConfirmedClient(
            ulong clientId,
            DateTimeOffset now,
            out ClientRecord? clientRecord)
        {
            clientRecord = null;

            if (!_clientsById.TryGetValue(clientId, out ClientRecord? record))
            {
                return nfsstat4.NFS4ERR_STALE_CLIENTID;
            }

            if (IsExpired(record, now))
            {
                RemoveClient(record);
                return nfsstat4.NFS4ERR_EXPIRED;
            }

            if (!record.IsConfirmed)
            {
                return nfsstat4.NFS4ERR_STALE_CLIENTID;
            }

            clientRecord = record;
            return nfsstat4.NFS4_OK;
        }

        private static byte[] CreateVerifier(ulong token)
        {
            byte[] verifier = new byte[8];
            BinaryPrimitives.WriteUInt64BigEndian(verifier, token);
            return verifier;
        }
    }
}
