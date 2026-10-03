namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client;
    using OpenNFS.Rpc.Telemetry;
    using OpenNFS.Telemetry;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// Telemetry cases proving instrumentation is inert when nothing listens and best-effort when a listener fails.
    /// </summary>
    internal static class TelemetryResilienceCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                new TestCaseDescriptor(
                    suiteId: "TelemetrySuites",
                    caseId: "UnobservedInstrumentationIsInert",
                    displayName: "With no listener subscribed, a full round trip succeeds and instrumentation helpers are no-ops",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: async cancellationToken =>
                    {
                        await RunRoundTripAsync(cancellationToken).ConfigureAwait(false);

                        if (!OpenNfsServerInstrumentation.ActivitySource.HasListeners())
                        {
                            if (OpenNfsServerInstrumentation.StartCompoundOperation("0", 24) is not null
                                || OpenNfsClientInstrumentation.StartRpc("unobserved") is not null)
                            {
                                throw new InvalidOperationException("Without a listener no spans may be created.");
                            }
                        }

                        OpenNfsServerInstrumentation.EndCompoundOperation(null, Stopwatch.GetTimestamp(), "0", 24, 0);
                        OpenNfsServerInstrumentation.FailCompoundOperation(null, Stopwatch.GetTimestamp(), "0", 24, new IOException("ignored"));
                        OpenNfsServerInstrumentation.EndCallback(null, OpenNfsTelemetryNames.CallbackNsmNotify, Stopwatch.GetTimestamp(), OpenNfsTelemetryNames.OutcomeSuccess, null);
                        OpenNfsServerInstrumentation.RecordIoBytes(OpenNfsTelemetryNames.DirectionRead, 0);
                        OpenNfsServerInstrumentation.RecordLeaseExpirations(0);
                        OpenNfsClientInstrumentation.EndRpc(null, Stopwatch.GetTimestamp(), "unobserved", 1, null);
                        OpenNfsTelemetryErrors.MarkFailed(null, new IOException("ignored"), "System.IO.IOException");
                        OpenNfsTelemetryErrors.EndActivity(null);
                    }),

                new TestCaseDescriptor(
                    suiteId: "TelemetrySuites",
                    caseId: "FaultyListenersCannotBreakRequests",
                    displayName: "A metrics or tracing listener that throws never changes request handling",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: async cancellationToken =>
                    {
                        int faults = 0;
                        using MeterListener meterListener = new MeterListener();
                        meterListener.InstrumentPublished = (instrument, listener) =>
                        {
                            if (instrument.Meter.Name == OpenNfsTelemetryNames.ServerMeterName || instrument.Meter.Name == OpenNfsTelemetryNames.ClientMeterName)
                            {
                                listener.EnableMeasurementEvents(instrument);
                            }
                        };
                        meterListener.SetMeasurementEventCallback<long>((instrument, value, tags, state) =>
                        {
                            Interlocked.Increment(ref faults);
                            throw new InvalidOperationException("faulty metrics listener");
                        });
                        meterListener.SetMeasurementEventCallback<double>((instrument, value, tags, state) =>
                        {
                            Interlocked.Increment(ref faults);
                            throw new InvalidOperationException("faulty metrics listener");
                        });
                        meterListener.Start();

                        using ActivityListener activityListener = new ActivityListener
                        {
                            ShouldListenTo = source => source.Name == OpenNfsTelemetryNames.ServerActivitySourceName || source.Name == OpenNfsTelemetryNames.ClientActivitySourceName,
                            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,
                            ActivityStopped = activity => throw new InvalidOperationException("faulty tracing listener"),
                        };
                        ActivitySource.AddActivityListener(activityListener);

                        await RunRoundTripAsync(cancellationToken).ConfigureAwait(false);

                        if (Volatile.Read(ref faults) == 0)
                        {
                            throw new InvalidOperationException("Expected the faulty listener to have been invoked during the round trip.");
                        }
                    }),
            };
        }

        private static async Task RunRoundTripAsync(CancellationToken cancellationToken)
        {
            byte[] payload = Encoding.UTF8.GetBytes("resilience-payload");
            await using EphemeralOpenNfsServer server = await EphemeralOpenNfsServer.StartAsync(cancellationToken).ConfigureAwait(false);
            (OpenNfsClient client, OpenNfsMountSession session) = await server.ConnectAndMountAsync(cancellationToken).ConfigureAwait(false);
            await using (client)
            await using (session)
            {
                await session.Files.WriteAllBytesAsync("/resilience.bin", payload, OpenNfsWriteStability.FileSync, cancellationToken).ConfigureAwait(false);
                byte[] readBack = await session.Files.ReadAllBytesAsync("/resilience.bin", cancellationToken).ConfigureAwait(false);
                IReadOnlyList<OpenNfsV3DirectoryEntry> entries = await session.Directories.ListAsync("/", cancellationToken).ConfigureAwait(false);
                if (!readBack.SequenceEqual(payload) || !entries.Any(entry => entry.Name == "resilience.bin"))
                {
                    throw new InvalidOperationException("The round trip must behave identically regardless of telemetry listeners.");
                }
            }
        }
    }
}
