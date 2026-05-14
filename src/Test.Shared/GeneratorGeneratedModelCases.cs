namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using NfsV3Generated = OpenNFS.Protocol.V3.Generated;
    using NfsV41Generated = OpenNFS.Protocol.V41.Generated;
    using RpcGenerated = OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.Xdr;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// Generated RPC and cross-project model round-trip suites.
    /// </summary>
    internal static class GeneratorGeneratedModelCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                new TestCaseDescriptor(
                    suiteId: "RpcXdrSuites",
                    caseId: "GeneratedRpcModelRoundTrip",
                    displayName: "Generated RPC models round-trip through the shared XDR runtime",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: _ =>
                    {
                        RpcGenerated.reply_body expected = new RpcGenerated.reply_body
                        {
                            stat = RpcGenerated.reply_stat.MSG_ACCEPTED,
                            areply = new RpcGenerated.accepted_reply
                            {
                                verf = new RpcGenerated.opaque_auth
                                {
                                    flavor = RpcGenerated.auth_flavor.AUTH_SYS,
                                    body = new byte[] { 0xAA, 0xBB, 0xCC },
                                },
                                reply_data = new RpcGenerated.accepted_reply_reply_data
                                {
                                    stat = RpcGenerated.accept_stat.PROG_MISMATCH,
                                    mismatch_info = new RpcGenerated.accepted_reply_reply_data_mismatch_info
                                    {
                                        low = 2,
                                        high = 4,
                                    },
                                },
                            },
                        };

                        XdrWriter writer = new XdrWriter();
                        expected.WriteTo(writer);

                        XdrReader reader = new XdrReader(writer.ToArray());
                        RpcGenerated.reply_body actual = RpcGenerated.reply_body.ReadFrom(reader);
                        reader.EnsureFullyConsumed();

                        if (actual.stat != RpcGenerated.reply_stat.MSG_ACCEPTED)
                        {
                            throw new InvalidOperationException("Expected generated RPC union discriminant to round-trip exactly.");
                        }

                        if (actual.areply?.verf?.flavor != RpcGenerated.auth_flavor.AUTH_SYS)
                        {
                            throw new InvalidOperationException("Expected generated RPC enum field to round-trip exactly.");
                        }

                        if (actual.areply?.verf?.body is null || !actual.areply.verf.body.SequenceEqual(new byte[] { 0xAA, 0xBB, 0xCC }))
                        {
                            throw new InvalidOperationException("Expected generated RPC opaque payload to round-trip exactly.");
                        }

                        if (actual.areply?.reply_data?.stat != RpcGenerated.accept_stat.PROG_MISMATCH
                            || actual.areply.reply_data.mismatch_info?.low != 2
                            || actual.areply.reply_data.mismatch_info?.high != 4)
                        {
                            throw new InvalidOperationException("Expected generated RPC nested union payload to round-trip exactly.");
                        }

                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor(
                    suiteId: "RpcXdrSuites",
                    caseId: "GeneratedNfsV3ModelRoundTrip",
                    displayName: "Generated NFSv3 typedef and struct models round-trip through the shared XDR runtime",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: _ =>
                    {
                        NfsV3Generated.diropargs3 expected = new NfsV3Generated.diropargs3
                        {
                            dir = new NfsV3Generated.nfs_fh3
                            {
                                data = new byte[] { 0x10, 0x20, 0x30, 0x40 },
                            },
                            name = new NfsV3Generated.filename3
                            {
                                Value = "export-root",
                            },
                        };

                        XdrWriter writer = new XdrWriter();
                        expected.WriteTo(writer);

                        XdrReader reader = new XdrReader(writer.ToArray());
                        NfsV3Generated.diropargs3 actual = NfsV3Generated.diropargs3.ReadFrom(reader);
                        reader.EnsureFullyConsumed();

                        if (actual.dir?.data is null || !actual.dir.data.SequenceEqual(new byte[] { 0x10, 0x20, 0x30, 0x40 }))
                        {
                            throw new InvalidOperationException("Expected generated NFSv3 opaque filehandle data to round-trip exactly.");
                        }

                        if (!string.Equals(actual.name?.Value, "export-root", StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException("Expected generated NFSv3 typedef-backed filename to round-trip exactly.");
                        }

                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor(
                    suiteId: "RpcXdrSuites",
                    caseId: "GeneratedCrossProjectModelRoundTrip",
                    displayName: "Generated cross-project NFSv4.1 models round-trip through the shared XDR runtime",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: _ =>
                    {
                        NfsV41Generated.callback_sec_parms4 expected = new NfsV41Generated.callback_sec_parms4
                        {
                            cb_secflavor = (uint)RpcGenerated.auth_flavor.AUTH_SYS,
                            cbsp_sys_cred = new RpcGenerated.authsys_parms
                            {
                                stamp = 7,
                                machinename = "callback-host",
                                uid = 42,
                                gid = 84,
                                gids = new uint[] { 9, 10, 11 },
                            },
                        };

                        XdrWriter writer = new XdrWriter();
                        expected.WriteTo(writer);

                        XdrReader reader = new XdrReader(writer.ToArray());
                        NfsV41Generated.callback_sec_parms4 actual = NfsV41Generated.callback_sec_parms4.ReadFrom(reader);
                        reader.EnsureFullyConsumed();

                        if (actual.cb_secflavor != (uint)RpcGenerated.auth_flavor.AUTH_SYS)
                        {
                            throw new InvalidOperationException("Expected generated NFSv4.1 callback discriminant to round-trip exactly.");
                        }

                        if (actual.cbsp_sys_cred is null
                            || actual.cbsp_sys_cred.stamp != 7
                            || !string.Equals(actual.cbsp_sys_cred.machinename, "callback-host", StringComparison.Ordinal)
                            || actual.cbsp_sys_cred.uid != 42
                            || actual.cbsp_sys_cred.gid != 84
                            || actual.cbsp_sys_cred.gids is null
                            || !actual.cbsp_sys_cred.gids.SequenceEqual(new uint[] { 9, 10, 11 }))
                        {
                            throw new InvalidOperationException("Expected generated cross-project callback security payload to round-trip exactly.");
                        }

                        return Task.CompletedTask;
                    }),
            };
        }
    }
}
