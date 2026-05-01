namespace OpenNFS.Server
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.Runtime.ExceptionServices;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    /// <summary>
    /// Public runnable application surface that hides the current version-specific hosting internals.
    /// </summary>
    public sealed class OpenNfsServerApplication : IAsyncDisposable
    {
        private const string NfsV3AssemblyName = "OpenNFS.Protocol.V3";
        private const string NfsV3HostTypeName = "OpenNFS.Protocol.V3.Hosting.OpenNfsTcpServerHost";
        private const string Nfs40AssemblyName = "OpenNFS.Protocol.V40";
        private const string Nfs40HostTypeName = "OpenNFS.Protocol.V40.Hosting.OpenNfsTcpNfs40ServerHost";
        private readonly SemaphoreSlim _lifecycleGate = new SemaphoreSlim(1, 1);
        private readonly OpenNfsServerApplicationOptions _options;
        private readonly OpenNfsServer _server;
        private bool _disposed;
        private IAsyncDisposable? _nfs40Host;
        private IAsyncDisposable? _nfsV3Host;

        internal OpenNfsServerApplication(OpenNfsServer server, OpenNfsServerApplicationOptions options)
        {
            ArgumentNullException.ThrowIfNull(server);
            ArgumentNullException.ThrowIfNull(options);

            _options = options.Clone();
            _options.Validate();
            _server = server;
            ListenerAddress = _options.ListenerAddress;
        }

        /// <summary>
        /// Gets the immutable server wrapper associated with this application.
        /// </summary>
        public OpenNfsServer Server => _server;

        /// <summary>
        /// Gets the configured listener bind address.
        /// </summary>
        public string ListenerAddress { get; }

        /// <summary>
        /// Gets a value indicating whether the application is currently running.
        /// </summary>
        public bool IsRunning { get; private set; }

        /// <summary>
        /// Gets the bound MOUNT v3 TCP port.
        /// Returns <c>0</c> when the NFSv3-era surface is disabled or the application has not been started.
        /// </summary>
        public int MountPort { get; private set; }

        /// <summary>
        /// Gets the bound NFSv3 TCP port.
        /// Returns <c>0</c> when the NFSv3-era surface is disabled or the application has not been started.
        /// </summary>
        public int NfsPort { get; private set; }

        /// <summary>
        /// Gets the bound NLM v4 TCP port.
        /// Returns <c>0</c> when the NFSv3-era surface is disabled or the application has not been started.
        /// </summary>
        public int NlmPort { get; private set; }

        /// <summary>
        /// Gets the bound NSM TCP port.
        /// Returns <c>0</c> when the NFSv3-era surface is disabled or the application has not been started.
        /// </summary>
        public int NsmPort { get; private set; }

        /// <summary>
        /// Gets the bound NFSv4.0 TCP port.
        /// Returns <c>0</c> when the NFSv4.0 surface is disabled or the application has not been started.
        /// </summary>
        public int Nfs40Port { get; private set; }

        /// <summary>
        /// Resolves and validates the exports exposed by the configured host surface.
        /// This mirrors the immutable server wrapper on the primary managed application path.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The validated export definitions.</returns>
        public Task<IReadOnlyList<OpenNfsExportDefinition>> GetExportsAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return _server.GetExportsAsync(cancellationToken);
        }

        /// <summary>
        /// Resolves and validates the exports exposed by the configured host surface using an explicit request context.
        /// This mirrors the immutable server wrapper on the primary managed application path.
        /// </summary>
        /// <param name="request">Request context for the export-resolution operation.</param>
        /// <returns>The response context containing the validated export definitions.</returns>
        public Task<NfsGetExportsResponse> GetExportsAsync(NfsGetExportsRequest request)
        {
            ThrowIfDisposed();
            return _server.GetExportsAsync(request);
        }

        /// <summary>
        /// Starts the configured listeners and preserves typed server failures in a non-throwing result envelope.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Non-throwing result envelope.</returns>
        public Task<OpenNfsServerResult> TryStartAsync(CancellationToken cancellationToken = default)
        {
            return OpenNfsServerResultFactory.TryAsync(() => StartAsync(cancellationToken));
        }

        /// <summary>
        /// Starts the configured server application listeners.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                ThrowIfDisposed();

                if (IsRunning)
                {
                    throw new OpenNfsServerStateException("The server application is already running.");
                }

                IAsyncDisposable? startedNfsV3Host = null;
                IAsyncDisposable? startedNfs40Host = null;

                try
                {
                    if (_options.EnableNfsV3)
                    {
                        object nfsV3Host = StartHost(
                            NfsV3AssemblyName,
                            NfsV3HostTypeName,
                            _server,
                            ListenerAddress,
                            _options.MountPort,
                            _options.NfsPort,
                            _options.NlmPort,
                            _options.NsmPort);
                        startedNfsV3Host = RequireAsyncDisposable(nfsV3Host, NfsV3HostTypeName);
                        MountPort = ReadRequiredIntProperty(nfsV3Host, nameof(MountPort));
                        NfsPort = ReadRequiredIntProperty(nfsV3Host, nameof(NfsPort));
                        NlmPort = ReadRequiredIntProperty(nfsV3Host, nameof(NlmPort));
                        NsmPort = ReadRequiredIntProperty(nfsV3Host, nameof(NsmPort));
                    }

                    if (_options.EnableNfs40)
                    {
                        object nfs40Host = StartHost(
                            Nfs40AssemblyName,
                            Nfs40HostTypeName,
                            _server,
                            ListenerAddress,
                            _options.Nfs40Port);
                        startedNfs40Host = RequireAsyncDisposable(nfs40Host, Nfs40HostTypeName);
                        Nfs40Port = ReadRequiredIntProperty(nfs40Host, nameof(NfsPort));
                    }

                    _nfsV3Host = startedNfsV3Host;
                    _nfs40Host = startedNfs40Host;
                    IsRunning = true;
                }
                catch (OperationCanceledException)
                {
                    if (startedNfs40Host is not null)
                    {
                        await startedNfs40Host.DisposeAsync().ConfigureAwait(false);
                    }

                    if (startedNfsV3Host is not null)
                    {
                        await startedNfsV3Host.DisposeAsync().ConfigureAwait(false);
                    }

                    ResetBoundPorts();
                    throw;
                }
                catch (Exception exception)
                {
                    if (startedNfs40Host is not null)
                    {
                        await startedNfs40Host.DisposeAsync().ConfigureAwait(false);
                    }

                    if (startedNfsV3Host is not null)
                    {
                        await startedNfsV3Host.DisposeAsync().ConfigureAwait(false);
                    }

                    ResetBoundPorts();

                    if (exception is OpenNfsServerException)
                    {
                        throw;
                    }

                    throw new OpenNfsServerStateException("Failed to start the managed OpenNFS listeners.", exception);
                }
            }
            finally
            {
                _lifecycleGate.Release();
            }
        }

        /// <summary>
        /// Stops the configured listeners and preserves typed server failures in a non-throwing result envelope.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Non-throwing result envelope.</returns>
        public Task<OpenNfsServerResult> TryStopAsync(CancellationToken cancellationToken = default)
        {
            return OpenNfsServerResultFactory.TryAsync(() => StopAsync(cancellationToken));
        }

        /// <summary>
        /// Stops the configured server application listeners.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            IAsyncDisposable? nfsV3Host = null;
            IAsyncDisposable? nfs40Host = null;

            ThrowIfDisposed();
            await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                ThrowIfDisposed();

                if (!IsRunning)
                {
                    return;
                }

                nfsV3Host = _nfsV3Host;
                nfs40Host = _nfs40Host;
                _nfsV3Host = null;
                _nfs40Host = null;
                IsRunning = false;
                ResetBoundPorts();
            }
            finally
            {
                _lifecycleGate.Release();
            }

            if (nfs40Host is not null)
            {
                await nfs40Host.DisposeAsync().ConfigureAwait(false);
            }

            if (nfsV3Host is not null)
            {
                await nfsV3Host.DisposeAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Starts the configured listeners, waits for cancellation, and preserves typed server failures in a non-throwing result envelope.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token used to terminate the running application.</param>
        /// <returns>Non-throwing result envelope.</returns>
        public Task<OpenNfsServerResult> TryRunAsync(CancellationToken cancellationToken = default)
        {
            return OpenNfsServerResultFactory.TryAsync(() => RunAsync(cancellationToken));
        }

        /// <summary>
        /// Starts the configured listeners, waits for cancellation, and then stops the application.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token used to terminate the running application.</param>
        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            await StartAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            finally
            {
                if (!_disposed)
                {
                    await StopAsync().ConfigureAwait(false);
                }
            }
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            IAsyncDisposable? nfsV3Host = null;
            IAsyncDisposable? nfs40Host = null;

            await _lifecycleGate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                nfsV3Host = _nfsV3Host;
                nfs40Host = _nfs40Host;
                _nfsV3Host = null;
                _nfs40Host = null;
                IsRunning = false;
                ResetBoundPorts();
            }
            finally
            {
                _lifecycleGate.Release();
            }

            if (nfs40Host is not null)
            {
                await nfs40Host.DisposeAsync().ConfigureAwait(false);
            }

            if (nfsV3Host is not null)
            {
                await nfsV3Host.DisposeAsync().ConfigureAwait(false);
            }

        }

        private static IAsyncDisposable RequireAsyncDisposable(object instance, string typeName)
        {
            if (instance is not IAsyncDisposable disposable)
            {
                throw new OpenNfsServerStateException(
                    "The internal host type '" + typeName + "' did not implement IAsyncDisposable as expected.");
            }

            return disposable;
        }

        private static int ReadRequiredIntProperty(object instance, string propertyName)
        {
            PropertyInfo? property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property is null || property.PropertyType != typeof(int))
            {
                throw new OpenNfsServerStateException(
                    "The internal host type '" + instance.GetType().FullName + "' did not expose expected int property '" + propertyName + "'.");
            }

            object? rawValue = property.GetValue(instance);
            if (rawValue is not int value)
            {
                throw new OpenNfsServerStateException(
                    "The internal host type '" + instance.GetType().FullName + "' returned a non-int value for '" + propertyName + "'.");
            }

            return value;
        }

        private static object StartHost(string assemblyName, string typeName, params object?[] arguments)
        {
            try
            {
                Assembly assembly = Assembly.Load(new AssemblyName(assemblyName));
                Type hostType = assembly.GetType(typeName, throwOnError: true)!
                    ?? throw new OpenNfsServerStateException("The internal host type '" + typeName + "' could not be loaded.");
                MethodInfo? startMethod = hostType.GetMethod("Start", BindingFlags.Public | BindingFlags.Static);
                if (startMethod is null)
                {
                    throw new OpenNfsServerStateException("The internal host type '" + typeName + "' did not expose a public static Start method.");
                }

                return startMethod.Invoke(null, arguments)
                    ?? throw new OpenNfsServerStateException("The internal host type '" + typeName + "' returned null from Start.");
            }
            catch (TargetInvocationException exception) when (exception.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
            catch (FileNotFoundException exception)
            {
                throw new OpenNfsServerStateException(
                    "The internal runtime assembly '" + assemblyName + "' is not available. Ensure the OpenNFS server package includes its bundled protocol runtime assemblies.",
                    exception);
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new OpenNfsServerStateException("The server application has been disposed.");
            }
        }

        private void ResetBoundPorts()
        {
            MountPort = 0;
            NfsPort = 0;
            NlmPort = 0;
            NsmPort = 0;
            Nfs40Port = 0;
        }
    }
}
