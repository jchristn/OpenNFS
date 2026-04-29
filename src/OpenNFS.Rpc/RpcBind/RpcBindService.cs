namespace OpenNFS.Rpc.RpcBind
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Xdr;

    /// <summary>
    /// Provides in-memory portmap and rpcbind registration handling over RPC envelopes.
    /// </summary>
    public sealed class RpcBindService
    {
        private readonly List<RpcBindingRegistration> registrations;
        private readonly string universalAddressHost;

        /// <summary>
        /// Initializes a new instance of the <see cref="RpcBindService"/> class.
        /// </summary>
        /// <param name="universalAddressHost">
        /// The host portion used when deriving rpcbind universal addresses from portmap registrations.
        /// </param>
        public RpcBindService(string universalAddressHost = "127.0.0.1")
        {
            if (string.IsNullOrWhiteSpace(universalAddressHost))
            {
                throw new ArgumentException("The universal address host must contain a non-empty value.", nameof(universalAddressHost));
            }

            this.universalAddressHost = universalAddressHost;
            registrations = new List<RpcBindingRegistration>();
        }

        /// <summary>
        /// Dispatches a portmap or rpcbind request carried inside an RPC call envelope.
        /// </summary>
        /// <param name="request">The incoming RPC request envelope.</param>
        /// <returns>The standards-compliant RPC reply envelope.</returns>
        public RpcMessageEnvelope Dispatch(RpcMessageEnvelope request)
        {
            ArgumentNullException.ThrowIfNull(request);

            rpc_msg_body? body = request.Header.body;
            call_body? callBody = body?.cbody;
            if (body?.mtype != msg_type.CALL || callBody is null)
            {
                return RpcMessageFactory.CreateAcceptedReply(request.Header.xid, accept_stat.GARBAGE_ARGS);
            }

            if (callBody.rpcvers != RpcProtocolConstants.RpcVersion)
            {
                return RpcMessageFactory.CreateRejectedReply(
                    request.Header.xid,
                    reject_stat.RPC_MISMATCH,
                    mismatchLowVersion: RpcProtocolConstants.RpcVersion,
                    mismatchHighVersion: RpcProtocolConstants.RpcVersion);
            }

            if (callBody.prog != (uint)PMAP_PROG_Program.Program)
            {
                return RpcMessageFactory.CreateAcceptedReply(request.Header.xid, accept_stat.PROG_UNAVAIL);
            }

            if (callBody.vers == (uint)PMAP_PROG_Program.Version_PMAP_VERS)
            {
                return DispatchPortmapCall(request, callBody.proc);
            }

            if (callBody.vers == (uint)RPCBPROG_Program.Version_RPCBVERS
                || callBody.vers == (uint)RPCBPROG_Program.Version_RPCBVERS4)
            {
                return DispatchRpcbindCall(request, callBody.vers, callBody.proc);
            }

            return RpcMessageFactory.CreateAcceptedReply(
                request.Header.xid,
                accept_stat.PROG_MISMATCH,
                mismatchLowVersion: (uint)PMAP_PROG_Program.Version_PMAP_VERS,
                mismatchHighVersion: (uint)RPCBPROG_Program.Version_RPCBVERS4);
        }

        /// <summary>
        /// Gets a snapshot of the currently registered bindings.
        /// </summary>
        /// <returns>A stable snapshot of the normalized bindings.</returns>
        public IReadOnlyList<RpcBindingRegistration> GetBindings()
        {
            return registrations
                .Select(static registration => new RpcBindingRegistration(
                    registration.ProgramNumber,
                    registration.VersionNumber,
                    registration.Protocol,
                    registration.Port,
                    registration.NetId,
                    registration.UniversalAddress,
                    registration.Owner))
                .ToArray();
        }

        /// <summary>
        /// Looks up a registered port using a portmap v2 query payload.
        /// </summary>
        /// <param name="request">The portmap request payload.</param>
        /// <returns>The registered port, or zero when no matching registration exists.</returns>
        public uint GetPort(mapping request)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (!RpcBindNetId.TryGetProtocol(request.prot, out RpcBindingProtocol protocol))
            {
                return 0;
            }

            RpcBindingRegistration? registration = FindRegistration(request.prog, request.vers, protocol);
            return registration?.Port ?? 0;
        }

        /// <summary>
        /// Looks up a registered rpcbind universal address using an rpcbind query payload.
        /// </summary>
        /// <param name="request">The rpcbind request payload.</param>
        /// <returns>The registered universal address, or an empty string when no matching registration exists.</returns>
        public string GetUniversalAddress(rpcb request)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (request.r_netid is null || !RpcBindNetId.TryGetProtocol(request.r_netid, out RpcBindingProtocol protocol))
            {
                return string.Empty;
            }

            RpcBindingRegistration? registration = FindRegistration(request.r_prog, request.r_vers, protocol);
            if (registration is null)
            {
                return string.Empty;
            }

            return registration.UniversalAddress;
        }

        /// <summary>
        /// Registers a portmap mapping payload.
        /// </summary>
        /// <param name="request">The portmap registration payload.</param>
        /// <returns><c>true</c> when the registration succeeds; otherwise <c>false</c>.</returns>
        public bool Register(mapping request)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (!RpcBindNetId.TryGetProtocol(request.prot, out RpcBindingProtocol protocol))
            {
                return false;
            }

            if (request.port > ushort.MaxValue)
            {
                return false;
            }

            string netId = RpcBindNetId.GetNetId(protocol);
            string universalAddress = RpcBindUniversalAddress.Create(universalAddressHost, request.port);

            return RegisterInternal(new RpcBindingRegistration(
                request.prog,
                request.vers,
                protocol,
                request.port,
                netId,
                universalAddress,
                string.Empty));
        }

        /// <summary>
        /// Registers an rpcbind payload.
        /// </summary>
        /// <param name="request">The rpcbind registration payload.</param>
        /// <returns><c>true</c> when the registration succeeds; otherwise <c>false</c>.</returns>
        public bool Register(rpcb request)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (request.r_netid is null || !RpcBindNetId.TryGetProtocol(request.r_netid, out RpcBindingProtocol protocol))
            {
                return false;
            }

            if (request.r_addr is null || !RpcBindUniversalAddress.TryGetPort(request.r_addr, out uint port))
            {
                return false;
            }

            return RegisterInternal(new RpcBindingRegistration(
                request.r_prog,
                request.r_vers,
                protocol,
                port,
                request.r_netid,
                request.r_addr,
                request.r_owner ?? string.Empty));
        }

        /// <summary>
        /// Unregisters a portmap mapping payload.
        /// </summary>
        /// <param name="request">The portmap unregistration payload.</param>
        /// <returns><c>true</c> when a registration was removed; otherwise <c>false</c>.</returns>
        public bool Unregister(mapping request)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (!RpcBindNetId.TryGetProtocol(request.prot, out RpcBindingProtocol protocol))
            {
                return false;
            }

            return RemoveRegistration(request.prog, request.vers, protocol);
        }

        /// <summary>
        /// Unregisters an rpcbind payload.
        /// </summary>
        /// <param name="request">The rpcbind unregistration payload.</param>
        /// <returns><c>true</c> when a registration was removed; otherwise <c>false</c>.</returns>
        public bool Unregister(rpcb request)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (request.r_netid is null || !RpcBindNetId.TryGetProtocol(request.r_netid, out RpcBindingProtocol protocol))
            {
                return false;
            }

            return RemoveRegistration(request.r_prog, request.r_vers, protocol);
        }

        private static byte[] EncodeBoolean(bool value)
        {
            return RpcBindPayloadCodec.WritePayload(value, static (writer, payload) => writer.WriteBoolean(payload));
        }

        private static byte[] EncodeString(string value)
        {
            return RpcBindPayloadCodec.WritePayload(value, static (writer, payload) => writer.WriteString(payload));
        }

        private static byte[] EncodeUInt32(uint value)
        {
            return RpcBindPayloadCodec.WritePayload(value, static (writer, payload) => writer.WriteUInt32(payload));
        }

        private static RpcMessageEnvelope CreateGarbageArgsReply(uint xid)
        {
            return RpcMessageFactory.CreateAcceptedReply(xid, accept_stat.GARBAGE_ARGS);
        }

        private static RpcMessageEnvelope CreateProcUnavailableReply(uint xid)
        {
            return RpcMessageFactory.CreateAcceptedReply(xid, accept_stat.PROC_UNAVAIL);
        }

        private RpcMessageEnvelope DispatchPortmapCall(RpcMessageEnvelope request, uint procedure)
        {
            try
            {
                switch (procedure)
                {
                    case (uint)PMAP_PROG_Program.Procedure_PMAP_VERS_PMAPPROC_NULL:
                        return RpcMessageFactory.CreateAcceptedReply(request.Header.xid, accept_stat.SUCCESS);
                    case (uint)PMAP_PROG_Program.Procedure_PMAP_VERS_PMAPPROC_SET:
                    {
                        mapping registration = RpcBindPayloadCodec.ReadPayload(request.ProcedurePayload, mapping.ReadFrom);
                        return RpcMessageFactory.CreateAcceptedReply(
                            request.Header.xid,
                            accept_stat.SUCCESS,
                            procedurePayload: EncodeBoolean(Register(registration)));
                    }

                    case (uint)PMAP_PROG_Program.Procedure_PMAP_VERS_PMAPPROC_UNSET:
                    {
                        mapping registration = RpcBindPayloadCodec.ReadPayload(request.ProcedurePayload, mapping.ReadFrom);
                        return RpcMessageFactory.CreateAcceptedReply(
                            request.Header.xid,
                            accept_stat.SUCCESS,
                            procedurePayload: EncodeBoolean(Unregister(registration)));
                    }

                    case (uint)PMAP_PROG_Program.Procedure_PMAP_VERS_PMAPPROC_GETPORT:
                    {
                        mapping lookup = RpcBindPayloadCodec.ReadPayload(request.ProcedurePayload, mapping.ReadFrom);
                        return RpcMessageFactory.CreateAcceptedReply(
                            request.Header.xid,
                            accept_stat.SUCCESS,
                            procedurePayload: EncodeUInt32(GetPort(lookup)));
                    }

                    default:
                        return CreateProcUnavailableReply(request.Header.xid);
                }
            }
            catch (XdrDataException)
            {
                return CreateGarbageArgsReply(request.Header.xid);
            }
        }

        private RpcMessageEnvelope DispatchRpcbindCall(RpcMessageEnvelope request, uint version, uint procedure)
        {
            try
            {
                switch (procedure)
                {
                    case 0:
                        return RpcMessageFactory.CreateAcceptedReply(request.Header.xid, accept_stat.SUCCESS);
                    case 1:
                    {
                        rpcb registration = RpcBindPayloadCodec.ReadPayload(request.ProcedurePayload, rpcb.ReadFrom);
                        return RpcMessageFactory.CreateAcceptedReply(
                            request.Header.xid,
                            accept_stat.SUCCESS,
                            procedurePayload: EncodeBoolean(Register(registration)));
                    }

                    case 2:
                    {
                        rpcb registration = RpcBindPayloadCodec.ReadPayload(request.ProcedurePayload, rpcb.ReadFrom);
                        return RpcMessageFactory.CreateAcceptedReply(
                            request.Header.xid,
                            accept_stat.SUCCESS,
                            procedurePayload: EncodeBoolean(Unregister(registration)));
                    }

                    case 3:
                    case 9 when version == (uint)RPCBPROG_Program.Version_RPCBVERS4:
                    {
                        rpcb lookup = RpcBindPayloadCodec.ReadPayload(request.ProcedurePayload, rpcb.ReadFrom);
                        return RpcMessageFactory.CreateAcceptedReply(
                            request.Header.xid,
                            accept_stat.SUCCESS,
                            procedurePayload: EncodeString(GetUniversalAddress(lookup)));
                    }

                    default:
                        return CreateProcUnavailableReply(request.Header.xid);
                }
            }
            catch (XdrDataException)
            {
                return CreateGarbageArgsReply(request.Header.xid);
            }
        }

        private RpcBindingRegistration? FindRegistration(uint programNumber, uint versionNumber, RpcBindingProtocol protocol)
        {
            return registrations.FirstOrDefault(
                registration => registration.ProgramNumber == programNumber
                    && registration.VersionNumber == versionNumber
                    && registration.Protocol == protocol);
        }

        private bool RegisterInternal(RpcBindingRegistration registration)
        {
            RpcBindingRegistration? existingRegistration = FindRegistration(
                registration.ProgramNumber,
                registration.VersionNumber,
                registration.Protocol);

            if (existingRegistration is not null)
            {
                bool identicalRegistration = existingRegistration.Port == registration.Port
                    && string.Equals(existingRegistration.NetId, registration.NetId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(existingRegistration.UniversalAddress, registration.UniversalAddress, StringComparison.Ordinal)
                    && string.Equals(existingRegistration.Owner, registration.Owner, StringComparison.Ordinal);

                if (!identicalRegistration)
                {
                    return false;
                }

                return true;
            }

            registrations.Add(registration);
            return true;
        }

        private bool RemoveRegistration(uint programNumber, uint versionNumber, RpcBindingProtocol protocol)
        {
            RpcBindingRegistration? existingRegistration = FindRegistration(programNumber, versionNumber, protocol);
            if (existingRegistration is null)
            {
                return false;
            }

            return registrations.Remove(existingRegistration);
        }
    }
}
