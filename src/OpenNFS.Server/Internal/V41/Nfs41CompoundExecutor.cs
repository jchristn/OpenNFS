namespace OpenNFS.Server.Internal.V41
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V41.Compound;
    using OpenNFS.Protocol.V41.Generated;
    using OpenNFS.Protocol.V41.Sessions;
    using OpenNFS.Rpc.Telemetry;
    using OpenNFS.Rpc.Xdr;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;
    using OpenNFS.Telemetry;

    internal sealed class Nfs41CompoundExecutor
    {
        internal const uint SupportedMinorVersion = 1;

        private const string MinorVersionName = "1";

        private static readonly byte[] WriteVerifier = CreateWriteVerifier();
        private readonly Nfs41OpenStateTable openStateTable = new Nfs41OpenStateTable();
        private readonly Nfs41SessionOperationProcessor sessionProcessor;
        private readonly OpenNfsServer server;

        internal Nfs41CompoundExecutor(OpenNfsServer server, Nfs41SessionOperationProcessor sessionProcessor)
        {
            ArgumentNullException.ThrowIfNull(server);
            ArgumentNullException.ThrowIfNull(sessionProcessor);

            this.server = server;
            this.sessionProcessor = sessionProcessor;
        }

        internal async Task<COMPOUND4res> ExecuteAsync(
            COMPOUND4args arguments,
            Nfs41OperationContext context,
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
            Nfs41CompoundState state = new Nfs41CompoundState();
            nfsstat4 finalStatus = nfsstat4.NFS4_OK;
            Nfs41SequenceOutcome? freshSequenceOutcome = null;
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
                            Nfs41SequenceOutcome sequenceOutcome = sessionProcessor.ProcessSequence(operation.opsequence ?? new SEQUENCE4args(), context);
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
                        case nfs_opnum4.OP_PUTROOTFH:
                            result = await HandlePutRootFileHandleAsync(state, cancellationToken).ConfigureAwait(false);
                            break;
                        case nfs_opnum4.OP_LOOKUP:
                            result = await HandleLookupAsync(operation.oplookup ?? new LOOKUP4args(), state, cancellationToken).ConfigureAwait(false);
                            break;
                        case nfs_opnum4.OP_GETATTR:
                            result = HandleGetAttributes(operation.opgetattr ?? new GETATTR4args(), state);
                            break;
                        case nfs_opnum4.OP_READDIR:
                            result = await HandleReadDirectoryAsync(operation.opreaddir ?? new READDIR4args(), state, cancellationToken).ConfigureAwait(false);
                            break;
                        case nfs_opnum4.OP_OPEN:
                            result = await HandleOpenAsync(operation.opopen ?? new OPEN4args(), state, context, cancellationToken).ConfigureAwait(false);
                            break;
                        case nfs_opnum4.OP_READ:
                            result = await HandleReadAsync(operation.opread ?? new READ4args(), state).ConfigureAwait(false);
                            break;
                        case nfs_opnum4.OP_WRITE:
                            result = await HandleWriteAsync(operation.opwrite ?? new WRITE4args(), state).ConfigureAwait(false);
                            break;
                        case nfs_opnum4.OP_CLOSE:
                            result = HandleClose(operation.opclose ?? new CLOSE4args(), state);
                            break;
                        case nfs_opnum4.OP_REMOVE:
                            result = await HandleRemoveAsync(operation.opremove ?? new REMOVE4args(), state, cancellationToken).ConfigureAwait(false);
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
                byte[] replyBytes = Nfs41ServerCompoundPayloadCodec.EncodeCompoundResult(response);
                sessionProcessor.RecordSequenceReply(freshSequenceOutcome, replyBytes);
            }
            else if (freshSequenceOutcome is not null)
            {
                sessionProcessor.RecordSequenceReply(freshSequenceOutcome, ReadOnlyMemory<byte>.Empty);
            }

            return response;
        }

        private async Task<nfs_resop4> HandlePutRootFileHandleAsync(
            Nfs41CompoundState state,
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
            Nfs41ResolvedHandle resolvedHandle = await CreateResolvedHandleAsync(
                rootExport,
                new NfsFileHandleTarget(rootExport.ExportPath, rootExport.SourcePath),
                cancellationToken).ConfigureAwait(false);
            state.SetCurrentHandle(resolvedHandle);

            return new nfs_resop4
            {
                resop = nfs_opnum4.OP_PUTROOTFH,
                opputrootfh = new PUTROOTFH4res { status = nfsstat4.NFS4_OK },
            };
        }

        private async Task<nfs_resop4> HandleLookupAsync(
            LOOKUP4args arguments,
            Nfs41CompoundState state,
            CancellationToken cancellationToken)
        {
            if (!state.TryGetCurrentHandle(out Nfs41ResolvedHandle? currentHandle) || currentHandle is null)
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

            NfsLookupPathResponse lookupResponse = await server.Settings.InstrumentedFileSystem.LookupPathAsync(
                new NfsLookupPathRequest(currentHandle.Target.SourcePath, entryName, cancellationToken)).ConfigureAwait(false);
            if (!lookupResponse.PathInfo.Exists)
            {
                return new nfs_resop4
                {
                    resop = nfs_opnum4.OP_LOOKUP,
                    oplookup = new LOOKUP4res { status = nfsstat4.NFS4ERR_NOENT },
                };
            }

            Nfs41ResolvedHandle resolvedHandle = await CreateResolvedHandleAsync(
                currentHandle.Export,
                new NfsFileHandleTarget(currentHandle.Target.ExportPath, lookupResponse.PathInfo.Path),
                cancellationToken).ConfigureAwait(false);
            state.SetCurrentHandle(resolvedHandle);

            return new nfs_resop4
            {
                resop = nfs_opnum4.OP_LOOKUP,
                oplookup = new LOOKUP4res { status = nfsstat4.NFS4_OK },
            };
        }

        private nfs_resop4 HandleGetAttributes(GETATTR4args arguments, Nfs41CompoundState state)
        {
            if (!state.TryGetCurrentHandle(out Nfs41ResolvedHandle? currentHandle) || currentHandle is null)
            {
                return new nfs_resop4
                {
                    resop = nfs_opnum4.OP_GETATTR,
                    opgetattr = new GETATTR4res { status = nfsstat4.NFS4ERR_NOFILEHANDLE },
                };
            }

            TryCreateAttributesResult attributeResult = TryCreateAttributes(currentHandle, arguments.attr_request);
            return new nfs_resop4
            {
                resop = nfs_opnum4.OP_GETATTR,
                opgetattr = attributeResult.Attributes is null
                    ? new GETATTR4res { status = attributeResult.Status }
                    : new GETATTR4res
                    {
                        status = nfsstat4.NFS4_OK,
                        resok4 = new GETATTR4resok { obj_attributes = attributeResult.Attributes },
                    },
            };
        }

        private async Task<nfs_resop4> HandleReadDirectoryAsync(
            READDIR4args arguments,
            Nfs41CompoundState state,
            CancellationToken cancellationToken)
        {
            if (!state.TryGetCurrentHandle(out Nfs41ResolvedHandle? currentHandle) || currentHandle is null)
            {
                return new nfs_resop4
                {
                    resop = nfs_opnum4.OP_READDIR,
                    opreaddir = new READDIR4res { status = nfsstat4.NFS4ERR_NOFILEHANDLE },
                };
            }

            if (currentHandle.PathInfo.Kind != NfsPathKind.Directory)
            {
                return new nfs_resop4
                {
                    resop = nfs_opnum4.OP_READDIR,
                    opreaddir = new READDIR4res { status = nfsstat4.NFS4ERR_NOTDIR },
                };
            }

            bitmap4 requestedAttributes = arguments.attr_request ?? new bitmap4 { Value = Array.Empty<uint>() };
            NfsReadDirectoryResponse directoryResponse = await server.Settings.InstrumentedFileSystem.ReadDirectoryAsync(
                new NfsReadDirectoryRequest(currentHandle.Target.SourcePath, cancellationToken)).ConfigureAwait(false);

            int startIndex = 0;
            ulong cookie = arguments.cookie?.Value ?? 0UL;
            if (cookie > 0)
            {
                startIndex = cookie >= (ulong)directoryResponse.Entries.Count
                    ? directoryResponse.Entries.Count
                    : (int)cookie;
            }

            entry4? head = null;
            entry4? tail = null;
            for (int index = startIndex; index < directoryResponse.Entries.Count; index++)
            {
                NfsDirectoryEntryInfo entry = directoryResponse.Entries[index];
                Nfs41ResolvedHandle entryHandle = await CreateResolvedHandleAsync(
                    currentHandle.Export,
                    new NfsFileHandleTarget(currentHandle.Target.ExportPath, entry.PathInfo.Path),
                    cancellationToken).ConfigureAwait(false);
                TryCreateAttributesResult attributeResult = TryCreateAttributes(entryHandle, requestedAttributes);
                if (attributeResult.Attributes is null)
                {
                    return new nfs_resop4
                    {
                        resop = nfs_opnum4.OP_READDIR,
                        opreaddir = new READDIR4res { status = attributeResult.Status },
                    };
                }

                entry4 next = new entry4
                {
                    cookie = new nfs_cookie4 { Value = (ulong)(index + 1) },
                    name = CreateComponent(entry.Name),
                    attrs = attributeResult.Attributes,
                };

                if (head is null)
                {
                    head = next;
                }
                else
                {
                    tail!.nextentry = next;
                }

                tail = next;
            }

            return new nfs_resop4
            {
                resop = nfs_opnum4.OP_READDIR,
                opreaddir = new READDIR4res
                {
                    status = nfsstat4.NFS4_OK,
                    resok4 = new READDIR4resok
                    {
                        cookieverf = new verifier4 { Value = CreateDirectoryVerifier(currentHandle.Target.SourcePath) },
                        reply = new dirlist4
                        {
                            entries = head,
                            eof = true,
                        },
                    },
                },
            };
        }

        private async Task<nfs_resop4> HandleOpenAsync(
            OPEN4args arguments,
            Nfs41CompoundState state,
            Nfs41OperationContext context,
            CancellationToken cancellationToken)
        {
            if (!state.TryGetCurrentHandle(out Nfs41ResolvedHandle? currentHandle) || currentHandle is null)
            {
                return new nfs_resop4
                {
                    resop = nfs_opnum4.OP_OPEN,
                    opopen = new OPEN4res { status = nfsstat4.NFS4ERR_NOFILEHANDLE },
                };
            }

            if (currentHandle.PathInfo.Kind != NfsPathKind.Directory)
            {
                return new nfs_resop4
                {
                    resop = nfs_opnum4.OP_OPEN,
                    opopen = new OPEN4res { status = nfsstat4.NFS4ERR_NOTDIR },
                };
            }

            if (currentHandle.Export.ReadOnly)
            {
                opentype4? openType = arguments.openhow?.opentype;
                if (openType == opentype4.OPEN4_CREATE || (arguments.share_access & (uint)Nfs41Constants.OPEN4_SHARE_ACCESS_WRITE) != 0)
                {
                    return new nfs_resop4
                    {
                        resop = nfs_opnum4.OP_OPEN,
                        opopen = new OPEN4res { status = nfsstat4.NFS4ERR_ROFS },
                    };
                }
            }

            if (context.CurrentSession is null
                || arguments.owner?.Value?.clientid is null
                || arguments.owner.Value.clientid.Value != context.CurrentSession.ClientId
                || arguments.owner.Value.owner is null
                || arguments.owner.Value.owner.Length == 0)
            {
                return new nfs_resop4
                {
                    resop = nfs_opnum4.OP_OPEN,
                    opopen = new OPEN4res { status = nfsstat4.NFS4ERR_BADOWNER },
                };
            }

            if (arguments.claim?.claim != open_claim_type4.CLAIM_NULL)
            {
                return new nfs_resop4
                {
                    resop = nfs_opnum4.OP_OPEN,
                    opopen = new OPEN4res { status = nfsstat4.NFS4ERR_NOTSUPP },
                };
            }

            string? entryName = TryDecodeComponent(arguments.claim.file);
            if (string.IsNullOrWhiteSpace(entryName) || arguments.openhow?.opentype is null)
            {
                return new nfs_resop4
                {
                    resop = nfs_opnum4.OP_OPEN,
                    opopen = new OPEN4res { status = nfsstat4.NFS4ERR_INVAL },
                };
            }

            NfsPathInfo pathInfo;
            switch (arguments.openhow.opentype.Value)
            {
                case opentype4.OPEN4_NOCREATE:
                    {
                        NfsLookupPathResponse lookupResponse = await server.Settings.InstrumentedFileSystem.LookupPathAsync(
                            new NfsLookupPathRequest(currentHandle.Target.SourcePath, entryName, cancellationToken)).ConfigureAwait(false);
                        if (!lookupResponse.PathInfo.Exists)
                        {
                            return new nfs_resop4
                            {
                                resop = nfs_opnum4.OP_OPEN,
                                opopen = new OPEN4res { status = nfsstat4.NFS4ERR_NOENT },
                            };
                        }

                        if (lookupResponse.PathInfo.Kind == NfsPathKind.Directory)
                        {
                            return new nfs_resop4
                            {
                                resop = nfs_opnum4.OP_OPEN,
                                opopen = new OPEN4res { status = nfsstat4.NFS4ERR_ISDIR },
                            };
                        }

                        pathInfo = lookupResponse.PathInfo;
                        break;
                    }

                case opentype4.OPEN4_CREATE:
                    {
                        createmode4? createMode = arguments.openhow.how?.mode;
                        if (createMode is null)
                        {
                            return new nfs_resop4
                            {
                                resop = nfs_opnum4.OP_OPEN,
                                opopen = new OPEN4res { status = nfsstat4.NFS4ERR_INVAL },
                            };
                        }

                        if (createMode != createmode4.UNCHECKED4 && createMode != createmode4.GUARDED4)
                        {
                            return new nfs_resop4
                            {
                                resop = nfs_opnum4.OP_OPEN,
                                opopen = new OPEN4res { status = nfsstat4.NFS4ERR_NOTSUPP },
                            };
                        }

                        NfsCreatePathResponse createResponse = await server.Settings.InstrumentedFileSystem.CreatePathAsync(
                            new NfsCreatePathRequest(
                                currentHandle.Target.SourcePath,
                                entryName,
                                NfsPathKind.File,
                                failIfExists: createMode == createmode4.GUARDED4,
                                cancellationToken)).ConfigureAwait(false);

                        if (!createResponse.CreatedNew && createMode == createmode4.GUARDED4)
                        {
                            return new nfs_resop4
                            {
                                resop = nfs_opnum4.OP_OPEN,
                                opopen = new OPEN4res { status = nfsstat4.NFS4ERR_EXIST },
                            };
                        }

                        if (createResponse.PathInfo.Kind == NfsPathKind.Directory)
                        {
                            return new nfs_resop4
                            {
                                resop = nfs_opnum4.OP_OPEN,
                                opopen = new OPEN4res { status = nfsstat4.NFS4ERR_ISDIR },
                            };
                        }

                        pathInfo = createResponse.PathInfo;
                        break;
                    }

                default:
                    return new nfs_resop4
                    {
                        resop = nfs_opnum4.OP_OPEN,
                        opopen = new OPEN4res { status = nfsstat4.NFS4ERR_NOTSUPP },
                    };
            }

            if (!pathInfo.Exists)
            {
                return new nfs_resop4
                {
                    resop = nfs_opnum4.OP_OPEN,
                    opopen = new OPEN4res { status = nfsstat4.NFS4ERR_NOENT },
                };
            }

            Nfs41ResolvedHandle resolvedHandle = await CreateResolvedHandleAsync(
                currentHandle.Export,
                new NfsFileHandleTarget(currentHandle.Target.ExportPath, pathInfo.Path),
                cancellationToken).ConfigureAwait(false);
            state.SetCurrentHandle(resolvedHandle);

            stateid4 stateId = openStateTable.Register(resolvedHandle, arguments.seqid?.Value ?? 1U);
            change_info4 changeInfo = CreateChangeInfo(resolvedHandle.PathInfo);

            return new nfs_resop4
            {
                resop = nfs_opnum4.OP_OPEN,
                opopen = new OPEN4res
                {
                    status = nfsstat4.NFS4_OK,
                    resok4 = new OPEN4resok
                    {
                        stateid = stateId,
                        cinfo = changeInfo,
                        rflags = 0,
                        attrset = new bitmap4 { Value = Array.Empty<uint>() },
                        delegation = new open_delegation4
                        {
                            delegation_type = open_delegation_type4.OPEN_DELEGATE_NONE,
                        },
                    },
                },
            };
        }

        private async Task<nfs_resop4> HandleReadAsync(READ4args arguments, Nfs41CompoundState state)
        {
            if (!TryGetReadableOpenHandle(state, arguments.stateid, out Nfs41ResolvedHandle? currentHandle, out nfs_resop4 failureResult))
            {
                return failureResult;
            }

            NfsReadFileResponse readResponse = await server.Settings.InstrumentedFileSystem.ReadFileAsync(
                new NfsReadFileRequest(
                    currentHandle!.Target.SourcePath,
                    arguments.offset?.Value ?? 0UL,
                    arguments.count?.Value ?? 0U)).ConfigureAwait(false);

            if (!readResponse.Found)
            {
                return new nfs_resop4
                {
                    resop = nfs_opnum4.OP_READ,
                    opread = new READ4res { status = nfsstat4.NFS4ERR_NOENT },
                };
            }

            return new nfs_resop4
            {
                resop = nfs_opnum4.OP_READ,
                opread = new READ4res
                {
                    status = nfsstat4.NFS4_OK,
                    resok4 = new READ4resok
                    {
                        eof = readResponse.EndOfFile,
                        data = readResponse.Data.ToArray(),
                    },
                },
            };
        }

        private async Task<nfs_resop4> HandleWriteAsync(WRITE4args arguments, Nfs41CompoundState state)
        {
            if (!TryGetReadableOpenHandle(state, arguments.stateid, out Nfs41ResolvedHandle? currentHandle, out nfs_resop4 failureResult))
            {
                return failureResult;
            }

            if (currentHandle!.Export.ReadOnly)
            {
                return new nfs_resop4
                {
                    resop = nfs_opnum4.OP_WRITE,
                    opwrite = new WRITE4res { status = nfsstat4.NFS4ERR_ROFS },
                };
            }

            NfsWriteFileResponse writeResponse = await server.Settings.InstrumentedFileSystem.WriteFileAsync(
                new NfsWriteFileRequest(
                    currentHandle.Target.SourcePath,
                    arguments.offset?.Value ?? 0UL,
                    arguments.data ?? Array.Empty<byte>(),
                    MapStability(arguments.stable))).ConfigureAwait(false);

            if (!writeResponse.PathInfo.Exists)
            {
                return new nfs_resop4
                {
                    resop = nfs_opnum4.OP_WRITE,
                    opwrite = new WRITE4res { status = nfsstat4.NFS4ERR_NOENT },
                };
            }

            return new nfs_resop4
            {
                resop = nfs_opnum4.OP_WRITE,
                opwrite = new WRITE4res
                {
                    status = nfsstat4.NFS4_OK,
                    resok4 = new WRITE4resok
                    {
                        count = new count4 { Value = writeResponse.BytesWritten },
                        committed = MapStability(writeResponse.CommittedStability),
                        writeverf = new verifier4 { Value = (byte[])WriteVerifier.Clone() },
                    },
                },
            };
        }

        private nfs_resop4 HandleClose(CLOSE4args arguments, Nfs41CompoundState state)
        {
            if (!state.TryGetCurrentHandle(out Nfs41ResolvedHandle? currentHandle) || currentHandle is null)
            {
                return new nfs_resop4
                {
                    resop = nfs_opnum4.OP_CLOSE,
                    opclose = new CLOSE4res { status = nfsstat4.NFS4ERR_NOFILEHANDLE },
                };
            }

            string? statePath = GetStatePath(arguments.open_stateid);
            if (!StringComparer.Ordinal.Equals(currentHandle.Target.SourcePath, statePath)
                || !openStateTable.Close(arguments.open_stateid, out stateid4? closedStateId)
                || closedStateId is null
                )
            {
                return new nfs_resop4
                {
                    resop = nfs_opnum4.OP_CLOSE,
                    opclose = new CLOSE4res { status = nfsstat4.NFS4ERR_BAD_STATEID },
                };
            }

            return new nfs_resop4
            {
                resop = nfs_opnum4.OP_CLOSE,
                opclose = new CLOSE4res
                {
                    status = nfsstat4.NFS4_OK,
                    open_stateid = closedStateId,
                },
            };
        }

        private async Task<nfs_resop4> HandleRemoveAsync(
            REMOVE4args arguments,
            Nfs41CompoundState state,
            CancellationToken cancellationToken)
        {
            if (!state.TryGetCurrentHandle(out Nfs41ResolvedHandle? currentHandle) || currentHandle is null)
            {
                return new nfs_resop4
                {
                    resop = nfs_opnum4.OP_REMOVE,
                    opremove = new REMOVE4res { status = nfsstat4.NFS4ERR_NOFILEHANDLE },
                };
            }

            if (currentHandle.PathInfo.Kind != NfsPathKind.Directory)
            {
                return new nfs_resop4
                {
                    resop = nfs_opnum4.OP_REMOVE,
                    opremove = new REMOVE4res { status = nfsstat4.NFS4ERR_NOTDIR },
                };
            }

            if (currentHandle.Export.ReadOnly)
            {
                return new nfs_resop4
                {
                    resop = nfs_opnum4.OP_REMOVE,
                    opremove = new REMOVE4res { status = nfsstat4.NFS4ERR_ROFS },
                };
            }

            string? entryName = TryDecodeComponent(arguments.target);
            if (string.IsNullOrWhiteSpace(entryName))
            {
                return new nfs_resop4
                {
                    resop = nfs_opnum4.OP_REMOVE,
                    opremove = new REMOVE4res { status = nfsstat4.NFS4ERR_INVAL },
                };
            }

            NfsLookupPathResponse lookupResponse = await server.Settings.InstrumentedFileSystem.LookupPathAsync(
                new NfsLookupPathRequest(currentHandle.Target.SourcePath, entryName, cancellationToken)).ConfigureAwait(false);
            if (!lookupResponse.PathInfo.Exists)
            {
                return new nfs_resop4
                {
                    resop = nfs_opnum4.OP_REMOVE,
                    opremove = new REMOVE4res { status = nfsstat4.NFS4ERR_NOENT },
                };
            }

            await server.Settings.InstrumentedFileSystem.DeletePathAsync(
                new NfsDeletePathRequest(
                    currentHandle.Target.SourcePath,
                    entryName,
                    lookupResponse.PathInfo.Kind,
                    cancellationToken)).ConfigureAwait(false);

            return new nfs_resop4
            {
                resop = nfs_opnum4.OP_REMOVE,
                opremove = new REMOVE4res
                {
                    status = nfsstat4.NFS4_OK,
                    resok4 = new REMOVE4resok
                    {
                        cinfo = CreateChangeInfo(currentHandle.PathInfo),
                    },
                },
            };
        }

        private bool TryGetReadableOpenHandle(
            Nfs41CompoundState state,
            stateid4? stateId,
            out Nfs41ResolvedHandle? currentHandle,
            out nfs_resop4 failureResult)
        {
            if (!state.TryGetCurrentHandle(out currentHandle) || currentHandle is null)
            {
                failureResult = CreateNoFileHandleResult(nfs_opnum4.OP_READ);
                return false;
            }

            if (currentHandle.PathInfo.Kind == NfsPathKind.Directory)
            {
                failureResult = new nfs_resop4
                {
                    resop = nfs_opnum4.OP_READ,
                    opread = new READ4res { status = nfsstat4.NFS4ERR_ISDIR },
                };
                return false;
            }

            if (!openStateTable.TryGet(stateId, out Nfs41OpenState? openState)
                || openState is null
                || !StringComparer.Ordinal.Equals(openState.Handle.Target.SourcePath, currentHandle.Target.SourcePath))
            {
                failureResult = new nfs_resop4
                {
                    resop = nfs_opnum4.OP_READ,
                    opread = new READ4res { status = nfsstat4.NFS4ERR_BAD_STATEID },
                };
                return false;
            }

            failureResult = new nfs_resop4 { resop = nfs_opnum4.OP_READ };
            return true;
        }

        private string? GetStatePath(stateid4? stateId)
        {
            return openStateTable.TryGet(stateId, out Nfs41OpenState? openState) && openState is not null
                ? openState.Handle.Target.SourcePath
                : null;
        }

        private async Task<Nfs41ResolvedHandle> CreateResolvedHandleAsync(
            OpenNfsExportDefinition export,
            NfsFileHandleTarget target,
            CancellationToken cancellationToken)
        {
            NfsFileHandle fileHandle = await server.CreateFileHandleAsync(target, cancellationToken).ConfigureAwait(false);
            NfsGetPathInfoResponse pathInfoResponse = await server.Settings.InstrumentedFileSystem.GetPathInfoAsync(
                new NfsGetPathInfoRequest(target.SourcePath, cancellationToken)).ConfigureAwait(false);
            return new Nfs41ResolvedHandle(export, fileHandle, target, pathInfoResponse.PathInfo);
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

        private static TryCreateAttributesResult TryCreateAttributes(Nfs41ResolvedHandle resolvedHandle, bitmap4? requestedAttributes)
        {
            List<ulong> attributeIds = GetRequestedAttributeIds(requestedAttributes);
            for (int index = 0; index < attributeIds.Count; index++)
            {
                if (!IsSupportedAttribute(attributeIds[index]))
                {
                    return new TryCreateAttributesResult(null, nfsstat4.NFS4ERR_ATTRNOTSUPP);
                }
            }

            XdrWriter writer = new XdrWriter();
            for (int index = 0; index < attributeIds.Count; index++)
            {
                WriteAttributeValue(writer, attributeIds[index], resolvedHandle);
            }

            return new TryCreateAttributesResult(
                new fattr4
                {
                    attrmask = CreateBitmap(attributeIds),
                    attr_vals = new attrlist4 { Value = writer.ToArray() },
                },
                nfsstat4.NFS4_OK);
        }

        private static List<ulong> GetRequestedAttributeIds(bitmap4? bitmap)
        {
            List<ulong> ids = new List<ulong>();
            uint[] words = bitmap?.Value ?? Array.Empty<uint>();
            for (int wordIndex = 0; wordIndex < words.Length; wordIndex++)
            {
                uint word = words[wordIndex];
                for (int bitIndex = 0; bitIndex < 32; bitIndex++)
                {
                    if ((word & (1u << bitIndex)) != 0)
                    {
                        ids.Add((ulong)(wordIndex * 32 + bitIndex));
                    }
                }
            }

            return ids;
        }

        private static bool IsSupportedAttribute(ulong attributeId)
        {
            return attributeId == Nfs41Constants.FATTR4_TYPE
                || attributeId == Nfs41Constants.FATTR4_SIZE
                || attributeId == Nfs41Constants.FATTR4_FILEID
                || attributeId == Nfs41Constants.FATTR4_MODE
                || attributeId == Nfs41Constants.FATTR4_NUMLINKS
                || attributeId == Nfs41Constants.FATTR4_OWNER
                || attributeId == Nfs41Constants.FATTR4_OWNER_GROUP
                || attributeId == Nfs41Constants.FATTR4_TIME_ACCESS
                || attributeId == Nfs41Constants.FATTR4_TIME_METADATA
                || attributeId == Nfs41Constants.FATTR4_TIME_MODIFY;
        }

        private static void WriteAttributeValue(XdrWriter writer, ulong attributeId, Nfs41ResolvedHandle resolvedHandle)
        {
            ulong fileId = CreateFileId(resolvedHandle.Target.SourcePath);
            switch (attributeId)
            {
                case Nfs41Constants.FATTR4_TYPE:
                    new fattr4_type { Value = MapPathKind(resolvedHandle.PathInfo.Kind) }.WriteTo(writer);
                    break;
                case Nfs41Constants.FATTR4_SIZE:
                    new fattr4_size { Value = resolvedHandle.PathInfo.Length }.WriteTo(writer);
                    break;
                case Nfs41Constants.FATTR4_FILEID:
                    new fattr4_fileid { Value = fileId }.WriteTo(writer);
                    break;
                case Nfs41Constants.FATTR4_MODE:
                    new fattr4_mode { Value = new mode4 { Value = resolvedHandle.PathInfo.Mode ?? CreateMode(resolvedHandle.PathInfo.Kind) } }.WriteTo(writer);
                    break;
                case Nfs41Constants.FATTR4_NUMLINKS:
                    new fattr4_numlinks { Value = resolvedHandle.PathInfo.Kind == NfsPathKind.Directory ? 2U : 1U }.WriteTo(writer);
                    break;
                case Nfs41Constants.FATTR4_OWNER:
                    new fattr4_owner { Value = CreateUtf8Mixed("root") }.WriteTo(writer);
                    break;
                case Nfs41Constants.FATTR4_OWNER_GROUP:
                    new fattr4_owner_group { Value = CreateUtf8Mixed("root") }.WriteTo(writer);
                    break;
                case Nfs41Constants.FATTR4_TIME_ACCESS:
                    new fattr4_time_access { Value = CreateNfsTime(resolvedHandle.PathInfo.AccessTimeUtc) }.WriteTo(writer);
                    break;
                case Nfs41Constants.FATTR4_TIME_METADATA:
                    new fattr4_time_metadata { Value = CreateNfsTime(resolvedHandle.PathInfo.ChangeTimeUtc) }.WriteTo(writer);
                    break;
                case Nfs41Constants.FATTR4_TIME_MODIFY:
                    new fattr4_time_modify { Value = CreateNfsTime(resolvedHandle.PathInfo.ModificationTimeUtc) }.WriteTo(writer);
                    break;
                default:
                    throw new NotSupportedException(
                        "Requested NFSv4.1 attribute " + attributeId.ToString(CultureInfo.InvariantCulture) + " is not supported by the current direct-server attribute encoder.");
            }
        }

        private static bitmap4 CreateBitmap(IReadOnlyList<ulong> attributeIds)
        {
            if (attributeIds.Count == 0)
            {
                return new bitmap4 { Value = Array.Empty<uint>() };
            }

            ulong highest = 0;
            for (int index = 0; index < attributeIds.Count; index++)
            {
                if (attributeIds[index] > highest)
                {
                    highest = attributeIds[index];
                }
            }

            uint[] words = new uint[(highest / 32UL) + 1UL];
            for (int index = 0; index < attributeIds.Count; index++)
            {
                int wordIndex = (int)(attributeIds[index] / 32UL);
                int bitIndex = (int)(attributeIds[index] % 32UL);
                words[wordIndex] |= 1u << bitIndex;
            }

            return new bitmap4 { Value = words };
        }

        private static utf8str_mixed CreateUtf8Mixed(string value)
        {
            return new utf8str_mixed
            {
                Value = new utf8string { Value = Encoding.UTF8.GetBytes(value ?? string.Empty) },
            };
        }

        private static nfstime4 CreateNfsTime(DateTimeOffset? value)
        {
            DateTimeOffset timestamp = value ?? DateTimeOffset.UtcNow;
            long seconds = timestamp.ToUnixTimeSeconds();
            long tickRemainder = (timestamp.UtcDateTime.Ticks % TimeSpan.TicksPerSecond + TimeSpan.TicksPerSecond) % TimeSpan.TicksPerSecond;
            uint nanoseconds = (uint)(tickRemainder * 100L);

            return new nfstime4
            {
                seconds = seconds,
                nseconds = nanoseconds,
            };
        }

        private static change_info4 CreateChangeInfo(NfsPathInfo pathInfo)
        {
            ulong changeId = CreateChangeId(pathInfo);
            return new change_info4
            {
                atomic = false,
                before = new changeid4 { Value = changeId },
                after = new changeid4 { Value = changeId },
            };
        }

        private static ulong CreateChangeId(NfsPathInfo pathInfo)
        {
            DateTimeOffset timestamp = pathInfo.ChangeTimeUtc
                ?? pathInfo.ModificationTimeUtc
                ?? pathInfo.AccessTimeUtc
                ?? DateTimeOffset.UtcNow;
            return unchecked((ulong)timestamp.UtcTicks) ^ pathInfo.Length;
        }

        private static ulong CreateFileId(string sourcePath)
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(sourcePath ?? string.Empty));
            return System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian(hash.AsSpan(0, 8));
        }

        private static uint CreateMode(NfsPathKind pathKind)
        {
            return pathKind switch
            {
                NfsPathKind.Directory => 0x1EDU,
                NfsPathKind.SymbolicLink => 0x1FFU,
                _ => 0x1A4U,
            };
        }

        private static nfs_ftype4 MapPathKind(NfsPathKind pathKind)
        {
            return pathKind switch
            {
                NfsPathKind.Directory => nfs_ftype4.NF4DIR,
                NfsPathKind.SymbolicLink => nfs_ftype4.NF4LNK,
                _ => nfs_ftype4.NF4REG,
            };
        }

        private static byte[] CreateDirectoryVerifier(string value)
        {
            byte[] verifier = new byte[8];
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty));
            Buffer.BlockCopy(hash, 0, verifier, 0, verifier.Length);
            return verifier;
        }

        private static byte[] CreateWriteVerifier()
        {
            byte[] verifier = new byte[8];
            RandomNumberGenerator.Fill(verifier);
            return verifier;
        }

        private static component4 CreateComponent(string value)
        {
            return new component4
            {
                Value = new utf8str_cs
                {
                    Value = new utf8string { Value = Encoding.UTF8.GetBytes(value ?? string.Empty) },
                },
            };
        }

        private static nfs_resop4 CreateNoFileHandleResult(nfs_opnum4 opnum)
        {
            return opnum switch
            {
                nfs_opnum4.OP_GETATTR => new nfs_resop4 { resop = opnum, opgetattr = new GETATTR4res { status = nfsstat4.NFS4ERR_NOFILEHANDLE } },
                nfs_opnum4.OP_READDIR => new nfs_resop4 { resop = opnum, opreaddir = new READDIR4res { status = nfsstat4.NFS4ERR_NOFILEHANDLE } },
                nfs_opnum4.OP_OPEN => new nfs_resop4 { resop = opnum, opopen = new OPEN4res { status = nfsstat4.NFS4ERR_NOFILEHANDLE } },
                nfs_opnum4.OP_READ => new nfs_resop4 { resop = opnum, opread = new READ4res { status = nfsstat4.NFS4ERR_NOFILEHANDLE } },
                nfs_opnum4.OP_WRITE => new nfs_resop4 { resop = opnum, opwrite = new WRITE4res { status = nfsstat4.NFS4ERR_NOFILEHANDLE } },
                nfs_opnum4.OP_CLOSE => new nfs_resop4 { resop = opnum, opclose = new CLOSE4res { status = nfsstat4.NFS4ERR_NOFILEHANDLE } },
                nfs_opnum4.OP_REMOVE => new nfs_resop4 { resop = opnum, opremove = new REMOVE4res { status = nfsstat4.NFS4ERR_NOFILEHANDLE } },
                _ => new nfs_resop4 { resop = nfs_opnum4.OP_ILLEGAL, opillegal = new ILLEGAL4res { status = nfsstat4.NFS4ERR_NOFILEHANDLE } },
            };
        }

        private static NfsWriteStability MapStability(stable_how4? value)
        {
            return value switch
            {
                stable_how4.DATA_SYNC4 => NfsWriteStability.DataSync,
                stable_how4.FILE_SYNC4 => NfsWriteStability.FileSync,
                _ => NfsWriteStability.Unstable,
            };
        }

        private static stable_how4 MapStability(NfsWriteStability value)
        {
            return value switch
            {
                NfsWriteStability.DataSync => stable_how4.DATA_SYNC4,
                NfsWriteStability.FileSync => stable_how4.FILE_SYNC4,
                _ => stable_how4.UNSTABLE4,
            };
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
            return result.resop switch
            {
                nfs_opnum4.OP_EXCHANGE_ID => result.opexchange_id?.eir_status,
                nfs_opnum4.OP_CREATE_SESSION => result.opcreate_session?.csr_status,
                nfs_opnum4.OP_DESTROY_SESSION => result.opdestroy_session?.dsr_status,
                nfs_opnum4.OP_DESTROY_CLIENTID => result.opdestroy_clientid?.dcr_status,
                nfs_opnum4.OP_BIND_CONN_TO_SESSION => result.opbind_conn_to_session?.bctsr_status,
                nfs_opnum4.OP_SEQUENCE => result.opsequence?.sr_status,
                nfs_opnum4.OP_PUTROOTFH => result.opputrootfh?.status,
                nfs_opnum4.OP_LOOKUP => result.oplookup?.status,
                nfs_opnum4.OP_GETATTR => result.opgetattr?.status,
                nfs_opnum4.OP_READDIR => result.opreaddir?.status,
                nfs_opnum4.OP_OPEN => result.opopen?.status,
                nfs_opnum4.OP_READ => result.opread?.status,
                nfs_opnum4.OP_WRITE => result.opwrite?.status,
                nfs_opnum4.OP_CLOSE => result.opclose?.status,
                nfs_opnum4.OP_REMOVE => result.opremove?.status,
                nfs_opnum4.OP_ILLEGAL => result.opillegal?.status,
                _ => null,
            };
        }

        private readonly record struct TryCreateAttributesResult(fattr4? Attributes, nfsstat4 Status);

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
