namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client;
    using OpenNFS.Client.Internal;
    using OpenNFS.Client.Raw;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Xdr;
    using OpenNFS.Server;
    using Test.Shared.Infrastructure;
    using static Test.Shared.MountSessionScenarioSupport;

    /// <summary>
    /// Unit-level cases for the v0.1.1 client and server additions.
    /// </summary>
    internal static class MountSessionUnitSupport
    {
        internal static Task ExecuteTimeConversionsRoundTripAsync(CancellationToken cancellationToken)
        {
            _ = cancellationToken;

            OpenNfsV3Time epoch = new OpenNfsV3Time(0, 0);
            Require(epoch.ToDateTimeUtc() == DateTime.UnixEpoch && epoch.ToDateTimeUtc().Kind == DateTimeKind.Utc, "Expected (0, 0) to convert to the Unix epoch in UTC.");

            OpenNfsV3Time value = new OpenNfsV3Time(981173106, 123456789);
            DateTime converted = value.ToDateTimeUtc();
            Require(
                converted == new DateTime(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc).AddTicks(1234567),
                "Expected nanoseconds to be truncated to 100-nanosecond ticks.");

            OpenNfsV3Time roundTrip = OpenNfsV3Time.FromDateTimeUtc(converted);
            Require(roundTrip.Seconds == 981173106 && roundTrip.Nanoseconds == 123456700, "Expected FromDateTimeUtc to preserve tick precision.");

            DateTime local = new DateTime(2020, 6, 1, 12, 0, 0, DateTimeKind.Local);
            Require(OpenNfsV3Time.FromDateTimeUtc(local).ToDateTimeUtc() == local.ToUniversalTime(), "Expected local times to be converted to UTC.");

            DateTime unspecified = new DateTime(2020, 6, 1, 12, 0, 0, DateTimeKind.Unspecified);
            Require(OpenNfsV3Time.FromDateTimeUtc(unspecified).ToDateTimeUtc() == DateTime.SpecifyKind(unspecified, DateTimeKind.Utc), "Expected unspecified times to be treated as UTC.");

            Require(new OpenNfsV3Time(uint.MaxValue, 999_999_999).ToDateTimeUtc().Year == 2106, "Expected the maximum NFSv3 time to convert without overflow.");
            Require(new OpenNfsV3Time(1, 2_000_000_000).ToDateTimeUtc() == DateTime.UnixEpoch.AddSeconds(1).AddTicks(9_999_999), "Expected out-of-range nanoseconds to be clamped.");

            RequireThrows<ArgumentOutOfRangeException>(() => OpenNfsV3Time.FromDateTimeUtc(new DateTime(1969, 12, 31, 23, 59, 59, DateTimeKind.Utc)));
            RequireThrows<ArgumentOutOfRangeException>(() => OpenNfsV3Time.FromDateTimeUtc(new DateTime(2107, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
            return Task.CompletedTask;
        }

        internal static Task ExecuteSetAttributesModelValidatesAsync(CancellationToken cancellationToken)
        {
            _ = cancellationToken;

            OpenNfsV3SetAttributes empty = new OpenNfsV3SetAttributes();
            Require(!empty.HasChanges, "Expected a default SETATTR model to request no changes.");
            Require(new OpenNfsV3SetAttributes(sizeBytes: 0).HasChanges, "Expected a size-only model to report changes.");
            Require(new OpenNfsV3SetAttributes(modifyTimeMode: OpenNfsV3TimeSetMode.SetToServerTime).HasChanges, "Expected a server-time model to report changes.");

            RequireThrows<ArgumentException>(() => _ = new OpenNfsV3SetAttributes(accessTimeMode: OpenNfsV3TimeSetMode.SetToClientTime));
            RequireThrows<ArgumentException>(() => _ = new OpenNfsV3SetAttributes(modifyTime: new OpenNfsV3Time(1, 0)));
            RequireThrows<ArgumentException>(() => _ = new OpenNfsV3SetAttributes(accessTimeMode: OpenNfsV3TimeSetMode.SetToServerTime, accessTime: new OpenNfsV3Time(1, 0)));
            RequireThrows<ArgumentOutOfRangeException>(() => _ = new OpenNfsV3SetAttributes(modifyTimeMode: (OpenNfsV3TimeSetMode)9));
            return Task.CompletedTask;
        }

        internal static async Task ExecuteSetAttributesPlanAndDecodeAsync(CancellationToken cancellationToken)
        {
            await using OpenNfsClient client = new OpenNfsClientBuilder().WithServer("127.0.0.1", 2049).Build();
            await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

            byte[] handle = new byte[] { 1, 2, 3, 4, 5 };
            OpenNfsV3ProcedurePlan plan = await client.Files.PrepareSetAttributesV3Async(
                handle,
                new OpenNfsV3SetAttributes(
                    mode: 420,
                    userId: 1000,
                    groupId: 1001,
                    sizeBytes: 12345,
                    accessTimeMode: OpenNfsV3TimeSetMode.SetToServerTime,
                    modifyTimeMode: OpenNfsV3TimeSetMode.SetToClientTime,
                    modifyTime: new OpenNfsV3Time(10, 20)),
                new OpenNfsV3Time(30, 40),
                cancellationToken).ConfigureAwait(false);

            Require(plan.ProgramNumber == 100003 && plan.VersionNumber == 3 && plan.ProcedureNumber == 2, "Expected the SETATTR plan to target NFSv3 procedure 2.");
            XdrReader reader = new XdrReader(plan.ProcedurePayload);
            SETATTR3args arguments = SETATTR3args.ReadFrom(reader);
            reader.EnsureFullyConsumed();
            sattr3 attributes = arguments.new_attributes!;
            Require(arguments.@object!.data!.AsSpan().SequenceEqual(handle), "Expected the SETATTR payload to carry the filehandle.");
            Require(attributes.mode!.set_it && attributes.mode.mode!.Value!.Value == 420, "Expected the mode to be encoded.");
            Require(attributes.uid!.set_it && attributes.uid.uid!.Value!.Value == 1000, "Expected the uid to be encoded.");
            Require(attributes.gid!.set_it && attributes.gid.gid!.Value!.Value == 1001, "Expected the gid to be encoded.");
            Require(attributes.size!.set_it && attributes.size.size!.Value!.Value == 12345, "Expected the size to be encoded.");
            Require(attributes.atime!.set_it == time_how.SET_TO_SERVER_TIME, "Expected the access time to use SET_TO_SERVER_TIME.");
            Require(
                attributes.mtime!.set_it == time_how.SET_TO_CLIENT_TIME && attributes.mtime.mtime_value!.seconds!.Value == 10 && attributes.mtime.mtime_value.nseconds!.Value == 20,
                "Expected the modification time to use SET_TO_CLIENT_TIME with the supplied value.");
            Require(
                arguments.guard!.check && arguments.guard.obj_ctime!.seconds!.Value == 30 && arguments.guard.obj_ctime.nseconds!.Value == 40,
                "Expected the ctime guard to be encoded.");

            OpenNfsV3ProcedurePlan unguarded = await client.Files.PrepareSetAttributesV3Async(handle, new OpenNfsV3SetAttributes(sizeBytes: 0), null, cancellationToken).ConfigureAwait(false);
            SETATTR3args unguardedArguments = SETATTR3args.ReadFrom(new XdrReader(unguarded.ProcedurePayload));
            Require(!unguardedArguments.guard!.check, "Expected a null guard to encode check = FALSE.");
            Require(!unguardedArguments.new_attributes!.mode!.set_it && unguardedArguments.new_attributes.atime!.set_it == time_how.DONT_CHANGE, "Expected unset members to encode as unchanged.");

            byte[] successReply = EncodeReply(new SETATTR3res
            {
                status = nfsstat3.NFS3_OK,
                resok = new SETATTR3resok
                {
                    obj_wcc = new wcc_data
                    {
                        before = new pre_op_attr { attributes_follow = false },
                        after = new post_op_attr { attributes_follow = false },
                    },
                },
            }.WriteTo);
            OpenNfsV3SetAttributesResult success = client.Files.ReadSetAttributesV3Result(successReply);
            Require(success.IsSuccess && success.Status == OpenNfsV3Status.Ok && success.Wcc is not null, "Expected a successful SETATTR reply to decode with wcc data.");

            byte[] failureReply = EncodeReply(new SETATTR3res
            {
                status = nfsstat3.NFS3ERR_NOT_SYNC,
                resfail = new SETATTR3resfail
                {
                    obj_wcc = new wcc_data
                    {
                        before = new pre_op_attr { attributes_follow = false },
                        after = new post_op_attr { attributes_follow = false },
                    },
                },
            }.WriteTo);
            OpenNfsV3SetAttributesResult failure = client.Files.ReadSetAttributesV3Result(failureReply);
            Require(!failure.IsSuccess && failure.Status == OpenNfsV3Status.NotSynchronized, "Expected NFS3ERR_NOT_SYNC to decode as NotSynchronized.");

        }

        internal static async Task ExecuteClientXidSeedsAreRandomizedAsync(CancellationToken cancellationToken)
        {
            FieldInfo field = typeof(OpenNfsClient).GetField("_NextXid", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Expected OpenNfsClient to keep its next xid in _NextXid.");

            List<int> seeds = new List<int>();
            for (int index = 0; index < 200; index++)
            {
                await using OpenNfsClient client = new OpenNfsClientBuilder().WithServer("127.0.0.1", 2049).Build();
                seeds.Add((int)field.GetValue(client)!);
            }

            int distinct = seeds.Distinct().Count();
            Require(
                distinct >= 199,
                "Expected clients created back-to-back to start from independent random xids (duplicate xid sequences let a server duplicate-request cache replay another client's reply), but only "
                + distinct + " of 200 seeds were distinct.");
            _ = cancellationToken;
        }

        internal static Task ExecuteTransferSizesAreSelectedFromFsInfoAsync(CancellationToken cancellationToken)
        {
            _ = cancellationToken;

            OpenNfsMountSessionTransferSizes preferred = OpenNfsMountSessionTransferSizes.FromFileSystemInfo(
                new OpenNfsV3FileSystemInfoResult(OpenNfsV3Status.Ok, readMaxBytes: 1048576, readPreferredBytes: 262144, writeMaxBytes: 1048576, writePreferredBytes: 131072, directoryPreferredBytes: 16384),
                datagramCapable: false);
            Require(preferred.ReadSize == 262144 && preferred.WriteSize == 131072 && preferred.DirectorySize == 16384, "Expected the preferred sizes to be used when within the maximums.");

            OpenNfsMountSessionTransferSizes bounded = OpenNfsMountSessionTransferSizes.FromFileSystemInfo(
                new OpenNfsV3FileSystemInfoResult(OpenNfsV3Status.Ok, readMaxBytes: 1024, readPreferredBytes: 65536, writeMaxBytes: 4096, writePreferredBytes: 8192),
                datagramCapable: false);
            Require(bounded.ReadSize == 1024 && bounded.WriteSize == 4096, "Expected preferred sizes above the maximum to be bounded by rtmax/wtmax.");

            OpenNfsMountSessionTransferSizes maximumOnly = OpenNfsMountSessionTransferSizes.FromFileSystemInfo(
                new OpenNfsV3FileSystemInfoResult(OpenNfsV3Status.Ok, readMaxBytes: 32768, writeMaxBytes: 16384),
                datagramCapable: false);
            Require(maximumOnly.ReadSize == 32768 && maximumOnly.WriteSize == 16384, "Expected the maximum to be used when no preferred size is advertised.");

            OpenNfsMountSessionTransferSizes zero = OpenNfsMountSessionTransferSizes.FromFileSystemInfo(
                new OpenNfsV3FileSystemInfoResult(OpenNfsV3Status.Ok),
                datagramCapable: false);
            Require(zero.ReadSize == 65536 && zero.WriteSize == 65536, "Expected a server that advertises nothing to fall back to 64 KiB.");

            OpenNfsMountSessionTransferSizes huge = OpenNfsMountSessionTransferSizes.FromFileSystemInfo(
                new OpenNfsV3FileSystemInfoResult(OpenNfsV3Status.Ok, readMaxBytes: uint.MaxValue, readPreferredBytes: uint.MaxValue, writeMaxBytes: uint.MaxValue, writePreferredBytes: uint.MaxValue),
                datagramCapable: false);
            Require(huge.ReadSize == 4 * 1024 * 1024 && huge.WriteSize == 4 * 1024 * 1024, "Expected absurd sizes to be capped at 4 MiB.");

            OpenNfsMountSessionTransferSizes datagram = OpenNfsMountSessionTransferSizes.FromFileSystemInfo(
                new OpenNfsV3FileSystemInfoResult(OpenNfsV3Status.Ok, readMaxBytes: 1048576, readPreferredBytes: 1048576, writeMaxBytes: 1048576, writePreferredBytes: 1048576),
                datagramCapable: true);
            Require(datagram.ReadSize == 32768 && datagram.WriteSize == 32768, "Expected UDP-capable transport policies to cap transfers at 32 KiB.");

            OpenNfsMountSessionTransferSizes fallback = OpenNfsMountSessionTransferSizes.CreateDefault(datagramCapable: false);
            Require(fallback.ReadSize == 65536 && fallback.WriteSize == 65536 && !fallback.FromServer, "Expected the default transfer size to be 64 KiB.");
            return Task.CompletedTask;
        }

        internal static Task ExecuteAlignedReadCountAsync(CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            const int Chunk = 1048576;
            Require(OpenNfsMountSessionReadSupport.AlignedReadCount(0, 5_000_000, Chunk) == Chunk, "Expected aligned offsets to use the full chunk.");
            Require(OpenNfsMountSessionReadSupport.AlignedReadCount((2 * 1048576) + 5, 1048574, Chunk) == Chunk - 5, "Expected the knfsd-failing READ(2 MiB + 5, 1 MiB - 2) to be shortened to end on a page boundary.");
            Require(OpenNfsMountSessionReadSupport.AlignedReadCount(4096 + 100, 10, Chunk) == 10, "Expected short unaligned reads to be left unchanged.");
            Require(OpenNfsMountSessionReadSupport.AlignedReadCount(5, 100_000, 1000) == 1000, "Expected sub-page chunk sizes not to be realigned.");
            Require((4095 + OpenNfsMountSessionReadSupport.AlignedReadCount(4095, Chunk * 2, Chunk)) % 4096 == 0, "Expected an unaligned full-chunk read to end on a page boundary.");
            return Task.CompletedTask;
        }

        internal static Task ExecuteLocalFileSystemAdvertisesAttributeMutationAsync(CancellationToken cancellationToken)
        {
            _ = cancellationToken;

            OpenNfsServer localServer = new OpenNfsServerBuilder().UseLocalFileSystem().Build();
            Require(localServer.Capabilities.Supports(NfsCapabilityKind.AttributeMutation), "Expected UseLocalFileSystem to register the attribute-mutation capability.");
            Require(localServer.Capabilities.AdvertisedCapabilities.Contains(NfsCapabilityKind.AttributeMutation), "Expected the attribute-mutation capability to be advertised.");
            Require(ReferenceEquals(localServer.Capabilities.AttributeMutation, OpenNFS.Server.FileSystems.LocalNfsFileSystem.Default), "Expected the local file system itself to be discovered as the capability.");

            OpenNfsServerSettings directSettings = new OpenNfsServerSettings(OpenNFS.Server.FileSystems.LocalNfsFileSystem.Default);
            Require(directSettings.Capabilities.AttributeMutation is not null, "Expected directly constructed settings to discover the capability from the file system.");

            DictionaryNfsFileSystem inMemory = new DictionaryNfsFileSystem(new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase));
            OpenNfsServer legacyServer = new OpenNfsServerBuilder().UseFileSystem(inMemory).Build();
            Require(!legacyServer.Capabilities.Supports(NfsCapabilityKind.AttributeMutation), "Expected hosts without the capability to keep it absent.");

            OpenNfsServer overridden = new OpenNfsServerBuilder()
                .UseFileSystem(inMemory)
                .UseAttributeMutation(OpenNFS.Server.FileSystems.LocalNfsFileSystem.Default)
                .Build();
            Require(overridden.Capabilities.Supports(NfsCapabilityKind.AttributeMutation), "Expected UseAttributeMutation to register the capability explicitly.");

            NfsPathInfo withMode = new NfsPathInfo("C:\\x", NfsPathKind.File, 1, null, null, null, 0x81A4);
            Require(withMode.Mode == 0x1A4, "Expected NfsPathInfo to keep only the low 12 mode bits.");
            Require(new NfsPathInfo("C:\\x", NfsPathKind.File).Mode is null, "Expected the legacy constructor to leave the mode unset.");
            return Task.CompletedTask;
        }

        internal static Task ExecuteGetPortCodecRoundTripsAsync(CancellationToken cancellationToken)
        {
            _ = cancellationToken;

            RpcMessageEnvelope call = OpenNfsPortmapperClient.CreateGetPortCall(0x01020304, 100005, 3, OpenNfsPortmapperClient.TcpProtocol);
            call_body body = call.Header.body!.cbody!;
            Require(call.Header.xid == 0x01020304, "Expected the GETPORT call to carry the supplied xid.");
            Require(body.prog == 100000 && body.vers == 2 && body.proc == 3, "Expected GETPORT to target program 100000 version 2 procedure 3.");
            Require(body.cred?.flavor == auth_flavor.AUTH_NONE, "Expected GETPORT to use AUTH_NONE.");

            byte[] expectedPayload = new byte[]
            {
                0x00, 0x01, 0x86, 0xA5,
                0x00, 0x00, 0x00, 0x03,
                0x00, 0x00, 0x00, 0x06,
                0x00, 0x00, 0x00, 0x00,
            };
            Require(call.ProcedurePayload.Span.SequenceEqual(expectedPayload), "Expected the GETPORT payload to be the XDR mapping {100005, 3, IPPROTO_TCP, 0}.");

            byte[] encodedCall = RpcMessageCodec.Encode(call);
            RpcMessageEnvelope decodedCall = RpcMessageCodec.Decode(encodedCall);
            Require(decodedCall.ProcedurePayload.Span.SequenceEqual(expectedPayload), "Expected the GETPORT call to survive an encode/decode round trip.");

            XdrWriter portWriter = new XdrWriter();
            portWriter.WriteUInt32(20048);
            RpcMessageEnvelope reply = RpcMessageFactory.CreateAcceptedReply(0x01020304, accept_stat.SUCCESS, RpcAuthenticationCodec.CreateNone(), portWriter.ToArray());
            Require(OpenNfsPortmapperClient.ReadGetPortReply(RpcMessageCodec.Decode(RpcMessageCodec.Encode(reply))) == 20048, "Expected the GETPORT reply to decode port 20048.");

            XdrWriter zeroWriter = new XdrWriter();
            zeroWriter.WriteUInt32(0);
            Require(
                OpenNfsPortmapperClient.ReadGetPortReply(RpcMessageFactory.CreateAcceptedReply(1, accept_stat.SUCCESS, RpcAuthenticationCodec.CreateNone(), zeroWriter.ToArray())) == 0,
                "Expected an unregistered program to decode as port 0.");
            return Task.CompletedTask;
        }

        internal static Task ExecutePortmapperSettingsDefaultAndValidateAsync(CancellationToken cancellationToken)
        {
            _ = cancellationToken;

            OpenNfsClientSettings defaults = new OpenNfsClientBuilder().WithServer("nfs.example").BuildSettings();
            Require(!defaults.EnablePortmapperDiscovery && defaults.PortmapperPort == 111 && !defaults.HasExplicitServerPort, "Expected portmapper discovery to be disabled by default with port 111 and an implicit NFS port.");

            OpenNfsClientSettings enabled = new OpenNfsClientBuilder()
                .WithServer("nfs.example")
                .WithPortmapperDiscovery()
                .WithPortmapperPort(1111)
                .BuildSettings();
            Require(enabled.EnablePortmapperDiscovery && enabled.PortmapperPort == 1111, "Expected WithPortmapperDiscovery and WithPortmapperPort to be captured.");

            Require(new OpenNfsClientBuilder().WithServer("nfs.example", 2049).BuildSettings().HasExplicitServerPort, "Expected WithServer(host, port) to mark the NFS port explicit.");
            Require(new OpenNfsClientBuilder().WithServerPort(3049).BuildSettings().HasExplicitServerPort, "Expected WithServerPort to mark the NFS port explicit.");
            Require(new OpenNfsClientBuilder().WithPrimaryEndpoint("nfs.example", 2049).BuildSettings().HasExplicitServerPort, "Expected WithPrimaryEndpoint to mark the NFS port explicit.");
            Require(new OpenNfsClientSettings("nfs.example").HasExplicitServerPort, "Expected directly constructed settings to treat the port as explicit.");
            Require(!new OpenNfsClientBuilder().WithPortmapperDiscovery().WithPortmapperDiscovery(false).BuildSettings().EnablePortmapperDiscovery, "Expected WithPortmapperDiscovery(false) to disable discovery.");

            OpenNfsClientSettings modeDefaults = new OpenNfsClientBuilder().BuildSettings();
            Require(modeDefaults.DefaultFileCreateMode == 420U && modeDefaults.DefaultDirectoryCreateMode == 493U, "Expected default create modes of 0644 and 0755.");
            OpenNfsClientSettings customModes = new OpenNfsClientBuilder().WithDefaultCreateModes(384U, 448U).BuildSettings();
            Require(customModes.DefaultFileCreateMode == 384U && customModes.DefaultDirectoryCreateMode == 448U, "Expected WithDefaultCreateModes to be captured.");
            RequireThrows<ArgumentOutOfRangeException>(() => new OpenNfsClientBuilder().WithDefaultCreateModes(4096U, 493U));
            RequireThrows<ArgumentOutOfRangeException>(() => new OpenNfsClientBuilder().WithPortmapperPort(0));
            RequireThrows<ArgumentOutOfRangeException>(() => new OpenNfsClientBuilder().WithPortmapperPort(65536));
            return Task.CompletedTask;
        }

        private static byte[] EncodeReply(Action<XdrWriter> writePayload)
        {
            XdrWriter writer = new XdrWriter();
            writePayload(writer);
            return RpcMessageCodec.Encode(RpcMessageFactory.CreateAcceptedReply(7, accept_stat.SUCCESS, RpcAuthenticationCodec.CreateNone(), writer.ToArray()));
        }

        private static void RequireThrows<TException>(Action action)
            where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException)
            {
                return;
            }

            throw new InvalidOperationException("Expected " + typeof(TException).Name + ".");
        }
    }
}
