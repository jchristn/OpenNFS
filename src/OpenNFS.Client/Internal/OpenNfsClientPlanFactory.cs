namespace OpenNFS.Client.Internal
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client.Compound;
    using OpenNFS.Client.Raw;

    internal sealed class OpenNfsClientPlanFactory
    {
        private readonly OpenNfsClientLifetime _lifetime;
        private readonly OpenNfsClientSettings _settings;

        internal OpenNfsClientPlanFactory(
            OpenNfsClientSettings settings,
            OpenNfsClientLifetime lifetime)
        {
            _settings = settings;
            _lifetime = lifetime;
        }

        internal Task<OpenNfsCompoundPlan> PrepareCompoundAsync(
            OpenNfsCompoundRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();
            _lifetime.ThrowIfOperationUnavailable();

            return Task.FromResult(new OpenNfsCompoundPlan(
                protocolVersion: request.ProtocolVersion,
                tag: request.Tag,
                operations: request.Operations,
                transportPolicy: OpenNfsClientTransportPolicy.TcpOnly,
                authenticationFlavor: _settings.AuthenticationFlavor,
                retryMode: request.RetryMode,
                candidateEndpoints: _settings.CandidateEndpoints,
                retryPlan: BuildRetryPlan(request.RetryMode)));
        }

        internal Task<OpenNfsV3ProcedurePlan> PrepareMountV3ProcedureAsync(
            OpenNfsV3ProcedureRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();
            _lifetime.ThrowIfOperationUnavailable();

            IReadOnlyCollection<OpenNfsEndpoint> candidateEndpoints = HasDedicatedMountEndpoint()
                ? new OpenNfsEndpoint[] { _settings.MountEndpoint }
                : _settings.CandidateEndpoints;

            return Task.FromResult(new OpenNfsV3ProcedurePlan(
                programNumber: request.ProgramNumber,
                versionNumber: request.VersionNumber,
                procedureNumber: request.ProcedureNumber,
                procedurePayload: request.ProcedurePayload.ToArray(),
                transportPolicy: _settings.TransportPolicy,
                authenticationFlavor: _settings.AuthenticationFlavor,
                retryMode: request.RetryMode,
                candidateEndpoints: candidateEndpoints,
                retryPlan: BuildRetryPlan(request.RetryMode)));
        }

        internal Task<OpenNfsV3ProcedurePlan> PrepareV3ProcedureAsync(
            OpenNfsV3ProcedureRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();
            _lifetime.ThrowIfOperationUnavailable();

            return Task.FromResult(new OpenNfsV3ProcedurePlan(
                programNumber: request.ProgramNumber,
                versionNumber: request.VersionNumber,
                procedureNumber: request.ProcedureNumber,
                procedurePayload: request.ProcedurePayload.ToArray(),
                transportPolicy: _settings.TransportPolicy,
                authenticationFlavor: _settings.AuthenticationFlavor,
                retryMode: request.RetryMode,
                candidateEndpoints: _settings.CandidateEndpoints,
                retryPlan: BuildRetryPlan(request.RetryMode)));
        }

        private OpenNfsRetryPlanStep[] BuildRetryPlan(OpenNfsRetryMode retryMode)
        {
            if (retryMode == OpenNfsRetryMode.SingleAttemptOnly)
            {
                return new OpenNfsRetryPlanStep[]
                {
                    new OpenNfsRetryPlanStep(1, TimeSpan.Zero),
                };
            }

            OpenNfsRetryPlanStep[] retryPlan = new OpenNfsRetryPlanStep[_settings.RetryPolicy.MaximumAttempts];
            retryPlan[0] = new OpenNfsRetryPlanStep(1, TimeSpan.Zero);

            for (int attemptNumber = 2; attemptNumber <= _settings.RetryPolicy.MaximumAttempts; attemptNumber++)
            {
                retryPlan[attemptNumber - 1] =
                    new OpenNfsRetryPlanStep(
                        attemptNumber,
                        _settings.RetryPolicy.GetDelayForRetry(attemptNumber - 1));
            }

            return retryPlan;
        }

        private bool HasDedicatedMountEndpoint()
        {
            return _settings.HasExplicitMountEndpoint
                && (!string.Equals(_settings.MountEndpoint.Host, _settings.PrimaryEndpoint.Host, StringComparison.OrdinalIgnoreCase)
                || _settings.MountEndpoint.Port != _settings.PrimaryEndpoint.Port);
        }
    }
}
