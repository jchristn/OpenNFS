namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Threading.Tasks;
    using OpenNFS.Client.Sessions;
    using OpenNFS.Protocol.V41.Generated;
    using OpenNFS.Server;
    using OpenNFS.Server.FileHandles;
    using OpenNFS.Telemetry;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// Telemetry cases for NFSv4.1 sessions over a real listener: session establishment, SEQUENCE slot results,
    /// per-operation COMPOUND telemetry for minor version 1, and the session gauge.
    /// </summary>
    internal static class TelemetryNfs41Cases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                new TestCaseDescriptor(
                    suiteId: "TelemetrySuites",
                    caseId: "Nfs41SessionsEmitSequenceAndOperationTelemetry",
                    displayName: "NFSv4.1 sessions emit SEQUENCE slot results, per-operation durations, and a session gauge",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: async cancellationToken =>
                    {
                        using TelemetryCapture capture = new TelemetryCapture();
                        string root = Path.Combine(Path.GetTempPath(), "OpenNFS.Telemetry41", Guid.NewGuid().ToString("N"));
                        string exportRoot = Path.Combine(root, "export");
                        Directory.CreateDirectory(exportRoot);
                        try
                        {
                            await using OpenNfsServerApplication application = new OpenNfsServerBuilder()
                                .UseLocalFileSystem()
                                .UseFileHandleProvider(new PersistentMappingHandleProvider(Path.Combine(root, "handles.json")))
                                .AddExport("/data", exportRoot)
                                .BuildApplication(
                                    new OpenNfsServerApplicationOptions
                                    {
                                        ListenerAddress = "127.0.0.1",
                                        EnableNfsV3 = false,
                                        EnableNfs41 = true,
                                        Nfs41Port = 0,
                                    });
                            await application.StartAsync(cancellationToken).ConfigureAwait(false);

                            OpenNfsV41ClientSessionOptions options = new OpenNfsV41ClientSessionOptions(
                                endpoint: new IPEndPoint(IPAddress.Loopback, application.Nfs41Port),
                                clientOwner: NfsV41SuiteSupport.BuildClientOwner(verifier: 0xA1, ownerSeed: 0x42));
                            options.RequestedSlots = 2;
                            options.CallTimeout = TimeSpan.FromSeconds(15);
                            await using OpenNfsV41ClientSession session = await OpenNfsV41ClientSession.EstablishAsync(options, cancellationToken).ConfigureAwait(false);

                            OpenNfsV41CompoundOutcome outcome = await session.SendCompoundAsync(
                                new[] { new nfs_argop4 { argop = nfs_opnum4.OP_PUTROOTFH } },
                                cacheReply: false,
                                tag: "telemetry-v41",
                                cancellationToken: cancellationToken).ConfigureAwait(false);
                            if (outcome.Response.status != nfsstat4.NFS4_OK)
                            {
                                throw new InvalidOperationException("Expected SEQUENCE + PUTROOTFH to succeed, but observed " + outcome.Response.status + ".");
                            }

                            capture.RecordObservableInstruments();
                            capture.Require(OpenNfsTelemetryNames.ServerNfs41Sessions, "established sessions are sampled");
                            capture.Require(OpenNfsTelemetryNames.ServerNfs41SequenceResults, "SEQUENCE slot evaluations are counted", OpenNfsTelemetryNames.AttributeResult, OpenNfsTelemetryNames.SlotStateFresh);
                            capture.Require(OpenNfsTelemetryNames.ServerCompoundOperationDuration, "CREATE_SESSION is measured", OpenNfsTelemetryNames.AttributeNfsMinorVersion, "1", OpenNfsTelemetryNames.AttributeNfsOperation, "CREATE_SESSION");
                            capture.Require(OpenNfsTelemetryNames.ServerCompoundOperationDuration, "SEQUENCE is measured", OpenNfsTelemetryNames.AttributeNfsMinorVersion, "1", OpenNfsTelemetryNames.AttributeNfsOperation, "SEQUENCE", OpenNfsTelemetryNames.AttributeOutcome, OpenNfsTelemetryNames.OutcomeSuccess);
                            capture.Require(OpenNfsTelemetryNames.ServerRpcDuration, "NFSv4.1 COMPOUND calls get server RPC telemetry", OpenNfsTelemetryNames.AttributeRpcMethod, "COMPOUND", OpenNfsTelemetryNames.AttributeRpcVersion, "4");
                            capture.Require(OpenNfsTelemetryNames.ServerConnectionsOpened, "the NFSv4.1 listener accepted the session connection", OpenNfsTelemetryNames.AttributeListener, OpenNfsTelemetryNames.ListenerNfs41);
                        }
                        finally
                        {
                            EphemeralOpenNfsServer.DeleteDirectory(root);
                        }
                    }),
            };
        }
    }
}
