namespace OpenNFS.Server.Internal.V42
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V41.Sessions;
    using OpenNFS.Protocol.V42.Generated;
    using OpenNFS.Protocol.V42.Server.Operations;
    using OpenNFS.Rpc.Telemetry;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;
    using OpenNFS.Telemetry;

    internal sealed class Nfs42CompoundExecutor
    {
        internal const uint SupportedMinorVersion = 2;

        private const string MinorVersionName = "2";

        private readonly Nfs42AdvisoryOperationsProcessor advisoryProcessor;
        private readonly Nfs42CopyCloneProcessor copyCloneProcessor;
        private readonly Nfs42SessionOperationProcessor sessionProcessor;
        private readonly Nfs42SparseFileProcessor sparseProcessor;
        private readonly OpenNfsServer server;

        internal Nfs42CompoundExecutor(OpenNfsServer server, Nfs42SessionOperationProcessor sessionProcessor)
        {
            ArgumentNullException.ThrowIfNull(server);
            ArgumentNullException.ThrowIfNull(sessionProcessor);

            this.server = server;
            this.sessionProcessor = sessionProcessor;
            sparseProcessor = new Nfs42SparseFileProcessor(server.Capabilities.TrackedSparse);
            copyCloneProcessor = new Nfs42CopyCloneProcessor(server.Capabilities.TrackedCopyClone);
            advisoryProcessor = new Nfs42AdvisoryOperationsProcessor();
        }

        internal async Task<COMPOUND4res> ExecuteAsync(
            COMPOUND4args arguments,
            Nfs42OperationContext context,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(arguments);
            ArgumentNullException.ThrowIfNull(context);
            cancellationToken.ThrowIfCancellationRequested();

            if (arguments.minorversion != SupportedMinorVersion)
            {
                return new COMPOUND4res
                {
                    status = nfsstat4.NFS4ERR_MINOR_VERS_MISMATCH,
                    tag = GetTagOrEmpty(arguments.tag),
                    resarray = Array.Empty<nfs_resop4>(),
                };
            }

            nfs_argop4[] operations = arguments.argarray ?? Array.Empty<nfs_argop4>();
            List<nfs_resop4> results = new List<nfs_resop4>(operations.Length);
            Nfs42CompoundState state = new Nfs42CompoundState();
            nfsstat4 finalStatus = nfsstat4.NFS4_OK;
            Nfs42SequenceOutcome? freshSequenceOutcome = null;
            bool hasObservedSequence = false;
            bool replayDetected = false;

            for (int index = 0; index < operations.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                nfs_argop4 operation = operations[index];
                nfs_opnum4? opnum = operation.argop;

                int operationNumber = (int)(opnum ?? nfs_opnum4.OP_ILLEGAL);
                long operationStartTimestamp = Stopwatch.GetTimestamp();
                Activity? operationActivity = OpenNfsServerInstrumentation.StartCompoundOperation(MinorVersionName, operationNumber);
                int telemetryStatus = (int)nfsstat4.NFS4_OK;
                Exception? telemetryException = null;
                try
                {
                    if (!hasObservedSequence
                        && opnum != nfs_opnum4.OP_EXCHANGE_ID
                        && opnum != nfs_opnum4.OP_CREATE_SESSION
                        && opnum != nfs_opnum4.OP_BIND_CONN_TO_SESSION
                        && opnum != nfs_opnum4.OP_DESTROY_SESSION
                        && opnum != nfs_opnum4.OP_DESTROY_CLIENTID
                        && opnum != nfs_opnum4.OP_SEQUENCE)
                    {
                        finalStatus = nfsstat4.NFS4ERR_OP_NOT_IN_SESSION;
                        telemetryStatus = (int)finalStatus;
                        results.Add(BuildOpNotInSessionResult());
                        break;
                    }

                    nfs_resop4 result;
                    switch (opnum)
                    {
                        case nfs_opnum4.OP_EXCHANGE_ID:
                            result = new nfs_resop4
                            {
                                resop = nfs_opnum4.OP_EXCHANGE_ID,
                                opexchange_id = sessionProcessor.ProcessExchangeId(operation.opexchange_id ?? new EXCHANGE_ID4args()),
                            };
                            break;
                        case nfs_opnum4.OP_CREATE_SESSION:
                            result = new nfs_resop4
                            {
                                resop = nfs_opnum4.OP_CREATE_SESSION,
                                opcreate_session = sessionProcessor.ProcessCreateSession(operation.opcreate_session ?? new CREATE_SESSION4args(), context),
                            };
                            break;
                        case nfs_opnum4.OP_DESTROY_SESSION:
                            result = new nfs_resop4
                            {
                                resop = nfs_opnum4.OP_DESTROY_SESSION,
                                opdestroy_session = sessionProcessor.ProcessDestroySession(operation.opdestroy_session ?? new DESTROY_SESSION4args()),
                            };
                            break;
                        case nfs_opnum4.OP_DESTROY_CLIENTID:
                            result = new nfs_resop4
                            {
                                resop = nfs_opnum4.OP_DESTROY_CLIENTID,
                                opdestroy_clientid = sessionProcessor.ProcessDestroyClientId(operation.opdestroy_clientid ?? new DESTROY_CLIENTID4args()),
                            };
                            break;
                        case nfs_opnum4.OP_BIND_CONN_TO_SESSION:
                            result = new nfs_resop4
                            {
                                resop = nfs_opnum4.OP_BIND_CONN_TO_SESSION,
                                opbind_conn_to_session = sessionProcessor.ProcessBindConnToSession(operation.opbind_conn_to_session ?? new BIND_CONN_TO_SESSION4args(), context),
                            };
                            break;
                        case nfs_opnum4.OP_SEQUENCE:
                            Nfs42SequenceOutcome sequenceOutcome = sessionProcessor.ProcessSequence(operation.opsequence ?? new SEQUENCE4args(), context);
                            OpenNfsServerInstrumentation.RecordSequenceResult(DescribeSlotState(sequenceOutcome.State));
                            hasObservedSequence = true;
                            result = new nfs_resop4
                            {
                                resop = nfs_opnum4.OP_SEQUENCE,
                                opsequence = sequenceOutcome.Result,
                            };

                            if (sequenceOutcome.State == Nfs41SlotState.Replay)
                            {
                                replayDetected = true;
                            }
                            else if (sequenceOutcome.State == Nfs41SlotState.Fresh)
                            {
                                freshSequenceOutcome = sequenceOutcome;
                            }
                            else
                            {
                                finalStatus = sequenceOutcome.Result.sr_status ?? nfsstat4.NFS4ERR_INVAL;
                                telemetryStatus = (int)finalStatus;
                                results.Add(result);
                                return new COMPOUND4res
                                {
                                    status = finalStatus,
                                    tag = GetTagOrEmpty(arguments.tag),
                                    resarray = results.ToArray(),
                                };
                            }

                            break;
                        case nfs_opnum4.OP_PUTFH:
                            result = await HandlePutFileHandleAsync(operation.opputfh ?? new PUTFH4args(), state, cancellationToken).ConfigureAwait(false);
                            break;
                        case nfs_opnum4.OP_PUTROOTFH:
                            result = await HandlePutRootFileHandleAsync(state, cancellationToken).ConfigureAwait(false);
                            break;
                        case nfs_opnum4.OP_SAVEFH:
                            result = HandleSaveFileHandle(state);
                            break;
                        case nfs_opnum4.OP_LOOKUP:
                            result = await HandleLookupAsync(operation.oplookup ?? new LOOKUP4args(), state, cancellationToken).ConfigureAwait(false);
                            break;
                        case nfs_opnum4.OP_ALLOCATE:
                            result = await HandleAllocateAsync(operation.opallocate ?? new ALLOCATE4args(), state).ConfigureAwait(false);
                            break;
                        case nfs_opnum4.OP_COPY:
                            result = await HandleCopyAsync(operation.opcopy ?? new COPY4args(), state).ConfigureAwait(false);
                            break;
                        case nfs_opnum4.OP_COPY_NOTIFY:
                            result = new nfs_resop4
                            {
                                resop = nfs_opnum4.OP_COPY_NOTIFY,
                                opcopy_notify = await advisoryProcessor.ProcessCopyNotifyAsync(operation.opoffload_notify ?? new COPY_NOTIFY4args()).ConfigureAwait(false),
                            };
                            break;
                        case nfs_opnum4.OP_DEALLOCATE:
                            result = await HandleDeallocateAsync(operation.opdeallocate ?? new DEALLOCATE4args(), state).ConfigureAwait(false);
                            break;
                        case nfs_opnum4.OP_IO_ADVISE:
                            result = await HandleIoAdviseAsync(operation.opio_advise ?? new IO_ADVISE4args(), state).ConfigureAwait(false);
                            break;
                        case nfs_opnum4.OP_OFFLOAD_CANCEL:
                            result = new nfs_resop4
                            {
                                resop = nfs_opnum4.OP_OFFLOAD_CANCEL,
                                opoffload_cancel = await advisoryProcessor.ProcessOffloadCancelAsync(operation.opoffload_cancel ?? new OFFLOAD_CANCEL4args()).ConfigureAwait(false),
                            };
                            break;
                        case nfs_opnum4.OP_OFFLOAD_STATUS:
                            result = new nfs_resop4
                            {
                                resop = nfs_opnum4.OP_OFFLOAD_STATUS,
                                opoffload_status = await advisoryProcessor.ProcessOffloadStatusAsync(operation.opoffload_status ?? new OFFLOAD_STATUS4args()).ConfigureAwait(false),
                            };
                            break;
                        case nfs_opnum4.OP_READ_PLUS:
                            result = await HandleReadPlusAsync(operation.opread_plus ?? new READ_PLUS4args(), state).ConfigureAwait(false);
                            break;
                        case nfs_opnum4.OP_SEEK:
                            result = await HandleSeekAsync(operation.opseek ?? new SEEK4args(), state).ConfigureAwait(false);
                            break;
                        case nfs_opnum4.OP_WRITE_SAME:
                            result = new nfs_resop4
                            {
                                resop = nfs_opnum4.OP_WRITE_SAME,
                                opwrite_same = await advisoryProcessor.ProcessWriteSameAsync(operation.opwrite_same ?? new WRITE_SAME4args()).ConfigureAwait(false),
                            };
                            break;
                        case nfs_opnum4.OP_CLONE:
                            result = await HandleCloneAsync(operation.opclone ?? new CLONE4args(), state).ConfigureAwait(false);
                            break;
                        default:
                            result = new nfs_resop4
                            {
                                resop = nfs_opnum4.OP_ILLEGAL,
                                opillegal = new ILLEGAL4res { status = nfsstat4.NFS4ERR_NOTSUPP },
                            };
                            finalStatus = nfsstat4.NFS4ERR_NOTSUPP;
                            break;
                    }

                    results.Add(result);

                    nfsstat4? operationStatus = ExtractStatus(result);
                    telemetryStatus = (int)(operationStatus ?? nfsstat4.NFS4_OK);
                    if (operationStatus is not null && operationStatus.Value != nfsstat4.NFS4_OK)
                    {
                        finalStatus = operationStatus.Value;
                        break;
                    }
                }
                catch (Exception exception)
                {
                    telemetryException = exception;
                    throw;
                }
                finally
                {
                    if (telemetryException is null)
                    {
                        OpenNfsServerInstrumentation.EndCompoundOperation(operationActivity, operationStartTimestamp, MinorVersionName, operationNumber, telemetryStatus);
                    }
                    else
                    {
                        OpenNfsServerInstrumentation.FailCompoundOperation(operationActivity, operationStartTimestamp, MinorVersionName, operationNumber, telemetryException);
                    }
                }
            }

            COMPOUND4res response = new COMPOUND4res
            {
                status = finalStatus,
                tag = GetTagOrEmpty(arguments.tag),
                resarray = results.ToArray(),
            };

            if (replayDetected)
            {
                return response;
            }

            if (freshSequenceOutcome is not null && freshSequenceOutcome.CacheRequested)
            {
                byte[] replyBytes = Nfs42CompoundPayloadCodec.EncodeCompoundResult(response);
                sessionProcessor.RecordSequenceReply(freshSequenceOutcome, replyBytes);
            }
            else if (freshSequenceOutcome is not null)
            {
                sessionProcessor.RecordSequenceReply(freshSequenceOutcome, ReadOnlyMemory<byte>.Empty);
            }

            return response;
        }

        private async Task<nfs_resop4> HandlePutFileHandleAsync(
            PUTFH4args arguments,
            Nfs42CompoundState state,
            CancellationToken cancellationToken)
        {
            if (arguments.@object?.Value is null || arguments.@object.Value.Length == 0)
            {
                return new nfs_resop4
                {
                    resop = nfs_opnum4.OP_PUTFH,
                    opputfh = new PUTFH4res { status = nfsstat4.NFS4ERR_BADHANDLE },
                };
            }

            if (arguments.@object.Value.Length > (int)Nfs42Constants.NFS4_FHSIZE)
            {
                return new nfs_resop4
                {
                    resop = nfs_opnum4.OP_PUTFH,
                    opputfh = new PUTFH4res { status = nfsstat4.NFS4ERR_BADHANDLE },
                };
            }

            Nfs42ResolvedHandle? resolvedHandle = await ResolveExistingHandleAsync(arguments.@object.Value, cancellationToken).ConfigureAwait(false);
            if (resolvedHandle is null)
            {
                return new nfs_resop4
                {
                    resop = nfs_opnum4.OP_PUTFH,
                    opputfh = new PUTFH4res { status = nfsstat4.NFS4ERR_STALE },
                };
            }

            state.SetCurrentHandle(resolvedHandle);
            return new nfs_resop4
            {
                resop = nfs_opnum4.OP_PUTFH,
                opputfh = new PUTFH4res { status = nfsstat4.NFS4_OK },
            };
        }

        private async Task<nfs_resop4> HandlePutRootFileHandleAsync(
            Nfs42CompoundState state,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<OpenNfsExportDefinition> exports = await server.GetExportsAsync(cancellationToken).ConfigureAwait(false);
            if (exports.Count == 0)
            {
                return new nfs_resop4
                {
                    resop = nfs_opnum4.OP_PUTROOTFH,
                    opputrootfh = new PUTROOTFH4res { status = nfsstat4.NFS4ERR_NOFILEHANDLE },
                };
            }

            OpenNfsExportDefinition rootExport = SelectRootExport(exports);
            Nfs42ResolvedHandle resolvedHandle =
                await CreateResolvedHandleAsync(
                    new NfsFileHandleTarget(rootExport.ExportPath, rootExport.SourcePath),
                    cancellationToken).ConfigureAwait(false);
            state.SetCurrentHandle(resolvedHandle);

            return new nfs_resop4
            {
                resop = nfs_opnum4.OP_PUTROOTFH,
                opputrootfh = new PUTROOTFH4res { status = nfsstat4.NFS4_OK },
            };
        }

        private static nfs_resop4 HandleSaveFileHandle(Nfs42CompoundState state)
        {
            return new nfs_resop4
            {
                resop = nfs_opnum4.OP_SAVEFH,
                opsavefh = new SAVEFH4res
                {
                    status = state.SaveCurrentHandle() ? nfsstat4.NFS4_OK : nfsstat4.NFS4ERR_NOFILEHANDLE,
                },
            };
        }

        private async Task<nfs_resop4> HandleLookupAsync(
            LOOKUP4args arguments,
            Nfs42CompoundState state,
            CancellationToken cancellationToken)
        {
            if (!state.TryGetCurrentHandle(out Nfs42ResolvedHandle? currentHandle) || currentHandle is null)
            {
                return new nfs_resop4
                {
                    resop = nfs_opnum4.OP_LOOKUP,
                    oplookup = new LOOKUP4res { status = nfsstat4.NFS4ERR_NOFILEHANDLE },
                };
            }

            if (currentHandle.PathInfo.Kind != NfsPathKind.Directory)
            {
                return new nfs_resop4
                {
                    resop = nfs_opnum4.OP_LOOKUP,
                    oplookup = new LOOKUP4res { status = nfsstat4.NFS4ERR_NOTDIR },
                };
            }

            string? entryName = TryDecodeComponent(arguments.objname);
            if (string.IsNullOrWhiteSpace(entryName))
            {
                return new nfs_resop4
                {
                    resop = nfs_opnum4.OP_LOOKUP,
                    oplookup = new LOOKUP4res { status = nfsstat4.NFS4ERR_INVAL },
                };
            }

            NfsLookupPathResponse lookupResponse =
                await server.Settings.InstrumentedFileSystem.LookupPathAsync(
                    new NfsLookupPathRequest(currentHandle.Target.SourcePath, entryName, cancellationToken)).ConfigureAwait(false);

            if (!lookupResponse.PathInfo.Exists)
            {
                return new nfs_resop4
                {
                    resop = nfs_opnum4.OP_LOOKUP,
                    oplookup = new LOOKUP4res { status = nfsstat4.NFS4ERR_NOENT },
                };
            }

            Nfs42ResolvedHandle resolvedHandle =
                await CreateResolvedHandleAsync(
                    new NfsFileHandleTarget(currentHandle.Target.ExportPath, lookupResponse.PathInfo.Path),
                    cancellationToken).ConfigureAwait(false);
            state.SetCurrentHandle(resolvedHandle);

            return new nfs_resop4
            {
                resop = nfs_opnum4.OP_LOOKUP,
                oplookup = new LOOKUP4res { status = nfsstat4.NFS4_OK },
            };
        }

        private async Task<nfs_resop4> HandleAllocateAsync(ALLOCATE4args arguments, Nfs42CompoundState state)
        {
            Nfs42ResolvedHandle? currentHandle = GetRequiredCurrentHandle(state, nfs_opnum4.OP_ALLOCATE, out nfs_resop4? failureResult);
            if (currentHandle is null || failureResult is null)
            {
                return failureResult ?? new nfs_resop4
                {
                    resop = nfs_opnum4.OP_ALLOCATE,
                    opallocate = new ALLOCATE4res { ar_status = nfsstat4.NFS4ERR_NOFILEHANDLE },
                };
            }

            ALLOCATE4res result = await sparseProcessor.ProcessAllocateAsync(arguments, currentHandle.Target.SourcePath).ConfigureAwait(false);
            return new nfs_resop4
            {
                resop = nfs_opnum4.OP_ALLOCATE,
                opallocate = result,
            };
        }

        private async Task<nfs_resop4> HandleCopyAsync(COPY4args arguments, Nfs42CompoundState state)
        {
            Nfs42ResolvedHandle? currentHandle = GetRequiredCurrentHandle(state, nfs_opnum4.OP_COPY, out nfs_resop4? currentFailure);
            if (currentHandle is null || currentFailure is null)
            {
                return currentFailure ?? new nfs_resop4
                {
                    resop = nfs_opnum4.OP_COPY,
                    opcopy = new COPY4res { cr_status = nfsstat4.NFS4ERR_NOFILEHANDLE },
                };
            }

            if (!state.TryGetSavedHandle(out Nfs42ResolvedHandle? savedHandle) || savedHandle is null)
            {
                return new nfs_resop4
                {
                    resop = nfs_opnum4.OP_COPY,
                    opcopy = new COPY4res { cr_status = nfsstat4.NFS4ERR_NOFILEHANDLE },
                };
            }

            COPY4res result = await copyCloneProcessor
                .ProcessCopyAsync(arguments, savedHandle.Target.SourcePath, currentHandle.Target.SourcePath)
                .ConfigureAwait(false);
            return new nfs_resop4
            {
                resop = nfs_opnum4.OP_COPY,
                opcopy = result,
            };
        }

        private async Task<nfs_resop4> HandleCloneAsync(CLONE4args arguments, Nfs42CompoundState state)
        {
            Nfs42ResolvedHandle? currentHandle = GetRequiredCurrentHandle(state, nfs_opnum4.OP_CLONE, out nfs_resop4? currentFailure);
            if (currentHandle is null || currentFailure is null)
            {
                return currentFailure ?? new nfs_resop4
                {
                    resop = nfs_opnum4.OP_CLONE,
                    opclone = new CLONE4res { cl_status = nfsstat4.NFS4ERR_NOFILEHANDLE },
                };
            }

            if (!state.TryGetSavedHandle(out Nfs42ResolvedHandle? savedHandle) || savedHandle is null)
            {
                return new nfs_resop4
                {
                    resop = nfs_opnum4.OP_CLONE,
                    opclone = new CLONE4res { cl_status = nfsstat4.NFS4ERR_NOFILEHANDLE },
                };
            }

            CLONE4res result = await copyCloneProcessor
                .ProcessCloneAsync(arguments, savedHandle.Target.SourcePath, currentHandle.Target.SourcePath)
                .ConfigureAwait(false);
            return new nfs_resop4
            {
                resop = nfs_opnum4.OP_CLONE,
                opclone = result,
            };
        }

        private async Task<nfs_resop4> HandleDeallocateAsync(DEALLOCATE4args arguments, Nfs42CompoundState state)
        {
            Nfs42ResolvedHandle? currentHandle = GetRequiredCurrentHandle(state, nfs_opnum4.OP_DEALLOCATE, out nfs_resop4? failureResult);
            if (currentHandle is null || failureResult is null)
            {
                return failureResult ?? new nfs_resop4
                {
                    resop = nfs_opnum4.OP_DEALLOCATE,
                    opdeallocate = new DEALLOCATE4res { dr_status = nfsstat4.NFS4ERR_NOFILEHANDLE },
                };
            }

            DEALLOCATE4res result = await sparseProcessor.ProcessDeallocateAsync(arguments, currentHandle.Target.SourcePath).ConfigureAwait(false);
            return new nfs_resop4
            {
                resop = nfs_opnum4.OP_DEALLOCATE,
                opdeallocate = result,
            };
        }

        private async Task<nfs_resop4> HandleIoAdviseAsync(IO_ADVISE4args arguments, Nfs42CompoundState state)
        {
            Nfs42ResolvedHandle? currentHandle = GetRequiredCurrentHandle(state, nfs_opnum4.OP_IO_ADVISE, out nfs_resop4? failureResult);
            if (currentHandle is null || failureResult is null)
            {
                return failureResult ?? new nfs_resop4
                {
                    resop = nfs_opnum4.OP_IO_ADVISE,
                    opio_advise = new IO_ADVISE4res { ior_status = nfsstat4.NFS4ERR_NOFILEHANDLE },
                };
            }

            _ = currentHandle;
            IO_ADVISE4res result = await advisoryProcessor.ProcessIoAdviseAsync(arguments).ConfigureAwait(false);
            return new nfs_resop4
            {
                resop = nfs_opnum4.OP_IO_ADVISE,
                opio_advise = result,
            };
        }

        private async Task<nfs_resop4> HandleReadPlusAsync(READ_PLUS4args arguments, Nfs42CompoundState state)
        {
            Nfs42ResolvedHandle? currentHandle = GetRequiredCurrentHandle(state, nfs_opnum4.OP_READ_PLUS, out nfs_resop4? failureResult);
            if (currentHandle is null || failureResult is null)
            {
                return failureResult ?? new nfs_resop4
                {
                    resop = nfs_opnum4.OP_READ_PLUS,
                    opread_plus = new READ_PLUS4res { rp_status = nfsstat4.NFS4ERR_NOFILEHANDLE },
                };
            }

            READ_PLUS4res result = await sparseProcessor.ProcessReadPlusAsync(arguments, currentHandle.Target.SourcePath).ConfigureAwait(false);
            return new nfs_resop4
            {
                resop = nfs_opnum4.OP_READ_PLUS,
                opread_plus = result,
            };
        }

        private async Task<nfs_resop4> HandleSeekAsync(SEEK4args arguments, Nfs42CompoundState state)
        {
            Nfs42ResolvedHandle? currentHandle = GetRequiredCurrentHandle(state, nfs_opnum4.OP_SEEK, out nfs_resop4? failureResult);
            if (currentHandle is null || failureResult is null)
            {
                return failureResult ?? new nfs_resop4
                {
                    resop = nfs_opnum4.OP_SEEK,
                    opseek = new SEEK4res { sa_status = nfsstat4.NFS4ERR_NOFILEHANDLE },
                };
            }

            SEEK4res result = await sparseProcessor.ProcessSeekAsync(arguments, currentHandle.Target.SourcePath).ConfigureAwait(false);
            return new nfs_resop4
            {
                resop = nfs_opnum4.OP_SEEK,
                opseek = result,
            };
        }

        private Nfs42ResolvedHandle? GetRequiredCurrentHandle(
            Nfs42CompoundState state,
            nfs_opnum4 opnum,
            out nfs_resop4? failureResult)
        {
            if (!state.TryGetCurrentHandle(out Nfs42ResolvedHandle? currentHandle) || currentHandle is null)
            {
                failureResult = CreateNoFileHandleResult(opnum);
                return null;
            }

            failureResult = new nfs_resop4
            {
                resop = opnum,
            };
            return currentHandle;
        }

        private static nfs_resop4 CreateNoFileHandleResult(nfs_opnum4 opnum)
        {
            switch (opnum)
            {
                case nfs_opnum4.OP_ALLOCATE:
                    return new nfs_resop4 { resop = opnum, opallocate = new ALLOCATE4res { ar_status = nfsstat4.NFS4ERR_NOFILEHANDLE } };
                case nfs_opnum4.OP_COPY:
                    return new nfs_resop4 { resop = opnum, opcopy = new COPY4res { cr_status = nfsstat4.NFS4ERR_NOFILEHANDLE } };
                case nfs_opnum4.OP_DEALLOCATE:
                    return new nfs_resop4 { resop = opnum, opdeallocate = new DEALLOCATE4res { dr_status = nfsstat4.NFS4ERR_NOFILEHANDLE } };
                case nfs_opnum4.OP_IO_ADVISE:
                    return new nfs_resop4 { resop = opnum, opio_advise = new IO_ADVISE4res { ior_status = nfsstat4.NFS4ERR_NOFILEHANDLE } };
                case nfs_opnum4.OP_READ_PLUS:
                    return new nfs_resop4 { resop = opnum, opread_plus = new READ_PLUS4res { rp_status = nfsstat4.NFS4ERR_NOFILEHANDLE } };
                case nfs_opnum4.OP_SEEK:
                    return new nfs_resop4 { resop = opnum, opseek = new SEEK4res { sa_status = nfsstat4.NFS4ERR_NOFILEHANDLE } };
                case nfs_opnum4.OP_CLONE:
                    return new nfs_resop4 { resop = opnum, opclone = new CLONE4res { cl_status = nfsstat4.NFS4ERR_NOFILEHANDLE } };
                default:
                    return new nfs_resop4 { resop = nfs_opnum4.OP_ILLEGAL, opillegal = new ILLEGAL4res { status = nfsstat4.NFS4ERR_NOFILEHANDLE } };
            }
        }

        private async Task<Nfs42ResolvedHandle> CreateResolvedHandleAsync(
            NfsFileHandleTarget target,
            CancellationToken cancellationToken)
        {
            NfsFileHandle fileHandle = await server.CreateFileHandleAsync(target, cancellationToken).ConfigureAwait(false);
            NfsGetPathInfoResponse pathInfoResponse =
                await server.Settings.InstrumentedFileSystem.GetPathInfoAsync(
                    new NfsGetPathInfoRequest(target.SourcePath, cancellationToken)).ConfigureAwait(false);
            return new Nfs42ResolvedHandle(fileHandle, target, pathInfoResponse.PathInfo);
        }

        private async Task<Nfs42ResolvedHandle?> ResolveExistingHandleAsync(byte[] fileHandleBytes, CancellationToken cancellationToken)
        {
            NfsResolveFileHandleResponse resolutionResponse =
                await server.ResolveFileHandleAsync(
                    new NfsResolveFileHandleRequest(new NfsFileHandle(fileHandleBytes), cancellationToken)).ConfigureAwait(false);

            if (!resolutionResponse.Resolution.Found || resolutionResponse.Resolution.Target is null)
            {
                return null;
            }

            NfsGetPathInfoResponse pathInfoResponse =
                await server.Settings.InstrumentedFileSystem.GetPathInfoAsync(
                    new NfsGetPathInfoRequest(resolutionResponse.Resolution.Target.SourcePath, cancellationToken)).ConfigureAwait(false);

            if (!pathInfoResponse.PathInfo.Exists)
            {
                return null;
            }

            return new Nfs42ResolvedHandle(
                new NfsFileHandle(fileHandleBytes),
                resolutionResponse.Resolution.Target,
                pathInfoResponse.PathInfo);
        }

        private static OpenNfsExportDefinition SelectRootExport(IReadOnlyList<OpenNfsExportDefinition> exports)
        {
            OpenNfsExportDefinition selected = exports[0];
            for (int index = 0; index < exports.Count; index++)
            {
                OpenNfsExportDefinition candidate = exports[index];
                if (string.Equals(candidate.ExportPath, "/", StringComparison.Ordinal))
                {
                    return candidate;
                }

                if (string.CompareOrdinal(candidate.ExportPath, selected.ExportPath) < 0)
                {
                    selected = candidate;
                }
            }

            return selected;
        }

        private static utf8str_cs GetTagOrEmpty(utf8str_cs? tag)
        {
            return tag ?? new utf8str_cs { Value = new utf8string { Value = Array.Empty<byte>() } };
        }

        private static nfs_resop4 BuildOpNotInSessionResult()
        {
            return new nfs_resop4
            {
                resop = nfs_opnum4.OP_ILLEGAL,
                opillegal = new ILLEGAL4res { status = nfsstat4.NFS4ERR_OP_NOT_IN_SESSION },
            };
        }

        private static string? TryDecodeComponent(component4? component)
        {
            byte[]? bytes = component?.Value?.Value?.Value;
            if (bytes is null || bytes.Length == 0)
            {
                return null;
            }

            return Encoding.UTF8.GetString(bytes);
        }

        private static nfsstat4? ExtractStatus(nfs_resop4 result)
        {
            switch (result.resop)
            {
                case nfs_opnum4.OP_EXCHANGE_ID:
                    return result.opexchange_id?.eir_status;
                case nfs_opnum4.OP_CREATE_SESSION:
                    return result.opcreate_session?.csr_status;
                case nfs_opnum4.OP_DESTROY_SESSION:
                    return result.opdestroy_session?.dsr_status;
                case nfs_opnum4.OP_DESTROY_CLIENTID:
                    return result.opdestroy_clientid?.dcr_status;
                case nfs_opnum4.OP_BIND_CONN_TO_SESSION:
                    return result.opbind_conn_to_session?.bctsr_status;
                case nfs_opnum4.OP_SEQUENCE:
                    return result.opsequence?.sr_status;
                case nfs_opnum4.OP_PUTFH:
                    return result.opputfh?.status;
                case nfs_opnum4.OP_PUTROOTFH:
                    return result.opputrootfh?.status;
                case nfs_opnum4.OP_SAVEFH:
                    return result.opsavefh?.status;
                case nfs_opnum4.OP_LOOKUP:
                    return result.oplookup?.status;
                case nfs_opnum4.OP_ALLOCATE:
                    return result.opallocate?.ar_status;
                case nfs_opnum4.OP_COPY:
                    return result.opcopy?.cr_status;
                case nfs_opnum4.OP_COPY_NOTIFY:
                    return result.opcopy_notify?.cnr_status;
                case nfs_opnum4.OP_DEALLOCATE:
                    return result.opdeallocate?.dr_status;
                case nfs_opnum4.OP_IO_ADVISE:
                    return result.opio_advise?.ior_status;
                case nfs_opnum4.OP_OFFLOAD_CANCEL:
                    return result.opoffload_cancel?.ocr_status;
                case nfs_opnum4.OP_OFFLOAD_STATUS:
                    return result.opoffload_status?.osr_status;
                case nfs_opnum4.OP_READ_PLUS:
                    return result.opread_plus?.rp_status;
                case nfs_opnum4.OP_SEEK:
                    return result.opseek?.sa_status;
                case nfs_opnum4.OP_WRITE_SAME:
                    return result.opwrite_same?.wsr_status;
                case nfs_opnum4.OP_CLONE:
                    return result.opclone?.cl_status;
                case nfs_opnum4.OP_ILLEGAL:
                    return result.opillegal?.status;
                default:
                    return null;
            }
        }

        private static string DescribeSlotState(Nfs41SlotState state)
        {
            switch (state)
            {
                case Nfs41SlotState.Fresh:
                    return OpenNfsTelemetryNames.SlotStateFresh;
                case Nfs41SlotState.Replay:
                    return OpenNfsTelemetryNames.SlotStateReplay;
                case Nfs41SlotState.BadSlot:
                    return OpenNfsTelemetryNames.SlotStateBadSlot;
                case Nfs41SlotState.Misordered:
                    return OpenNfsTelemetryNames.SlotStateMisordered;
                case Nfs41SlotState.RetryUncached:
                    return OpenNfsTelemetryNames.SlotStateRetryUncached;
                default:
                    return OpenNfsTelemetryNames.ValueOther;
            }
        }
    }
}
