namespace OpenNFS.Rpc.Telemetry
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A weak-reference registry of live objects that observable gauges sample at collection time. Registration never
    /// keeps an object alive, so a disposed server or client simply drops out of the gauges once collected.
    /// </summary>
    /// <typeparam name="T">The registered object type.</typeparam>
    /// <remarks>Thread safe.</remarks>
    internal sealed class OpenNfsTelemetryRegistry<T>
        where T : class
    {
        private readonly object _SyncRoot = new object();
        private readonly List<WeakReference<T>> _Entries = new List<WeakReference<T>>();

        internal void Register(T instance)
        {
            ArgumentNullException.ThrowIfNull(instance);
            lock (_SyncRoot)
            {
                _Entries.Add(new WeakReference<T>(instance));
            }
        }

        internal void Unregister(T instance)
        {
            ArgumentNullException.ThrowIfNull(instance);
            lock (_SyncRoot)
            {
                _Entries.RemoveAll(entry => !entry.TryGetTarget(out T? target) || ReferenceEquals(target, instance));
            }
        }

        internal List<T> Snapshot()
        {
            List<T> live = new List<T>();
            lock (_SyncRoot)
            {
                _Entries.RemoveAll(entry => !entry.TryGetTarget(out _));
                foreach (WeakReference<T> entry in _Entries)
                {
                    if (entry.TryGetTarget(out T? target))
                    {
                        live.Add(target);
                    }
                }
            }

            return live;
        }
    }
}
