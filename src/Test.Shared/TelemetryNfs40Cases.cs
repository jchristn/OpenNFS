namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V40.Compound;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Server;
    using OpenNFS.Telemetry;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// Telemetry cases for NFSv4.0 COMPOUND operations and NFSv4.0 state (clients, gauges, lease expiry).
    /// </summary>
    internal static class TelemetryNfs40Cases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                new TestCaseDescriptor(
                    suiteId: "TelemetrySuites",
                    caseId: "Nfs40CompoundOperationsEmitPerOperationTelemetry",
                    displayName: "NFSv4.0 COMPOUND operations emit per-operation spans and durations with protocol status",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: async cancellationToken =>
                    {
                        using TelemetryCapture capture = new TelemetryCapture();
                        Nfs40CompoundService service = new Nfs40CompoundService(CreateServer());

                        COMPOUND4res success = await DispatchAsync(
                            service,
                            0x7E100001,
                            new[]
                            {
                                new nfs_argop4 { argop = nfs_opnum4.OP_PUTROOTFH },
                                new nfs_argop4 { argop = nfs_opnum4.OP_GETFH },
                            },
                            cancellationToken).ConfigureAwait(false);
                        if (success.status != nfsstat4.NFS4_OK)
                        {
                            throw new InvalidOperationException("Expected PUTROOTFH + GETFH to succeed, but observed " + success.status + ".");
                        }

                        COMPOUND4res failure = await DispatchAsync(
                            service,
                            0x7E100002,
                            new[] { new nfs_argop4 { argop = nfs_opnum4.OP_GETFH } },
                            cancellationToken).ConfigureAwait(false);
                        if (failure.status != nfsstat4.NFS4ERR_NOFILEHANDLE)
                        {
                            throw new InvalidOperationException("Expected GETFH without a current filehandle to fail with NFS4ERR_NOFILEHANDLE.");
                        }

                        string operationDuration = OpenNfsTelemetryNames.ServerCompoundOperationDuration;
                        capture.Require(operationDuration, "PUTROOTFH is measured per operation", OpenNfsTelemetryNames.AttributeNfsMinorVersion, "0", OpenNfsTelemetryNames.AttributeNfsOperation, "PUTROOTFH", OpenNfsTelemetryNames.AttributeOutcome, OpenNfsTelemetryNames.OutcomeSuccess, OpenNfsTelemetryNames.AttributeStatus, "NFS4_OK");
                        capture.Require(operationDuration, "GETFH is measured per operation", OpenNfsTelemetryNames.AttributeNfsOperation, "GETFH", OpenNfsTelemetryNames.AttributeOutcome, OpenNfsTelemetryNames.OutcomeSuccess);
                        capture.Require(operationDuration, "a failed operation carries its NFSv4 status", OpenNfsTelemetryNames.AttributeNfsOperation, "GETFH", OpenNfsTelemetryNames.AttributeOutcome, OpenNfsTelemetryNames.OutcomeNfsError, OpenNfsTelemetryNames.AttributeStatus, "NFS4ERR_NOFILEHANDLE");

                        Activity operationSpan = capture.RequireActivity("nfs4 PUTROOTFH", ActivityKind.Internal, "each COMPOUND operation gets a span");
                        if (!Equals(operationSpan.GetTagItem(OpenNfsTelemetryNames.AttributeNfsOperation), "PUTROOTFH")
                            || !Equals(operationSpan.GetTagItem(OpenNfsTelemetryNames.AttributeStatus), "NFS4_OK"))
                        {
                            throw new InvalidOperationException("Expected the COMPOUND operation span to carry the operation and status attributes.");
                        }
                    }),

                new TestCaseDescriptor(
                    suiteId: "TelemetrySuites",
                    caseId: "Nfs40StateGaugesAndLeaseExpiryAreObservable",
                    displayName: "NFSv4.0 client state is sampled by gauges and lease expiry is counted",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: async cancellationToken =>
                    {
                        using TelemetryCapture capture = new TelemetryCapture();
                        MutableClock clock = new MutableClock(new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero));
                        Nfs40CompoundService service = new Nfs40CompoundService(
                            CreateServer(),
                            leaseWindow: TimeSpan.FromMinutes(1),
                            gracePeriodDuration: TimeSpan.FromMinutes(1),
                            utcNow: clock.UtcNow);

                        await RegisterClientAsync(service, "telemetry-lease-a", 0x7E200001, cancellationToken).ConfigureAwait(false);
                        capture.RecordObservableInstruments();
                        if (!capture.Require(OpenNfsTelemetryNames.ServerNfs4Clients, "confirmed clients are sampled").Any(measurement => measurement.Value >= 1))
                        {
                            throw new InvalidOperationException("Expected the NFSv4.0 client gauge to include the confirmed client. Observed: " + capture.Describe(OpenNfsTelemetryNames.ServerNfs4Clients));
                        }

                        capture.Require(OpenNfsTelemetryNames.ServerNfs4Opens, "open state is sampled");
                        capture.Require(OpenNfsTelemetryNames.ServerNfs4Locks, "lock state is sampled");
                        capture.Require(OpenNfsTelemetryNames.ServerNfs4Delegations, "delegation state is sampled");
                        capture.Require(OpenNfsTelemetryNames.ServerNfs4GracePeriodActive, "grace periods are sampled");

                        clock.Advance(TimeSpan.FromMinutes(5));
                        await RegisterClientAsync(service, "telemetry-lease-b", 0x7E200003, cancellationToken).ConfigureAwait(false);

                        double expired = capture.Require(OpenNfsTelemetryNames.ServerNfs4LeaseExpirations, "the first client's lease lapsed").Sum(measurement => measurement.Value);
                        if (expired < 1)
                        {
                            throw new InvalidOperationException("Expected at least one NFSv4.0 lease expiration to be counted.");
                        }
                    }),
            };
        }

        private static OpenNfsServer CreateServer()
        {
            DictionaryNfsFileSystem fileSystem = new DictionaryNfsFileSystem(
                new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
                {
                    [@"C:\exports"] = NfsPathKind.Directory,
                    [@"C:\exports\notes.txt"] = NfsPathKind.File,
                },
                new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
                {
                    [@"C:\exports\notes.txt"] = Encoding.UTF8.GetBytes("telemetry-v4"),
                });

            return new OpenNfsServerBuilder()
                .UseFileSystem(fileSystem)
                .AddExport("/", @"C:\exports")
                .Build();
        }

        private static async Task<COMPOUND4res> DispatchAsync(
            Nfs40CompoundService service,
            uint xid,
            nfs_argop4[] operations,
            CancellationToken cancellationToken)
        {
            return NfsV40SuiteSupport.ReadCompoundReply(
                await service.DispatchAsync(
                    NfsV40SuiteSupport.CreateCompoundCall(xid, "telemetry", 0U, operations),
                    cancellationToken).ConfigureAwait(false));
        }

        private static async Task RegisterClientAsync(Nfs40CompoundService service, string clientName, uint xid, CancellationToken cancellationToken)
        {
            byte[] verifier = new byte[] { 7, 7, 7, 7, 7, 7, 7, 7 };
            COMPOUND4res setClientId = await DispatchAsync(
                service,
                xid,
                new[]
                {
                    new nfs_argop4
                    {
                        argop = nfs_opnum4.OP_SETCLIENTID,
                        opsetclientid = NfsV40SuiteSupport.CreateSetClientIdArguments(clientName, verifier),
                    },
                },
                cancellationToken).ConfigureAwait(false);
            SETCLIENTID4resok result = setClientId.resarray?[0].opsetclientid?.resok4
                ?? throw new InvalidOperationException("Expected SETCLIENTID to return a client id.");

            COMPOUND4res confirm = await DispatchAsync(
                service,
                xid + 1,
                new[]
                {
                    new nfs_argop4
                    {
                        argop = nfs_opnum4.OP_SETCLIENTID_CONFIRM,
                        opsetclientid_confirm = NfsV40SuiteSupport.CreateSetClientIdConfirmArguments(
                            result.clientid!.Value,
                            result.setclientid_confirm!.Value!),
                    },
                },
                cancellationToken).ConfigureAwait(false);
            if (confirm.status != nfsstat4.NFS4_OK)
            {
                throw new InvalidOperationException("Expected SETCLIENTID_CONFIRM to succeed, but observed " + confirm.status + ".");
            }
        }
    }
}
