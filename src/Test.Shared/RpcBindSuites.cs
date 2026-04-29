namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcBind;
    using OpenNFS.Rpc.RpcMessages;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// Touchstone suites covering rpcbind and portmap registration flows.
    /// </summary>
    public static class RpcBindSuites
    {
        /// <summary>
        /// Creates the shared rpcbind suite catalog.
        /// </summary>
        /// <returns>The configured suite descriptor.</returns>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(
                suiteId: "RpcBindSuites",
                displayName: "Rpcbind and Portmap",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        suiteId: "RpcBindSuites",
                        caseId: "RegisterLookupUnregister",
                        displayName: "Rpcbind and portmap register, lookup, and unregister mappings",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: _ =>
                        {
                            RpcBindService service = new RpcBindService();

                            mapping nfsTcpMapping = new mapping
                            {
                                prog = (uint)NFS_PROGRAM_Program.Program,
                                vers = (uint)NFS_PROGRAM_Program.Version_NFS_V3,
                                prot = (uint)RpcBindingProtocol.Tcp,
                                port = 2049,
                            };

                            RpcMessageEnvelope portmapSetReply = service.Dispatch(
                                PortmapProtocolCodec.CreateSetCall(
                                    xid: 0x01010101,
                                    registration: nfsTcpMapping));

                            if (!PortmapProtocolCodec.ReadBooleanReply(portmapSetReply))
                            {
                                throw new InvalidOperationException("Expected portmap registration for NFSv3/TCP to succeed.");
                            }

                            RpcMessageEnvelope portmapLookupReply = service.Dispatch(
                                PortmapProtocolCodec.CreateGetPortCall(
                                    xid: 0x01010102,
                                    request: new mapping
                                    {
                                        prog = nfsTcpMapping.prog,
                                        vers = nfsTcpMapping.vers,
                                        prot = nfsTcpMapping.prot,
                                    }));

                            if (PortmapProtocolCodec.ReadPortReply(portmapLookupReply) != 2049)
                            {
                                throw new InvalidOperationException("Expected portmap lookup to resolve the registered NFSv3/TCP port.");
                            }

                            RpcMessageEnvelope rpcbindLookupReply = service.Dispatch(
                                RpcBindProtocolCodec.CreateGetAddressCall(
                                    xid: 0x01010103,
                                    rpcbindVersion: (uint)RPCBPROG_Program.Version_RPCBVERS4,
                                    request: new rpcb
                                    {
                                        r_prog = nfsTcpMapping.prog,
                                        r_vers = nfsTcpMapping.vers,
                                        r_netid = "tcp",
                                        r_addr = string.Empty,
                                        r_owner = string.Empty,
                                    }));

                            string nfsUniversalAddress = RpcBindProtocolCodec.ReadAddressReply(rpcbindLookupReply);
                            if (!string.Equals(nfsUniversalAddress, "127.0.0.1.8.1", StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException("Expected rpcbind lookup to derive the universal address for the registered NFSv3/TCP mapping.");
                            }

                            rpcb mountUdpRegistration = new rpcb
                            {
                                r_prog = (uint)MOUNT_PROGRAM_Program.Program,
                                r_vers = (uint)MOUNT_PROGRAM_Program.Version_MOUNT_V3,
                                r_netid = "udp",
                                r_addr = "127.0.0.1.8.2",
                                r_owner = "sample-owner",
                            };

                            RpcMessageEnvelope rpcbindSetReply = service.Dispatch(
                                RpcBindProtocolCodec.CreateSetCall(
                                    xid: 0x01010104,
                                    rpcbindVersion: (uint)RPCBPROG_Program.Version_RPCBVERS4,
                                    registration: mountUdpRegistration));

                            if (!RpcBindProtocolCodec.ReadBooleanReply(rpcbindSetReply))
                            {
                                throw new InvalidOperationException("Expected rpcbind registration for MOUNTv3/UDP to succeed.");
                            }

                            RpcMessageEnvelope mountPortLookupReply = service.Dispatch(
                                PortmapProtocolCodec.CreateGetPortCall(
                                    xid: 0x01010105,
                                    request: new mapping
                                    {
                                        prog = mountUdpRegistration.r_prog,
                                        vers = mountUdpRegistration.r_vers,
                                        prot = (uint)RpcBindingProtocol.Udp,
                                    }));

                            if (PortmapProtocolCodec.ReadPortReply(mountPortLookupReply) != 2050)
                            {
                                throw new InvalidOperationException("Expected rpcbind registration to be visible through portmap UDP lookup.");
                            }

                            RpcMessageEnvelope rpcbindUnsetReply = service.Dispatch(
                                RpcBindProtocolCodec.CreateUnsetCall(
                                    xid: 0x01010106,
                                    rpcbindVersion: (uint)RPCBPROG_Program.Version_RPCBVERS4,
                                    registration: new rpcb
                                    {
                                        r_prog = nfsTcpMapping.prog,
                                        r_vers = nfsTcpMapping.vers,
                                        r_netid = "tcp",
                                        r_addr = string.Empty,
                                        r_owner = string.Empty,
                                    }));

                            if (!RpcBindProtocolCodec.ReadBooleanReply(rpcbindUnsetReply))
                            {
                                throw new InvalidOperationException("Expected rpcbind unregistration for NFSv3/TCP to succeed.");
                            }

                            RpcMessageEnvelope portmapUnsetReply = service.Dispatch(
                                PortmapProtocolCodec.CreateUnsetCall(
                                    xid: 0x01010107,
                                    registration: new mapping
                                    {
                                        prog = mountUdpRegistration.r_prog,
                                        vers = mountUdpRegistration.r_vers,
                                        prot = (uint)RpcBindingProtocol.Udp,
                                    }));

                            if (!PortmapProtocolCodec.ReadBooleanReply(portmapUnsetReply))
                            {
                                throw new InvalidOperationException("Expected portmap unregistration for MOUNTv3/UDP to succeed.");
                            }

                            RpcMessageEnvelope postUnmountPortLookupReply = service.Dispatch(
                                PortmapProtocolCodec.CreateGetPortCall(
                                    xid: 0x01010108,
                                    request: new mapping
                                    {
                                        prog = nfsTcpMapping.prog,
                                        vers = nfsTcpMapping.vers,
                                        prot = (uint)RpcBindingProtocol.Tcp,
                                    }));

                            if (PortmapProtocolCodec.ReadPortReply(postUnmountPortLookupReply) != 0)
                            {
                                throw new InvalidOperationException("Expected the unregistered NFSv3/TCP mapping to resolve to port 0.");
                            }

                            RpcMessageEnvelope postUnmountAddressLookupReply = service.Dispatch(
                                RpcBindProtocolCodec.CreateGetAddressCall(
                                    xid: 0x01010109,
                                    rpcbindVersion: (uint)RPCBPROG_Program.Version_RPCBVERS4,
                                    request: new rpcb
                                    {
                                        r_prog = mountUdpRegistration.r_prog,
                                        r_vers = mountUdpRegistration.r_vers,
                                        r_netid = "udp",
                                        r_addr = string.Empty,
                                        r_owner = string.Empty,
                                    }));

                            if (!string.IsNullOrEmpty(RpcBindProtocolCodec.ReadAddressReply(postUnmountAddressLookupReply)))
                            {
                                throw new InvalidOperationException("Expected the unregistered MOUNTv3/UDP mapping to resolve to an empty universal address.");
                            }

                            IReadOnlyList<RpcBindingRegistration> remainingBindings = service.GetBindings();
                            if (remainingBindings.Count != 0)
                            {
                                throw new InvalidOperationException(
                                    "Expected all rpcbind registrations to be removed, but found: "
                                    + string.Join(", ", remainingBindings.Select(static binding => binding.ProgramNumber + "/" + binding.VersionNumber + "/" + binding.Protocol.ToString())));
                            }

                            return Task.CompletedTask;
                        })
                });
        }
    }
}
