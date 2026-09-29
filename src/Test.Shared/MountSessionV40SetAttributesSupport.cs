namespace Test.Shared
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client;
    using OpenNFS.Client.Compound;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Rpc.Xdr;
    using OpenNFS.Server;
    using OpenNFS.Server.FileHandles;
    using Test.Shared.Infrastructure;
    using static Test.Shared.MountSessionScenarioSupport;

    /// <summary>
    /// NFSv4.0 <c>SETATTR</c> cases for the size, mode, and time_modify_set attributes routed through <c>INfsAttributeMutation</c>.
    /// </summary>
    internal static class MountSessionV40SetAttributesSupport
    {
        internal static async Task ExecuteV40SetAttributesTruncatesThroughCapabilityAsync(CancellationToken cancellationToken)
        {
            string rootDirectory = Path.Combine(Path.GetTempPath(), "OpenNFS.V40SetAttr", Guid.NewGuid().ToString("N"));
            string sourceRoot = Path.Combine(rootDirectory, "export");
            Directory.CreateDirectory(sourceRoot);
            string filePath = Path.Combine(sourceRoot, "truncate-me.bin");
            await File.WriteAllBytesAsync(filePath, CreatePayload(1000, seed: 40), cancellationToken).ConfigureAwait(false);

            try
            {
                await using OpenNfsServerApplication application = new OpenNfsServerBuilder()
                    .UseLocalFileSystem()
                    .UseFileHandleProvider(new PersistentMappingHandleProvider(Path.Combine(rootDirectory, "handles.json")))
                    .AddExport("/export", sourceRoot)
                    .BuildApplication(new OpenNfsServerApplicationOptions
                    {
                        ListenerAddress = "127.0.0.1",
                        EnableNfsV3 = false,
                        EnableNfs40 = true,
                        Nfs40Port = 0,
                    });
                await application.StartAsync(cancellationToken).ConfigureAwait(false);

                await using OpenNfsClient client = new OpenNfsClientBuilder()
                    .WithServer("127.0.0.1", application.Nfs40Port)
                    .Build();
                await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

                OpenNfsV40LookupResult root = await client.Directories.GetRootV40Async(cancellationToken).ConfigureAwait(false);
                Require(root.IsSuccess, "Expected PUTROOTFH to succeed.");
                OpenNfsV40LookupResult export = await client.Directories.LookupV40Async(root.ObjectFileHandle.ToArray(), "export", cancellationToken).ConfigureAwait(false);
                if (!export.IsSuccess)
                {
                    export = root;
                }

                OpenNfsV40LookupResult file = await client.Directories.LookupV40Async(export.ObjectFileHandle.ToArray(), "truncate-me.bin", cancellationToken).ConfigureAwait(false);
                Require(file.IsSuccess, "Expected LOOKUP of the file to succeed.");

                XdrWriter values = new XdrWriter();
                new fattr4_size { Value = 7 }.WriteTo(values);
                new fattr4_time_modify_set
                {
                    Value = new settime4
                    {
                        set_it = time_how4.SET_TO_CLIENT_TIME4,
                        time = new nfstime4 { seconds = 981173106, nseconds = 0 },
                    },
                }.WriteTo(values);

                nfsstat4 status = await ExecuteSetAttrAsync(
                    client,
                    file.ObjectFileHandle.ToArray(),
                    new uint[] { 1U << (int)Nfs40Constants.FATTR4_SIZE, 1U << ((int)Nfs40Constants.FATTR4_TIME_MODIFY_SET - 32) },
                    values.ToArray(),
                    cancellationToken).ConfigureAwait(false);
                Require(status == nfsstat4.NFS4_OK, "Expected NFSv4.0 SETATTR size + time_modify_set to succeed but observed " + status + ".");
                Require(new FileInfo(filePath).Length == 7, "Expected NFSv4.0 SETATTR size to truncate the host file to 7 bytes.");
                Require(
                    File.GetLastWriteTimeUtc(filePath) == DateTimeOffset.FromUnixTimeSeconds(981173106).UtcDateTime,
                    "Expected NFSv4.0 SETATTR time_modify_set to update the host modification time.");

                XdrWriter timeOnlyValues = new XdrWriter();
                new fattr4_time_modify_set
                {
                    Value = new settime4
                    {
                        set_it = time_how4.SET_TO_CLIENT_TIME4,
                        time = new nfstime4 { seconds = 1000000000, nseconds = 5 },
                    },
                }.WriteTo(timeOnlyValues);
                nfsstat4 timeOnlyStatus = await ExecuteSetAttrAsync(
                    client,
                    file.ObjectFileHandle.ToArray(),
                    new uint[] { 0U, 1U << ((int)Nfs40Constants.FATTR4_TIME_MODIFY_SET - 32) },
                    timeOnlyValues.ToArray(),
                    cancellationToken).ConfigureAwait(false);
                Require(timeOnlyStatus == nfsstat4.NFS4_OK, "Expected a time_modify_set-only NFSv4.0 SETATTR to succeed but observed " + timeOnlyStatus + ".");
                Require(
                    File.GetLastWriteTimeUtc(filePath) == DateTimeOffset.FromUnixTimeSeconds(1000000000).UtcDateTime,
                    "Expected a time_modify_set-only SETATTR to update the host modification time.");

                XdrWriter directoryValues = new XdrWriter();
                new fattr4_size { Value = 0 }.WriteTo(directoryValues);
                nfsstat4 directoryStatus = await ExecuteSetAttrAsync(
                    client,
                    export.ObjectFileHandle.ToArray(),
                    new uint[] { 1U << (int)Nfs40Constants.FATTR4_SIZE },
                    directoryValues.ToArray(),
                    cancellationToken).ConfigureAwait(false);
                Require(directoryStatus == nfsstat4.NFS4ERR_ISDIR, "Expected NFSv4.0 SETATTR size on a directory to return NFS4ERR_ISDIR but observed " + directoryStatus + ".");
            }
            finally
            {
                EphemeralOpenNfsServer.DeleteDirectory(rootDirectory);
            }
        }

        private static async Task<nfsstat4> ExecuteSetAttrAsync(
            OpenNfsClient client,
            byte[] fileHandle,
            uint[] attributeMask,
            byte[] attributeValues,
            CancellationToken cancellationToken)
        {
            OpenNfsCompoundReply reply = await client.ExecuteCompoundAsync(
                new OpenNfsCompoundRequest(
                    OpenNfsProtocolVersion.Nfs40,
                    "setattr-mutation",
                    new OpenNfsCompoundOperation[]
                    {
                        new OpenNfsCompoundOperation(
                            (uint)nfs_opnum4.OP_PUTFH,
                            Encode(new PUTFH4args { @object = new nfs_fh4 { Value = fileHandle } }.WriteTo)),
                        new OpenNfsCompoundOperation(
                            (uint)nfs_opnum4.OP_SETATTR,
                            Encode(new SETATTR4args
                            {
                                stateid = new stateid4 { seqid = 0U, other = new byte[12] },
                                obj_attributes = new fattr4
                                {
                                    attrmask = new bitmap4 { Value = attributeMask },
                                    attr_vals = new attrlist4 { Value = attributeValues },
                                },
                            }.WriteTo)),
                    }),
                OpenNfsOperationIdempotency.NonIdempotent,
                cancellationToken).ConfigureAwait(false);

            XdrReader reader = new XdrReader(reply.ReadAcceptedSuccessProcedurePayload());
            COMPOUND4res result = COMPOUND4res.ReadFrom(reader);
            if (result.resarray is null || result.resarray.Length < 2)
            {
                return result.status ?? nfsstat4.NFS4ERR_SERVERFAULT;
            }

            return result.resarray[1].opsetattr?.status ?? nfsstat4.NFS4ERR_SERVERFAULT;
        }

        private static byte[] Encode(Action<XdrWriter> write)
        {
            XdrWriter writer = new XdrWriter();
            write(writer);
            return writer.ToArray();
        }
    }
}
