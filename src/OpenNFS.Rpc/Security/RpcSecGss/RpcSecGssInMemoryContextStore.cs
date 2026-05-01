namespace OpenNFS.Rpc.Security.RpcSecGss
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Provides an in-memory <see cref="IRpcSecGssContextStore"/> suitable for single-process servers and tests.
    /// </summary>
    public sealed class RpcSecGssInMemoryContextStore : IRpcSecGssContextStore
    {
        private readonly object gate;
        private readonly Dictionary<string, RpcSecGssContext> entries;

        /// <summary>
        /// Initializes a new instance of the <see cref="RpcSecGssInMemoryContextStore"/> class.
        /// </summary>
        public RpcSecGssInMemoryContextStore()
        {
            gate = new object();
            entries = new Dictionary<string, RpcSecGssContext>(StringComparer.Ordinal);
        }

        /// <inheritdoc />
        public void Register(RpcSecGssContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            string key = ToKey(context.ContextHandle);
            lock (gate)
            {
                entries[key] = context;
            }
        }

        /// <inheritdoc />
        public bool TryGet(ReadOnlyMemory<byte> contextHandle, out RpcSecGssContext? context)
        {
            string key = ToKey(contextHandle);
            lock (gate)
            {
                if (entries.TryGetValue(key, out RpcSecGssContext? entry))
                {
                    context = entry;
                    return true;
                }
            }

            context = null;
            return false;
        }

        /// <inheritdoc />
        public bool Remove(ReadOnlyMemory<byte> contextHandle)
        {
            string key = ToKey(contextHandle);
            lock (gate)
            {
                return entries.Remove(key);
            }
        }

        private static string ToKey(ReadOnlyMemory<byte> contextHandle)
        {
            return Convert.ToHexString(contextHandle.Span);
        }
    }
}
