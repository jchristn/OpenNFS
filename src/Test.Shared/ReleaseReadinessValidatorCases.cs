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
    /// Checklist, skipped-test, honesty, and release-plan readiness suites.
    /// </summary>
    internal static class ReleaseReadinessValidatorCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
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

            };
        }
    }
}
