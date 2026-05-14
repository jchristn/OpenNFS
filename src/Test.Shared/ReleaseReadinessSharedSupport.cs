namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Test.Shared.Infrastructure;

    /// <summary>
    /// Shared script, path, and cleanup helpers for release-readiness suites.
    /// </summary>
    internal static class ReleaseReadinessSharedSupport
    {
        internal static async Task AssertPlanModeAsync(
            string relativeScriptPath,
            IReadOnlyList<string> arguments,
            string expectedToken,
            CancellationToken cancellationToken)
        {
            PowerShellCommandResult result = await RunScriptAsync(relativeScriptPath, arguments, cancellationToken).ConfigureAwait(false);
            if (result.ExitCode != 0
                || !result.StandardOutput.Contains(expectedToken, StringComparison.OrdinalIgnoreCase)
                || !result.StandardOutput.Contains("results", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Expected plan mode for '" + relativeScriptPath + "' to succeed and describe the harness."
                    + Environment.NewLine
                    + result.StandardOutput
                    + Environment.NewLine
                    + result.StandardError);
            }
        }

        internal static bool ContainsEither(PowerShellCommandResult result, string first, string second)
        {
            return result.StandardOutput.Contains(first, StringComparison.OrdinalIgnoreCase)
                || result.StandardError.Contains(first, StringComparison.OrdinalIgnoreCase)
                || result.StandardOutput.Contains(second, StringComparison.OrdinalIgnoreCase)
                || result.StandardError.Contains(second, StringComparison.OrdinalIgnoreCase);
        }

        internal static string CreateTempDirectory(string scenarioName)
        {
            string tempPath = Path.Combine(
                Path.GetTempPath(),
                "OpenNFS.ReleaseReadiness",
                scenarioName + "." + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempPath);
            return tempPath;
        }

        internal static async Task<PowerShellCommandResult> RunScriptAsync(
            string relativeScriptPath,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken,
            TimeSpan? timeout = null)
        {
            string repositoryRoot = RepositoryPaths.GetRepositoryRoot();
            string scriptPath = Path.Combine(repositoryRoot, relativeScriptPath.Replace('\\', Path.DirectorySeparatorChar));
            return await PowerShellCli.RunScriptAsync(
                scriptPath,
                arguments,
                repositoryRoot,
                cancellationToken,
                timeout).ConfigureAwait(false);
        }

        internal static void TryDeleteDirectory(string directoryPath)
        {
            try
            {
                if (Directory.Exists(directoryPath))
                {
                    Directory.Delete(directoryPath, recursive: true);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
