namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using OpenNFS.Client;
    using OpenNFS.Client.Compound;
    using OpenNFS.Server;
    using OpenNFS.Server.Requests;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;
    using static Test.Shared.ClientV40SuiteSupport;

    internal static class ClientV40SecurityAndAclCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                new TestCaseDescriptor(
                    suiteId: "ClientV40Suites",
                    caseId: "GroupedSecurityAndIdentityApisPositive",
                    displayName: "Grouped NFSv4.0 SECINFO and identity-attribute APIs decode successful security discovery and owner mapping",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: async cancellationToken =>
                    {
                        OpenNfsServer server = CreateServer(
                            new TestNfsIdMapper(
                                owner: "owner@example.test",
                                ownerGroup: "group@example.test"));
                        NfsFileHandle docsHandle = await server.CreateFileHandleAsync(
                            new NfsFileHandleTarget("/", @"C:\exports\docs"),
                            cancellationToken).ConfigureAwait(false);
                        NfsFileHandle noteHandle = await server.CreateFileHandleAsync(
                            new NfsFileHandleTarget("/", @"C:\exports\docs\notes.txt"),
                            cancellationToken).ConfigureAwait(false);

                        await RunAgainstLoopbackServiceAsync(
                            server,
                            expectedCallCount: 2,
                            async client =>
                            {
                                OpenNfsCompoundPlan securityPlan = await client.Directories.PrepareGetSecurityInfoV40Async(
                                    docsHandle.ToArray(),
                                    "notes.txt",
                                    cancellationToken).ConfigureAwait(false);
                                OpenNfsCompoundPlan attributesPlan = await client.Files.PrepareGetAttributesV40Async(
                                    noteHandle.ToArray(),
                                    new[]
                                    {
                                        OpenNfsV40AttributeKind.Type,
                                        OpenNfsV40AttributeKind.Owner,
                                        OpenNfsV40AttributeKind.OwnerGroup,
                                    },
                                    cancellationToken).ConfigureAwait(false);

                                if (securityPlan.ProtocolVersion != OpenNfsProtocolVersion.Nfs40
                                    || securityPlan.Operations.Count != 2
                                    || attributesPlan.ProtocolVersion != OpenNfsProtocolVersion.Nfs40
                                    || attributesPlan.Operations.Count != 2)
                                {
                                    throw new InvalidOperationException("Expected grouped NFSv4.0 SECINFO and selected-GETATTR planning to emit the expected COMPOUND shape.");
                                }

                                OpenNfsV40SecurityInfoResult securityInfoResult =
                                    await client.Directories.GetSecurityInfoV40Async(
                                        docsHandle.ToArray(),
                                        "notes.txt",
                                        cancellationToken).ConfigureAwait(false);
                                OpenNfsV40GetAttributesResult attributesResult =
                                    await client.Files.GetAttributesV40Async(
                                        noteHandle.ToArray(),
                                        new[]
                                        {
                                            OpenNfsV40AttributeKind.Type,
                                            OpenNfsV40AttributeKind.Owner,
                                            OpenNfsV40AttributeKind.OwnerGroup,
                                        },
                                        cancellationToken).ConfigureAwait(false);

                                if (!securityInfoResult.IsSuccess
                                    || securityInfoResult.SecurityFlavors.Count != 2
                                    || securityInfoResult.SecurityFlavors[0].Flavor != OpenNfsRpcAuthenticationFlavor.AuthNone
                                    || securityInfoResult.SecurityFlavors[1].Flavor != OpenNfsRpcAuthenticationFlavor.AuthSys
                                    || !attributesResult.IsSuccess
                                    || attributesResult.Attributes?.FileType != OpenNfsV40FileType.RegularFile
                                    || !string.Equals(attributesResult.Attributes.Owner, "owner@example.test", StringComparison.Ordinal)
                                    || !string.Equals(attributesResult.Attributes.OwnerGroup, "group@example.test", StringComparison.Ordinal))
                                {
                                    throw new InvalidOperationException("Expected grouped NFSv4.0 SECINFO and identity attributes to surface AUTH_NONE, AUTH_SYS, owner, and owner-group data.");
                                }
                            },
                            cancellationToken).ConfigureAwait(false);
                    }),

                new TestCaseDescriptor(
                    suiteId: "ClientV40Suites",
                    caseId: "GroupedSecurityAndIdentityApisNegative",
                    displayName: "Grouped NFSv4.0 SECINFO and identity-attribute APIs surface negative discovery and unsupported-attribute results cleanly",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: async cancellationToken =>
                    {
                        OpenNfsServer server = CreateServer();
                        NfsFileHandle docsHandle = await server.CreateFileHandleAsync(
                            new NfsFileHandleTarget("/", @"C:\exports\docs"),
                            cancellationToken).ConfigureAwait(false);
                        NfsFileHandle noteHandle = await server.CreateFileHandleAsync(
                            new NfsFileHandleTarget("/", @"C:\exports\docs\notes.txt"),
                            cancellationToken).ConfigureAwait(false);

                        await RunAgainstLoopbackServiceAsync(
                            server,
                            expectedCallCount: 2,
                            async client =>
                            {
                                OpenNfsV40SecurityInfoResult missingEntrySecurityInfo =
                                    await client.Directories.GetSecurityInfoV40Async(
                                        docsHandle.ToArray(),
                                        "missing.txt",
                                        cancellationToken).ConfigureAwait(false);
                                OpenNfsV40GetAttributesResult unsupportedAttributes =
                                    await client.Files.GetAttributesV40Async(
                                        noteHandle.ToArray(),
                                        new[]
                                        {
                                            OpenNfsV40AttributeKind.Owner,
                                            OpenNfsV40AttributeKind.OwnerGroup,
                                        },
                                        cancellationToken).ConfigureAwait(false);

                                if (missingEntrySecurityInfo.Status != OpenNfsV40Status.NoEnt
                                    || unsupportedAttributes.Status != OpenNfsV40Status.AttributeNotSupported)
                                {
                                    throw new InvalidOperationException("Expected grouped NFSv4.0 SECINFO and identity attribute requests to preserve NOENT and ATTRNOTSUPP failures.");
                                }
                            },
                            cancellationToken).ConfigureAwait(false);
                    }),

                new TestCaseDescriptor(
                    suiteId: "ClientV40Suites",
                    caseId: "GroupedAclApisPositive",
                    displayName: "Grouped NFSv4.0 ACL APIs round-trip ACL support and ACL replacement successfully",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: async cancellationToken =>
                    {
                        TestNfsAcls acls = new TestNfsAcls(
                            initialEntries: new Dictionary<string, IReadOnlyList<NfsAclEntry>>(StringComparer.OrdinalIgnoreCase)
                            {
                                [@"C:\exports\docs\notes.txt"] = new[]
                                {
                                    new NfsAclEntry(
                                        NfsAclEntryType.Allow,
                                        NfsAclEntryFlags.None,
                                        NfsAclPermissionMask.ReadData | NfsAclPermissionMask.ReadAcl,
                                        "EVERYONE@"),
                                },
                            });

                        OpenNfsServer server = CreateServer(acls: acls);
                        NfsFileHandle noteHandle = await server.CreateFileHandleAsync(
                            new NfsFileHandleTarget("/", @"C:\exports\docs\notes.txt"),
                            cancellationToken).ConfigureAwait(false);

                        await RunAgainstLoopbackServiceAsync(
                            server,
                            expectedCallCount: 3,
                            async client =>
                            {
                                OpenNfsV40GetAclResult initialAclResult = await client.Files.GetAclV40Async(
                                    noteHandle.ToArray(),
                                    cancellationToken).ConfigureAwait(false);

                                OpenNfsV40AclEntry[] updatedAcl = new[]
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
                                };

                                OpenNfsV40SetAclResult setAclResult = await client.Files.SetAclV40Async(
                                    noteHandle.ToArray(),
                                    updatedAcl,
                                    cancellationToken).ConfigureAwait(false);
                                OpenNfsV40GetAclResult rereadAclResult = await client.Files.GetAclV40Async(
                                    noteHandle.ToArray(),
                                    cancellationToken).ConfigureAwait(false);

                                if (!initialAclResult.IsSuccess
                                    || initialAclResult.SupportedAcls != (OpenNfsV40AclSupport.AllowAcl | OpenNfsV40AclSupport.DenyAcl)
                                    || initialAclResult.Entries.Count != 1
                                    || !string.Equals(initialAclResult.Entries[0].Who, "EVERYONE@", StringComparison.Ordinal)
                                    || !setAclResult.IsSuccess
                                    || setAclResult.SetAttributeMaskWords.Count < 1
                                    || (setAclResult.SetAttributeMaskWords[0] & (1U << (int)OpenNfsV40AttributeKind.Acl)) == 0U
                                    || !rereadAclResult.IsSuccess
                                    || rereadAclResult.Entries.Count != 2
                                    || !string.Equals(rereadAclResult.Entries[0].Who, "interop-user@example.test", StringComparison.Ordinal)
                                    || rereadAclResult.Entries[0].Permissions != (
                                        OpenNfsV40AclPermissionMask.ReadData
                                        | OpenNfsV40AclPermissionMask.WriteData
                                        | OpenNfsV40AclPermissionMask.ReadAcl)
                                    || rereadAclResult.Entries[1].EntryType != OpenNfsV40AclEntryType.Deny)
                                {
                                    throw new InvalidOperationException("Expected grouped NFSv4.0 ACL helpers to round-trip ACL support flags and replacement ACL entries.");
                                }
                            },
                            cancellationToken).ConfigureAwait(false);
                    }),

                new TestCaseDescriptor(
                    suiteId: "ClientV40Suites",
                    caseId: "GroupedAclApisNegative",
                    displayName: "Grouped NFSv4.0 ACL APIs surface unsupported-attribute results cleanly when ACL capability is absent",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: async cancellationToken =>
                    {
                        OpenNfsServer server = CreateServer();
                        NfsFileHandle noteHandle = await server.CreateFileHandleAsync(
                            new NfsFileHandleTarget("/", @"C:\exports\docs\notes.txt"),
                            cancellationToken).ConfigureAwait(false);

                        await RunAgainstLoopbackServiceAsync(
                            server,
                            expectedCallCount: 2,
                            async client =>
                            {
                                OpenNfsV40GetAclResult getAclResult = await client.Files.GetAclV40Async(
                                    noteHandle.ToArray(),
                                    cancellationToken).ConfigureAwait(false);
                                OpenNfsV40SetAclResult setAclResult = await client.Files.SetAclV40Async(
                                    noteHandle.ToArray(),
                                    new[]
                                    {
                                        new OpenNfsV40AclEntry(
                                            OpenNfsV40AclEntryType.Allow,
                                            OpenNfsV40AclEntryFlags.None,
                                            OpenNfsV40AclPermissionMask.ReadData,
                                            "EVERYONE@"),
                                    },
                                    cancellationToken).ConfigureAwait(false);

                                if (getAclResult.Status != OpenNfsV40Status.AttributeNotSupported
                                    || setAclResult.Status != OpenNfsV40Status.AttributeNotSupported)
                                {
                                    throw new InvalidOperationException("Expected grouped NFSv4.0 ACL helpers to preserve ATTRNOTSUPP when the host does not expose ACL capability.");
                                }
                            },
                            cancellationToken).ConfigureAwait(false);
                    }),
            };
        }
    }
}
