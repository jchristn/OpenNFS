namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V40.Compound;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Server;
    using OpenNFS.Server.Requests;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// NFSv4.0 COMPOUND operation-binding cases.
    /// </summary>
    internal static class NfsV40CompoundOperationBindingCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new List<TestCaseDescriptor>
            {
                new TestCaseDescriptor(
                    suiteId: "NfsV40Suites",
                    caseId: "AllAdvertisedOpsBound",
                    displayName: "NFSv4.0 COMPOUND binds every legal v4.0 core operation explicitly",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: async cancellationToken =>
                    {
                        DictionaryNfsFileSystem fileSystem = new DictionaryNfsFileSystem(
                            new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
                            {
                                [@"C:\exports"] = NfsPathKind.Directory,
                            });

                        OpenNfsServer server = new OpenNfsServerBuilder()
                            .UseFileSystem(fileSystem)
                            .AddExport("/", @"C:\exports")
                            .Build();

                        Nfs40CompoundExecutor executor = new Nfs40CompoundExecutor(server);
                        foreach (nfs_opnum4 operationNumber in Enum.GetValues<nfs_opnum4>())
                        {
                            if (operationNumber == nfs_opnum4.OP_ILLEGAL)
                            {
                                continue;
                            }

                            COMPOUND4res result = await executor.ExecuteAsync(
                                new COMPOUND4args
                                {
                                    tag = new utf8str_cs
                                    {
                                        Value = new utf8string
                                        {
                                            Value = Encoding.ASCII.GetBytes(operationNumber.ToString()),
                                        },
                                    },
                                    minorversion = 0U,
                                    argarray = new[]
                                    {
                                        new nfs_argop4
                                        {
                                            argop = operationNumber,
                                        },
                                    },
                                },
                                cancellationToken).ConfigureAwait(false);

                            if (result.resarray is null
                                || result.resarray.Length != 1
                                || result.resarray[0].resop != operationNumber
                                || result.resarray[0].resop == nfs_opnum4.OP_ILLEGAL
                                || result.status == nfsstat4.NFS4ERR_OP_ILLEGAL)
                            {
                                throw new InvalidOperationException(
                                    "Expected legal NFSv4.0 operation "
                                    + operationNumber
                                    + " to bind to an explicit result arm instead of the generic OP_ILLEGAL path.");
                            }
                        }
                    }),
            };
        }
    }
}
