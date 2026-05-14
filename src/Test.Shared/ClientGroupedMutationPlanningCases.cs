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
    /// Grouped mutation planning and NULL-reply validation suites.
    /// </summary>
    internal static class ClientGroupedMutationPlanningCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                    new TestCaseDescriptor(
                        suiteId: "ClientGroupedSuites",
                        caseId: "MutationAndAdministrationApisUseExpectedProgramsAndPayloadShapes",
                        displayName: "Grouped mutation and administration APIs target the expected programs and payload shapes",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            OpenNfsClient client = new OpenNfsClientBuilder()
                                .WithPrimaryEndpoint("mutations.example", 2049)
                                .Build();

                            await client.OpenAsync(cancellationToken).ConfigureAwait(false);

                            OpenNfsV3ProcedurePlan createFilePlan = await client.Directories.PrepareCreateFileV3Async(
                                new byte[] { 0x21, 0x22 },
                                "draft.txt",
                                failIfExists: true,
                                cancellationToken).ConfigureAwait(false);
                            OpenNfsV3ProcedurePlan createDirectoryPlan = await client.Directories.PrepareCreateDirectoryV3Async(
                                new byte[] { 0x31, 0x32 },
                                "docs",
                                cancellationToken).ConfigureAwait(false);
                            OpenNfsV3ProcedurePlan removeFilePlan = await client.Directories.PrepareRemoveFileV3Async(
                                new byte[] { 0x41, 0x42 },
                                "draft.txt",
                                cancellationToken).ConfigureAwait(false);
                            OpenNfsV3ProcedurePlan removeDirectoryPlan = await client.Directories.PrepareRemoveDirectoryV3Async(
                                new byte[] { 0x51, 0x52 },
                                "docs",
                                cancellationToken).ConfigureAwait(false);
                            OpenNfsV3ProcedurePlan renamePlan = await client.Directories.PrepareRenameV3Async(
                                new byte[] { 0x61, 0x62 },
                                "old.txt",
                                new byte[] { 0x71, 0x72 },
                                "new.txt",
                                cancellationToken).ConfigureAwait(false);
                            OpenNfsV3ProcedurePlan symbolicLinkPlan = await client.Directories.PrepareCreateSymbolicLinkV3Async(
                                new byte[] { 0x81, 0x82 },
                                "latest",
                                "../releases/current",
                                cancellationToken).ConfigureAwait(false);
                            OpenNfsV3ProcedurePlan hardLinkPlan = await client.Directories.PrepareCreateHardLinkV3Async(
                                new byte[] { 0x91, 0x92 },
                                new byte[] { 0xA1, 0xA2 },
                                "draft-link.txt",
                                cancellationToken).ConfigureAwait(false);
                            OpenNfsV3ProcedurePlan pingNfsPlan = await client.Administration.PreparePingNfsV3Async(cancellationToken).ConfigureAwait(false);
                            OpenNfsV3ProcedurePlan pingMountPlan = await client.Administration.PreparePingMountV3Async(cancellationToken).ConfigureAwait(false);
                            OpenNfsV3ProcedurePlan pingNlmPlan = await client.Administration.PreparePingNlmV4Async(cancellationToken).ConfigureAwait(false);

                            CREATE3args createFileArguments = CREATE3args.ReadFrom(new XdrReader(createFilePlan.ProcedurePayload));
                            MKDIR3args createDirectoryArguments = MKDIR3args.ReadFrom(new XdrReader(createDirectoryPlan.ProcedurePayload));
                            REMOVE3args removeFileArguments = REMOVE3args.ReadFrom(new XdrReader(removeFilePlan.ProcedurePayload));
                            RMDIR3args removeDirectoryArguments = RMDIR3args.ReadFrom(new XdrReader(removeDirectoryPlan.ProcedurePayload));
                            RENAME3args renameArguments = RENAME3args.ReadFrom(new XdrReader(renamePlan.ProcedurePayload));
                            SYMLINK3args symbolicLinkArguments = SYMLINK3args.ReadFrom(new XdrReader(symbolicLinkPlan.ProcedurePayload));
                            LINK3args hardLinkArguments = LINK3args.ReadFrom(new XdrReader(hardLinkPlan.ProcedurePayload));

                            if (createFilePlan.ProgramNumber != 100003
                                || createFilePlan.VersionNumber != 3
                                || createFilePlan.ProcedureNumber != 8
                                || createFileArguments.where?.dir?.data is null
                                || !createFileArguments.where.dir.data.AsSpan().SequenceEqual(new byte[] { 0x21, 0x22 })
                                || !string.Equals(createFileArguments.where.name?.Value, "draft.txt", StringComparison.Ordinal)
                                || createFileArguments.how?.mode != createmode3.GUARDED
                                || createFileArguments.how.obj_attributes is null)
                            {
                                throw new InvalidOperationException("Expected the grouped CREATE API to target NFSPROC3_CREATE with a guarded create payload.");
                            }

                            if (createDirectoryPlan.ProcedureNumber != 9
                                || createDirectoryArguments.where?.dir?.data is null
                                || !createDirectoryArguments.where.dir.data.AsSpan().SequenceEqual(new byte[] { 0x31, 0x32 })
                                || !string.Equals(createDirectoryArguments.where.name?.Value, "docs", StringComparison.Ordinal)
                                || createDirectoryArguments.attributes is null)
                            {
                                throw new InvalidOperationException("Expected the grouped MKDIR API to target NFSPROC3_MKDIR with the parent handle and directory name.");
                            }

                            if (removeFilePlan.ProcedureNumber != 12
                                || removeFileArguments.@object?.dir?.data is null
                                || !removeFileArguments.@object.dir.data.AsSpan().SequenceEqual(new byte[] { 0x41, 0x42 })
                                || !string.Equals(removeFileArguments.@object.name?.Value, "draft.txt", StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException("Expected the grouped REMOVE API to target NFSPROC3_REMOVE with a diropargs payload.");
                            }

                            if (removeDirectoryPlan.ProcedureNumber != 13
                                || removeDirectoryArguments.@object?.dir?.data is null
                                || !removeDirectoryArguments.@object.dir.data.AsSpan().SequenceEqual(new byte[] { 0x51, 0x52 })
                                || !string.Equals(removeDirectoryArguments.@object.name?.Value, "docs", StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException("Expected the grouped RMDIR API to target NFSPROC3_RMDIR with a diropargs payload.");
                            }

                            if (renamePlan.ProcedureNumber != 14
                                || renameArguments.from?.dir?.data is null
                                || renameArguments.to?.dir?.data is null
                                || !renameArguments.from.dir.data.AsSpan().SequenceEqual(new byte[] { 0x61, 0x62 })
                                || !renameArguments.to.dir.data.AsSpan().SequenceEqual(new byte[] { 0x71, 0x72 })
                                || !string.Equals(renameArguments.from.name?.Value, "old.txt", StringComparison.Ordinal)
                                || !string.Equals(renameArguments.to.name?.Value, "new.txt", StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException("Expected the grouped RENAME API to target NFSPROC3_RENAME with distinct source and destination diropargs payloads.");
                            }

                            if (symbolicLinkPlan.ProcedureNumber != 10
                                || symbolicLinkArguments.where?.dir?.data is null
                                || !symbolicLinkArguments.where.dir.data.AsSpan().SequenceEqual(new byte[] { 0x81, 0x82 })
                                || !string.Equals(symbolicLinkArguments.where.name?.Value, "latest", StringComparison.Ordinal)
                                || symbolicLinkArguments.symlink?.symlink_attributes is null
                                || !string.Equals(symbolicLinkArguments.symlink.symlink_data?.Value, "../releases/current", StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException("Expected the grouped SYMLINK API to target NFSPROC3_SYMLINK with the parent handle, entry name, and link target.");
                            }

                            if (hardLinkPlan.ProcedureNumber != 15
                                || hardLinkArguments.file?.data is null
                                || hardLinkArguments.link?.dir?.data is null
                                || !hardLinkArguments.file.data.AsSpan().SequenceEqual(new byte[] { 0x91, 0x92 })
                                || !hardLinkArguments.link.dir.data.AsSpan().SequenceEqual(new byte[] { 0xA1, 0xA2 })
                                || !string.Equals(hardLinkArguments.link.name?.Value, "draft-link.txt", StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException("Expected the grouped LINK API to target NFSPROC3_LINK with the source filehandle and destination diropargs payload.");
                            }

                            if (pingNfsPlan.ProgramNumber != 100003
                                || pingNfsPlan.VersionNumber != 3
                                || pingNfsPlan.ProcedureNumber != 0
                                || pingNfsPlan.ProcedurePayload.Length != 0
                                || pingMountPlan.ProgramNumber != 100005
                                || pingMountPlan.VersionNumber != 3
                                || pingMountPlan.ProcedureNumber != 0
                                || pingMountPlan.ProcedurePayload.Length != 0
                                || pingNlmPlan.ProgramNumber != 100021
                                || pingNlmPlan.VersionNumber != 4
                                || pingNlmPlan.ProcedureNumber != 0
                                || pingNlmPlan.ProcedurePayload.Length != 0)
                            {
                                throw new InvalidOperationException("Expected the grouped administration APIs to target the correct NULL procedures without payload bytes.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ClientGroupedSuites",
                        caseId: "MutationAndAdministrationApisRejectInvalidArgumentsAndUnexpectedVoidPayloads",
                        displayName: "Grouped mutation and administration APIs reject invalid arguments and unexpected NULL reply payloads",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            OpenNfsClient client = new OpenNfsClientBuilder()
                                .WithPrimaryEndpoint("mutations.example", 2049)
                                .Build();

                            await client.OpenAsync(cancellationToken).ConfigureAwait(false);

                            try
                            {
                                await client.Directories.PrepareCreateFileV3Async(
                                    Array.Empty<byte>(),
                                    "draft.txt",
                                    failIfExists: true,
                                    cancellationToken).ConfigureAwait(false);
                                throw new InvalidOperationException("Expected grouped CREATE planning to reject an empty parent directory filehandle.");
                            }
                            catch (ArgumentException exception)
                            {
                                if (!string.Equals(exception.ParamName, "directoryHandle", StringComparison.Ordinal))
                                {
                                    throw new InvalidOperationException("Expected grouped CREATE planning failures to identify the invalid directoryHandle parameter.");
                                }
                            }

                            try
                            {
                                await client.Directories.PrepareRenameV3Async(
                                    new byte[] { 0x11 },
                                    "old.txt",
                                    new byte[] { 0x12 },
                                    " ",
                                    cancellationToken).ConfigureAwait(false);
                                throw new InvalidOperationException("Expected grouped RENAME planning to reject a whitespace-only destination entry name.");
                            }
                            catch (ArgumentException exception)
                            {
                                if (!string.Equals(exception.ParamName, "entryName", StringComparison.Ordinal))
                                {
                                    throw new InvalidOperationException("Expected grouped RENAME planning failures to identify the invalid entryName parameter.");
                                }
                            }

                            try
                            {
                                await client.Directories.PrepareCreateSymbolicLinkV3Async(
                                    new byte[] { 0x21 },
                                    "latest",
                                    " ",
                                    cancellationToken).ConfigureAwait(false);
                                throw new InvalidOperationException("Expected grouped SYMLINK planning to reject a whitespace-only target path.");
                            }
                            catch (ArgumentException exception)
                            {
                                if (!string.Equals(exception.ParamName, "targetPath", StringComparison.Ordinal))
                                {
                                    throw new InvalidOperationException("Expected grouped SYMLINK planning failures to identify the invalid targetPath parameter.");
                                }
                            }

                            client.Administration.ReadPingNfsV3Result(EncodeAcceptedSuccessReply(Array.Empty<byte>()));
                            client.Administration.ReadPingMountV3Result(EncodeAcceptedSuccessReply(Array.Empty<byte>()));
                            client.Administration.ReadPingNlmV4Result(EncodeAcceptedSuccessReply(Array.Empty<byte>()));

                            try
                            {
                                client.Administration.ReadPingNfsV3Result(EncodeAcceptedSuccessReply(new byte[] { 0x00, 0x00, 0x00, 0x01 }));
                                throw new InvalidOperationException("Expected grouped NULL reply validation to reject unexpected payload bytes.");
                            }
                            catch (InvalidDataException exception)
                            {
                                if (!exception.Message.Contains("must not carry a procedure result payload", StringComparison.Ordinal))
                                {
                                    throw new InvalidOperationException("Expected grouped NULL reply validation failures to explain the unexpected payload bytes.");
                                }
                            }

                            try
                            {
                                client.Administration.ReadPingMountV3Result(
                                    RpcMessageCodec.Encode(
                                        RpcMessageFactory.CreateRejectedReply(
                                            xid: 0x01020304,
                                            status: RpcGenerated.reject_stat.AUTH_ERROR,
                                            authenticationStatus: RpcGenerated.auth_stat.AUTH_BADCRED)));
                                throw new InvalidOperationException("Expected grouped NULL reply validation to reject denied RPC replies.");
                            }
                            catch (InvalidDataException exception)
                            {
                                if (!exception.Message.Contains("AUTH_ERROR", StringComparison.Ordinal)
                                    || !exception.Message.Contains("AUTH_BADCRED", StringComparison.Ordinal))
                                {
                                    throw new InvalidOperationException("Expected grouped NULL reply decoding failures to surface both the rejected status and the authentication failure code.");
                                }
                            }

                        }),
            };
        }
    }
}
