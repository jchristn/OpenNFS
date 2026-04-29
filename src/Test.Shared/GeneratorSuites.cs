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

    /// <summary>
    /// Touchstone suites covering the baseline RPC and XDR generator and runtime scaffolding.
    /// </summary>
    public static class GeneratorSuites
    {
        /// <summary>
        /// Creates the baseline RPC and XDR suite for generator and runtime validation.
        /// </summary>
        /// <returns>The configured suite descriptor.</returns>
        public static TestSuiteDescriptor RpcXdrSuites()
        {
            return new TestSuiteDescriptor(
                suiteId: "RpcXdrSuites",
                displayName: "RPC and XDR Foundation",
                cases: new List<TestCaseDescriptor>
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
                            string replyBodyCodecPath = Path.Combine(repositoryRoot, "src", "OpenNFS.Rpc", "Generated", "reply_body.Xdr.g.cs");
                            string authFlavorCodecPath = Path.Combine(repositoryRoot, "src", "OpenNFS.Rpc", "Generated", "auth_flavor.XdrCodec.g.cs");
                            string callbackSecParmsPath = Path.Combine(repositoryRoot, "src", "OpenNFS.Protocol.V41", "Generated", "callback_sec_parms4.g.cs");

                            string rpcConstants = await File.ReadAllTextAsync(rpcConstantsPath, cancellationToken).ConfigureAwait(false);
                            if (!rpcConstants.Contains("public const ulong RPCB_PORT = 111;", StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException("Expected emitted RPC constants output to contain the normalized RPCB port constant.");
                            }

                            string replyBodyCodec = await File.ReadAllTextAsync(replyBodyCodecPath, cancellationToken).ConfigureAwait(false);
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

                    new TestCaseDescriptor(
                        suiteId: "RpcXdrSuites",
                        caseId: "ScalarRoundTrip",
                        displayName: "XDR runtime round-trips scalar, opaque, string, and array values",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: _ =>
                        {
                            XdrWriter writer = new XdrWriter();
                            byte[] expectedFixedOpaque = new byte[] { 0x01, 0x02, 0x03 };
                            byte[] expectedVariableOpaque = new byte[] { 0x10, 0x20, 0x30, 0x40, 0x50 };
                            int[] expectedArray = new int[] { 7, -8, 9 };

                            writer.WriteBoolean(true);
                            writer.WriteInt32(-17);
                            writer.WriteUInt32(19);
                            writer.WriteInt64(-1234567890123456789L);
                            writer.WriteUInt64(12345678901234567890UL);
                            writer.WriteSingle(3.25F);
                            writer.WriteDouble(-12.5D);
                            writer.WriteFixedOpaque(expectedFixedOpaque);
                            writer.WriteVariableOpaque(expectedVariableOpaque, maximumLength: 8);
                            writer.WriteString("nfs-test", maximumUtf8ByteLength: 16);
                            writer.WriteVariableArray<int>(
                                expectedArray,
                                maximumCount: 4,
                                writeElement: static (xdrWriter, value) => xdrWriter.WriteInt32(value));

                            byte[] encoded = writer.ToArray();
                            if ((encoded.Length % 4) != 0)
                            {
                                throw new InvalidOperationException("Expected XDR output length to remain four-byte aligned.");
                            }

                            XdrReader reader = new XdrReader(encoded);
                            if (!reader.ReadBoolean())
                            {
                                throw new InvalidOperationException("Expected round-tripped boolean value to be true.");
                            }

                            if (reader.ReadInt32() != -17)
                            {
                                throw new InvalidOperationException("Expected round-tripped Int32 value to match.");
                            }

                            if (reader.ReadUInt32() != 19)
                            {
                                throw new InvalidOperationException("Expected round-tripped UInt32 value to match.");
                            }

                            if (reader.ReadInt64() != -1234567890123456789L)
                            {
                                throw new InvalidOperationException("Expected round-tripped Int64 value to match.");
                            }

                            if (reader.ReadUInt64() != 12345678901234567890UL)
                            {
                                throw new InvalidOperationException("Expected round-tripped UInt64 value to match.");
                            }

                            if (reader.ReadSingle() != 3.25F)
                            {
                                throw new InvalidOperationException("Expected round-tripped single value to match.");
                            }

                            if (reader.ReadDouble() != -12.5D)
                            {
                                throw new InvalidOperationException("Expected round-tripped double value to match.");
                            }

                            byte[] actualFixedOpaque = reader.ReadFixedOpaque(3);
                            if (!actualFixedOpaque.SequenceEqual(expectedFixedOpaque))
                            {
                                throw new InvalidOperationException("Expected fixed opaque payload to round-trip exactly.");
                            }

                            byte[] actualVariableOpaque = reader.ReadVariableOpaque(maximumLength: 8);
                            if (!actualVariableOpaque.SequenceEqual(expectedVariableOpaque))
                            {
                                throw new InvalidOperationException("Expected variable opaque payload to round-trip exactly.");
                            }

                            if (!string.Equals(reader.ReadString(maximumUtf8ByteLength: 16), "nfs-test", StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException("Expected string payload to round-trip exactly.");
                            }

                            IReadOnlyList<int> actualArray = reader.ReadVariableArray<int>(
                                maximumCount: 4,
                                readElement: static xdrReader => xdrReader.ReadInt32());
                            if (actualArray.Count != expectedArray.Length)
                            {
                                throw new InvalidOperationException("Expected array payload length to round-trip exactly.");
                            }

                            for (int index = 0; index < expectedArray.Length; index++)
                            {
                                if (actualArray[index] != expectedArray[index])
                                {
                                    throw new InvalidOperationException("Expected array payload element " + index + " to round-trip exactly.");
                                }
                            }

                            reader.EnsureFullyConsumed();
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "RpcXdrSuites",
                        caseId: "UnionRoundTrip",
                        displayName: "XDR runtime round-trips discriminated unions",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: _ =>
                        {
                            static void WriteUnionArm(XdrWriter writer, uint discriminant)
                            {
                                switch (discriminant)
                                {
                                    case 0:
                                        writer.WriteString("alpha");
                                        break;
                                    case 1:
                                        writer.WriteUInt64(42);
                                        break;
                                    case 2:
                                        break;
                                    default:
                                        throw new InvalidOperationException("Unexpected discriminant " + discriminant + " during union write.");
                                }
                            }

                            static string ReadUnionArm(XdrReader reader, uint discriminant)
                            {
                                switch (discriminant)
                                {
                                    case 0:
                                        return "string:" + reader.ReadString();
                                    case 1:
                                        return "number:" + reader.ReadUInt64();
                                    case 2:
                                        return "void";
                                    default:
                                        throw new InvalidOperationException("Unexpected discriminant " + discriminant + " during union read.");
                                }
                            }

                            XdrWriter writer = new XdrWriter();
                            writer.WriteDiscriminatedUnion<uint>(
                                0,
                                writeDiscriminant: static (xdrWriter, value) => xdrWriter.WriteUInt32(value),
                                writeArm: WriteUnionArm);
                            writer.WriteDiscriminatedUnion<uint>(
                                1,
                                writeDiscriminant: static (xdrWriter, value) => xdrWriter.WriteUInt32(value),
                                writeArm: WriteUnionArm);
                            writer.WriteDiscriminatedUnion<uint>(
                                2,
                                writeDiscriminant: static (xdrWriter, value) => xdrWriter.WriteUInt32(value),
                                writeArm: WriteUnionArm);

                            XdrReader reader = new XdrReader(writer.ToArray());
                            string first = reader.ReadDiscriminatedUnion<uint, string>(
                                readDiscriminant: static xdrReader => xdrReader.ReadUInt32(),
                                readArm: ReadUnionArm);
                            string second = reader.ReadDiscriminatedUnion<uint, string>(
                                readDiscriminant: static xdrReader => xdrReader.ReadUInt32(),
                                readArm: ReadUnionArm);
                            string third = reader.ReadDiscriminatedUnion<uint, string>(
                                readDiscriminant: static xdrReader => xdrReader.ReadUInt32(),
                                readArm: ReadUnionArm);

                            if (!string.Equals(first, "string:alpha", StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException("Expected string union arm to round-trip exactly.");
                            }

                            if (!string.Equals(second, "number:42", StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException("Expected numeric union arm to round-trip exactly.");
                            }

                            if (!string.Equals(third, "void", StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException("Expected void union arm to round-trip exactly.");
                            }

                            reader.EnsureFullyConsumed();
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "RpcXdrSuites",
                        caseId: "DecodeBoundsFailures",
                        displayName: "XDR runtime rejects truncated and over-limit payloads",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: _ =>
                        {
                            byte[] truncatedString = new byte[] { 0x00, 0x00, 0x00, 0x04, 0x41, 0x42 };
                            byte[] overLimitString = new byte[] { 0x00, 0x00, 0x00, 0x05, 0x68, 0x65, 0x6C, 0x6C, 0x6F, 0x00, 0x00, 0x00 };
                            byte[] invalidBoolean = new byte[] { 0x00, 0x00, 0x00, 0x02 };

                            ExpectXdrDataException(
                                () =>
                                {
                                    XdrReader reader = new XdrReader(truncatedString);
                                    reader.ReadString();
                                },
                                expectedMessageFragment: "Insufficient data",
                                expectedPosition: 4);

                            ExpectXdrDataException(
                                () =>
                                {
                                    XdrReader reader = new XdrReader(overLimitString);
                                    reader.ReadString(maximumUtf8ByteLength: 4);
                                },
                                expectedMessageFragment: "maximum allowed length",
                                expectedPosition: 0);

                            ExpectXdrDataException(
                                () =>
                                {
                                    XdrReader reader = new XdrReader(invalidBoolean);
                                    reader.ReadBoolean();
                                },
                                expectedMessageFragment: "boolean value",
                                expectedPosition: 0);

                            return Task.CompletedTask;
                        }),

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
                        })
                });
        }

        private static void ExpectXdrDataException(
            Action action,
            string expectedMessageFragment,
            int expectedPosition)
        {
            try
            {
                action();
                throw new InvalidOperationException("Expected XDR decoding to fail with message fragment '" + expectedMessageFragment + "'.");
            }
            catch (XdrDataException exception)
            {
                if (!exception.Message.Contains(expectedMessageFragment, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Expected XDR decoding to fail with message fragment '" + expectedMessageFragment + "', but received: " + exception.Message);
                }

                if (exception.Position != expectedPosition)
                {
                    throw new InvalidOperationException(
                        "Expected XDR decoding failure position " + expectedPosition + ", but received " + exception.Position + ".");
                }
            }
        }

        private static async Task ExpectInvalidDataAsync(
            Func<Task> action,
            string expectedMessageFragment,
            System.Threading.CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await action().ConfigureAwait(false);
                throw new InvalidOperationException("Expected generator validation to fail with message fragment '" + expectedMessageFragment + "'.");
            }
            catch (InvalidDataException exception)
            {
                if (!exception.Message.Contains(expectedMessageFragment, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Expected generator validation to fail with message fragment '" + expectedMessageFragment + "', but received: " + exception.Message);
                }
            }
        }

        private static async Task<TemporaryGeneratorScenario> CreateTemporaryGeneratorScenarioAsync(
            string tempRoot,
            System.Threading.CancellationToken cancellationToken)
        {
            string inputDirectory = Path.Combine(tempRoot, "specs");
            string outputDirectory = Path.Combine(tempRoot, "output");
            string generatedDirectoryPath = Path.Combine(outputDirectory, "Generated");
            Directory.CreateDirectory(inputDirectory);
            Directory.CreateDirectory(outputDirectory);

            string xdrPath = Path.Combine(inputDirectory, "simple.x");
            string projectPath = Path.Combine(outputDirectory, "Temp.Generated.csproj");
            string configurationPath = Path.Combine(tempRoot, "xdrgen.json");

            string xdrSource = """
const SIMPLE_LIMIT = 1;

enum simple_enum {
    SIMPLE_ZERO = 0
};

struct simple_struct {
    unsigned int value;
};

program SIMPLE_PROG {
    version SIMPLE_V1 {
        void SIMPLE_NULL(void) = 0;
    } = 1;
} = 100;
""";

            string projectSource = """
<Project Sdk="Microsoft.NET.Sdk">
</Project>
""";

            string configurationSource = """
{
  "projects": [
    {
      "id": "temp",
      "inputDirectory": "specs",
      "outputProjectPath": "output/Temp.Generated.csproj",
      "outputNamespace": "Temp.Generated",
      "entryPointFiles": [
        "simple.x"
      ]
    }
  ]
}
""";

            UTF8Encoding encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            await File.WriteAllTextAsync(xdrPath, xdrSource, encoding, cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(projectPath, projectSource, encoding, cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(configurationPath, configurationSource, encoding, cancellationToken).ConfigureAwait(false);

            return new TemporaryGeneratorScenario(configurationPath, generatedDirectoryPath);
        }

        private static string CreateTempDirectory()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), "OpenNFS-GeneratorTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);
            return tempDirectory;
        }

        private sealed class TemporaryGeneratorScenario
        {
            public TemporaryGeneratorScenario(string configurationPath, string generatedDirectoryPath)
            {
                ConfigurationPath = configurationPath;
                GeneratedDirectoryPath = generatedDirectoryPath;
            }

            public string ConfigurationPath { get; }

            public string GeneratedDirectoryPath { get; }
        }
    }
}
