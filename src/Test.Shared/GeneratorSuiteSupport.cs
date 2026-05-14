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

    /// <summary>
    /// Shared helpers for the baseline RPC and XDR generator suite catalog.
    /// </summary>
    internal static class GeneratorSuiteSupport
    {
        internal static void ExpectXdrDataException(
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

        internal static async Task ExpectInvalidDataAsync(
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

        internal static async Task<TemporaryGeneratorScenario> CreateTemporaryGeneratorScenarioAsync(
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

        internal static string CreateTempDirectory()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), "OpenNFS-GeneratorTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);
            return tempDirectory;
        }

        internal sealed class TemporaryGeneratorScenario
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
