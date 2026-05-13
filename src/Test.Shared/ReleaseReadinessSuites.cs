namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// Touchstone suites covering release-checklist and conformance-harness automation.
    /// </summary>
    public static class ReleaseReadinessSuites
    {
        /// <summary>
        /// Creates the shared release-readiness suite catalog.
        /// </summary>
        /// <returns>The configured suite descriptor.</returns>
        public static TestSuiteDescriptor Create()
        {
            DockerInteropEnvironmentProbe probe = DockerInteropEnvironmentProbe.Current;

            return new TestSuiteDescriptor(
                suiteId: "ReleaseReadinessSuites",
                displayName: "Release Readiness Automation",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        suiteId: "ReleaseReadinessSuites",
                        caseId: "ReleaseChecklistValidatorPositive",
                        displayName: "Release checklist validator accepts the current repository checklist",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            PowerShellCommandResult result = await RunScriptAsync(
                                @"scripts\release\Assert-ReleaseChecklist.ps1",
                                Array.Empty<string>(),
                                cancellationToken).ConfigureAwait(false);

                            if (result.ExitCode != 0)
                            {
                                throw new InvalidOperationException(
                                    "Expected the current release checklist to validate cleanly."
                                    + Environment.NewLine
                                    + result.StandardOutput
                                    + Environment.NewLine
                                    + result.StandardError);
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ReleaseReadinessSuites",
                        caseId: "ReleaseChecklistValidatorNegative",
                        displayName: "Release checklist validator rejects a checklist that omits mandatory conformance gates",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            string tempRoot = CreateTempDirectory("ReleaseChecklistValidatorNegative");
                            try
                            {
                                string checklistPath = Path.Combine(tempRoot, "release-checklist.md");
                                await File.WriteAllTextAsync(
                                    checklistPath,
                                    "# Incomplete checklist" + Environment.NewLine + "- dotnet build" + Environment.NewLine,
                                    cancellationToken).ConfigureAwait(false);

                                PowerShellCommandResult result = await RunScriptAsync(
                                    @"scripts\release\Assert-ReleaseChecklist.ps1",
                                    new[] { "-ChecklistPath", checklistPath },
                                    cancellationToken).ConfigureAwait(false);

                                if (result.ExitCode == 0
                                    || !ContainsEither(result, "pjdfstest", "Connectathon")
                                    || !ContainsEither(result, "pynfs", "Linux mount"))
                                {
                                    throw new InvalidOperationException(
                                        "Expected the release checklist validator to fail with the missing mandatory support gates."
                                        + Environment.NewLine
                                        + result.StandardOutput
                                        + Environment.NewLine
                                        + result.StandardError);
                                }
                            }
                            finally
                            {
                                TryDeleteDirectory(tempRoot);
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ReleaseReadinessSuites",
                        caseId: "NoSkippedTestsValidatorPositive",
                        displayName: "Skipped-test validator accepts the current repository state",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            PowerShellCommandResult result = await RunScriptAsync(
                                @"scripts\release\Assert-NoSkippedTests.ps1",
                                Array.Empty<string>(),
                                cancellationToken).ConfigureAwait(false);

                            if (result.ExitCode != 0)
                            {
                                throw new InvalidOperationException(
                                    "Expected the current repository to pass skipped-test validation."
                                    + Environment.NewLine
                                    + result.StandardOutput
                                    + Environment.NewLine
                                    + result.StandardError);
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ReleaseReadinessSuites",
                        caseId: "NoSkippedTestsValidatorNegative",
                        displayName: "Skipped-test validator rejects explicit skip markers",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            string tempRoot = CreateTempDirectory("NoSkippedTestsValidatorNegative");
                            try
                            {
                                string sourceDirectory = Path.Combine(tempRoot, "src");
                                Directory.CreateDirectory(sourceDirectory);
                                string sourcePath = Path.Combine(sourceDirectory, "SkippedFact.cs");
                                await File.WriteAllTextAsync(
                                    sourcePath,
                                    "[Fact(" + "Sk" + "ip = \"no\")]" + Environment.NewLine + "public sealed class SkippedFact { }" + Environment.NewLine,
                                    cancellationToken).ConfigureAwait(false);

                                PowerShellCommandResult result = await RunScriptAsync(
                                    @"scripts\release\Assert-NoSkippedTests.ps1",
                                    new[] { "-RepositoryRoot", tempRoot },
                                    cancellationToken).ConfigureAwait(false);

                                if (result.ExitCode == 0
                                    || !ContainsEither(result, "Sk" + "ip =", "Skipped-test validation failed"))
                                {
                                    throw new InvalidOperationException(
                                        "Expected the skipped-test validator to reject an explicit skip marker."
                                        + Environment.NewLine
                                        + result.StandardOutput
                                        + Environment.NewLine
                                        + result.StandardError);
                                }
                            }
                            finally
                            {
                                TryDeleteDirectory(tempRoot);
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ReleaseReadinessSuites",
                        caseId: "RepositoryHonestyValidatorPositive",
                        displayName: "Repository honesty validator accepts the current repository state",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            PowerShellCommandResult result = await RunScriptAsync(
                                @"scripts\release\Assert-RepositoryHonesty.ps1",
                                Array.Empty<string>(),
                                cancellationToken).ConfigureAwait(false);

                            if (result.ExitCode != 0)
                            {
                                throw new InvalidOperationException(
                                    "Expected the current repository to pass honesty validation."
                                    + Environment.NewLine
                                    + result.StandardOutput
                                    + Environment.NewLine
                                    + result.StandardError);
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ReleaseReadinessSuites",
                        caseId: "RepositoryHonestyValidatorNegative",
                        displayName: "Repository honesty validator rejects placeholder markers in a synthetic repository root",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: ExecuteRepositoryHonestyValidatorNegativeAsync),

                    new TestCaseDescriptor(
                        suiteId: "ReleaseReadinessSuites",
                        caseId: "ReleaseValidationPlanMentionsMandatoryGates",
                        displayName: "Release validation plan enumerates mandatory local and external support gates",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            PowerShellCommandResult result = await RunScriptAsync(
                                @"scripts\release\Invoke-ReleaseValidation.ps1",
                                new[] { "-PlanOnly", "-IncludePack" },
                                cancellationToken,
                                timeout: TimeSpan.FromMinutes(5)).ConfigureAwait(false);

                            if (result.ExitCode != 0
                                || !ContainsEither(result, "Generate-Xdr.ps1 -Check", "Test.Automated")
                                || !ContainsEither(result, "Assert-NoSkippedTests.ps1", "release-checklist.md")
                                || !ContainsEither(result, "Assert-RepositoryHonesty.ps1", "honesty")
                                || !ContainsEither(result, "pjdfstest", "Connectathon")
                                || !ContainsEither(result, "pynfs", "OpenNFS.Server"))
                            {
                                throw new InvalidOperationException(
                                    "Expected the release validation plan to list the mandatory local and external gates."
                                    + Environment.NewLine
                                    + result.StandardOutput
                                    + Environment.NewLine
                                    + result.StandardError);
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ReleaseReadinessSuites",
                        caseId: "ConformanceHarnessScriptsPlanAndValidation",
                        displayName: "Conformance harness scripts expose plan mode and reject execution without required external suite roots",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            await AssertPlanModeAsync(
                                @"scripts\interop\Invoke-PrivilegedInterop.ps1",
                                new[] { "-PlanOnly", "-ResultsDirectory", "artifacts/interop-privileged" },
                                "privileged-interop",
                                cancellationToken).ConfigureAwait(false);

                            await AssertPlanModeAsync(
                                @"scripts\interop\pjdfstest\Invoke-Pjdfstest.ps1",
                                new[]
                                {
                                    "-PlanOnly",
                                    "-ProtocolVersion", "NfsV3",
                                    "-ServerHost", "sample-host",
                                    "-ExportPath", "/export",
                                    "-MountPoint", "/mnt/opennfs",
                                    "-ResultsDirectory", "artifacts/pjdfstest",
                                },
                                "pjdfstest",
                                cancellationToken).ConfigureAwait(false);

                            await AssertPlanModeAsync(
                                @"scripts\interop\connectathon\Invoke-Connectathon.ps1",
                                new[]
                                {
                                    "-PlanOnly",
                                    "-ProtocolVersion", "NfsV3",
                                    "-ServerHost", "sample-host",
                                    "-ExportPath", "/export",
                                    "-MountPoint", "/mnt/opennfs",
                                    "-ResultsDirectory", "artifacts/connectathon",
                                },
                                "connectathon",
                                cancellationToken).ConfigureAwait(false);

                            await AssertPlanModeAsync(
                                @"scripts\interop\pynfs\Invoke-Pynfs.ps1",
                                new[]
                                {
                                    "-PlanOnly",
                                    "-MinorVersion", "0",
                                    "-ServerHost", "sample-host",
                                    "-ExportPath", "/export",
                                    "-ResultsDirectory", "artifacts/pynfs",
                                },
                                "pynfs",
                                cancellationToken).ConfigureAwait(false);

                            PowerShellCommandResult pjdfstestNegative = await RunScriptAsync(
                                @"scripts\interop\pjdfstest\Invoke-Pjdfstest.ps1",
                                new[]
                                {
                                    "-ProtocolVersion", "NfsV3",
                                    "-ServerHost", "sample-host",
                                    "-ExportPath", "/export",
                                    "-MountPoint", "/mnt/opennfs",
                                    "-ResultsDirectory", "artifacts/pjdfstest",
                                },
                                cancellationToken).ConfigureAwait(false);

                            PowerShellCommandResult pynfsNegative = await RunScriptAsync(
                                @"scripts\interop\pynfs\Invoke-Pynfs.ps1",
                                new[]
                                {
                                    "-MinorVersion", "0",
                                    "-ServerHost", "sample-host",
                                    "-ExportPath", "/export",
                                    "-ResultsDirectory", "artifacts/pynfs",
                                },
                                cancellationToken).ConfigureAwait(false);

                            if (pjdfstestNegative.ExitCode == 0
                                || !ContainsEither(pjdfstestNegative, "PJDFSTEST_ROOT", "SuiteRoot"))
                            {
                                throw new InvalidOperationException(
                                    "Expected pjdfstest execution without a suite root to fail clearly."
                                    + Environment.NewLine
                                    + pjdfstestNegative.StandardOutput
                                    + Environment.NewLine
                                    + pjdfstestNegative.StandardError);
                            }

                            if (pynfsNegative.ExitCode == 0
                                || !ContainsEither(pynfsNegative, "PYNFS_ROOT", "SuiteRoot"))
                            {
                                throw new InvalidOperationException(
                                    "Expected pynfs execution without a suite root to fail clearly."
                                    + Environment.NewLine
                                    + pynfsNegative.StandardOutput
                                    + Environment.NewLine
                                    + pynfsNegative.StandardError);
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ReleaseReadinessSuites",
                        caseId: "PrivilegedInteropWrapperValidatesScenarioInventory",
                        displayName: "Privileged interop wrapper validates the required kernel, replay, and recovery scenario inventory",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: ExecutePrivilegedInteropWrapperValidatesScenarioInventoryAsync),

                    new TestCaseDescriptor(
                        suiteId: "ReleaseReadinessSuites",
                        caseId: "ConformanceHarnessScriptsExecuteSyntheticSuites",
                        displayName: "Conformance harness scripts execute synthetic mounted-suite and pynfs roots against the sample server",
                        tags: new List<string> { TestCategories.Interop, TestCategories.Privileged, TestCategories.Automated },
                        skip: !probe.IsAvailable,
                        skipReason: probe.SkipReason,
                        executeAsync: ExecuteConformanceHarnessScriptsExecuteSyntheticSuitesAsync),

                    new TestCaseDescriptor(
                        suiteId: "ReleaseReadinessSuites",
                        caseId: "WorkflowFilesReferenceReleaseAutomation",
                        displayName: "Workflow files reference release automation and conformance harness entry points",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            string repositoryRoot = RepositoryPaths.GetRepositoryRoot();
                            string buildWorkflowPath = Path.Combine(repositoryRoot, ".github", "workflows", "build.yaml");
                            string testWorkflowPath = Path.Combine(repositoryRoot, ".github", "workflows", "test.yaml");
                            string hostedWorkflowPath = Path.Combine(repositoryRoot, ".github", "workflows", "interop-hosted.yaml");
                            string selfHostedWorkflowPath = Path.Combine(repositoryRoot, ".github", "workflows", "interop-selfhosted.yaml");
                            string pynfsWorkflowPath = Path.Combine(repositoryRoot, ".github", "workflows", "pynfs.yaml");

                            string buildWorkflow = await File.ReadAllTextAsync(buildWorkflowPath, cancellationToken).ConfigureAwait(false);
                            string testWorkflow = await File.ReadAllTextAsync(testWorkflowPath, cancellationToken).ConfigureAwait(false);
                            string hostedWorkflow = await File.ReadAllTextAsync(hostedWorkflowPath, cancellationToken).ConfigureAwait(false);
                            string selfHostedWorkflow = await File.ReadAllTextAsync(selfHostedWorkflowPath, cancellationToken).ConfigureAwait(false);
                            string pynfsWorkflow = await File.ReadAllTextAsync(pynfsWorkflowPath, cancellationToken).ConfigureAwait(false);

                            if (!buildWorkflow.Contains("Invoke-ReleaseValidation.ps1 -PlanOnly", StringComparison.Ordinal)
                                || !buildWorkflow.Contains("Assert-RepositoryHonesty.ps1", StringComparison.Ordinal)
                                || !testWorkflow.Contains("Assert-NoSkippedTests.ps1", StringComparison.Ordinal)
                                || !testWorkflow.Contains("Assert-RepositoryHonesty.ps1", StringComparison.Ordinal)
                                || !hostedWorkflow.Contains("Invoke-Pjdfstest.ps1 -ProtocolVersion NfsV3", StringComparison.Ordinal)
                                || !hostedWorkflow.Contains("Invoke-Connectathon.ps1 -ProtocolVersion NfsV3", StringComparison.Ordinal)
                                || !hostedWorkflow.Contains("Invoke-Pynfs.ps1 -MinorVersion 0", StringComparison.Ordinal)
                                || !hostedWorkflow.Contains("-UseSampleServer", StringComparison.Ordinal)
                                || !selfHostedWorkflow.Contains("Invoke-PrivilegedInterop.ps1", StringComparison.Ordinal)
                                || !selfHostedWorkflow.Contains("$env:PJDFSTEST_ROOT", StringComparison.Ordinal)
                                || !selfHostedWorkflow.Contains("$env:CONNECTATHON_ROOT", StringComparison.Ordinal)
                                || !selfHostedWorkflow.Contains("$env:PYNFS_ROOT", StringComparison.Ordinal)
                                || !pynfsWorkflow.Contains("$env:PYNFS_ENTRYPOINT", StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException("Expected the workflow files to reference the new release-validation and conformance harness scripts.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ReleaseReadinessSuites",
                        caseId: "PackedServerPackageServesLinuxKernelClient",
                        displayName: "A clean packaged OpenNFS.Server consumer can be mounted and used by a Linux kernel client",
                        tags: new List<string> { TestCategories.Interop, TestCategories.Privileged, TestCategories.Automated },
                        skip: !probe.IsAvailable,
                        skipReason: probe.SkipReason,
                        executeAsync: ExecutePackedServerPackageServesLinuxKernelClientAsync),

                    new TestCaseDescriptor(
                        suiteId: "ReleaseReadinessSuites",
                        caseId: "PackedServerPackageDeniesLinuxKernelClientMount",
                        displayName: "A clean packaged OpenNFS.Server consumer preserves denied Linux kernel mount behavior",
                        tags: new List<string> { TestCategories.Interop, TestCategories.Privileged, TestCategories.Automated },
                        skip: !probe.IsAvailable,
                        skipReason: probe.SkipReason,
                        executeAsync: ExecutePackedServerPackageDeniesLinuxKernelClientMountAsync),

                    new TestCaseDescriptor(
                        suiteId: "ReleaseReadinessSuites",
                        caseId: "PackedClientPackageExecutesAgainstSampleKnfsdAndGanesha",
                        displayName: "A clean packaged OpenNFS.Client consumer executes the current peer matrix against the sample server, knfsd, and nfs-ganesha",
                        tags: new List<string> { TestCategories.Interop, TestCategories.Privileged, TestCategories.Automated },
                        skip: !probe.IsAvailable,
                        skipReason: probe.SkipReason,
                        executeAsync: ExecutePackedClientPackageExecutesAgainstPeerMatrixAsync),

                    new TestCaseDescriptor(
                        suiteId: "ReleaseReadinessSuites",
                        caseId: "PackedClientPackageSurfacesNegativeResultsAgainstSampleKnfsdAndGanesha",
                        displayName: "A clean packaged OpenNFS.Client consumer surfaces negative results against the sample server, knfsd, and nfs-ganesha",
                        tags: new List<string> { TestCategories.Interop, TestCategories.Privileged, TestCategories.Automated },
                        skip: !probe.IsAvailable,
                        skipReason: probe.SkipReason,
                        executeAsync: ExecutePackedClientPackageSurfacesNegativeResultsAgainstPeerMatrixAsync),
                });
        }

        private static async Task ExecuteRepositoryHonestyValidatorNegativeAsync(CancellationToken cancellationToken)
        {
            string tempRoot = CreateTempDirectory("RepositoryHonestyValidatorNegative");
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

                PowerShellCommandResult result = await RunScriptAsync(
                    @"scripts\release\Assert-RepositoryHonesty.ps1",
                    new[] { "-RepositoryRoot", tempRoot },
                    cancellationToken).ConfigureAwait(false);

                if (result.ExitCode == 0
                    || !ContainsEither(result, "NotImplementedException", "honesty validation failed"))
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
                TryDeleteDirectory(tempRoot);
            }
        }

        private static async Task ExecutePrivilegedInteropWrapperValidatesScenarioInventoryAsync(CancellationToken cancellationToken)
        {
            string tempRoot = CreateTempDirectory("PrivilegedInteropWrapperValidation");

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
                    CreateSyntheticTouchstoneResult("InteropSuites", "OpenNfsClientSurfacesNegativeResultsAgainstLinuxKnfsdServer"),
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

                PowerShellCommandResult positiveResult = await RunScriptAsync(
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
                    || requiredCases.GetArrayLength() != 15
                    || !root.TryGetProperty("validatedCases", out JsonElement validatedCases)
                    || validatedCases.GetArrayLength() != 15)
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

                PowerShellCommandResult negativeResult = await RunScriptAsync(
                    @"scripts\interop\Invoke-PrivilegedInterop.ps1",
                    new[]
                    {
                        "-ResultsDirectory", resultsDirectory,
                        "-TouchstoneResultsPath", resultsPath,
                    },
                    cancellationToken,
                    timeout: TimeSpan.FromMinutes(5)).ConfigureAwait(false);

                if (negativeResult.ExitCode == 0
                    || !ContainsEither(negativeResult, "OpenNfsClientReadsAndWritesAgainstLinuxKnfsdServer", "required case"))
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
                TryDeleteDirectory(tempRoot);
            }
        }

        private static async Task ExecuteConformanceHarnessScriptsExecuteSyntheticSuitesAsync(CancellationToken cancellationToken)
        {
            string tempRoot = CreateTempDirectory("ConformanceHarnessSyntheticSuites");

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
                // Synthetic Connectathon layout: a runtests script at the root and a per-subset
                // directory matching the real cthon04 convention. The script must be executable so
                // the Linux container that runs it can invoke `./runtests`.
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
                PowerShellCommandResult pjdfstestResult = await RunScriptAsync(
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
                PowerShellCommandResult connectathonResult = await RunScriptAsync(
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
                PowerShellCommandResult pynfsResult = await RunScriptAsync(
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
                TryDeleteDirectory(tempRoot);
            }
        }

        private static object CreateSyntheticTouchstoneResult(string suiteId, string caseId)
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

        private static async Task ExecutePackedServerPackageServesLinuxKernelClientAsync(CancellationToken cancellationToken)
        {
            string programSource = PackagedConsumerProgramSourceFactory.CreateServerApplicationProgramSource(denyMounts: false);

            await using PackedOpenNfsServerProcess process =
                await PackedOpenNfsServerProcess.StartAsync(programSource, cancellationToken).ConfigureAwait(false);

            DockerCommandResult result = await DockerLinuxNfsClient.RunCommandAsync(
                CreateLinuxMountReadWriteCommand(process.MountPort, process.NfsPort, "/data", "hello.txt"),
                cancellationToken).ConfigureAwait(false);

            string combinedOutput = result.StandardOutput + Environment.NewLine + result.StandardError;
            if (result.ExitCode != 0
                || !combinedOutput.Contains("hello-from-packed-server", StringComparison.Ordinal)
                || !combinedOutput.Contains("UPDATED-FROM-LINUX-CLIENT", StringComparison.Ordinal)
                || !combinedOutput.Contains("hello.txt", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Expected a clean packaged OpenNFS.Server consumer to be mountable and writable from a Linux kernel client."
                    + Environment.NewLine
                    + combinedOutput);
            }
        }

        private static async Task ExecutePackedServerPackageDeniesLinuxKernelClientMountAsync(CancellationToken cancellationToken)
        {
            string programSource = PackagedConsumerProgramSourceFactory.CreateServerApplicationProgramSource(denyMounts: true);

            await using PackedOpenNfsServerProcess process =
                await PackedOpenNfsServerProcess.StartAsync(programSource, cancellationToken).ConfigureAwait(false);

            DockerCommandResult result = await DockerLinuxNfsClient.RunCommandAsync(
                CreateLinuxDeniedMountCommand(process.MountPort, process.NfsPort, "/data"),
                cancellationToken).ConfigureAwait(false);

            string combinedOutput = result.StandardOutput + Environment.NewLine + result.StandardError;
            if (result.ExitCode != 0
                || !combinedOutput.Contains("DENIED", StringComparison.Ordinal)
                || combinedOutput.Contains("unexpected success", StringComparison.Ordinal)
                || combinedOutput.Contains("hello-from-packed-server", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Expected a clean packaged OpenNFS.Server consumer to preserve denied Linux mount behavior."
                    + Environment.NewLine
                    + combinedOutput);
            }
        }

        private static async Task ExecutePackedClientPackageExecutesAgainstPeerMatrixAsync(CancellationToken cancellationToken)
        {
            string sampleRoot = CreateTempDirectory("PackedClientPeerMatrixPositive.Sample");
            string sampleSource = Path.Combine(sampleRoot, "source");
            string sampleMapping = Path.Combine(sampleRoot, "handles.json");

            try
            {
                await using SampleOpenNfsServerProcess sample = await SampleOpenNfsServerProcess.StartAsync(
                    sampleSource,
                    sampleMapping,
                    denyMounts: false,
                    cancellationToken).ConfigureAwait(false);
                await using DockerLinuxKnfsdServerContainer knfsd =
                    await DockerLinuxKnfsdServerContainer.StartAsync(cancellationToken).ConfigureAwait(false);
                await using DockerLinuxNfsV40ServerContainer ganesha =
                    await DockerLinuxNfsV40ServerContainer.StartAsync(cancellationToken).ConfigureAwait(false);

                string programSource = PackagedConsumerProgramSourceFactory.CreateClientPeerMatrixPositiveProgramSource(
                    "127.0.0.1",
                    sample.MountPort,
                    sample.NfsPort,
                    sample.Nfs40Port,
                    "127.0.0.1",
                    knfsd.MountPort,
                    knfsd.NfsPort,
                    "127.0.0.1",
                    ganesha.NfsPort);

                DotnetCommandResult result = await RunExternalPackageConsumerProjectAsync(
                    Path.Combine("src", "OpenNFS.Client", "OpenNFS.Client.csproj"),
                    "OpenNFS.Client",
                    programSource,
                    cancellationToken,
                    timeout: TimeSpan.FromMinutes(8)).ConfigureAwait(false);

                if (!result.StandardOutput.Contains("PACKAGE CLIENT MATRIX POSITIVE OK", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Expected the clean packaged OpenNFS.Client consumer to complete the current positive peer matrix."
                        + Environment.NewLine
                        + "stdout:"
                        + Environment.NewLine
                        + result.StandardOutput
                        + Environment.NewLine
                        + "stderr:"
                        + Environment.NewLine
                        + result.StandardError);
                }
            }
            finally
            {
                TryDeleteDirectory(sampleRoot);
            }
        }

        private static async Task ExecutePackedClientPackageSurfacesNegativeResultsAgainstPeerMatrixAsync(CancellationToken cancellationToken)
        {
            string sampleRoot = CreateTempDirectory("PackedClientPeerMatrixNegative.Sample");
            string sampleSource = Path.Combine(sampleRoot, "source");
            string sampleMapping = Path.Combine(sampleRoot, "handles.json");

            try
            {
                await using SampleOpenNfsServerProcess sample = await SampleOpenNfsServerProcess.StartAsync(
                    sampleSource,
                    sampleMapping,
                    denyMounts: false,
                    cancellationToken).ConfigureAwait(false);
                await using DockerLinuxKnfsdServerContainer knfsd =
                    await DockerLinuxKnfsdServerContainer.StartAsync(cancellationToken).ConfigureAwait(false);
                await using DockerLinuxNfsV40ServerContainer ganesha =
                    await DockerLinuxNfsV40ServerContainer.StartAsync(cancellationToken).ConfigureAwait(false);

                string programSource = PackagedConsumerProgramSourceFactory.CreateClientPeerMatrixNegativeProgramSource(
                    "127.0.0.1",
                    sample.MountPort,
                    sample.NfsPort,
                    sample.Nfs40Port,
                    "127.0.0.1",
                    knfsd.NfsPort,
                    "127.0.0.1",
                    ganesha.NfsPort);

                DotnetCommandResult result = await RunExternalPackageConsumerProjectAsync(
                    Path.Combine("src", "OpenNFS.Client", "OpenNFS.Client.csproj"),
                    "OpenNFS.Client",
                    programSource,
                    cancellationToken,
                    timeout: TimeSpan.FromMinutes(8)).ConfigureAwait(false);

                if (!result.StandardOutput.Contains("PACKAGE CLIENT MATRIX NEGATIVE OK", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Expected the clean packaged OpenNFS.Client consumer to complete the current negative peer matrix."
                        + Environment.NewLine
                        + "stdout:"
                        + Environment.NewLine
                        + result.StandardOutput
                        + Environment.NewLine
                        + "stderr:"
                        + Environment.NewLine
                        + result.StandardError);
                }
            }
            finally
            {
                TryDeleteDirectory(sampleRoot);
            }
        }

        private static async Task<DotnetCommandResult> RunExternalPackageConsumerProjectAsync(
            string packageProjectRelativePath,
            string packageId,
            string programSource,
            CancellationToken cancellationToken,
            TimeSpan timeout)
        {
            await using ExternalPackageConsumerProject project = await ExternalPackageConsumerSupport.CreateSinglePackageConsoleAppAsync(
                packageProjectRelativePath,
                packageId,
                programSource,
                cancellationToken).ConfigureAwait(false);

            return await DotnetCli.RunCheckedAsync(
                new[]
                {
                    "run",
                    "--disable-build-servers",
                    "--project",
                    project.ProjectPath,
                    "-c",
                    "Release",
                    "--no-restore",
                },
                project.ProjectDirectory,
                cancellationToken,
                timeout).ConfigureAwait(false);
        }

        private static string CreateLinuxMountReadWriteCommand(int mountPort, int nfsPort, string exportPath, string fileName)
        {
            return string.Concat(
                "set -eu; ",
                "mkdir -p /mnt/opennfs; ",
                "mount -t nfs -o vers=3,proto=tcp,mountproto=tcp,port=", nfsPort.ToString(),
                ",mountport=", mountPort.ToString(),
                ",nolock,soft,timeo=10,retrans=1 host.docker.internal:", exportPath, " /mnt/opennfs; ",
                "cat /mnt/opennfs/", fileName, "; ",
                "printf 'UPDATED-FROM-LINUX-CLIENT' | dd of=/mnt/opennfs/", fileName, " conv=notrunc status=none; ",
                "sync; ",
                "cat /mnt/opennfs/", fileName, "; ",
                "ls -1 /mnt/opennfs; ",
                "umount /mnt/opennfs");
        }

        private static string CreateLinuxDeniedMountCommand(int mountPort, int nfsPort, string exportPath)
        {
            return string.Concat(
                "set -eu; ",
                "mkdir -p /mnt/opennfs; ",
                "set +e; ",
                "mount -t nfs -o vers=3,proto=tcp,mountproto=tcp,port=", nfsPort.ToString(),
                ",mountport=", mountPort.ToString(),
                ",nolock,soft,timeo=10,retrans=1 host.docker.internal:", exportPath, " /mnt/opennfs >/tmp/mount.log 2>&1; ",
                "status=$?; ",
                "cat /tmp/mount.log; ",
                "if [ $status -eq 0 ]; then echo unexpected success; umount /mnt/opennfs; exit 1; fi; ",
                "echo DENIED; ",
                "exit 0");
        }

        private static async Task AssertPlanModeAsync(
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

        private static bool ContainsEither(PowerShellCommandResult result, string first, string second)
        {
            return result.StandardOutput.Contains(first, StringComparison.OrdinalIgnoreCase)
                || result.StandardError.Contains(first, StringComparison.OrdinalIgnoreCase)
                || result.StandardOutput.Contains(second, StringComparison.OrdinalIgnoreCase)
                || result.StandardError.Contains(second, StringComparison.OrdinalIgnoreCase);
        }

        private static string CreateTempDirectory(string scenarioName)
        {
            string tempPath = Path.Combine(
                Path.GetTempPath(),
                "OpenNFS.ReleaseReadiness",
                scenarioName + "." + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempPath);
            return tempPath;
        }

        private static async Task<PowerShellCommandResult> RunScriptAsync(
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

        private static void TryDeleteDirectory(string directoryPath)
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
