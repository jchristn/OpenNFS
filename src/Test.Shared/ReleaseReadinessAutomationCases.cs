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
    using static Test.Shared.ReleaseReadinessSuiteSupport;

    /// <summary>
    /// Conformance harness, privileged wrapper, workflow, and synthetic automation suites.
    /// </summary>
    internal static class ReleaseReadinessAutomationCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            DockerInteropEnvironmentProbe probe = DockerInteropEnvironmentProbe.Current;

            return new TestCaseDescriptor[]
            {
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
                                || !selfHostedWorkflow.Contains("scripts/interop/pjdfstest/external", StringComparison.Ordinal)
                                || !selfHostedWorkflow.Contains("$env:CONNECTATHON_ROOT", StringComparison.Ordinal)
                                || !selfHostedWorkflow.Contains("scripts/interop/connectathon/external", StringComparison.Ordinal)
                                || !selfHostedWorkflow.Contains("$env:PYNFS_ROOT", StringComparison.Ordinal)
                                || !pynfsWorkflow.Contains("$env:PYNFS_ENTRYPOINT", StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException("Expected the workflow files to reference the new release-validation and conformance harness scripts.");
                            }
                        }),

            };
        }
    }
}
