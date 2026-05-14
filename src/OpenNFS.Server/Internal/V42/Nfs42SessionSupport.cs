namespace OpenNFS.Server.Internal.V42
{
    using System;
    using OpenNFS.Protocol.V41.Compound;
    using OpenNFS.Protocol.V41.Sessions;
    using OpenNFS.Protocol.V42.Generated;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Xdr;
    using Nfs41Generated = OpenNFS.Protocol.V41.Generated;

    internal static class Nfs42CompoundPayloadCodec
    {
        internal static RpcMessageEnvelope CreateAcceptedSuccessReply(uint xid, COMPOUND4res value)
        {
            ArgumentNullException.ThrowIfNull(value);

            XdrWriter writer = new XdrWriter();
            value.WriteTo(writer);

            return RpcMessageFactory.CreateAcceptedReply(
                xid: xid,
                status: accept_stat.SUCCESS,
                procedurePayload: writer.ToArray());
        }

        internal static byte[] EncodeCompoundResult(COMPOUND4res value)
        {
            ArgumentNullException.ThrowIfNull(value);

            XdrWriter writer = new XdrWriter();
            value.WriteTo(writer);
            return writer.ToArray();
        }

        internal static T ReadPayload<T>(ReadOnlyMemory<byte> payload, Func<XdrReader, T> readValue)
        {
            ArgumentNullException.ThrowIfNull(readValue);

            XdrReader reader = new XdrReader(payload);
            T value = readValue(reader);
            reader.EnsureFullyConsumed();
            return value;
        }
    }

    internal sealed class Nfs42OperationContext
    {
        internal Nfs42OperationContext(string connectionIdentity)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(connectionIdentity);
            ConnectionIdentity = connectionIdentity;
        }

        internal string ConnectionIdentity { get; }

        internal Nfs41Session? CurrentSession { get; set; }
    }

    internal sealed class Nfs42SequenceOutcome
    {
        private Nfs42SequenceOutcome(Nfs41SequenceOutcome innerOutcome, SEQUENCE4res result)
        {
            ArgumentNullException.ThrowIfNull(innerOutcome);
            ArgumentNullException.ThrowIfNull(result);

            InnerOutcome = innerOutcome;
            Result = result;
        }

        internal Nfs41SequenceOutcome InnerOutcome { get; }

        internal Nfs41SlotState State
        {
            get
            {
                return InnerOutcome.State;
            }
        }

        internal SEQUENCE4res Result { get; }

        internal Nfs41Session? Session
        {
            get
            {
                return InnerOutcome.Session;
            }
        }

        internal uint SlotId
        {
            get
            {
                return InnerOutcome.SlotId;
            }
        }

        internal uint SequenceId
        {
            get
            {
                return InnerOutcome.SequenceId;
            }
        }

        internal bool CacheRequested
        {
            get
            {
                return InnerOutcome.CacheRequested;
            }
        }

        internal ReadOnlyMemory<byte> CachedReply
        {
            get
            {
                return InnerOutcome.CachedReply;
            }
        }

        internal static Nfs42SequenceOutcome FromInner(Nfs41SequenceOutcome innerOutcome)
        {
            ArgumentNullException.ThrowIfNull(innerOutcome);
            return new Nfs42SequenceOutcome(
                innerOutcome,
                Nfs42WireTypeBridge.ToV42(innerOutcome.Result));
        }
    }

    internal sealed class Nfs42SessionOperationProcessor
    {
        private readonly Nfs41SessionOperationProcessor innerProcessor;

        internal Nfs42SessionOperationProcessor(Nfs41SessionOperationProcessor innerProcessor)
        {
            ArgumentNullException.ThrowIfNull(innerProcessor);
            this.innerProcessor = innerProcessor;
        }

        internal EXCHANGE_ID4res ProcessExchangeId(EXCHANGE_ID4args arguments)
        {
            ArgumentNullException.ThrowIfNull(arguments);
            Nfs41Generated.EXCHANGE_ID4res innerResult =
                innerProcessor.ProcessExchangeId(Nfs42WireTypeBridge.ToV41(arguments));
            return Nfs42WireTypeBridge.ToV42(innerResult);
        }

        internal CREATE_SESSION4res ProcessCreateSession(CREATE_SESSION4args arguments, Nfs42OperationContext context)
        {
            ArgumentNullException.ThrowIfNull(arguments);
            ArgumentNullException.ThrowIfNull(context);

            Nfs41OperationContext innerContext = new Nfs41OperationContext(context.ConnectionIdentity);
            innerContext.CurrentSession = context.CurrentSession;
            Nfs41Generated.CREATE_SESSION4res innerResult =
                innerProcessor.ProcessCreateSession(Nfs42WireTypeBridge.ToV41(arguments), innerContext);
            context.CurrentSession = innerContext.CurrentSession;
            return Nfs42WireTypeBridge.ToV42(innerResult);
        }

        internal DESTROY_SESSION4res ProcessDestroySession(DESTROY_SESSION4args arguments)
        {
            ArgumentNullException.ThrowIfNull(arguments);
            Nfs41Generated.DESTROY_SESSION4res innerResult =
                innerProcessor.ProcessDestroySession(Nfs42WireTypeBridge.ToV41(arguments));
            return Nfs42WireTypeBridge.ToV42(innerResult);
        }

        internal DESTROY_CLIENTID4res ProcessDestroyClientId(DESTROY_CLIENTID4args arguments)
        {
            ArgumentNullException.ThrowIfNull(arguments);
            Nfs41Generated.DESTROY_CLIENTID4res innerResult =
                innerProcessor.ProcessDestroyClientId(Nfs42WireTypeBridge.ToV41(arguments));
            return Nfs42WireTypeBridge.ToV42(innerResult);
        }

        internal BIND_CONN_TO_SESSION4res ProcessBindConnToSession(BIND_CONN_TO_SESSION4args arguments, Nfs42OperationContext context)
        {
            ArgumentNullException.ThrowIfNull(arguments);
            ArgumentNullException.ThrowIfNull(context);

            Nfs41OperationContext innerContext = new Nfs41OperationContext(context.ConnectionIdentity);
            innerContext.CurrentSession = context.CurrentSession;
            Nfs41Generated.BIND_CONN_TO_SESSION4res innerResult =
                innerProcessor.ProcessBindConnToSession(Nfs42WireTypeBridge.ToV41(arguments), innerContext);
            context.CurrentSession = innerContext.CurrentSession;
            return Nfs42WireTypeBridge.ToV42(innerResult);
        }

        internal Nfs42SequenceOutcome ProcessSequence(SEQUENCE4args arguments, Nfs42OperationContext context)
        {
            ArgumentNullException.ThrowIfNull(arguments);
            ArgumentNullException.ThrowIfNull(context);

            Nfs41OperationContext innerContext = new Nfs41OperationContext(context.ConnectionIdentity);
            innerContext.CurrentSession = context.CurrentSession;
            Nfs41SequenceOutcome innerOutcome =
                innerProcessor.ProcessSequence(Nfs42WireTypeBridge.ToV41(arguments), innerContext);
            context.CurrentSession = innerContext.CurrentSession;
            return Nfs42SequenceOutcome.FromInner(innerOutcome);
        }

        internal void RecordSequenceReply(Nfs42SequenceOutcome outcome, ReadOnlyMemory<byte> replyBytes)
        {
            ArgumentNullException.ThrowIfNull(outcome);
            innerProcessor.RecordSequenceReply(outcome.InnerOutcome, replyBytes);
        }
    }

    internal static class Nfs42WireTypeBridge
    {
        internal static Nfs41Generated.EXCHANGE_ID4args ToV41(EXCHANGE_ID4args value)
        {
            return Convert(value, static (source, writer) => source.WriteTo(writer), Nfs41Generated.EXCHANGE_ID4args.ReadFrom);
        }

        internal static EXCHANGE_ID4res ToV42(Nfs41Generated.EXCHANGE_ID4res value)
        {
            return Convert(value, static (source, writer) => source.WriteTo(writer), EXCHANGE_ID4res.ReadFrom);
        }

        internal static Nfs41Generated.CREATE_SESSION4args ToV41(CREATE_SESSION4args value)
        {
            return Convert(value, static (source, writer) => source.WriteTo(writer), Nfs41Generated.CREATE_SESSION4args.ReadFrom);
        }

        internal static CREATE_SESSION4res ToV42(Nfs41Generated.CREATE_SESSION4res value)
        {
            return Convert(value, static (source, writer) => source.WriteTo(writer), CREATE_SESSION4res.ReadFrom);
        }

        internal static Nfs41Generated.DESTROY_SESSION4args ToV41(DESTROY_SESSION4args value)
        {
            return Convert(value, static (source, writer) => source.WriteTo(writer), Nfs41Generated.DESTROY_SESSION4args.ReadFrom);
        }

        internal static DESTROY_SESSION4res ToV42(Nfs41Generated.DESTROY_SESSION4res value)
        {
            return Convert(value, static (source, writer) => source.WriteTo(writer), DESTROY_SESSION4res.ReadFrom);
        }

        internal static Nfs41Generated.DESTROY_CLIENTID4args ToV41(DESTROY_CLIENTID4args value)
        {
            return Convert(value, static (source, writer) => source.WriteTo(writer), Nfs41Generated.DESTROY_CLIENTID4args.ReadFrom);
        }

        internal static DESTROY_CLIENTID4res ToV42(Nfs41Generated.DESTROY_CLIENTID4res value)
        {
            return Convert(value, static (source, writer) => source.WriteTo(writer), DESTROY_CLIENTID4res.ReadFrom);
        }

        internal static Nfs41Generated.BIND_CONN_TO_SESSION4args ToV41(BIND_CONN_TO_SESSION4args value)
        {
            return Convert(value, static (source, writer) => source.WriteTo(writer), Nfs41Generated.BIND_CONN_TO_SESSION4args.ReadFrom);
        }

        internal static BIND_CONN_TO_SESSION4res ToV42(Nfs41Generated.BIND_CONN_TO_SESSION4res value)
        {
            return Convert(value, static (source, writer) => source.WriteTo(writer), BIND_CONN_TO_SESSION4res.ReadFrom);
        }

        internal static Nfs41Generated.SEQUENCE4args ToV41(SEQUENCE4args value)
        {
            return Convert(value, static (source, writer) => source.WriteTo(writer), Nfs41Generated.SEQUENCE4args.ReadFrom);
        }

        internal static SEQUENCE4res ToV42(Nfs41Generated.SEQUENCE4res value)
        {
            return Convert(value, static (source, writer) => source.WriteTo(writer), SEQUENCE4res.ReadFrom);
        }

        private static TTarget Convert<TSource, TTarget>(
            TSource value,
            Action<TSource, XdrWriter> writeValue,
            Func<XdrReader, TTarget> readValue)
        {
            ArgumentNullException.ThrowIfNull(value);
            ArgumentNullException.ThrowIfNull(writeValue);
            ArgumentNullException.ThrowIfNull(readValue);

            XdrWriter writer = new XdrWriter();
            writeValue(value, writer);

            XdrReader reader = new XdrReader(writer.ToArray());
            TTarget result = readValue(reader);
            reader.EnsureFullyConsumed();
            return result;
        }
    }
}
