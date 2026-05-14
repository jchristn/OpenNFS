namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client;
    using OpenNFS.Client.Compound;
    using OpenNFS.Client.Internal;
    using OpenNFS.Client.Raw;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Transport;
    using OpenNFS.Rpc.Xdr;
    using RpcGenerated = OpenNFS.Rpc.Generated;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;
    using static Test.Shared.ClientGroupedSuiteSupport;    /// <summary>
    /// Grouped client surface exposure and request-shape suites.
    /// </summary>
    internal static class ClientGroupedFoundationShapeCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                    new TestCaseDescriptor(
                        suiteId: "ClientGroupedSuites",
                        caseId: "GroupedApisAreExposedOnClient",
                        displayName: "Client exposes grouped convenience API categories alongside low-level access",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: _ =>
                        {
                            OpenNfsClient client = new OpenNfsClientBuilder().Build();

                            if (client.Directories is null
                                || client.Files is null
                                || client.Exports is null
                                || client.Locks is null
                                || client.Sessions is null
                                || client.Administration is null)
                            {
                                throw new InvalidOperationException("Expected the client to expose all grouped convenience API categories.");
                            }

                            return Task.CompletedTask;

                        }),

                    new TestCaseDescriptor(
                        suiteId: "ClientGroupedSuites",
                        caseId: "ExportAndAdministrationApisTargetExpectedPrograms",
                        displayName: "Export and administration APIs target the expected ONC RPC programs",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            OpenNfsClient client = new OpenNfsClientBuilder()
                                .WithPrimaryEndpoint("exports.example", 2049)
                                .Build();

                            await client.OpenAsync(cancellationToken).ConfigureAwait(false);

                            OpenNfsV3ProcedurePlan mountPlan = await client.Exports.PrepareMountV3Async("/srv/share", cancellationToken).ConfigureAwait(false);
                            OpenNfsV3ProcedurePlan exportPlan = await client.Exports.PrepareListExportsV3Async(cancellationToken).ConfigureAwait(false);
                            OpenNfsV3ProcedurePlan pingPlan = await client.Administration.PreparePingNlmV4Async(cancellationToken).ConfigureAwait(false);

                            if (mountPlan.ProgramNumber != 100005
                                || mountPlan.VersionNumber != 3
                                || mountPlan.ProcedureNumber != 1
                                || !mountPlan.ProcedurePayload.Span.SequenceEqual(new byte[]
                                {
                                    0x00, 0x00, 0x00, 0x0A,
                                    0x2F, 0x73, 0x72, 0x76, 0x2F, 0x73, 0x68, 0x61, 0x72, 0x65,
                                    0x00, 0x00,
                                }))
                            {
                                throw new InvalidOperationException("Expected the grouped export API to target the MOUNT v3 program and XDR-encode the export path.");
                            }

                            if (exportPlan.ProgramNumber != 100005
                                || exportPlan.VersionNumber != 3
                                || exportPlan.ProcedureNumber != 5
                                || exportPlan.ProcedurePayload.Length != 0)
                            {
                                throw new InvalidOperationException("Expected the grouped export API to target the MOUNT v3 EXPORT procedure.");
                            }

                            if (pingPlan.ProgramNumber != 100021
                                || pingPlan.VersionNumber != 4
                                || pingPlan.ProcedureNumber != 0
                                || pingPlan.ProcedurePayload.Length != 0)
                            {
                                throw new InvalidOperationException("Expected the grouped administration API to target the NLM v4 NULL procedure.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ClientGroupedSuites",
                        caseId: "DirectoryFileSessionAndLockApisUseExpectedDefaults",
                        displayName: "Directory, file, session, and lock APIs use the expected defaults and payload encoding",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            OpenNfsClient client = new OpenNfsClientBuilder()
                                .WithPrimaryEndpoint("grouped.example", 2049)
                                .Build();

                            await client.OpenAsync(cancellationToken).ConfigureAwait(false);

                            OpenNfsV3ProcedurePlan lookupPlan = await client.Directories.PrepareLookupV3Async(
                                new byte[] { 0xAA, 0xBB, 0xCC },
                                "log",
                                cancellationToken).ConfigureAwait(false);

                            OpenNfsV3ProcedurePlan readPlan = await client.Files.PrepareReadV3Async(
                                new byte[] { 0x01, 0x02 },
                                0x0102030405060708UL,
                                4096,
                                cancellationToken).ConfigureAwait(false);

                            OpenNfsV3ProcedurePlan accessPlan = await client.Files.PrepareAccessV3Async(
                                new byte[] { 0x05, 0x06 },
                                OpenNfsV3AccessMask.Read | OpenNfsV3AccessMask.Lookup,
                                cancellationToken).ConfigureAwait(false);

                            OpenNfsV3ProcedurePlan readdirPlusPlan = await client.Directories.PrepareReadDirectoryPlusV3Async(
                                new byte[] { 0x0A, 0x0B, 0x0C, 0x0D },
                                0x1112131415161718UL,
                                new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 },
                                2048,
                                4096,
                                cancellationToken).ConfigureAwait(false);

                            OpenNfsV3ProcedurePlan readLinkPlan = await client.Files.PrepareReadLinkV3Async(
                                new byte[] { 0x30, 0x31, 0x32 },
                                cancellationToken).ConfigureAwait(false);

                            OpenNfsV3ProcedurePlan fsInfoPlan = await client.Files.PrepareFileSystemInfoV3Async(
                                new byte[] { 0x44 },
                                cancellationToken).ConfigureAwait(false);

                            OpenNfsCompoundOperation[] operations =
                            {
                                new OpenNfsCompoundOperation(24, Array.Empty<byte>()),
                            };

                            OpenNfsCompoundPlan sessionPlan = await client.Sessions.PrepareAsync(operations, cancellationToken).ConfigureAwait(false);
                            OpenNfsCompoundPlan lockPlan = await client.Locks.PrepareV4Async(operations, cancellationToken).ConfigureAwait(false);

                            if (lookupPlan.ProgramNumber != 100003
                                || lookupPlan.VersionNumber != 3
                                || lookupPlan.ProcedureNumber != 3
                                || !lookupPlan.ProcedurePayload.Span.SequenceEqual(new byte[]
                                {
                                    0x00, 0x00, 0x00, 0x03,
                                    0xAA, 0xBB, 0xCC, 0x00,
                                    0x00, 0x00, 0x00, 0x03,
                                    0x6C, 0x6F, 0x67, 0x00,
                                }))
                            {
                                throw new InvalidOperationException("Expected the grouped directory API to target NFSv3 LOOKUP and XDR-encode the handle/name pair.");
                            }

                            if (readPlan.ProgramNumber != 100003
                                || readPlan.VersionNumber != 3
                                || readPlan.ProcedureNumber != 6
                                || !readPlan.ProcedurePayload.Span.SequenceEqual(new byte[]
                                {
                                    0x00, 0x00, 0x00, 0x02,
                                    0x01, 0x02, 0x00, 0x00,
                                    0x01, 0x02, 0x03, 0x04,
                                    0x05, 0x06, 0x07, 0x08,
                                    0x00, 0x00, 0x10, 0x00,
                                }))
                            {
                                throw new InvalidOperationException("Expected the grouped file API to target NFSv3 READ and XDR-encode the handle, offset, and count.");
                            }

                            if (accessPlan.ProgramNumber != 100003
                                || accessPlan.VersionNumber != 3
                                || accessPlan.ProcedureNumber != 4
                                || !accessPlan.ProcedurePayload.Span.SequenceEqual(new byte[]
                                {
                                    0x00, 0x00, 0x00, 0x02,
                                    0x05, 0x06, 0x00, 0x00,
                                    0x00, 0x00, 0x00, 0x03,
                                }))
                            {
                                throw new InvalidOperationException("Expected the grouped file API to target NFSv3 ACCESS and XDR-encode the requested access mask.");
                            }

                            if (readdirPlusPlan.ProgramNumber != 100003
                                || readdirPlusPlan.VersionNumber != 3
                                || readdirPlusPlan.ProcedureNumber != 17
                                || !readdirPlusPlan.ProcedurePayload.Span.SequenceEqual(new byte[]
                                {
                                    0x00, 0x00, 0x00, 0x04,
                                    0x0A, 0x0B, 0x0C, 0x0D,
                                    0x11, 0x12, 0x13, 0x14,
                                    0x15, 0x16, 0x17, 0x18,
                                    0x01, 0x02, 0x03, 0x04,
                                    0x05, 0x06, 0x07, 0x08,
                                    0x00, 0x00, 0x08, 0x00,
                                    0x00, 0x00, 0x10, 0x00,
                                }))
                            {
                                throw new InvalidOperationException("Expected the grouped directory API to target NFSv3 READDIRPLUS and XDR-encode the cookie, verifier, and count fields.");
                            }

                            if (readLinkPlan.ProgramNumber != 100003
                                || readLinkPlan.VersionNumber != 3
                                || readLinkPlan.ProcedureNumber != 5
                                || !readLinkPlan.ProcedurePayload.Span.SequenceEqual(new byte[]
                                {
                                    0x00, 0x00, 0x00, 0x03,
                                    0x30, 0x31, 0x32, 0x00,
                                }))
                            {
                                throw new InvalidOperationException("Expected the grouped file API to target NFSv3 READLINK and XDR-encode the symbolic-link filehandle.");
                            }

                            if (fsInfoPlan.ProgramNumber != 100003
                                || fsInfoPlan.VersionNumber != 3
                                || fsInfoPlan.ProcedureNumber != 19
                                || !fsInfoPlan.ProcedurePayload.Span.SequenceEqual(new byte[]
                                {
                                    0x00, 0x00, 0x00, 0x01,
                                    0x44, 0x00, 0x00, 0x00,
                                }))
                            {
                                throw new InvalidOperationException("Expected the grouped file API to target NFSv3 FSINFO and XDR-encode the filesystem root handle.");
                            }

                            if (sessionPlan.ProtocolVersion != OpenNfsProtocolVersion.Nfs41
                                || !string.Equals(sessionPlan.Tag, "session", StringComparison.Ordinal)
                                || sessionPlan.TransportPolicy != OpenNfsClientTransportPolicy.TcpOnly)
                            {
                                throw new InvalidOperationException("Expected the grouped session API to default to NFSv4.1 and the session tag while preserving TCP-only COMPOUND transport.");
                            }

                            if (lockPlan.ProtocolVersion != OpenNfsProtocolVersion.Nfs40
                                || !string.Equals(lockPlan.Tag, "lock", StringComparison.Ordinal)
                                || lockPlan.TransportPolicy != OpenNfsClientTransportPolicy.TcpOnly)
                            {
                                throw new InvalidOperationException("Expected the grouped lock API to default to NFSv4.0 and the lock tag while preserving TCP-only COMPOUND transport.");
                            }
                        }),
            };
        }
    }
}
