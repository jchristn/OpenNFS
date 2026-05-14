namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client;
    using OpenNFS.Protocol.V40.Hosting;
    using OpenNFS.Server;
    using OpenNFS.Server.FileHandles;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;
    using static Test.Shared.InteropSuiteSupport;
    /// <summary>
    /// Shared execution helpers for OpenNFS host NFSv4.0 interop flows.
    /// </summary>
    internal static class InteropOpenNfsHostV40Support
    {
        internal static async Task ExecuteClientAgainstOpenNfsServerOverNfs40Async(CancellationToken cancellationToken)
        {
            OpenNfsServer server = await CreateOpenNfsV40InteropServerAsync(
                cancellationToken,
                includeAcls: true,
                includeDelegations: true).ConfigureAwait(false);

            await using OpenNfsTcpNfs40ServerHost host = OpenNfsTcpNfs40ServerHost.Start(
                server,
                listenerAddress: "0.0.0.0",
                nfsPort: 0);
            await using OpenNfsClient client = new OpenNfsClientBuilder()
                .WithServer("127.0.0.1", host.NfsPort)
                .Build();
            await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

            OpenNfsV40LookupResult rootResult = await client.Directories.GetRootV40Async(cancellationToken).ConfigureAwait(false);
            if (!rootResult.IsSuccess || rootResult.ObjectFileHandle.Length == 0)
            {
                throw new InvalidOperationException("Expected NFSv4.0 PUTROOTFH against the OpenNFS host to return a usable root filehandle.");
            }

            byte[] rootHandle = rootResult.ObjectFileHandle.ToArray();
            OpenNfsV40ReadDirectoryResult rootDirectoryResult = await client.Directories.ReadDirectoryV40Async(
                rootHandle,
                0UL,
                new byte[8],
                4096U,
                cancellationToken).ConfigureAwait(false);
            string[] rootNames = rootDirectoryResult.Entries
                .Select(static entry => entry.Name)
                .OrderBy(static name => name, StringComparer.Ordinal)
                .ToArray();
            if (!rootDirectoryResult.IsSuccess || !rootNames.SequenceEqual(new[] { "docs", "hello.txt" }))
            {
                throw new InvalidOperationException(
                    "Expected the OpenNFS NFSv4.0 root listing to contain exactly 'docs' and 'hello.txt', but found: " + string.Join(", ", rootNames) + ".");
            }

            OpenNfsV40LookupResult docsLookup = await client.Directories.LookupV40Async(rootHandle, "docs", cancellationToken).ConfigureAwait(false);
            if (!docsLookup.IsSuccess || docsLookup.ObjectFileHandle.Length == 0)
            {
                throw new InvalidOperationException("Expected NFSv4.0 LOOKUP for 'docs' to succeed against the OpenNFS host.");
            }

            byte[] docsHandle = docsLookup.ObjectFileHandle.ToArray();
              OpenNfsV40LookupResult noteLookup = await client.Directories.LookupV40Async(docsHandle, "notes.txt", cancellationToken).ConfigureAwait(false);
              if (!noteLookup.IsSuccess || noteLookup.ObjectFileHandle.Length == 0)
              {
                  throw new InvalidOperationException("Expected NFSv4.0 LOOKUP for 'notes.txt' to succeed against the OpenNFS host.");
              }

              byte[] noteHandle = noteLookup.ObjectFileHandle.ToArray();
            OpenNfsV40SecurityInfoResult securityInfoResult = await client.Directories.GetSecurityInfoV40Async(
                docsHandle,
                "notes.txt",
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40GetAttributesResult identityAttributesResult = await client.Files.GetAttributesV40Async(
                noteHandle,
                new[]
                {
                    OpenNfsV40AttributeKind.Type,
                    OpenNfsV40AttributeKind.Owner,
                    OpenNfsV40AttributeKind.OwnerGroup,
                },
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40GetAclResult initialAclResult = await client.Files.GetAclV40Async(
                noteHandle,
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40SetAclResult setAclResult = await client.Files.SetAclV40Async(
                noteHandle,
                new[]
                {
                    new OpenNfsV40AclEntry(
                        OpenNfsV40AclEntryType.Allow,
                        OpenNfsV40AclEntryFlags.None,
                        OpenNfsV40AclPermissionMask.ReadData
                            | OpenNfsV40AclPermissionMask.WriteData
                            | OpenNfsV40AclPermissionMask.ReadAcl,
                        "interop-user@example.test"),
                    new OpenNfsV40AclEntry(
                        OpenNfsV40AclEntryType.Deny,
                        OpenNfsV40AclEntryFlags.None,
                        OpenNfsV40AclPermissionMask.Delete,
                        "EVERYONE@"),
                },
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40SetIdentityResult setIdentityResult = await client.Identity.SetOwnerAndGroupV40Async(
                noteHandle,
                "interop-updated-owner@example.test",
                "interop-updated-group@example.test",
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40GetAclResult updatedAclResult = await client.Files.GetAclV40Async(
                noteHandle,
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40GetIdentityResult updatedIdentityResult = await client.Identity.GetOwnerAndGroupV40Async(
                noteHandle,
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40ReadResult noteRead = await client.Files.ReadV40Async(noteHandle, 0UL, 64U, cancellationToken).ConfigureAwait(false);
            if (!noteRead.IsSuccess || !string.Equals(Encoding.UTF8.GetString(noteRead.Data.Span), "hello-v40", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected the OpenNFS NFSv4.0 read path to return the seeded note payload.");
            }

            if (!securityInfoResult.IsSuccess
                || securityInfoResult.SecurityFlavors.Count != 2
                || securityInfoResult.SecurityFlavors[0].Flavor != OpenNfsRpcAuthenticationFlavor.AuthNone
                || securityInfoResult.SecurityFlavors[1].Flavor != OpenNfsRpcAuthenticationFlavor.AuthSys
                || !identityAttributesResult.IsSuccess
                || !string.Equals(identityAttributesResult.Attributes?.Owner, "interop-owner@example.test", StringComparison.Ordinal)
                || !string.Equals(identityAttributesResult.Attributes?.OwnerGroup, "interop-group@example.test", StringComparison.Ordinal)
                || !initialAclResult.IsSuccess
                || initialAclResult.SupportedAcls != (OpenNfsV40AclSupport.AllowAcl | OpenNfsV40AclSupport.DenyAcl)
                || initialAclResult.Entries.Count != 1
                || !string.Equals(initialAclResult.Entries[0].Who, "EVERYONE@", StringComparison.Ordinal)
                || !setAclResult.IsSuccess
                || !setIdentityResult.IsSuccess
                || setIdentityResult.Identity is null
                || !string.Equals(setIdentityResult.Identity.ServerOwner, "interop-updated-owner@example.test", StringComparison.Ordinal)
                || !string.Equals(setIdentityResult.Identity.ServerOwnerGroup, "interop-updated-group@example.test", StringComparison.Ordinal)
                || !updatedAclResult.IsSuccess
                || updatedAclResult.Entries.Count != 2
                || !string.Equals(updatedAclResult.Entries[0].Who, "interop-user@example.test", StringComparison.Ordinal)
                || updatedAclResult.Entries[1].EntryType != OpenNfsV40AclEntryType.Deny
                || !updatedIdentityResult.IsSuccess
                || updatedIdentityResult.Identity is null
                || !string.Equals(updatedIdentityResult.Identity.ServerOwner, "interop-updated-owner@example.test", StringComparison.Ordinal)
                || !string.Equals(updatedIdentityResult.Identity.ServerOwnerGroup, "interop-updated-group@example.test", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected the OpenNFS NFSv4.0 peer path to surface SECINFO, identity mapping updates, and host-backed ACL round-trips.");
            }

            OpenNfsV40CreateResult createDirectoryResult = await client.Directories.CreateDirectoryV40Async(
                rootHandle,
                "scratch",
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40DirectoryMutationResult removeDirectoryResult = await client.Directories.RemoveEntryV40Async(
                rootHandle,
                "scratch",
                cancellationToken).ConfigureAwait(false);
            if (!createDirectoryResult.IsSuccess || !removeDirectoryResult.IsSuccess)
            {
                throw new InvalidOperationException("Expected NFSv4.0 CREATE and REMOVE to manage a temporary directory on the OpenNFS host.");
            }

            byte[] clientVerifier = new byte[] { 0x56, 0x34, 0x12, 0x90, 0x78, 0x56, 0x34, 0x12 };
            OpenNfsV40SetClientIdResult setClientIdResult = await client.Sessions.SetClientIdV40Async(
                "interop-v40-client",
                clientVerifier,
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40SessionResult confirmClientIdResult = await client.Sessions.ConfirmClientIdV40Async(
                setClientIdResult.ClientId,
                setClientIdResult.ConfirmationVerifier.ToArray(),
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40OpenResult openResult = await client.Files.OpenExistingV40Async(
                docsHandle,
                setClientIdResult.ClientId,
                "interop-v40-owner",
                "notes.txt",
                OpenNfsV40ShareAccess.Both,
                OpenNfsV40ShareDeny.None,
                1U,
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40OpenResult delegatedOpenResult = await client.Files.OpenExistingV40Async(
                docsHandle,
                setClientIdResult.ClientId,
                "interop-v40-owner-delegation",
                "notes.txt",
                OpenNfsV40ShareAccess.Read,
                OpenNfsV40ShareDeny.None,
                1U,
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40DelegationReturnResult returnDelegationResult = await client.Files.ReturnDelegationV40Async(
                noteHandle,
                delegatedOpenResult.Delegation!.StateId,
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40StateIdResult openConfirmResult = await client.Files.ConfirmOpenV40Async(
                openResult.StateId!,
                2U,
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40LockResult lockResult = await client.Locks.LockFromOpenV40Async(
                noteHandle,
                openConfirmResult.StateId!,
                3U,
                setClientIdResult.ClientId,
                "interop-v40-lock-owner",
                1U,
                OpenNfsV40LockType.Write,
                0UL,
                5UL,
                reclaim: false,
                cancellationToken).ConfigureAwait(false);
            byte[] updatedBytes = Encoding.UTF8.GetBytes("updated-v40-from-client");
            OpenNfsV40WriteResult writeResult = await client.Files.WriteV40Async(
                noteHandle,
                lockResult.StateId!,
                0UL,
                OpenNfsWriteStability.DataSync,
                updatedBytes,
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40CommitResult commitResult = await client.Files.CommitV40Async(
                noteHandle,
                0UL,
                (uint)updatedBytes.Length,
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40ReadResult rereadResult = await client.Files.ReadV40Async(
                noteHandle,
                0UL,
                64U,
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40LockResult unlockResult = await client.Locks.UnlockV40Async(
                noteHandle,
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
            NfsReadFileResponse hostRead = await server.Settings.FileSystem.ReadFileAsync(
                new NfsReadFileRequest(
                    @"C:\exports\docs\notes.txt",
                    0UL,
                    64U,
                    cancellationToken)).ConfigureAwait(false);

            if (!setClientIdResult.IsSuccess
                || !confirmClientIdResult.IsSuccess
                || !openResult.IsSuccess
                || openResult.StateId is null
                || !delegatedOpenResult.IsSuccess
                || delegatedOpenResult.Delegation is null
                || delegatedOpenResult.Delegation.DelegationType != OpenNfsV40DelegationType.Read
                || !returnDelegationResult.IsSuccess
                || !openConfirmResult.IsSuccess
                || !lockResult.IsSuccess
                || lockResult.StateId is null
                || !writeResult.IsSuccess
                || writeResult.Count != (uint)updatedBytes.Length
                || !commitResult.IsSuccess
                || !commitResult.Verifier.Span.SequenceEqual(writeResult.Verifier.Span)
                || !rereadResult.IsSuccess
                || !string.Equals(Encoding.UTF8.GetString(rereadResult.Data.Span), "updated-v40-from-client", StringComparison.Ordinal)
                || !unlockResult.IsSuccess
                || !closeResult.IsSuccess
                || !hostRead.Found
                || !string.Equals(Encoding.UTF8.GetString(hostRead.Data.Span), "updated-v40-from-client", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected NFSv4.0 stateful open, write, commit, lock, and read-back flows to succeed against the OpenNFS host.");
            }
        }

        internal static async Task ExecuteNegativeClientAgainstOpenNfsServerOverNfs40Async(CancellationToken cancellationToken)
        {
            OpenNfsServer server = await CreateOpenNfsV40InteropServerAsync(
                cancellationToken,
                includeAcls: false,
                includeDelegations: false).ConfigureAwait(false);

            await using OpenNfsTcpNfs40ServerHost host = OpenNfsTcpNfs40ServerHost.Start(
                server,
                listenerAddress: "0.0.0.0",
                nfsPort: 0);
            await using OpenNfsClient client = new OpenNfsClientBuilder()
                .WithServer("127.0.0.1", host.NfsPort)
                .Build();
            await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

            OpenNfsV40LookupResult rootResult = await client.Directories.GetRootV40Async(cancellationToken).ConfigureAwait(false);
            if (!rootResult.IsSuccess || rootResult.ObjectFileHandle.Length == 0)
            {
                throw new InvalidOperationException("Expected the negative OpenNFS NFSv4.0 variant to obtain the root handle before issuing failure-path requests.");
            }

            byte[] rootHandle = rootResult.ObjectFileHandle.ToArray();
            OpenNfsV40LookupResult missingLookupResult = await client.Directories.LookupV40Async(
                rootHandle,
                "missing.txt",
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40ReadResult invalidReadResult = await client.Files.ReadV40Async(
                rootHandle,
                0UL,
                16U,
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40LookupResult docsLookupResult = await client.Directories.LookupV40Async(
                rootHandle,
                "docs",
                cancellationToken).ConfigureAwait(false);
              OpenNfsV40LookupResult noteLookupResult = await client.Directories.LookupV40Async(
                  docsLookupResult.ObjectFileHandle.ToArray(),
                  "notes.txt",
                  cancellationToken).ConfigureAwait(false);
            OpenNfsV40SecurityInfoResult missingSecurityInfoResult = await client.Directories.GetSecurityInfoV40Async(
                docsLookupResult.ObjectFileHandle.ToArray(),
                "missing.txt",
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40GetAclResult unsupportedAclReadResult = await client.Files.GetAclV40Async(
                noteLookupResult.ObjectFileHandle.ToArray(),
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40SetAclResult unsupportedAclWriteResult = await client.Files.SetAclV40Async(
                noteLookupResult.ObjectFileHandle.ToArray(),
                new[]
                {
                    new OpenNfsV40AclEntry(
                        OpenNfsV40AclEntryType.Allow,
                        OpenNfsV40AclEntryFlags.None,
                        OpenNfsV40AclPermissionMask.ReadData,
                        "EVERYONE@"),
                },
                cancellationToken).ConfigureAwait(false);

              byte[] clientVerifier = new byte[] { 0x10, 0x32, 0x54, 0x76, 0x98, 0xBA, 0xDC, 0xFE };
              OpenNfsV40SetClientIdResult setClientIdResult = await client.Sessions.SetClientIdV40Async(
                  "interop-v40-negative",
                  clientVerifier,
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40SessionResult confirmClientIdResult = await client.Sessions.ConfirmClientIdV40Async(
                setClientIdResult.ClientId,
                setClientIdResult.ConfirmationVerifier.ToArray(),
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40OpenResult missingOpenResult = await client.Files.OpenExistingV40Async(
                rootHandle,
                setClientIdResult.ClientId,
                "interop-v40-negative-owner",
                "missing.txt",
                OpenNfsV40ShareAccess.Read,
                OpenNfsV40ShareDeny.None,
                1U,
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40OpenResult nonDelegatedOpenResult = await client.Files.OpenExistingV40Async(
                docsLookupResult.ObjectFileHandle.ToArray(),
                setClientIdResult.ClientId,
                "interop-v40-negative-owner-open",
                "notes.txt",
                OpenNfsV40ShareAccess.Read,
                OpenNfsV40ShareDeny.None,
                1U,
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40DelegationReturnResult badDelegationReturnResult = await client.Files.ReturnDelegationV40Async(
                noteLookupResult.ObjectFileHandle.ToArray(),
                new OpenNfsV40StateId(1U, new byte[12]),
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40WriteResult badStateWriteResult = await client.Files.WriteV40Async(
                noteLookupResult.ObjectFileHandle.ToArray(),
                new OpenNfsV40StateId(1U, new byte[12]),
                0UL,
                OpenNfsWriteStability.FileSync,
                Encoding.UTF8.GetBytes("nope"),
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40CommitResult directoryCommitResult = await client.Files.CommitV40Async(
                rootHandle,
                0UL,
                1U,
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40StateIdResult badStateCloseResult = await client.Files.CloseV40Async(
                new OpenNfsV40StateId(1U, new byte[12]),
                1U,
                cancellationToken).ConfigureAwait(false);

              if (missingLookupResult.Status != OpenNfsV40Status.NoEnt
                  || invalidReadResult.Status != OpenNfsV40Status.IsDirectory
                  || !docsLookupResult.IsSuccess
                  || !noteLookupResult.IsSuccess
                  || missingSecurityInfoResult.Status != OpenNfsV40Status.NoEnt
                  || unsupportedAclReadResult.Status != OpenNfsV40Status.AttributeNotSupported
                  || unsupportedAclWriteResult.Status != OpenNfsV40Status.AttributeNotSupported
                  || !setClientIdResult.IsSuccess
                  || !confirmClientIdResult.IsSuccess
                  || missingOpenResult.Status != OpenNfsV40Status.NoEnt
                  || !nonDelegatedOpenResult.IsSuccess
                  || nonDelegatedOpenResult.Delegation is not null
                  || badDelegationReturnResult.Status != OpenNfsV40Status.BadStateId
                  || badStateWriteResult.Status != OpenNfsV40Status.BadStateId
                  || directoryCommitResult.Status != OpenNfsV40Status.IsDirectory
                  || badStateCloseResult.Status != OpenNfsV40Status.BadStateId)
              {
                  throw new InvalidOperationException("Expected negative NFSv4.0 peer validation against the OpenNFS host to preserve NOENT, SECINFO NOENT, ACL ATTRNOTSUPP, ISDIR, BAD_STATEID, and directory-commit behavior.");
              }
        }
    }
}

