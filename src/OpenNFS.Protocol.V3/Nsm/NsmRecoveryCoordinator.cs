namespace OpenNFS.Protocol.V3.Nsm
{
    using System;
    using System.Collections.Generic;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Protocol.V3.Nsm.Callbacks;
    using OpenNFS.Server;

    internal sealed class NsmRecoveryCoordinator
    {
        private readonly Func<DateTimeOffset> _utcNow;
        private readonly Dictionary<string, RegisteredMonitor> _registeredMonitors;
        private readonly Dictionary<string, int> _remoteHostStates;
        private readonly TimeSpan _gracePeriodDuration;
        private readonly object _syncRoot;
        private DateTimeOffset? _gracePeriodEndsUtc;
        private int _localState;

        internal NsmRecoveryCoordinator(
            TimeSpan? gracePeriodDuration = null,
            Func<DateTimeOffset>? utcNow = null)
        {
            TimeSpan resolvedGracePeriodDuration = gracePeriodDuration ?? TimeSpan.FromMinutes(90);
            if (resolvedGracePeriodDuration <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(gracePeriodDuration),
                    resolvedGracePeriodDuration,
                    "The NSM grace period must be greater than zero.");
            }

            _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
            _gracePeriodDuration = resolvedGracePeriodDuration;
            _registeredMonitors = new Dictionary<string, RegisteredMonitor>(StringComparer.Ordinal);
            _remoteHostStates = new Dictionary<string, int>(StringComparer.Ordinal);
            _syncRoot = new object();
            _localState = 1;
        }

        internal int GetStateForHost(string monitoredHostName)
        {
            if (string.IsNullOrWhiteSpace(monitoredHostName))
            {
                throw new ArgumentException("The monitored host name must be non-empty.", nameof(monitoredHostName));
            }

            lock (_syncRoot)
            {
                return _remoteHostStates.TryGetValue(monitoredHostName, out int remoteState)
                    ? remoteState
                    : _localState;
            }
        }

        internal bool IsGracePeriodActive()
        {
            DateTimeOffset now = _utcNow();
            lock (_syncRoot)
            {
                if (!_gracePeriodEndsUtc.HasValue)
                {
                    return false;
                }

                if (_gracePeriodEndsUtc.Value <= now)
                {
                    _gracePeriodEndsUtc = null;
                    return false;
                }

                return true;
            }
        }

        internal bool ShouldDenyDuringGracePeriod(NfsLockOperation operation, bool reclaim)
        {
            if (!IsGracePeriodActive())
            {
                return false;
            }

            return operation == NfsLockOperation.Test
                || (operation == NfsLockOperation.Lock && !reclaim);
        }

        internal sm_stat_res RegisterMonitor(mon monitor)
        {
            ArgumentNullException.ThrowIfNull(monitor);
            mon_id monitorIdentity = monitor.mon_id
                ?? throw new ArgumentException("The NSM monitor payload omitted mon_id.", nameof(monitor));
            my_id callbackIdentity = monitorIdentity.my_id
                ?? throw new ArgumentException("The NSM monitor payload omitted mon_id.my_id.", nameof(monitor));
            string monitoredHostName = RequireHostName(monitorIdentity.mon_name, "mon.mon_id.mon_name");
            string callbackHostName = RequireHostName(callbackIdentity.my_name, "mon.mon_id.my_id.my_name");
            byte[] privateData = RequireFixedPrivateData(monitor.priv, "mon.priv");

            RegisteredMonitor registration = new RegisteredMonitor(
                monitoredHostName,
                callbackHostName,
                callbackIdentity.my_prog,
                callbackIdentity.my_vers,
                callbackIdentity.my_proc,
                privateData);

            lock (_syncRoot)
            {
                _registeredMonitors[registration.Key] = registration;
                return CreateSuccessStatus(_localState);
            }
        }

        internal sm_stat UnregisterMonitor(mon_id monitorIdentity)
        {
            ArgumentNullException.ThrowIfNull(monitorIdentity);
            my_id callbackIdentity = monitorIdentity.my_id
                ?? throw new ArgumentException("The NSM unmonitor payload omitted my_id.", nameof(monitorIdentity));
            string monitoredHostName = RequireHostName(monitorIdentity.mon_name, "mon_id.mon_name");
            string callbackHostName = RequireHostName(callbackIdentity.my_name, "mon_id.my_id.my_name");
            string key = RegisteredMonitor.CreateKey(
                monitoredHostName,
                callbackHostName,
                callbackIdentity.my_prog,
                callbackIdentity.my_vers,
                callbackIdentity.my_proc);

            lock (_syncRoot)
            {
                _registeredMonitors.Remove(key);
                return new sm_stat
                {
                    state = _localState,
                };
            }
        }

        internal sm_stat UnregisterAll(my_id callbackIdentity)
        {
            ArgumentNullException.ThrowIfNull(callbackIdentity);
            string callbackHostName = RequireHostName(callbackIdentity.my_name, "my_id.my_name");

            lock (_syncRoot)
            {
                List<string> keysToRemove = new List<string>();
                foreach (KeyValuePair<string, RegisteredMonitor> pair in _registeredMonitors)
                {
                    RegisteredMonitor registration = pair.Value;
                    if (registration.CallbackProgramNumber == callbackIdentity.my_prog
                        && registration.CallbackVersionNumber == callbackIdentity.my_vers
                        && registration.CallbackProcedureNumber == callbackIdentity.my_proc
                        && string.Equals(registration.CallbackHostName, callbackHostName, StringComparison.Ordinal))
                    {
                        keysToRemove.Add(pair.Key);
                    }
                }

                for (int index = 0; index < keysToRemove.Count; index++)
                {
                    _registeredMonitors.Remove(keysToRemove[index]);
                }

                return new sm_stat
                {
                    state = _localState,
                };
            }
        }

        internal IReadOnlyList<NsmNotificationCallback> HandleRemoteNotify(stat_chge stateChange)
        {
            ArgumentNullException.ThrowIfNull(stateChange);
            string monitoredHostName = RequireHostName(stateChange.mon_name, "stat_chge.mon_name");

            lock (_syncRoot)
            {
                _remoteHostStates[monitoredHostName] = stateChange.state;
                List<NsmNotificationCallback> callbacks = new List<NsmNotificationCallback>();
                foreach (RegisteredMonitor registration in _registeredMonitors.Values)
                {
                    if (!string.Equals(registration.MonitoredHostName, monitoredHostName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    callbacks.Add(
                        new NsmNotificationCallback(
                            registration.CallbackHostName,
                            registration.CallbackProgramNumber,
                            registration.CallbackVersionNumber,
                            registration.CallbackProcedureNumber,
                            monitoredHostName,
                            stateChange.state,
                            registration.PrivateData));
                }

                return callbacks;
            }
        }

        internal void SimulateLocalCrash()
        {
            lock (_syncRoot)
            {
                _localState = unchecked(_localState + 1);
                if (_localState <= 0)
                {
                    _localState = 1;
                }

                _gracePeriodEndsUtc = _utcNow().Add(_gracePeriodDuration);
            }
        }

        private static sm_stat_res CreateSuccessStatus(int state)
        {
            return new sm_stat_res
            {
                res_stat = res.STAT_SUCC,
                state = state,
            };
        }

        private static string RequireHostName(string? value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("The NSM field '" + fieldName + "' must contain a non-empty host name.", fieldName);
            }

            return value;
        }

        private static byte[] RequireFixedPrivateData(byte[]? value, string fieldName)
        {
            if (value is null || value.Length != 16)
            {
                throw new ArgumentException("The NSM field '" + fieldName + "' must contain exactly 16 bytes.", fieldName);
            }

            return value.AsSpan().ToArray();
        }

        private sealed class RegisteredMonitor
        {
            private readonly byte[] _privateData;

            internal RegisteredMonitor(
                string monitoredHostName,
                string callbackHostName,
                int callbackProgramNumber,
                int callbackVersionNumber,
                int callbackProcedureNumber,
                byte[] privateData)
            {
                MonitoredHostName = monitoredHostName;
                CallbackHostName = callbackHostName;
                CallbackProgramNumber = callbackProgramNumber;
                CallbackVersionNumber = callbackVersionNumber;
                CallbackProcedureNumber = callbackProcedureNumber;
                _privateData = privateData.AsSpan().ToArray();
            }

            internal string MonitoredHostName { get; }

            internal string CallbackHostName { get; }

            internal int CallbackProgramNumber { get; }

            internal int CallbackVersionNumber { get; }

            internal int CallbackProcedureNumber { get; }

            internal ReadOnlyMemory<byte> PrivateData => _privateData;

            internal string Key => CreateKey(
                MonitoredHostName,
                CallbackHostName,
                CallbackProgramNumber,
                CallbackVersionNumber,
                CallbackProcedureNumber);

            internal static string CreateKey(
                string monitoredHostName,
                string callbackHostName,
                int callbackProgramNumber,
                int callbackVersionNumber,
                int callbackProcedureNumber)
            {
                return monitoredHostName
                    + "\n"
                    + callbackHostName
                    + "\n"
                    + callbackProgramNumber.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + "\n"
                    + callbackVersionNumber.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + "\n"
                    + callbackProcedureNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }
    }
}
