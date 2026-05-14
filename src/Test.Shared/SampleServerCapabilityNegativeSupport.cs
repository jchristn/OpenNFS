namespace Test.Shared
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using OpenNFS.Client;
    using Test.Shared.Infrastructure;
    using static Test.Shared.SampleServerSuiteSupport;

    /// <summary>
    /// Negative lookup and conflicting-lock capability flows for the sample artifact.
    /// </summary>
    internal static class SampleServerCapabilityNegativeSupport
    {
        internal static async Task ExecuteCapabilitySurfaceReportsNegativeLockAndLookupPathsAsync(
            System.Threading.CancellationToken cancellationToken)
        {
            string rootDirectory = Path.Combine(Path.GetTempPath(), "OpenNFS.SampleCapabilitiesNegative", Guid.NewGuid().ToString("N"));
            string configDirectory = Path.Combine(rootDirectory, "config");
            string configPath = Path.Combine(configDirectory, "sample-config.json");

            try
            {
                Directory.CreateDirectory(configDirectory);
                await WriteSampleConfigurationAsync(
                    configPath,
                    new
                    {
                        serverName = "Capability Negative Sample",
                        exportPath = "/exports/sample",
                        sourcePath = "content/export",
                        owner = "sample-owner@example.test",
                        ownerGroup = "sample-group@example.test",
                        mappingPath = "state/handles.json",
                        listenerAddress = "0.0.0.0",
                        mountPort = 20048,
                        nfsPort = 2049,
                        nfs40Port = 3049,
                        denyMounts = false,
                    },
                    cancellationToken).ConfigureAwait(false);

                await using SampleOpenNfsServerProcess process =
                    await SampleOpenNfsServerProcess.StartAsync(configPath, cancellationToken).ConfigureAwait(false);
                await using OpenNfsClient client = new OpenNfsClientBuilder()
                    .WithServer("127.0.0.1", process.Nfs40Port)
                    .Build();
                await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

                SampleV40HandleResolution handles =
                    await ResolveSampleV40HandlesAsync(client, cancellationToken).ConfigureAwait(false);
                byte[] rootHandle = handles.RootHandle;
                byte[] docsHandle = handles.DocsHandle;
                byte[] nestedHandle = handles.NestedHandle;

                OpenNfsV40LookupResult missingLookupResult = await client.Directories.LookupV40Async(
                    rootHandle,
                    "missing.txt",
                    cancellationToken).ConfigureAwait(false);

                byte[] verifierA = new byte[] { 0x10, 0x32, 0x54, 0x76, 0x98, 0xBA, 0xDC, 0xFE };
                OpenNfsV40SetClientIdResult setClientIdResultA = await client.Sessions.SetClientIdV40Async(
                    "sample-capability-negative-a",
                    verifierA,
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40SessionResult confirmClientIdResultA = await client.Sessions.ConfirmClientIdV40Async(
                    setClientIdResultA.ClientId,
                    setClientIdResultA.ConfirmationVerifier.ToArray(),
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40OpenResult openResultA = await client.Files.OpenExistingV40Async(
                    docsHandle,
                    setClientIdResultA.ClientId,
                    "sample-capability-negative-owner-a",
                    "nested.txt",
                    OpenNfsV40ShareAccess.Both,
                    OpenNfsV40ShareDeny.None,
                    1U,
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40StateIdResult confirmOpenResultA = await client.Files.ConfirmOpenV40Async(
                    openResultA.StateId!,
                    2U,
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40LockResult initialLockResult = await client.Locks.LockFromOpenV40Async(
                    nestedHandle,
                    confirmOpenResultA.StateId!,
                    3U,
                    setClientIdResultA.ClientId,
                    "sample-capability-negative-lock-owner-a",
                    1U,
                    OpenNfsV40LockType.Write,
                    0UL,
                    8UL,
                    reclaim: false,
                    cancellationToken).ConfigureAwait(false);

                byte[] verifierB = new byte[] { 0xEF, 0xCD, 0xAB, 0x89, 0x67, 0x45, 0x23, 0x01 };
                OpenNfsV40SetClientIdResult setClientIdResultB = await client.Sessions.SetClientIdV40Async(
                    "sample-capability-negative-b",
                    verifierB,
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40SessionResult confirmClientIdResultB = await client.Sessions.ConfirmClientIdV40Async(
                    setClientIdResultB.ClientId,
                    setClientIdResultB.ConfirmationVerifier.ToArray(),
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40OpenResult openResultB = await client.Files.OpenExistingV40Async(
                    docsHandle,
                    setClientIdResultB.ClientId,
                    "sample-capability-negative-owner-b",
                    "nested.txt",
                    OpenNfsV40ShareAccess.Both,
                    OpenNfsV40ShareDeny.None,
                    1U,
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40StateIdResult confirmOpenResultB = await client.Files.ConfirmOpenV40Async(
                    openResultB.StateId!,
                    2U,
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40LockResult lockTestResult = await client.Locks.TestV40Async(
                    nestedHandle,
                    setClientIdResultB.ClientId,
                    "sample-capability-negative-test-owner",
                    OpenNfsV40LockType.Write,
                    0UL,
                    8UL,
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40LockResult lockDeniedResult = await client.Locks.LockFromOpenV40Async(
                    nestedHandle,
                    confirmOpenResultB.StateId!,
                    3U,
                    setClientIdResultB.ClientId,
                    "sample-capability-negative-lock-owner-b",
                    1U,
                    OpenNfsV40LockType.Write,
                    0UL,
                    8UL,
                    reclaim: false,
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40StateIdResult closeWhileLockedResult = await client.Files.CloseV40Async(
                    confirmOpenResultA.StateId!,
                    4U,
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40LockResult unlockResult = await client.Locks.UnlockV40Async(
                    nestedHandle,
                    initialLockResult.StateId!,
                    2U,
                    OpenNfsV40LockType.Write,
                    0UL,
                    8UL,
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40StateIdResult closeResultA = await client.Files.CloseV40Async(
                    confirmOpenResultA.StateId!,
                    4U,
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40StateIdResult closeResultB = await client.Files.CloseV40Async(
                    confirmOpenResultB.StateId!,
                    3U,
                    cancellationToken).ConfigureAwait(false);

                if (missingLookupResult.Status != OpenNfsV40Status.NoEnt
                    || !confirmClientIdResultA.IsSuccess
                    || !openResultA.IsSuccess
                    || !confirmOpenResultA.IsSuccess
                    || !initialLockResult.IsSuccess
                    || !confirmClientIdResultB.IsSuccess
                    || !openResultB.IsSuccess
                    || !confirmOpenResultB.IsSuccess
                    || lockTestResult.Status != OpenNfsV40Status.Denied
                    || lockTestResult.Conflict is null
                    || lockDeniedResult.Status != OpenNfsV40Status.Denied
                    || lockDeniedResult.Conflict is null
                    || closeWhileLockedResult.Status != OpenNfsV40Status.LocksHeld
                    || !unlockResult.IsSuccess
                    || !closeResultA.IsSuccess
                    || !closeResultB.IsSuccess)
                {
                    throw new InvalidOperationException(
                        "Expected the sample artifact to surface negative lookup, conflicting lock, and lock-held close paths over the public NFSv4.0 client path."
                        + " lookup=" + missingLookupResult.Status
                        + " confirmA=" + confirmClientIdResultA.Status
                        + " openA=" + openResultA.Status
                        + " confirmOpenA=" + confirmOpenResultA.Status
                        + " initialLock=" + initialLockResult.Status
                        + " confirmB=" + confirmClientIdResultB.Status
                        + " openB=" + openResultB.Status
                        + " confirmOpenB=" + confirmOpenResultB.Status
                        + " lockTest=" + lockTestResult.Status
                        + " lockDenied=" + lockDeniedResult.Status
                        + " closeWhileLocked=" + closeWhileLockedResult.Status
                        + " unlock=" + unlockResult.Status
                        + " closeA=" + closeResultA.Status
                        + " closeB=" + closeResultB.Status
                        + " conflictA=" + (lockTestResult.Conflict?.ClientId.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "<null>")
                        + " conflictB=" + (lockDeniedResult.Conflict?.ClientId.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "<null>"));
                }
            }
            finally
            {
                if (Directory.Exists(rootDirectory))
                {
                    Directory.Delete(rootDirectory, recursive: true);
                }
            }
        }
    }
}
