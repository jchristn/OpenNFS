namespace OpenNFS.Server.FileHandles
{
    using System;
    using System.Collections.Generic;

    internal sealed class PersistentHandleProviderState
    {
        public Dictionary<string, string> IdentityToHandle { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

        public Dictionary<string, PersistentHandleTargetRecord> HandleToTarget { get; set; } = new Dictionary<string, PersistentHandleTargetRecord>(StringComparer.Ordinal);

        internal void EnsureInitialized()
        {
            IdentityToHandle ??= new Dictionary<string, string>(StringComparer.Ordinal);
            HandleToTarget ??= new Dictionary<string, PersistentHandleTargetRecord>(StringComparer.Ordinal);
        }
    }
}
