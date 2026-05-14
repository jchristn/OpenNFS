namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Threading.Tasks;
    using OpenNFS.Client;
    using OpenNFS.Server;
    using Test.Shared.Infrastructure;
    using static Test.Shared.SampleServerSuiteSupport;

    /// <summary>
    /// Positive capability, ACL, idmap, delegation, and restart-persistence flows for the sample artifact.
    /// </summary>
    internal static class SampleServerCapabilityRoundTripSupport
    {
        internal static async Task ExecuteCapabilitySurfaceRoundTripsAndPersistsAcrossRestartAsync(
            System.Threading.CancellationToken cancellationToken)
        {
            string rootDirectory = Path.Combine(Path.GetTempPath(), "OpenNFS.SampleCapabilities", Guid.NewGuid().ToString("N"));
            string configDirectory = Path.Combine(rootDirectory, "config");
            string configPath = Path.Combine(configDirectory, "sample-config.json");
            const string owner = "sample-owner@example.test";
            const string ownerGroup = "sample-group@example.test";

            OpenNfsV40AclEntry[] updatedEntries =
            {
                new OpenNfsV40AclEntry(
                    OpenNfsV40AclEntryType.Allow,
                    OpenNfsV40AclEntryFlags.None,
                    OpenNfsV40AclPermissionMask.ReadData
                        | OpenNfsV40AclPermissionMask.WriteData
                        | OpenNfsV40AclPermissionMask.ReadAcl,
                    "capability-user@example.test"),
                new OpenNfsV40AclEntry(
                    OpenNfsV40AclEntryType.Deny,
                    OpenNfsV40AclEntryFlags.None,
                    OpenNfsV40AclPermissionMask.Delete,
                    "EVERYONE@"),
            };

            try
            {
                Directory.CreateDirectory(configDirectory);
                await WriteSampleConfigurationAsync(
                    configPath,
                    new
                    {
                        serverName = "Capability Sample",
                        exportPath = "/exports/sample",
                        sourcePath = "content/export",
                        owner,
                        ownerGroup,
                        mappingPath = "state/handles.json",
                        listenerAddress = "0.0.0.0",
                        mountPort = 20048,
                        nfsPort = 2049,
                        nfs40Port = 3049,
                        denyMounts = false,
                    },
                    cancellationToken).ConfigureAwait(false);

                await using (SampleOpenNfsServerProcess process =
                    await SampleOpenNfsServerProcess.StartAsync(configPath, cancellationToken).ConfigureAwait(false))
                {
                    await using OpenNfsClient client = new OpenNfsClientBuilder()
                        .WithServer("127.0.0.1", process.Nfs40Port)
                        .Build();
                    await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

                    SampleV40HandleResolution handles =
                        await ResolveSampleV40HandlesAsync(client, cancellationToken).ConfigureAwait(false);
                    byte[] docsHandle = handles.DocsHandle;
                    byte[] nestedHandle = handles.NestedHandle;

                    OpenNfsV40GetAttributesResult identityAttributesResult = await client.Files.GetAttributesV40Async(
                        nestedHandle,
                        new[]
                        {
                            OpenNfsV40AttributeKind.Type,
                            OpenNfsV40AttributeKind.Owner,
                            OpenNfsV40AttributeKind.OwnerGroup,
                        },
                        cancellationToken).ConfigureAwait(false);
                    OpenNfsV40GetAclResult initialAclResult = await client.Files.GetAclV40Async(
                        nestedHandle,
                        cancellationToken).ConfigureAwait(false);
                    OpenNfsV40SetAclResult setAclResult = await client.Files.SetAclV40Async(
                        nestedHandle,
                        updatedEntries,
                        cancellationToken).ConfigureAwait(false);
                    OpenNfsV40SetIdentityResult setIdentityResult = await client.Identity.SetOwnerAndGroupV40Async(
                        nestedHandle,
                        "capability-owner@example.test",
                        "capability-group@example.test",
                        cancellationToken).ConfigureAwait(false);
                    OpenNfsV40GetAclResult rereadAclResult = await client.Files.GetAclV40Async(
                        nestedHandle,
                        cancellationToken).ConfigureAwait(false);

                    byte[] clientVerifier = new byte[] { 0x22, 0x44, 0x66, 0x88, 0xAA, 0xCC, 0xEE, 0x10 };
                    OpenNfsV40SetClientIdResult setClientIdResult = await client.Sessions.SetClientIdV40Async(
                        "sample-capability-client",
                        clientVerifier,
                        cancellationToken).ConfigureAwait(false);
                    OpenNfsV40SessionResult confirmClientIdResult = await client.Sessions.ConfirmClientIdV40Async(
                        setClientIdResult.ClientId,
                        setClientIdResult.ConfirmationVerifier.ToArray(),
                        cancellationToken).ConfigureAwait(false);
                    OpenNfsV40OpenResult delegatedOpenResult = await client.Files.OpenExistingV40Async(
                        docsHandle,
                        setClientIdResult.ClientId,
                        "sample-capability-read-owner",
                        "nested.txt",
                        OpenNfsV40ShareAccess.Read,
                        OpenNfsV40ShareDeny.None,
                        1U,
                        cancellationToken).ConfigureAwait(false);
                    OpenNfsV40DelegationReturnResult returnDelegationResult = await client.Files.ReturnDelegationV40Async(
                        nestedHandle,
                        delegatedOpenResult.Delegation!.StateId,
                        cancellationToken).ConfigureAwait(false);
                    OpenNfsV40OpenResult writeOpenResult = await client.Files.OpenExistingV40Async(
                        docsHandle,
                        setClientIdResult.ClientId,
                        "sample-capability-write-owner",
                        "nested.txt",
                        OpenNfsV40ShareAccess.Both,
                        OpenNfsV40ShareDeny.None,
                        1U,
                        cancellationToken).ConfigureAwait(false);
                    OpenNfsV40StateIdResult openConfirmResult = await client.Files.ConfirmOpenV40Async(
                        writeOpenResult.StateId!,
                        2U,
                        cancellationToken).ConfigureAwait(false);
                    OpenNfsV40LockResult lockResult = await client.Locks.LockFromOpenV40Async(
                        nestedHandle,
                        openConfirmResult.StateId!,
                        3U,
                        setClientIdResult.ClientId,
                        "sample-capability-lock-owner",
                        1U,
                        OpenNfsV40LockType.Write,
                        0UL,
                        5UL,
                        reclaim: false,
                        cancellationToken).ConfigureAwait(false);
                    OpenNfsV40LockResult unlockResult = await client.Locks.UnlockV40Async(
                        nestedHandle,
                        lockResult.StateId!,
                        2U,
                        OpenNfsV40LockType.Write,
                        0UL,
                        5UL,
                        cancellationToken).ConfigureAwait(false);
                    OpenNfsV40StateIdResult closeResult = await client.Files.CloseV40Async(
                        openConfirmResult.StateId!,
                        4U,
                        cancellationToken).ConfigureAwait(false);

                    if (!identityAttributesResult.IsSuccess
                        || !string.Equals(identityAttributesResult.Attributes?.Owner, owner, StringComparison.Ordinal)
                        || !string.Equals(identityAttributesResult.Attributes?.OwnerGroup, ownerGroup, StringComparison.Ordinal)
                        || !initialAclResult.IsSuccess
                        || initialAclResult.SupportedAcls != (OpenNfsV40AclSupport.AllowAcl | OpenNfsV40AclSupport.DenyAcl)
                        || initialAclResult.Entries.Count != 2
                        || !string.Equals(initialAclResult.Entries[0].Who, owner, StringComparison.Ordinal)
                        || !string.Equals(initialAclResult.Entries[1].Who, ownerGroup, StringComparison.Ordinal)
                        || !setAclResult.IsSuccess
                        || !setIdentityResult.IsSuccess
                        || setIdentityResult.Identity is null
                        || !string.Equals(setIdentityResult.Identity.ServerOwner, "capability-owner@example.test", StringComparison.Ordinal)
                        || !string.Equals(setIdentityResult.Identity.ServerOwnerGroup, "capability-group@example.test", StringComparison.Ordinal)
                        || !rereadAclResult.IsSuccess
                        || rereadAclResult.Entries.Count != 2
                        || !string.Equals(rereadAclResult.Entries[0].Who, "capability-user@example.test", StringComparison.Ordinal)
                        || rereadAclResult.Entries[1].EntryType != OpenNfsV40AclEntryType.Deny
                        || !setClientIdResult.IsSuccess
                        || !confirmClientIdResult.IsSuccess
                        || !delegatedOpenResult.IsSuccess
                        || delegatedOpenResult.Delegation is null
                        || delegatedOpenResult.Delegation.DelegationType != OpenNfsV40DelegationType.Read
                        || !returnDelegationResult.IsSuccess
                        || !writeOpenResult.IsSuccess
                        || writeOpenResult.StateId is null
                        || !openConfirmResult.IsSuccess
                        || openConfirmResult.StateId is null
                        || !lockResult.IsSuccess
                        || lockResult.StateId is null
                        || !unlockResult.IsSuccess
                        || !closeResult.IsSuccess)
                    {
                        throw new InvalidOperationException("Expected the sample artifact to surface idmap, ACL, delegation, and lock flows over the public NFSv4.0 client path.");
                    }
                }

                await using SampleOpenNfsServerProcess restartedProcess =
                    await SampleOpenNfsServerProcess.StartAsync(configPath, cancellationToken).ConfigureAwait(false);
                await using OpenNfsClient restartedClient = new OpenNfsClientBuilder()
                    .WithServer("127.0.0.1", restartedProcess.Nfs40Port)
                    .Build();
                await restartedClient.ConnectAsync(cancellationToken).ConfigureAwait(false);

                SampleV40HandleResolution restartedHandles =
                    await ResolveSampleV40HandlesAsync(restartedClient, cancellationToken).ConfigureAwait(false);
                byte[] restartedNestedHandle = restartedHandles.NestedHandle;
                OpenNfsV40GetAttributesResult restartedIdentityAttributesResult = await restartedClient.Files.GetAttributesV40Async(
                    restartedNestedHandle,
                    new[]
                    {
                        OpenNfsV40AttributeKind.Owner,
                        OpenNfsV40AttributeKind.OwnerGroup,
                    },
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40GetAclResult persistedAclResult = await restartedClient.Files.GetAclV40Async(
                    restartedNestedHandle,
                    cancellationToken).ConfigureAwait(false);

                if (!restartedIdentityAttributesResult.IsSuccess
                    || !string.Equals(restartedIdentityAttributesResult.Attributes?.Owner, "capability-owner@example.test", StringComparison.Ordinal)
                    || !string.Equals(restartedIdentityAttributesResult.Attributes?.OwnerGroup, "capability-group@example.test", StringComparison.Ordinal)
                    || !persistedAclResult.IsSuccess
                    || persistedAclResult.Entries.Count != 2
                    || !string.Equals(persistedAclResult.Entries[0].Who, "capability-user@example.test", StringComparison.Ordinal)
                    || persistedAclResult.Entries[1].EntryType != OpenNfsV40AclEntryType.Deny)
                {
                    throw new InvalidOperationException("Expected the sample artifact to preserve ACL state and updated owner/group mapping across restart.");
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
