namespace Test.Shared
{
    using System;
    using System.IO;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Test.Shared.Infrastructure;

    /// <summary>
    /// Synthetic repository, interop-wrapper, and conformance-harness validation flows.
    /// </summary>
    internal static class ReleaseReadinessHarnessValidationSupport
    {
        internal static async Task ExecuteRepositoryHonestyValidatorNegativeAsync(CancellationToken cancellationToken)
        {
            string tempRoot = ReleaseReadinessSharedSupport.CreateTempDirectory("RepositoryHonestyValidatorNegative");
            string repositoryRoot = RepositoryPaths.GetRepositoryRoot();

            try
            {
                Directory.CreateDirectory(Path.Combine(tempRoot, "docs"));
                Directory.CreateDirectory(Path.Combine(tempRoot, "src"));
                Directory.CreateDirectory(Path.Combine(tempRoot, "scripts"));

                await File.WriteAllTextAsync(
                    Path.Combine(tempRoot, "README.md"),
                    await File.ReadAllTextAsync(Path.Combine(repositoryRoot, "README.md"), cancellationToken).ConfigureAwait(false),
                    cancellationToken).ConfigureAwait(false);
                await File.WriteAllTextAsync(
                    Path.Combine(tempRoot, "OPENNFS.md"),
                    await File.ReadAllTextAsync(Path.Combine(repositoryRoot, "OPENNFS.md"), cancellationToken).ConfigureAwait(false),
                    cancellationToken).ConfigureAwait(false);
                await File.WriteAllTextAsync(
                    Path.Combine(tempRoot, "docs", "release-checklist.md"),
                    await File.ReadAllTextAsync(Path.Combine(repositoryRoot, "docs", "release-checklist.md"), cancellationToken).ConfigureAwait(false),
                    cancellationToken).ConfigureAwait(false);
                await File.WriteAllTextAsync(
                    Path.Combine(tempRoot, "src", "BadPlaceholder.cs"),
                    "namespace SyntheticRepo { internal static class BadPlaceholder { internal static void Throw() { throw new NotImplementedException(); } } }",
                    cancellationToken).ConfigureAwait(false);

                PowerShellCommandResult result = await ReleaseReadinessSharedSupport.RunScriptAsync(
                    @"scripts\release\Assert-RepositoryHonesty.ps1",
                    new[] { "-RepositoryRoot", tempRoot },
                    cancellationToken).ConfigureAwait(false);

                if (result.ExitCode == 0
                    || !ReleaseReadinessSharedSupport.ContainsEither(result, "NotImplementedException", "honesty validation failed"))
                {
                    throw new InvalidOperationException(
                        "Expected repository honesty validation to reject a synthetic placeholder implementation."
                        + Environment.NewLine
                        + result.StandardOutput
                        + Environment.NewLine
                        + result.StandardError);
                }
            }
            finally
            {
                ReleaseReadinessSharedSupport.TryDeleteDirectory(tempRoot);
            }
        }

