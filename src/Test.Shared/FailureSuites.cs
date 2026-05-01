namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Server;
    using OpenNFS.Server.FileHandles;
    using OpenNFS.Server.FileSystems;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// Touchstone suites covering forced-disconnect and duplicate-on-wire recovery behavior.
    /// </summary>
    public static class FailureSuites
    {
        /// <summary>
        /// Creates the shared failure-injection suite catalog.
        /// </summary>
        /// <returns>The configured suite descriptor.</returns>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(
                suiteId: "FailureSuites",
                displayName: "Failure Injection",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        suiteId: "FailureSuites",
                        caseId: "IdempotentReadRetriesAfterForcedDisconnectPositive",
                        displayName: "Idempotent NFSv3 reads retry successfully after a forced disconnect on the first attempt",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecuteIdempotentReadRetriesAfterForcedDisconnectPositiveAsync),

                    new TestCaseDescriptor(
                        suiteId: "FailureSuites",
                        caseId: "NonIdempotentCreateStopsAfterForcedDisconnectNegative",
                        displayName: "Non-idempotent NFSv3 create operations stop after a forced disconnect instead of replaying automatically",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecuteNonIdempotentCreateStopsAfterForcedDisconnectNegativeAsync),

                    new TestCaseDescriptor(
                        suiteId: "FailureSuites",
                        caseId: "DuplicateWireWriteTriggersSingleHostMutationPositive",
                        displayName: "A duplicate NFSv3 WRITE injected on the wire produces one host mutation and byte-stable duplicate replies",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecuteDuplicateWireWriteTriggersSingleHostMutationPositiveAsync),
                });
        }

        private static async Task ExecuteDuplicateWireWriteTriggersSingleHostMutationPositiveAsync(CancellationToken cancellationToken)
        {
            await using TemporaryExportRoot exportRoot = TemporaryExportRoot.Create("DuplicateWireWrite");
            string exportSourcePath = exportRoot.ExportRootPath;
            string filePath = exportRoot.ResolveRelativePath("data.bin");

            DictionaryNfsFileSystem fileSystem = new DictionaryNfsFileSystem(
                new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
                {
                    [exportSourcePath] = NfsPathKind.Directory,
                    [filePath] = NfsPathKind.File,
                },
                new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
                {
                    [filePath] = Array.Empty<byte>(),
                });

            OpenNfsServer server = new OpenNfsServerBuilder()
                .UseFileSystem(fileSystem)
                .UseFileHandleProvider(new PersistentMappingHandleProvider(exportRoot.MappingFilePath))
                .AddExport("/export", exportSourcePath)
                .Build();

            await using OpenNfsTcpInteropHost host = OpenNfsTcpInteropHost.Start(server);
            await using FaultInjectingRpcProxy proxy = FaultInjectingRpcProxy.Start(
                "127.0.0.1",
                host.NfsPort,
                FaultInjectingRpcProxyMode.DuplicateFirstForwardedRequest);

            NfsFileHandle fileHandle = await server.CreateFileHandleAsync(
                new NfsFileHandleTarget("/export/data.bin", filePath),
                cancellationToken).ConfigureAwait(false);

            await using OpenNfsClient client = new OpenNfsClientBuilder()
                .WithServer("127.0.0.1", proxy.LocalPort)
                .Build();
            await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

            byte[] payload = Encoding.UTF8.GetBytes("duplicate-write-payload");
            OpenNfsV3WriteResult writeResult = await client.Files.WriteV3Async(
                fileHandle.ToArray(),
                0UL,
                OpenNfsWriteStability.FileSync,
                payload,
                cancellationToken).ConfigureAwait(false);

            if (!writeResult.IsSuccess || writeResult.Count != payload.Length)
            {
                throw new InvalidOperationException("Expected the duplicate-on-wire WRITE to preserve a successful typed client result.");
            }

            if (fileSystem.WriteRequestCount != 1)
            {
                throw new InvalidOperationException("Expected a duplicate NFSv3 WRITE injected on the wire to reach the host file system only once.");
            }

            NfsReadFileResponse readResponse = await fileSystem.ReadFileAsync(
                new NfsReadFileRequest(filePath, 0UL, 128U, cancellationToken)).ConfigureAwait(false);
            if (!readResponse.Data.Span.SequenceEqual(payload))
            {
                throw new InvalidOperationException("Expected the final file bytes to match the client WRITE payload after duplicate-request replay.");
            }

            RpcCaptureAssertions.AssertCallRoutingSequence(
                proxy.CapturedClientRequests,
                (uint)NFS_PROGRAM_Program.Program,
                (uint)NFS_PROGRAM_Program.Version_NFS_V3,
                (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_WRITE);
            RpcCaptureAssertions.AssertAuthSysCredential(
                proxy.CapturedClientRequests[0],
                TestPrincipalIdentity.CreateDefaultAuthSys());

            if (proxy.CapturedServerReplies.Count != 2
                || !proxy.CapturedServerReplies[0].EncodedMessage.AsSpan().SequenceEqual(proxy.CapturedServerReplies[1].EncodedMessage))
            {
                throw new InvalidOperationException("Expected the duplicate-on-wire WRITE path to observe two byte-stable server replies for the same replayed request.");
            }
        }

        private static async Task ExecuteIdempotentReadRetriesAfterForcedDisconnectPositiveAsync(CancellationToken cancellationToken)
        {
            await using TemporaryExportRoot exportRoot = TemporaryExportRoot.Create("DisconnectReadRetry");
            string exportSourcePath = exportRoot.ExportRootPath;
            string filePath = exportRoot.ResolveRelativePath("hello.txt");
            exportRoot.WriteAllText("hello.txt", "retry-through-disconnect");

            OpenNfsServer server = new OpenNfsServerBuilder()
                .UseLocalFileSystem()
                .UseFileHandleProvider(new PersistentMappingHandleProvider(exportRoot.MappingFilePath))
                .AddExport("/export", exportSourcePath)
                .Build();

            await using OpenNfsTcpInteropHost host = OpenNfsTcpInteropHost.Start(server);
            await using FaultInjectingRpcProxy proxy = FaultInjectingRpcProxy.Start(
                "127.0.0.1",
                host.NfsPort,
                FaultInjectingRpcProxyMode.DropFirstRequestWithoutForwarding);

            NfsFileHandle fileHandle = await server.CreateFileHandleAsync(
                new NfsFileHandleTarget("/export/hello.txt", filePath),
                cancellationToken).ConfigureAwait(false);

            await using OpenNfsClient client = new OpenNfsClientBuilder()
                .WithServer("127.0.0.1", proxy.LocalPort)
                .WithRetryPolicy(new OpenNfsRetryPolicy(maximumAttempts: 2))
                .Build();
            await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

            OpenNfsV3ReadResult readResult = await client.Files.ReadV3Async(
                fileHandle.ToArray(),
                0UL,
                128U,
                cancellationToken).ConfigureAwait(false);

            if (!readResult.IsSuccess
                || !string.Equals(Encoding.UTF8.GetString(readResult.Data.Span), "retry-through-disconnect", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected the idempotent NFSv3 READ to succeed after the first forced disconnect triggered a retry.");
            }

            RpcCaptureAssertions.AssertCallRoutingSequence(
                proxy.CapturedClientRequests,
                (uint)NFS_PROGRAM_Program.Program,
                (uint)NFS_PROGRAM_Program.Version_NFS_V3,
                (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_READ,
                (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_READ);
            RpcCaptureAssertions.AssertAuthSysCredential(
                proxy.CapturedClientRequests[0],
                TestPrincipalIdentity.CreateDefaultAuthSys());

            if (proxy.CapturedServerReplies.Count != 1)
            {
                throw new InvalidOperationException("Expected only the retried NFSv3 READ attempt to reach the backend server and produce a reply.");
            }
        }

        private static async Task ExecuteNonIdempotentCreateStopsAfterForcedDisconnectNegativeAsync(CancellationToken cancellationToken)
        {
            await using TemporaryExportRoot exportRoot = TemporaryExportRoot.Create("DisconnectCreateNegative");
            string exportSourcePath = exportRoot.ExportRootPath;

            OpenNfsServer server = new OpenNfsServerBuilder()
                .UseLocalFileSystem()
                .UseFileHandleProvider(new PersistentMappingHandleProvider(exportRoot.MappingFilePath))
                .AddExport("/export", exportSourcePath)
                .Build();

            await using OpenNfsTcpInteropHost host = OpenNfsTcpInteropHost.Start(server);
            await using FaultInjectingRpcProxy proxy = FaultInjectingRpcProxy.Start(
                "127.0.0.1",
                host.NfsPort,
                FaultInjectingRpcProxyMode.DropFirstRequestWithoutForwarding);

            NfsFileHandle directoryHandle = await server.CreateFileHandleAsync(
                new NfsFileHandleTarget("/export", exportSourcePath),
                cancellationToken).ConfigureAwait(false);

            await using OpenNfsClient client = new OpenNfsClientBuilder()
                .WithServer("127.0.0.1", proxy.LocalPort)
                .WithRetryPolicy(new OpenNfsRetryPolicy(maximumAttempts: 2))
                .Build();
            await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                _ = await client.Directories.CreateFileV3Async(
                    directoryHandle.ToArray(),
                    "created.txt",
                    failIfExists: true,
                    cancellationToken).ConfigureAwait(false);
                throw new InvalidOperationException("Expected the non-idempotent NFSv3 CREATE to fail after the forced disconnect instead of replaying automatically.");
            }
            catch (OpenNfsClientIoException exception)
            {
                if (!exception.Message.Contains("failed", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected the forced-disconnect CREATE failure to surface a typed client I/O exception.");
                }
            }

            string createdFilePath = exportRoot.ResolveRelativePath("created.txt");
            if (File.Exists(createdFilePath))
            {
                throw new InvalidOperationException("Expected the forced-disconnect CREATE failure not to leave behind a created file on disk.");
            }

            RpcCaptureAssertions.AssertCallRoutingSequence(
                proxy.CapturedClientRequests,
                (uint)NFS_PROGRAM_Program.Program,
                (uint)NFS_PROGRAM_Program.Version_NFS_V3,
                (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_CREATE);
            RpcCaptureAssertions.AssertAuthSysCredential(
                proxy.CapturedClientRequests[0],
                TestPrincipalIdentity.CreateDefaultAuthSys());

            if (proxy.CapturedServerReplies.Count != 0)
            {
                throw new InvalidOperationException("Expected the forced-disconnect CREATE failure to terminate before any backend reply was produced.");
            }
        }
    }
}
