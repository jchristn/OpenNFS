namespace OpenNFS.Protocol.V3.Nsm.Callbacks
{
    using System;

    internal sealed class NsmNotificationCallback
    {
        private readonly byte[] _privateData;

        internal NsmNotificationCallback(
            string callbackHostName,
            int callbackProgramNumber,
            int callbackVersionNumber,
            int callbackProcedureNumber,
            string monitoredHostName,
            int state,
            ReadOnlyMemory<byte> privateData)
        {
            if (string.IsNullOrWhiteSpace(callbackHostName))
            {
                throw new ArgumentException("The NSM callback target host name must be non-empty.", nameof(callbackHostName));
            }

            if (string.IsNullOrWhiteSpace(monitoredHostName))
            {
                throw new ArgumentException("The monitored host name must be non-empty.", nameof(monitoredHostName));
            }

            CallbackHostName = callbackHostName;
            CallbackProgramNumber = callbackProgramNumber;
            CallbackVersionNumber = callbackVersionNumber;
            CallbackProcedureNumber = callbackProcedureNumber;
            MonitoredHostName = monitoredHostName;
            State = state;
            _privateData = privateData.ToArray();
        }

        internal string CallbackHostName { get; }

        internal int CallbackProgramNumber { get; }

        internal int CallbackVersionNumber { get; }

        internal int CallbackProcedureNumber { get; }

        internal string MonitoredHostName { get; }

        internal int State { get; }

        internal ReadOnlyMemory<byte> PrivateData => _privateData;
    }
}