        internal static async Task ExecutePrivilegedInteropWrapperValidatesScenarioInventoryAsync(CancellationToken cancellationToken)
        {
            string tempRoot = ReleaseReadinessSharedSupport.CreateTempDirectory("PrivilegedInteropWrapperValidation");

            try
            {
                string resultsDirectory = Path.Combine(tempRoot, "results");
                Directory.CreateDirectory(resultsDirectory);

                string resultsPath = Path.Combine(resultsDirectory, "touchstone-results.json");
                object[] positiveResults =
                {
                    CreateSyntheticTouchstoneResult("InteropSuites", "LinuxKernelClientMountsOpenNfsServer"),
                    CreateSyntheticTouchstoneResult("InteropSuites", "LinuxKernelClientMountsSampleOpenNfsServerArtifact"),
                    CreateSyntheticTouchstoneResult("InteropSuites", "OpenNfsClientReadsAndWritesAgainstLinuxKnfsdServer"),
                    CreateSyntheticTouchstoneResult("InteropSuites", "OpenNfsClientReadsAndWritesAgainstLinuxKnfsdServerOverNfs40"),
                    CreateSyntheticTouchstoneResult("ReplaySuites", "DisconnectReplayRecovery"),
                    CreateSyntheticTouchstoneResult("NlmSuites", "ReclaimAfterServerRestartPositive"),
                    CreateSyntheticTouchstoneResult("NfsV40Suites", "ReclaimAfterLeaseRecovery"),
                    CreateSyntheticTouchstoneResult("NfsV41Suites", "SessionReplayAfterReconnect"),
                    CreateSyntheticTouchstoneResult("SecuritySuites", "Krb5ReadWrite"),
                    CreateSyntheticTouchstoneResult("SecuritySuites", "Krb5iDetectsTamper"),
                    CreateSyntheticTouchstoneResult("SecuritySuites", "Krb5pEncryptsPayload"),
                    CreateSyntheticTouchstoneResult("SecuritySuites", "RpcSecGssContextEstablishment"),
                    CreateSyntheticTouchstoneResult("SecuritySuites", "RpcSecGssIntegrityFailureRejected"),
                    CreateSyntheticTouchstoneResult("SecuritySuites", "ServerBuilderRegistersRpcSecGssMechanism"),
                };

                await File.WriteAllTextAsync(
                    resultsPath,
                    JsonSerializer.Serialize(positiveResults),
                    cancellationToken).ConfigureAwait(false);

                PowerShellCommandResult positiveResult = await ReleaseReadinessSharedSupport.RunScriptAsync(
                    @"scripts\interop\Invoke-PrivilegedInterop.ps1",
                    new[]
                    {
                        "-ResultsDirectory", resultsDirectory,
                        "-TouchstoneResultsPath", resultsPath,
                    },
                    cancellationToken,
                    timeout: TimeSpan.FromMinutes(5)).ConfigureAwait(false);

                if (positiveResult.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        "Expected the privileged interop wrapper to accept a complete required scenario inventory."
                        + Environment.NewLine
                        + positiveResult.StandardOutput
                        + Environment.NewLine
                        + positiveResult.StandardError);
                }

                string manifestPath = Path.Combine(resultsDirectory, "privileged-interop-manifest.json");
                if (!File.Exists(manifestPath))
                {
                    throw new InvalidOperationException("Expected the privileged interop wrapper to emit a manifest.");
                }

                using JsonDocument manifest = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath, cancellationToken).ConfigureAwait(false));
                JsonElement root = manifest.RootElement;
                if (!root.TryGetProperty("requiredCases", out JsonElement requiredCases)
                    || requiredCases.GetArrayLength() != 14
                    || !root.TryGetProperty("validatedCases", out JsonElement validatedCases)
                    || validatedCases.GetArrayLength() != 14)
                {
                    throw new InvalidOperationException("Expected the privileged interop manifest to record every required validated case.");
                }

                object[] negativeResults =
                {
                    CreateSyntheticTouchstoneResult("InteropSuites", "LinuxKernelClientMountsOpenNfsServer"),
                    CreateSyntheticTouchstoneResult("ReplaySuites", "DisconnectReplayRecovery"),
                };

                await File.WriteAllTextAsync(
                    resultsPath,
                    JsonSerializer.Serialize(negativeResults),
                    cancellationToken).ConfigureAwait(false);

                PowerShellCommandResult negativeResult = await ReleaseReadinessSharedSupport.RunScriptAsync(
                    @"scripts\interop\Invoke-PrivilegedInterop.ps1",
                    new[]
                    {
                        "-ResultsDirectory", resultsDirectory,
                        "-TouchstoneResultsPath", resultsPath,
                    },
                    cancellationToken,
                    timeout: TimeSpan.FromMinutes(5)).ConfigureAwait(false);

