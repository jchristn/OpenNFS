namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using NfsV3Generated = OpenNFS.Protocol.V3.Generated;
    using NfsV41Generated = OpenNFS.Protocol.V41.Generated;
    using RpcGenerated = OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.Xdr;
    using OpenNFS.XdrGen.Model;
    using OpenNFS.XdrGen.Parsing;
    using OpenNFS.XdrGen;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;
    using static Test.Shared.GeneratorSuiteSupport;

    /// <summary>
    /// Manifest, source-discovery, emission, determinism, and check-mode generator suites.
    /// </summary>
    internal static class GeneratorManifestAndEmissionCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                    new TestCaseDescriptor(
                        suiteId: "RpcXdrSuites",
                        caseId: "ManifestLoads",
                        displayName: "Generator manifest loads and validates",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            string repositoryRoot = RepositoryPaths.GetRepositoryRoot();
                            string configurationPath = Path.Combine(repositoryRoot, "src", "OpenNFS.XdrGen", "xdrgen.json");

                            XdrGeneratorCommand command = new XdrGeneratorCommand();
                            XdrGenerationResult result = await command.RunAsync(
                                new XdrGeneratorOptions(configurationPath, checkOnly: true),
                                cancellationToken).ConfigureAwait(false);

                            if (result.Configuration.Projects.Count != 5)
                            {
                                throw new InvalidOperationException("Expected exactly five XDR project mappings in the baseline manifest.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "RpcXdrSuites",
                        caseId: "VendoredSourceFilesDetected",
                        displayName: "Generator discovers vendored source files for every mapping",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            string repositoryRoot = RepositoryPaths.GetRepositoryRoot();
                            string configurationPath = Path.Combine(repositoryRoot, "src", "OpenNFS.XdrGen", "xdrgen.json");

                            XdrGeneratorCommand command = new XdrGeneratorCommand();
                            XdrGenerationResult result = await command.RunAsync(
                                new XdrGeneratorOptions(configurationPath, checkOnly: true),
                                cancellationToken).ConfigureAwait(false);

                            foreach (XdrProjectMapping mapping in result.Configuration.Projects)
                            {
                                if (mapping.SourceFiles.Count < 1)
                                {
                                    throw new InvalidOperationException("Expected at least one vendored source file for mapping '" + mapping.Id + "'.");
                                }
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "RpcXdrSuites",
                        caseId: "GenerationEntryPointsDetected",
                        displayName: "Generator discovers generation entry points for every mapping",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            string repositoryRoot = RepositoryPaths.GetRepositoryRoot();
                            string configurationPath = Path.Combine(repositoryRoot, "src", "OpenNFS.XdrGen", "xdrgen.json");

                            XdrGeneratorCommand command = new XdrGeneratorCommand();
                            XdrGenerationResult result = await command.RunAsync(
                                new XdrGeneratorOptions(configurationPath, checkOnly: true),
                                cancellationToken).ConfigureAwait(false);

                            foreach (XdrProjectMapping mapping in result.Configuration.Projects)
                            {
                                if (mapping.EntryPointFiles.Count < 1)
                                {
                                    throw new InvalidOperationException("Expected at least one generation entry-point file for mapping '" + mapping.Id + "'.");
                                }

                                foreach (string entryPointFile in mapping.EntryPointFiles)
                                {
                                    if (!mapping.SourceFiles.Contains(entryPointFile, StringComparer.OrdinalIgnoreCase))
                                    {
                                        throw new InvalidOperationException("Expected generation entry-point file '" + entryPointFile + "' to be included in the vendored source-file list for mapping '" + mapping.Id + "'.");
                                    }
                                }
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "RpcXdrSuites",
                        caseId: "VendoredXdrSourcesParse",
                        displayName: "Generator parses vendored XDR source files into AST documents",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            string repositoryRoot = RepositoryPaths.GetRepositoryRoot();
                            string configurationPath = Path.Combine(repositoryRoot, "src", "OpenNFS.XdrGen", "xdrgen.json");

                            XdrGeneratorCommand command = new XdrGeneratorCommand();
                            XdrGenerationResult result = await command.RunAsync(
                                new XdrGeneratorOptions(configurationPath, checkOnly: true),
                                cancellationToken).ConfigureAwait(false);

                            int expectedDocumentCount = result.Configuration.Projects
                                .SelectMany(mapping => mapping.SourceFiles)
                                .Count(path => string.Equals(Path.GetExtension(path), ".x", StringComparison.OrdinalIgnoreCase));

                            int uniqueExpectedCount = result.Configuration.Projects
                                .SelectMany(mapping => mapping.SourceFiles)
                                .Where(path => string.Equals(Path.GetExtension(path), ".x", StringComparison.OrdinalIgnoreCase))
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .Count();

                            if (result.ParsedDocuments.Count != uniqueExpectedCount)
                            {
                                throw new InvalidOperationException("Expected " + uniqueExpectedCount + " unique parsed XDR documents but found " + result.ParsedDocuments.Count + ".");
                            }

                            if (expectedDocumentCount < uniqueExpectedCount)
                            {
                                throw new InvalidOperationException("Expected document counting assumptions to be monotonic.");
                            }

                            foreach (XdrDocument document in result.ParsedDocuments)
                            {
                                if (document.Definitions.Count < 1)
                                {
                                    throw new InvalidOperationException("Expected parsed XDR document '" + document.FilePath + "' to contain at least one top-level definition.");
                                }
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "RpcXdrSuites",
                        caseId: "MalformedInputProducesContext",
                        displayName: "Parser reports contextual errors for malformed XDR",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: _ =>
                        {
                            XdrParser parser = new XdrParser();

                            try
                            {
                                parser.ParseDocument("malformed.x", "struct broken { unsigned int value };\n");
                                throw new InvalidOperationException("Expected malformed XDR input to throw.");
                            }
                            catch (XdrParseException exception)
                            {
                                if (!exception.Message.Contains("malformed.x(1,", StringComparison.Ordinal))
                                {
                                    throw new InvalidOperationException("Expected parser error to include file and line context.");
                                }
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "RpcXdrSuites",
                        caseId: "GeneratedDirectoriesCreated",
                        displayName: "Generator materializes output directories",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            string tempRoot = CreateTempDirectory();

                            try
                            {
                                TemporaryGeneratorScenario scenario = await CreateTemporaryGeneratorScenarioAsync(tempRoot, cancellationToken).ConfigureAwait(false);
                                XdrGeneratorCommand command = new XdrGeneratorCommand();
                                XdrGenerationResult result = await command.RunAsync(
                                    new XdrGeneratorOptions(scenario.ConfigurationPath),
                                    cancellationToken).ConfigureAwait(false);

                                if (result.EnsuredGeneratedDirectories.Count != 1)
                                {
                                    throw new InvalidOperationException("Expected the temporary generator scenario to materialize exactly one generated output directory.");
                                }

                                foreach (string generatedDirectory in result.EnsuredGeneratedDirectories)
                                {
                                    if (!Directory.Exists(generatedDirectory))
                                    {
                                        throw new DirectoryNotFoundException("The expected generated output directory was not created: " + generatedDirectory);
                                    }
                                }

                                if (!Directory.Exists(scenario.GeneratedDirectoryPath))
                                {
                                    throw new DirectoryNotFoundException("The expected temporary generated output directory was not created: " + scenario.GeneratedDirectoryPath);
                                }

                                if (!Directory.EnumerateFiles(scenario.GeneratedDirectoryPath, "*.g.cs", SearchOption.TopDirectoryOnly).Any())
                                {
                                    throw new InvalidOperationException("Expected the temporary generator scenario to emit at least one generated C# file.");
                                }
                            }
                            finally
                            {
                                Directory.Delete(tempRoot, recursive: true);
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "RpcXdrSuites",
                        caseId: "AllVendoredSpecsEmit",
                        displayName: "Generator verifies the checked-in emitted corpus for all vendored specs",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            string repositoryRoot = RepositoryPaths.GetRepositoryRoot();
                            string configurationPath = Path.Combine(repositoryRoot, "src", "OpenNFS.XdrGen", "xdrgen.json");

                            XdrGeneratorCommand command = new XdrGeneratorCommand();
                            XdrGenerationResult result = await command.RunAsync(
                                new XdrGeneratorOptions(configurationPath, checkOnly: true),
                                cancellationToken).ConfigureAwait(false);

                            if (result.GeneratedFiles.Count < 1000)
                            {
                                throw new InvalidOperationException("Expected broad corpus emission to produce at least 1000 generated files, but found " + result.GeneratedFiles.Count + ".");
                            }

                            foreach (OpenNFS.XdrGen.Emission.GeneratedCSharpFile generatedFile in result.GeneratedFiles)
                            {
                                if (!File.Exists(generatedFile.FilePath))
                                {
                                    throw new FileNotFoundException("The expected generated C# file was not emitted.", generatedFile.FilePath);
                                }
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "RpcXdrSuites",
                        caseId: "GeneratorIsDeterministic",
                        displayName: "Generator plans deterministic output and matches checked-in shapes",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            string repositoryRoot = RepositoryPaths.GetRepositoryRoot();
                            string configurationPath = Path.Combine(repositoryRoot, "src", "OpenNFS.XdrGen", "xdrgen.json");

                            XdrGeneratorCommand command = new XdrGeneratorCommand();
                            XdrGenerationResult firstResult = await command.RunAsync(
                                new XdrGeneratorOptions(configurationPath, checkOnly: true),
                                cancellationToken).ConfigureAwait(false);
                            XdrGenerationResult secondResult = await command.RunAsync(
                                new XdrGeneratorOptions(configurationPath, checkOnly: true),
                                cancellationToken).ConfigureAwait(false);

                            if (firstResult.GeneratedFiles.Count != secondResult.GeneratedFiles.Count)
                            {
                                throw new InvalidOperationException("Expected repeated generator planning to produce the same number of generated files.");
                            }

                            for (int index = 0; index < firstResult.GeneratedFiles.Count; index++)
                            {
                                OpenNFS.XdrGen.Emission.GeneratedCSharpFile firstFile = firstResult.GeneratedFiles[index];
                                OpenNFS.XdrGen.Emission.GeneratedCSharpFile secondFile = secondResult.GeneratedFiles[index];

                                if (!string.Equals(firstFile.FilePath, secondFile.FilePath, StringComparison.OrdinalIgnoreCase))
                                {
                                    throw new InvalidOperationException("Expected repeated generator planning to preserve generated file ordering.");
                                }

                                if (!string.Equals(firstFile.SourceText, secondFile.SourceText, StringComparison.Ordinal))
                                {
                                    throw new InvalidOperationException("Expected repeated generator planning to produce identical generated file text for '" + firstFile.FilePath + "'.");
                                }
                            }

                            string rpcConstantsPath = Path.Combine(repositoryRoot, "src", "OpenNFS.Rpc", "Generated", "RpcConstants.g.cs");
                            string replyBodyModelPath = Path.Combine(repositoryRoot, "src", "OpenNFS.Rpc", "Generated", "reply_body.g.cs");
                            string authFlavorCodecPath = Path.Combine(repositoryRoot, "src", "OpenNFS.Rpc", "Generated", "auth_flavor.XdrCodec.g.cs");
                            string callbackSecParmsPath = Path.Combine(repositoryRoot, "src", "OpenNFS.Protocol.V41", "Generated", "callback_sec_parms4.g.cs");

                            string rpcConstants = await File.ReadAllTextAsync(rpcConstantsPath, cancellationToken).ConfigureAwait(false);
                            if (!rpcConstants.Contains("public const ulong RPCB_PORT = 111;", StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException("Expected emitted RPC constants output to contain the normalized RPCB port constant.");
                            }

                            string replyBodyCodec = await File.ReadAllTextAsync(replyBodyModelPath, cancellationToken).ConfigureAwait(false);
                            if (!replyBodyCodec.Contains("public void WriteTo(XdrWriter writer)", StringComparison.Ordinal)
                                || !replyBodyCodec.Contains("public static reply_body ReadFrom(XdrReader reader)", StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException("Expected emitted RPC codec output to provide deterministic WriteTo and ReadFrom methods for reply_body.");
                            }

                            string authFlavorCodec = await File.ReadAllTextAsync(authFlavorCodecPath, cancellationToken).ConfigureAwait(false);
                            if (!authFlavorCodec.Contains("internal static class auth_flavor_XdrCodec", StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException("Expected emitted enum codec output to provide a deterministic auth_flavor codec companion.");
                            }

                            string callbackSecParms = await File.ReadAllTextAsync(callbackSecParmsPath, cancellationToken).ConfigureAwait(false);
                            if (!callbackSecParms.Contains("public OpenNFS.Rpc.Generated.authsys_parms? cbsp_sys_cred", StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException("Expected emitted NFSv4.1 callback security output to reference the shared RPC authsys_parms model.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "RpcXdrSuites",
                        caseId: "CheckModeRejectsGeneratedOutputDrift",
                        displayName: "Generator check mode rejects extra, missing, and stale generated files",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            string tempRoot = CreateTempDirectory();

                            try
                            {
                                TemporaryGeneratorScenario scenario = await CreateTemporaryGeneratorScenarioAsync(tempRoot, cancellationToken).ConfigureAwait(false);
                                XdrGeneratorCommand command = new XdrGeneratorCommand();

                                await command.RunAsync(new XdrGeneratorOptions(scenario.ConfigurationPath), cancellationToken).ConfigureAwait(false);

                                string extraFilePath = Path.Combine(scenario.GeneratedDirectoryPath, "unexpected.g.cs");
                                await File.WriteAllTextAsync(extraFilePath, "// extra\n", cancellationToken).ConfigureAwait(false);
                                await ExpectInvalidDataAsync(
                                    () => command.RunAsync(new XdrGeneratorOptions(scenario.ConfigurationPath, checkOnly: true), cancellationToken),
                                    "unexpected file(s)",
                                    cancellationToken).ConfigureAwait(false);

                                File.Delete(extraFilePath);
                                string missingFilePath = Path.Combine(scenario.GeneratedDirectoryPath, "simple_struct.g.cs");
                                File.Delete(missingFilePath);
                                await ExpectInvalidDataAsync(
                                    () => command.RunAsync(new XdrGeneratorOptions(scenario.ConfigurationPath, checkOnly: true), cancellationToken),
                                    "missing file(s)",
                                    cancellationToken).ConfigureAwait(false);

                                await command.RunAsync(new XdrGeneratorOptions(scenario.ConfigurationPath), cancellationToken).ConfigureAwait(false);
                                await File.WriteAllTextAsync(missingFilePath, "// stale\n", cancellationToken).ConfigureAwait(false);
                                await ExpectInvalidDataAsync(
                                    () => command.RunAsync(new XdrGeneratorOptions(scenario.ConfigurationPath, checkOnly: true), cancellationToken),
                                    "stale",
                                    cancellationToken).ConfigureAwait(false);
                            }
                            finally
                            {
                                if (Directory.Exists(tempRoot))
                                {
                                    Directory.Delete(tempRoot, recursive: true);
                                }
                            }
                        }),

            };
        }
    }
}
