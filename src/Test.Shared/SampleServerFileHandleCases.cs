namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using OpenNFS.Client;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Security.RpcSecGss;
    using OpenNFS.Rpc.Transport;
    using OpenNFS.Server;
    using OpenNFS.Server.FileHandles;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;
    using static Test.Shared.SampleServerSuiteSupport;

    /// <summary>
    /// Intrinsic and persistent filehandle sample-server foundation suites.
    /// </summary>
    internal static class SampleServerFileHandleCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases(DockerInteropEnvironmentProbe probe)
        {
            return new TestCaseDescriptor[]
            {
                    new TestCaseDescriptor(
                        suiteId: "SampleServerSuites",
                        caseId: "IntrinsicFileHandleRoundTripsDeterministically",
                        displayName: "Intrinsic filehandles round-trip deterministically",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            IntrinsicHandleProvider provider = new IntrinsicHandleProvider();
                            NfsFileHandleTarget target = new NfsFileHandleTarget(
                                exportPath: "/exports/alpha",
                                sourcePath: @"C:\Exports\Alpha\File.txt",
                                stableIdentity: new NfsFileHandleIdentity("inode", "42"));

                            NfsCreateFileHandleResponse firstCreateResponse =
                                await provider.CreateAsync(new NfsCreateFileHandleRequest(target, cancellationToken)).ConfigureAwait(false);
                            NfsCreateFileHandleResponse secondCreateResponse =
                                await provider.CreateAsync(new NfsCreateFileHandleRequest(target, cancellationToken)).ConfigureAwait(false);
                            NfsFileHandle firstHandle = firstCreateResponse.FileHandle;
                            NfsFileHandle secondHandle = secondCreateResponse.FileHandle;

                            if (!firstHandle.Bytes.Span.SequenceEqual(secondHandle.Bytes.Span))
                            {
                                throw new InvalidOperationException("Expected intrinsic filehandles for the same target to be deterministic.");
                            }

                            NfsResolveFileHandleResponse resolveResponse =
                                await provider.ResolveAsync(new NfsResolveFileHandleRequest(firstHandle, cancellationToken)).ConfigureAwait(false);
                            NfsFileHandleResolution resolution = resolveResponse.Resolution;

                            if (!resolution.Found
                                || resolution.Target is null
                                || !string.Equals(resolution.Target.ExportPath, target.ExportPath, StringComparison.Ordinal)
                                || !string.Equals(resolution.Target.SourcePath, target.SourcePath, StringComparison.Ordinal)
                                || !string.Equals(resolution.Target.StableIdentity?.Scheme, "inode", StringComparison.Ordinal)
                                || !string.Equals(resolution.Target.StableIdentity?.Value, "42", StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException("Expected intrinsic filehandles to round-trip their target and stable identity metadata.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "SampleServerSuites",
                        caseId: "FileHandleSurvivesRestartWithPersistentProvider",
                        displayName: "Persistent filehandles survive provider restart and track stable identities",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            string tempDirectory = Path.Combine(Path.GetTempPath(), "OpenNFS.Tests", Guid.NewGuid().ToString("N"));
                            string mappingFilePath = Path.Combine(tempDirectory, "filehandles.json");

                            try
                            {
                                NfsFileHandleIdentity stableIdentity = new NfsFileHandleIdentity("inode", "9001");
                                NfsFileHandleTarget originalTarget = new NfsFileHandleTarget(
                                    exportPath: "/exports/sample",
                                    sourcePath: @"C:\Exports\Sample\OldName.txt",
                                    stableIdentity: stableIdentity);
                                NfsFileHandleTarget renamedTarget = new NfsFileHandleTarget(
                                    exportPath: "/exports/sample",
                                    sourcePath: @"C:\Exports\Sample\NewName.txt",
                                    stableIdentity: stableIdentity);

                                PersistentMappingHandleProvider firstProvider = new PersistentMappingHandleProvider(mappingFilePath);
                                NfsCreateFileHandleResponse originalCreateResponse =
                                    await firstProvider.CreateAsync(new NfsCreateFileHandleRequest(originalTarget, cancellationToken)).ConfigureAwait(false);
                                NfsFileHandle originalHandle = originalCreateResponse.FileHandle;

                                PersistentMappingHandleProvider secondProvider = new PersistentMappingHandleProvider(mappingFilePath);
                                NfsResolveFileHandleResponse restartResolveResponse =
                                    await secondProvider.ResolveAsync(new NfsResolveFileHandleRequest(originalHandle, cancellationToken)).ConfigureAwait(false);
                                NfsFileHandleResolution restartResolution = restartResolveResponse.Resolution;

                                if (!restartResolution.Found
                                    || restartResolution.Target is null
                                    || !string.Equals(restartResolution.Target.SourcePath, originalTarget.SourcePath, StringComparison.Ordinal))
                                {
                                    throw new InvalidOperationException("Expected the persistent filehandle provider to resolve a handle after provider restart.");
                                }

                                NfsCreateFileHandleResponse renamedCreateResponse =
                                    await secondProvider.CreateAsync(new NfsCreateFileHandleRequest(renamedTarget, cancellationToken)).ConfigureAwait(false);
                                NfsFileHandle renamedHandle = renamedCreateResponse.FileHandle;
                                if (!originalHandle.Bytes.Span.SequenceEqual(renamedHandle.Bytes.Span))
                                {
                                    throw new InvalidOperationException("Expected the persistent filehandle provider to reuse the same handle for the same stable identity after a path change.");
                                }

                                PersistentMappingHandleProvider thirdProvider = new PersistentMappingHandleProvider(mappingFilePath);
                                NfsResolveFileHandleResponse renamedResolveResponse =
                                    await thirdProvider.ResolveAsync(new NfsResolveFileHandleRequest(originalHandle, cancellationToken)).ConfigureAwait(false);
                                NfsFileHandleResolution renamedResolution = renamedResolveResponse.Resolution;

                                if (!renamedResolution.Found
                                    || renamedResolution.Target is null
                                    || !string.Equals(renamedResolution.Target.SourcePath, renamedTarget.SourcePath, StringComparison.Ordinal)
                                    || !string.Equals(renamedResolution.Target.StableIdentity?.Scheme, stableIdentity.Scheme, StringComparison.Ordinal)
                                    || !string.Equals(renamedResolution.Target.StableIdentity?.Value, stableIdentity.Value, StringComparison.Ordinal))
                                {
                                    throw new InvalidOperationException("Expected persistent filehandle resolution to follow the latest path mapped to a stable identity across restarts.");
                                }
                            }
                            finally
                            {
                                if (Directory.Exists(tempDirectory))
                                {
                                    Directory.Delete(tempDirectory, recursive: true);
                                }
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "SampleServerSuites",
                        caseId: "ServerBuildsWithDefaultIntrinsicFileHandleProvider",
                        displayName: "Server settings default to the intrinsic filehandle provider",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            DictionaryNfsFileSystem fileSystem =
                                new DictionaryNfsFileSystem(new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase));

                            OpenNfsServer server = new OpenNfsServerBuilder()
                                .UseFileSystem(fileSystem)
                                .Build();

                            if (server.Settings.FileHandleProvider is not IntrinsicHandleProvider)
                            {
                                throw new InvalidOperationException("Expected the public server surface to default to the intrinsic filehandle provider when no explicit provider is configured.");
                            }

                            NfsFileHandleTarget target = new NfsFileHandleTarget("/exports/default", @"C:\Exports\Default\One.txt");
                            NfsCreateFileHandleResponse createResponse =
                                await server.CreateFileHandleAsync(new NfsCreateFileHandleRequest(target, cancellationToken)).ConfigureAwait(false);
                            NfsResolveFileHandleResponse resolveResponse =
                                await server.ResolveFileHandleAsync(
                                    new NfsResolveFileHandleRequest(createResponse.FileHandle, cancellationToken)).ConfigureAwait(false);
                            NfsFileHandleResolution resolution = resolveResponse.Resolution;

                            if (!resolution.Found
                                || resolution.Target is null
                                || !string.Equals(resolution.Target.ExportPath, target.ExportPath, StringComparison.Ordinal)
                                || !string.Equals(resolution.Target.SourcePath, target.SourcePath, StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException("Expected the server wrapper to delegate filehandle creation and resolution through the configured default provider.");
                            }
                        }),

            };
        }
    }
}