                if (negativeResult.ExitCode == 0
                    || !ReleaseReadinessSharedSupport.ContainsEither(negativeResult, "OpenNfsClientReadsAndWritesAgainstLinuxKnfsdServer", "required case"))
                {
                    throw new InvalidOperationException(
                        "Expected the privileged interop wrapper to reject a missing required scenario."
                        + Environment.NewLine
                        + negativeResult.StandardOutput
                        + Environment.NewLine
                        + negativeResult.StandardError);
                }
            }
            finally
            {
                ReleaseReadinessSharedSupport.TryDeleteDirectory(tempRoot);
            }
        }

        internal static async Task ExecuteConformanceHarnessScriptsExecuteSyntheticSuitesAsync(CancellationToken cancellationToken)
        {
            string tempRoot = ReleaseReadinessSharedSupport.CreateTempDirectory("ConformanceHarnessSyntheticSuites");

            try
            {
                string suiteRoot = Path.Combine(tempRoot, "suites");
                string resultsRoot = Path.Combine(tempRoot, "results");
                Directory.CreateDirectory(suiteRoot);
                Directory.CreateDirectory(resultsRoot);

                string pjdfstestRoot = Path.Combine(suiteRoot, "pjdfstest");
                string connectathonRoot = Path.Combine(suiteRoot, "connectathon");
                string pynfsRoot = Path.Combine(suiteRoot, "pynfs");
                Directory.CreateDirectory(Path.Combine(pjdfstestRoot, "tests"));
                Directory.CreateDirectory(Path.Combine(connectathonRoot, "general"));
                Directory.CreateDirectory(pynfsRoot);

                string tapScript = string.Join(
                    "\n",
                    "use strict;",
                    "use warnings;",
                    "my $mount = $ENV{OPENNFS_MOUNT_POINT} // '';",
                    "print \"1..2\\n\";",
                    "if (-d $mount) { print \"ok 1 - mount exists\\n\"; } else { print \"not ok 1 - mount exists\\n\"; exit 1; }",
                    "if (-f \"$mount/hello.txt\") { print \"ok 2 - seeded file visible\\n\"; } else { print \"not ok 2 - seeded file visible\\n\"; exit 1; }",
                    string.Empty);

                await File.WriteAllTextAsync(Path.Combine(pjdfstestRoot, "tests", "smoke.t"), tapScript, cancellationToken).ConfigureAwait(false);
                string cthonRunScript = string.Join(
                    "\n",
                    "#!/bin/sh",
                    "echo Result: PASS",
                    string.Empty);
                string cthonRunPath = Path.Combine(connectathonRoot, "runtests");
                await File.WriteAllTextAsync(cthonRunPath, cthonRunScript, cancellationToken).ConfigureAwait(false);
                if (!OperatingSystem.IsWindows())
                {
                    File.SetUnixFileMode(cthonRunPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                        | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                        | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
                }
                await File.WriteAllTextAsync(Path.Combine(connectathonRoot, "general", "test01.t"), tapScript, cancellationToken).ConfigureAwait(false);

                string pynfsEntryPoint = string.Join(
                    "\n",
                    "import os",
                    "import socket",
                    "host = os.environ['OPENNFS_SERVER_HOST']",
                    "port = int(os.environ['OPENNFS_SERVER_PORT'])",
                    "with socket.create_connection((host, port), timeout=5):",
                    "    pass",
                    "print('PYNFS SYNTHETIC OK')",
                    string.Empty);
                await File.WriteAllTextAsync(Path.Combine(pynfsRoot, "entry.py"), pynfsEntryPoint, cancellationToken).ConfigureAwait(false);

                string pjdfstestResults = Path.Combine(resultsRoot, "pjdfstest");
                PowerShellCommandResult pjdfstestResult = await ReleaseReadinessSharedSupport.RunScriptAsync(
                    @"scripts\interop\pjdfstest\Invoke-Pjdfstest.ps1",
                    new[]
                    {
                        "-ProtocolVersion", "NfsV3",
                        "-ExportPath", "/exports/sample",
                        "-MountPoint", "/mnt/opennfs",
                        "-ResultsDirectory", pjdfstestResults,
                        "-SuiteRoot", pjdfstestRoot,
                        "-Subset", "smoke",
                        "-UseSampleServer",
                    },
                    cancellationToken,
                    timeout: TimeSpan.FromMinutes(10)).ConfigureAwait(false);

                if (pjdfstestResult.ExitCode != 0
                    || !File.Exists(Path.Combine(pjdfstestResults, "pjdfstest-manifest.json"))
                    || !File.ReadAllText(Path.Combine(pjdfstestResults, "stdout.log")).Contains("Result: PASS", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Expected the pjdfstest harness to execute a synthetic mounted suite successfully."
                        + Environment.NewLine
                        + pjdfstestResult.StandardOutput
                        + Environment.NewLine
                        + pjdfstestResult.StandardError);
                }

                string connectathonResults = Path.Combine(resultsRoot, "connectathon");
                PowerShellCommandResult connectathonResult = await ReleaseReadinessSharedSupport.RunScriptAsync(
                    @"scripts\interop\connectathon\Invoke-Connectathon.ps1",
                    new[]
                    {
                        "-ProtocolVersion", "NfsV3",
                        "-ExportPath", "/exports/sample",
                        "-MountPoint", "/mnt/opennfs",
                        "-ResultsDirectory", connectathonResults,
                        "-SuiteRoot", connectathonRoot,
                        "-Subset", "general",
                        "-UseSampleServer",
                    },
                    cancellationToken,
                    timeout: TimeSpan.FromMinutes(10)).ConfigureAwait(false);

                if (connectathonResult.ExitCode != 0
                    || !File.Exists(Path.Combine(connectathonResults, "connectathon-manifest.json"))
                    || !File.ReadAllText(Path.Combine(connectathonResults, "stdout.log")).Contains("Result: PASS", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Expected the Connectathon harness to execute a synthetic mounted suite successfully."
                        + Environment.NewLine
                        + connectathonResult.StandardOutput
                        + Environment.NewLine
                        + connectathonResult.StandardError);
                }

                string pynfsResults = Path.Combine(resultsRoot, "pynfs");
                PowerShellCommandResult pynfsResult = await ReleaseReadinessSharedSupport.RunScriptAsync(
                    @"scripts\interop\pynfs\Invoke-Pynfs.ps1",
                    new[]
                    {
                        "-MinorVersion", "0",
                        "-ExportPath", "/exports/sample",
                        "-ResultsDirectory", pynfsResults,
                        "-SuiteRoot", pynfsRoot,
                        "-EntryPoint", "entry.py",
                        "-UseSampleServer",
                    },
                    cancellationToken,
                    timeout: TimeSpan.FromMinutes(10)).ConfigureAwait(false);

                if (pynfsResult.ExitCode != 0
                    || !File.Exists(Path.Combine(pynfsResults, "pynfs-manifest.json"))
                    || !File.ReadAllText(Path.Combine(pynfsResults, "stdout.log")).Contains("PYNFS SYNTHETIC OK", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Expected the pynfs harness to execute a synthetic direct-peer suite successfully."
                        + Environment.NewLine
                        + pynfsResult.StandardOutput
                        + Environment.NewLine
                        + pynfsResult.StandardError);
                }
            }
            finally
            {
                ReleaseReadinessSharedSupport.TryDeleteDirectory(tempRoot);
            }
        }

        internal static object CreateSyntheticTouchstoneResult(string suiteId, string caseId)
        {
            return new
            {
                testId = suiteId + "." + caseId,
                suiteId,
                caseId,
                displayName = caseId,
                success = true,
                skipped = false,
                durationMs = 1,
                message = (string?)null,
            };
        }
    }
}
