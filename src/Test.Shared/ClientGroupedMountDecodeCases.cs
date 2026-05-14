namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using OpenNFS.Client;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Xdr;
    using RpcGenerated = OpenNFS.Rpc.Generated;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;
    using static Test.Shared.ClientGroupedSuiteSupport;

    internal static class ClientGroupedMountDecodeCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                new TestCaseDescriptor(
                    suiteId: "ClientGroupedSuites",
                    caseId: "MountApisDecodeTypedReplies",
                    displayName: "Grouped export APIs decode typed MOUNT v3 replies without requiring an open client",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: _ =>
                    {
                        OpenNfsClient client = new OpenNfsClientBuilder().Build();

                        OpenNfsMountV3Result mountResult = client.Exports.ReadMountV3Result(
                            EncodeAcceptedReply(
                                new mountres3
                                {
                                    fhs_status = mountstat3.MNT3_OK,
                                    mountinfo = new mountres3_ok
                                    {
                                        fhandle = new fhandle3
                                        {
                                            Value = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD },
                                        },
                                        auth_flavors = new[] { 0, 1, 6 },
                                    },
                                },
                                static (value, writer) => value.WriteTo(writer)));

                        if (!mountResult.IsSuccess
                            || mountResult.Status != OpenNfsMountV3Status.Ok
                            || !mountResult.RootFileHandle.Span.SequenceEqual(new byte[] { 0xAA, 0xBB, 0xCC, 0xDD })
                            || mountResult.SupportedAuthenticationFlavors.Count != 3
                            || mountResult.SupportedAuthenticationFlavors[0] != OpenNfsRpcAuthenticationFlavor.AuthNone
                            || mountResult.SupportedAuthenticationFlavors[1] != OpenNfsRpcAuthenticationFlavor.AuthSys
                            || mountResult.SupportedAuthenticationFlavors[2] != OpenNfsRpcAuthenticationFlavor.RpcSecGss)
                        {
                            throw new InvalidOperationException("Expected the grouped export API to decode a successful MOUNT v3 MNT reply into a typed result model.");
                        }

                        OpenNfsMountV3Result deniedMountResult = client.Exports.ReadMountV3Result(
                            EncodeAcceptedReply(
                                new mountres3
                                {
                                    fhs_status = mountstat3.MNT3ERR_ACCES,
                                },
                                static (value, writer) => value.WriteTo(writer)));

                        if (deniedMountResult.IsSuccess
                            || deniedMountResult.Status != OpenNfsMountV3Status.AccessDenied
                            || deniedMountResult.RootFileHandle.Length != 0
                            || deniedMountResult.SupportedAuthenticationFlavors.Count != 0)
                        {
                            throw new InvalidOperationException("Expected non-successful MOUNT v3 MNT replies to surface status without success-only payload data.");
                        }

                        IReadOnlyList<OpenNfsExportV3Entry> exports = client.Exports.ReadListExportsV3Result(
                            EncodeAcceptedReply(
                                new exports
                                {
                                    Value = new exportnode
                                    {
                                        ex_dir = new dirpath
                                        {
                                            Value = "/srv/share",
                                        },
                                        ex_groups = new groups
                                        {
                                            Value = new groupnode
                                            {
                                                gr_name = new name
                                                {
                                                    Value = "ops",
                                                },
                                                gr_next = new groups
                                                {
                                                    Value = new groupnode
                                                    {
                                                        gr_name = new name
                                                        {
                                                            Value = "qa",
                                                        },
                                                        gr_next = new groups(),
                                                    },
                                                },
                                            },
                                        },
                                        ex_next = new exports
                                        {
                                            Value = new exportnode
                                            {
                                                ex_dir = new dirpath
                                                {
                                                    Value = "/srv/public",
                                                },
                                                ex_groups = new groups(),
                                                ex_next = new exports(),
                                            },
                                        },
                                    },
                                },
                                static (value, writer) => value.WriteTo(writer)));

                        if (exports.Count != 2
                            || !string.Equals(exports[0].ExportPath, "/srv/share", StringComparison.Ordinal)
                            || exports[0].AuthorizedClientGroups.Count != 2
                            || !string.Equals(exports[0].AuthorizedClientGroups[0], "ops", StringComparison.Ordinal)
                            || !string.Equals(exports[0].AuthorizedClientGroups[1], "qa", StringComparison.Ordinal)
                            || !string.Equals(exports[1].ExportPath, "/srv/public", StringComparison.Ordinal)
                            || exports[1].AuthorizedClientGroups.Count != 0)
                        {
                            throw new InvalidOperationException("Expected the grouped export API to decode typed MOUNT v3 EXPORT entries.");
                        }

                        IReadOnlyList<OpenNfsMountedExportV3Entry> mountedExports = client.Exports.ReadListMountsV3Result(
                            EncodeAcceptedReply(
                                new mountlist
                                {
                                    Value = new mountbody
                                    {
                                        ml_hostname = new name
                                        {
                                            Value = "client-a",
                                        },
                                        ml_directory = new dirpath
                                        {
                                            Value = "/srv/share",
                                        },
                                        ml_next = new mountlist
                                        {
                                            Value = new mountbody
                                            {
                                                ml_hostname = new name
                                                {
                                                    Value = "client-b",
                                                },
                                                ml_directory = new dirpath
                                                {
                                                    Value = "/srv/public",
                                                },
                                                ml_next = new mountlist(),
                                            },
                                        },
                                    },
                                },
                                static (value, writer) => value.WriteTo(writer)));

                        if (mountedExports.Count != 2
                            || !string.Equals(mountedExports[0].HostName, "client-a", StringComparison.Ordinal)
                            || !string.Equals(mountedExports[0].ExportPath, "/srv/share", StringComparison.Ordinal)
                            || !string.Equals(mountedExports[1].HostName, "client-b", StringComparison.Ordinal)
                            || !string.Equals(mountedExports[1].ExportPath, "/srv/public", StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException("Expected the grouped export API to decode typed MOUNT v3 DUMP entries.");
                        }

                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor(
                    suiteId: "ClientGroupedSuites",
                    caseId: "MountApisRejectRpcFailuresAndUnexpectedVoidPayloads",
                    displayName: "Grouped export reply decoders reject RPC failures and unexpected UMNT payload bytes",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: _ =>
                    {
                        OpenNfsClient client = new OpenNfsClientBuilder().Build();

                        client.Exports.ReadUnmountV3Result(EncodeAcceptedSuccessReply(Array.Empty<byte>()));
                        client.Exports.ReadUnmountAllV3Result(EncodeAcceptedSuccessReply(Array.Empty<byte>()));

                        try
                        {
                            client.Exports.ReadUnmountV3Result(EncodeAcceptedSuccessReply(new byte[] { 0x00, 0x00, 0x00, 0x01 }));
                            throw new InvalidOperationException("Expected UMNT reply validation to reject unexpected procedure payload bytes.");
                        }
                        catch (InvalidDataException exception)
                        {
                            if (!exception.Message.Contains("must not carry a procedure result payload", StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException("Expected the UMNT reply validation failure to explain the unexpected payload bytes.");
                            }
                        }

                        try
                        {
                            client.Exports.ReadListExportsV3Result(
                                RpcMessageCodec.Encode(
                                    RpcMessageFactory.CreateRejectedReply(
                                        xid: 0x01020304,
                                        status: RpcGenerated.reject_stat.AUTH_ERROR,
                                        authenticationStatus: RpcGenerated.auth_stat.AUTH_BADCRED)));
                            throw new InvalidOperationException("Expected grouped MOUNT reply decoding to reject denied RPC replies.");
                        }
                        catch (InvalidDataException exception)
                        {
                            if (!exception.Message.Contains("AUTH_ERROR", StringComparison.Ordinal)
                                || !exception.Message.Contains("AUTH_BADCRED", StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException("Expected denied RPC replies to surface both the rejected status and authentication failure code.");
                            }
                        }

                        return Task.CompletedTask;
                    }),
            };
        }
    }
}
