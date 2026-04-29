namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using OpenNFS.Client.Internal;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Protocol.V3.Nsm;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Xdr;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// Touchstone suites covering the first NSM monitor, notify, and grace-period recovery surface.
    /// </summary>
    public static class NsmSuites
    {
        /// <summary>
        /// Creates the shared NSM suite catalog.
        /// </summary>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(
                suiteId: "NsmSuites",
                displayName: "NSM Foundation",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        suiteId: "NsmSuites",
                        caseId: "MonitorNotifyRecoveryPositive",
                        displayName: "NSM monitors hosts, dispatches notify callbacks, and bumps local state on simulated crash",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            MutableClock clock = new MutableClock(new DateTimeOffset(2026, 4, 28, 8, 0, 0, TimeSpan.Zero));
                            NsmRecoveryCoordinator coordinator = new NsmRecoveryCoordinator(TimeSpan.FromMinutes(5), clock.UtcNow);
                            RecordingNsmNotificationDispatcher dispatcher = new RecordingNsmNotificationDispatcher();
                            NsmService service = new NsmService(coordinator, dispatcher);

                            RpcMessageEnvelope monitorReply = await service.DispatchAsync(
                                CreateCall(
                                    0x5001,
                                    (uint)SM_PROG_Program.Procedure_SM_VERS_SM_MON,
                                    CreateMonitorArguments(
                                        "linux-client",
                                        "127.0.0.1",
                                        123,
                                        1,
                                        9,
                                        CreatePrivateData(0xA1))),
                                cancellationToken).ConfigureAwait(false);

                            sm_stat_res monitorResult = ReadStatResult(monitorReply, "SM_MON");
                            if (monitorResult.res_stat != res.STAT_SUCC || monitorResult.state != 1)
                            {
                                throw new InvalidOperationException("Expected NSM MON to register successfully and return the initial local state.");
                            }

                            RpcMessageEnvelope notifyReply = await service.DispatchAsync(
                                CreateCall(
                                    0x5002,
                                    (uint)SM_PROG_Program.Procedure_SM_VERS_SM_NOTIFY,
                                    CreateNotifyArguments("linux-client", 44)),
                                cancellationToken).ConfigureAwait(false);
                            OpenNfsRpcReplyDecoder.EnsureAcceptedSuccessWithoutPayload(RpcMessageCodec.Encode(notifyReply), "SM_NOTIFY");

                            if (dispatcher.Callbacks.Count != 1)
                            {
                                throw new InvalidOperationException("Expected NSM NOTIFY to dispatch exactly one callback for the monitored host.");
                            }

                            OpenNFS.Protocol.V3.Nsm.Callbacks.NsmNotificationCallback callback = dispatcher.Callbacks[0];
                            if (!string.Equals(callback.CallbackHostName, "127.0.0.1", StringComparison.Ordinal)
                                || callback.CallbackProgramNumber != 123
                                || callback.CallbackVersionNumber != 1
                                || callback.CallbackProcedureNumber != 9
                                || !string.Equals(callback.MonitoredHostName, "linux-client", StringComparison.Ordinal)
                                || callback.State != 44
                                || !callback.PrivateData.Span.SequenceEqual(CreatePrivateData(0xA1)))
                            {
                                throw new InvalidOperationException("Expected the NSM notify callback payload to match the monitor registration.");
                            }

                            RpcMessageEnvelope statReply = await service.DispatchAsync(
                                CreateCall(
                                    0x5003,
                                    (uint)SM_PROG_Program.Procedure_SM_VERS_SM_STAT,
                                    CreateStatArguments("linux-client")),
                                cancellationToken).ConfigureAwait(false);
                            sm_stat_res statResult = ReadStatResult(statReply, "SM_STAT");
                            if (statResult.res_stat != res.STAT_SUCC || statResult.state != 44)
                            {
                                throw new InvalidOperationException("Expected NSM STAT to surface the observed remote state after NOTIFY.");
                            }

                            RpcMessageEnvelope crashReply = await service.DispatchAsync(
                                CreateCall(
                                    0x5004,
                                    (uint)SM_PROG_Program.Procedure_SM_VERS_SM_SIMU_CRASH,
                                    Array.Empty<byte>()),
                                cancellationToken).ConfigureAwait(false);
                            OpenNfsRpcReplyDecoder.EnsureAcceptedSuccessWithoutPayload(RpcMessageCodec.Encode(crashReply), "SM_SIMU_CRASH");

                            RpcMessageEnvelope postCrashStatReply = await service.DispatchAsync(
                                CreateCall(
                                    0x5005,
                                    (uint)SM_PROG_Program.Procedure_SM_VERS_SM_STAT,
                                    CreateStatArguments("unseen-host")),
                                cancellationToken).ConfigureAwait(false);
                            sm_stat_res postCrashStat = ReadStatResult(postCrashStatReply, "SM_STAT");
                            if (postCrashStat.res_stat != res.STAT_SUCC || postCrashStat.state != 2)
                            {
                                throw new InvalidOperationException("Expected SM_SIMU_CRASH to bump the local NSM state and surface it through SM_STAT.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NsmSuites",
                        caseId: "MonitorNotifyRecoveryNegative",
                        displayName: "NSM unmonitor flows suppress callbacks and malformed payloads fail cleanly",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            NsmRecoveryCoordinator coordinator = new NsmRecoveryCoordinator(TimeSpan.FromMinutes(5));
                            RecordingNsmNotificationDispatcher dispatcher = new RecordingNsmNotificationDispatcher();
                            NsmService service = new NsmService(coordinator, dispatcher);
                            byte[] privateData = CreatePrivateData(0xB1);

                            _ = await service.DispatchAsync(
                                CreateCall(
                                    0x5101,
                                    (uint)SM_PROG_Program.Procedure_SM_VERS_SM_MON,
                                    CreateMonitorArguments("linux-client", "127.0.0.1", 321, 2, 5, privateData)),
                                cancellationToken).ConfigureAwait(false);

                            RpcMessageEnvelope unmonitorReply = await service.DispatchAsync(
                                CreateCall(
                                    0x5102,
                                    (uint)SM_PROG_Program.Procedure_SM_VERS_SM_UNMON,
                                    CreateUnmonitorArguments("linux-client", "127.0.0.1", 321, 2, 5)),
                                cancellationToken).ConfigureAwait(false);
                            sm_stat unmonitorResult = ReadSimpleState(unmonitorReply, "SM_UNMON");
                            if (unmonitorResult.state != 1)
                            {
                                throw new InvalidOperationException("Expected SM_UNMON to return the current local state.");
                            }

                            RpcMessageEnvelope notifyAfterUnmonitorReply = await service.DispatchAsync(
                                CreateCall(
                                    0x5103,
                                    (uint)SM_PROG_Program.Procedure_SM_VERS_SM_NOTIFY,
                                    CreateNotifyArguments("linux-client", 77)),
                                cancellationToken).ConfigureAwait(false);
                            OpenNfsRpcReplyDecoder.EnsureAcceptedSuccessWithoutPayload(RpcMessageCodec.Encode(notifyAfterUnmonitorReply), "SM_NOTIFY");
                            if (dispatcher.Callbacks.Count != 0)
                            {
                                throw new InvalidOperationException("Expected SM_UNMON to suppress later notify callbacks for the removed monitor.");
                            }

                            _ = await service.DispatchAsync(
                                CreateCall(
                                    0x5104,
                                    (uint)SM_PROG_Program.Procedure_SM_VERS_SM_MON,
                                    CreateMonitorArguments("linux-client", "127.0.0.1", 321, 2, 5, privateData)),
                                cancellationToken).ConfigureAwait(false);
                            _ = await service.DispatchAsync(
                                CreateCall(
                                    0x5105,
                                    (uint)SM_PROG_Program.Procedure_SM_VERS_SM_MON,
                                    CreateMonitorArguments("other-client", "127.0.0.1", 321, 2, 5, privateData)),
                                cancellationToken).ConfigureAwait(false);

                            RpcMessageEnvelope unmonitorAllReply = await service.DispatchAsync(
                                CreateCall(
                                    0x5106,
                                    (uint)SM_PROG_Program.Procedure_SM_VERS_SM_UNMON_ALL,
                                    CreateUnmonitorAllArguments("127.0.0.1", 321, 2, 5)),
                                cancellationToken).ConfigureAwait(false);
                            sm_stat unmonitorAllResult = ReadSimpleState(unmonitorAllReply, "SM_UNMON_ALL");
                            if (unmonitorAllResult.state != 1)
                            {
                                throw new InvalidOperationException("Expected SM_UNMON_ALL to return the current local state.");
                            }

                            RpcMessageEnvelope notifyAfterUnmonitorAllReply = await service.DispatchAsync(
                                CreateCall(
                                    0x5107,
                                    (uint)SM_PROG_Program.Procedure_SM_VERS_SM_NOTIFY,
                                    CreateNotifyArguments("other-client", 88)),
                                cancellationToken).ConfigureAwait(false);
                            OpenNfsRpcReplyDecoder.EnsureAcceptedSuccessWithoutPayload(RpcMessageCodec.Encode(notifyAfterUnmonitorAllReply), "SM_NOTIFY");
                            if (dispatcher.Callbacks.Count != 0)
                            {
                                throw new InvalidOperationException("Expected SM_UNMON_ALL to remove every registration for the callback identity.");
                            }

                            RpcMessageEnvelope malformedReply = await service.DispatchAsync(
                                CreateCall(
                                    0x5108,
                                    (uint)SM_PROG_Program.Procedure_SM_VERS_SM_MON,
                                    new byte[] { 0x00, 0x00, 0x00, 0x01 }),
                                cancellationToken).ConfigureAwait(false);

                            bool sawGarbageArguments = false;
                            try
                            {
                                _ = ReadStatResult(malformedReply, "SM_MON");
                            }
                            catch (InvalidDataException)
                            {
                                sawGarbageArguments = true;
                            }

                            if (!sawGarbageArguments)
                            {
                                throw new InvalidOperationException("Expected malformed NSM monitor payloads to fail with garbage arguments.");
                            }
                        }),
                });
        }

        private static RpcMessageEnvelope CreateCall(uint xid, uint procedureNumber, byte[] procedurePayload)
        {
            return RpcMessageFactory.CreateCall(
                xid: xid,
                program: (uint)SM_PROG_Program.Program,
                version: (uint)SM_PROG_Program.Version_SM_VERS,
                procedure: procedureNumber,
                credential: RpcAuthenticationCodec.CreateNone(),
                verifier: RpcAuthenticationCodec.CreateNone(),
                procedurePayload: procedurePayload);
        }

        private static sm_stat_res ReadStatResult(RpcMessageEnvelope reply, string operationName)
        {
            ReadOnlyMemory<byte> payload = OpenNfsRpcReplyDecoder.ReadAcceptedSuccessProcedurePayload(
                RpcMessageCodec.Encode(reply),
                operationName);
            XdrReader reader = new XdrReader(payload);
            sm_stat_res result = sm_stat_res.ReadFrom(reader);
            reader.EnsureFullyConsumed();
            return result;
        }

        private static sm_stat ReadSimpleState(RpcMessageEnvelope reply, string operationName)
        {
            ReadOnlyMemory<byte> payload = OpenNfsRpcReplyDecoder.ReadAcceptedSuccessProcedurePayload(
                RpcMessageCodec.Encode(reply),
                operationName);
            XdrReader reader = new XdrReader(payload);
            sm_stat result = sm_stat.ReadFrom(reader);
            reader.EnsureFullyConsumed();
            return result;
        }

        private static byte[] CreateMonitorArguments(
            string monitoredHostName,
            string callbackHostName,
            int callbackProgramNumber,
            int callbackVersionNumber,
            int callbackProcedureNumber,
            byte[] privateData)
        {
            mon arguments = new mon
            {
                mon_id = new mon_id
                {
                    mon_name = monitoredHostName,
                    my_id = new my_id
                    {
                        my_name = callbackHostName,
                        my_prog = callbackProgramNumber,
                        my_vers = callbackVersionNumber,
                        my_proc = callbackProcedureNumber,
                    },
                },
                priv = privateData,
            };

            return Encode(arguments.WriteTo);
        }

        private static byte[] CreateUnmonitorArguments(
            string monitoredHostName,
            string callbackHostName,
            int callbackProgramNumber,
            int callbackVersionNumber,
            int callbackProcedureNumber)
        {
            mon_id arguments = new mon_id
            {
                mon_name = monitoredHostName,
                my_id = new my_id
                {
                    my_name = callbackHostName,
                    my_prog = callbackProgramNumber,
                    my_vers = callbackVersionNumber,
                    my_proc = callbackProcedureNumber,
                },
            };

            return Encode(arguments.WriteTo);
        }

        private static byte[] CreateUnmonitorAllArguments(
            string callbackHostName,
            int callbackProgramNumber,
            int callbackVersionNumber,
            int callbackProcedureNumber)
        {
            my_id arguments = new my_id
            {
                my_name = callbackHostName,
                my_prog = callbackProgramNumber,
                my_vers = callbackVersionNumber,
                my_proc = callbackProcedureNumber,
            };

            return Encode(arguments.WriteTo);
        }

        private static byte[] CreateNotifyArguments(string monitoredHostName, int state)
        {
            stat_chge arguments = new stat_chge
            {
                mon_name = monitoredHostName,
                state = state,
            };

            return Encode(arguments.WriteTo);
        }

        private static byte[] CreateStatArguments(string monitoredHostName)
        {
            sm_name arguments = new sm_name
            {
                mon_name = monitoredHostName,
            };

            return Encode(arguments.WriteTo);
        }

        private static byte[] CreatePrivateData(byte seed)
        {
            byte[] value = new byte[16];
            for (int index = 0; index < value.Length; index++)
            {
                value[index] = unchecked((byte)(seed + index));
            }

            return value;
        }

        private static byte[] Encode(Action<XdrWriter> writeAction)
        {
            XdrWriter writer = new XdrWriter();
            writeAction(writer);
            return writer.ToArray();
        }
    }
}
