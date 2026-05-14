namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Test.Shared.Infrastructure;

    /// <summary>
    /// Shared helpers for the release-checklist and conformance-harness suite catalog.
    /// </summary>
    internal static class ReleaseReadinessSuiteSupport
    {
        internal static Task ExecuteRepositoryHonestyValidatorNegativeAsync(CancellationToken cancellationToken)
        {
            return ReleaseReadinessHarnessValidationSupport.ExecuteRepositoryHonestyValidatorNegativeAsync(cancellationToken);
        }

        internal static Task ExecutePrivilegedInteropWrapperValidatesScenarioInventoryAsync(CancellationToken cancellationToken)
        {
            return ReleaseReadinessHarnessValidationSupport.ExecutePrivilegedInteropWrapperValidatesScenarioInventoryAsync(cancellationToken);
        }

        internal static Task ExecuteConformanceHarnessScriptsExecuteSyntheticSuitesAsync(CancellationToken cancellationToken)
        {
            return ReleaseReadinessHarnessValidationSupport.ExecuteConformanceHarnessScriptsExecuteSyntheticSuitesAsync(cancellationToken);
        }

        internal static object CreateSyntheticTouchstoneResult(string suiteId, string caseId)
        {
            return ReleaseReadinessHarnessValidationSupport.CreateSyntheticTouchstoneResult(suiteId, caseId);
        }

        internal static Task ExecutePackedServerPackageServesLinuxKernelClientAsync(CancellationToken cancellationToken)
        {
            return ReleaseReadinessPackagingSupport.ExecutePackedServerPackageServesLinuxKernelClientAsync(cancellationToken);
        }

        internal static Task ExecutePackedServerPackageDeniesLinuxKernelClientMountAsync(CancellationToken cancellationToken)
        {
            return ReleaseReadinessPackagingSupport.ExecutePackedServerPackageDeniesLinuxKernelClientMountAsync(cancellationToken);
        }

        internal static Task ExecutePackedClientPackageExecutesAgainstPeerMatrixAsync(CancellationToken cancellationToken)
        {
            return ReleaseReadinessPackagingSupport.ExecutePackedClientPackageExecutesAgainstPeerMatrixAsync(cancellationToken);
        }

        internal static Task ExecutePackedClientPackageSurfacesNegativeResultsAgainstPeerMatrixAsync(CancellationToken cancellationToken)
        {
            return ReleaseReadinessPackagingSupport.ExecutePackedClientPackageSurfacesNegativeResultsAgainstPeerMatrixAsync(cancellationToken);
        }

        internal static Task<DotnetCommandResult> RunExternalPackageConsumerProjectAsync(
            string packageProjectRelativePath,
            string packageId,
            string programSource,
            CancellationToken cancellationToken,
            TimeSpan timeout)
        {
            return ReleaseReadinessPackagingSupport.RunExternalPackageConsumerProjectAsync(
                packageProjectRelativePath,
                packageId,
                programSource,
                cancellationToken,
                timeout);
        }

        internal static string CreateLinuxMountReadWriteCommand(int mountPort, int nfsPort, string exportPath, string fileName)
        {
            return ReleaseReadinessPackagingSupport.CreateLinuxMountReadWriteCommand(mountPort, nfsPort, exportPath, fileName);
        }

        internal static string CreateLinuxDeniedMountCommand(int mountPort, int nfsPort, string exportPath)
        {
            return ReleaseReadinessPackagingSupport.CreateLinuxDeniedMountCommand(mountPort, nfsPort, exportPath);
        }

        internal static Task AssertPlanModeAsync(
            string relativeScriptPath,
            IReadOnlyList<string> arguments,
            string expectedToken,
            CancellationToken cancellationToken)
        {
            return ReleaseReadinessSharedSupport.AssertPlanModeAsync(relativeScriptPath, arguments, expectedToken, cancellationToken);
        }

        internal static bool ContainsEither(PowerShellCommandResult result, string first, string second)
        {
            return ReleaseReadinessSharedSupport.ContainsEither(result, first, second);
        }

        internal static string CreateTempDirectory(string scenarioName)
        {
            return ReleaseReadinessSharedSupport.CreateTempDirectory(scenarioName);
        }

        internal static Task<PowerShellCommandResult> RunScriptAsync(
            string relativeScriptPath,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken,
            TimeSpan? timeout = null)
        {
            return ReleaseReadinessSharedSupport.RunScriptAsync(relativeScriptPath, arguments, cancellationToken, timeout);
        }

        internal static void TryDeleteDirectory(string directoryPath)
        {
            ReleaseReadinessSharedSupport.TryDeleteDirectory(directoryPath);
        }
    }
}
