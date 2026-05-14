namespace Test.Shared.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class PackedOpenNfsServerProcess : IAsyncDisposable
    {
        private readonly ExternalPackageConsumerProject _project;
        private readonly Process _process;
        private readonly StringBuilder _standardError;
        private readonly Task _standardErrorTask;
        private readonly StringBuilder _standardOutput;
        private readonly Task _standardOutputTask;

        private PackedOpenNfsServerProcess(
            ExternalPackageConsumerProject project,
            Process process,
            StringBuilder standardOutput,
            StringBuilder standardError,
            Task standardOutputTask,
            Task standardErrorTask,
            int mountPort,
            int nfsPort,
            int nfs40Port,
            int nfs41Port,
            int nfs42Port)
        {
            _project = project;
            _process = process;
            _standardOutput = standardOutput;
            _standardError = standardError;
            _standardOutputTask = standardOutputTask;
            _standardErrorTask = standardErrorTask;
            MountPort = mountPort;
            NfsPort = nfsPort;
            Nfs40Port = nfs40Port;
            Nfs41Port = nfs41Port;
            Nfs42Port = nfs42Port;
        }

        internal int MountPort { get; }

        internal int NfsPort { get; }

        internal int Nfs40Port { get; }

        internal int Nfs41Port { get; }

        internal int Nfs42Port { get; }

        internal static async Task<PackedOpenNfsServerProcess> StartAsync(
            string programSource,
            CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(programSource);

            ExternalPackageConsumerProject project = await ExternalPackageConsumerSupport.CreateSinglePackageConsoleAppAsync(
                System.IO.Path.Combine("src", "OpenNFS.Server", "OpenNFS.Server.csproj"),
                "OpenNFS.Server",
                programSource,
                cancellationToken).ConfigureAwait(false);

            try
            {
                await DotnetCli.RunCheckedAsync(
                    new[]
                    {
                        "build",
                        "--disable-build-servers",
                        project.ProjectPath,
                        "-c",
                        "Release",
                        "--no-restore",
                    },
                    project.ProjectDirectory,
                    cancellationToken,
                    timeout: TimeSpan.FromMinutes(3)).ConfigureAwait(false);

                Process process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "dotnet",
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WorkingDirectory = project.ProjectDirectory,
                    },
                    EnableRaisingEvents = true,
                };

                process.StartInfo.ArgumentList.Add("run");
                process.StartInfo.ArgumentList.Add("--disable-build-servers");
                process.StartInfo.ArgumentList.Add("--project");
                process.StartInfo.ArgumentList.Add(project.ProjectPath);
                process.StartInfo.ArgumentList.Add("-c");
                process.StartInfo.ArgumentList.Add("Release");
                process.StartInfo.ArgumentList.Add("--no-build");
                process.StartInfo.ArgumentList.Add("--no-restore");

                try
                {
                    if (!process.Start())
                    {
                        throw new InvalidOperationException("The packed OpenNFS server consumer process could not be started.");
                    }
                }
                catch (Win32Exception exception)
                {
                    throw new InvalidOperationException("The dotnet CLI could not be started for the packed OpenNFS server consumer process.", exception);
                }

                TaskCompletionSource<(int MountPort, int NfsPort, int Nfs40Port, int Nfs41Port, int Nfs42Port)> readyTcs =
                    new TaskCompletionSource<(int MountPort, int NfsPort, int Nfs40Port, int Nfs41Port, int Nfs42Port)>(TaskCreationOptions.RunContinuationsAsynchronously);

                StringBuilder standardOutput = new StringBuilder();
                StringBuilder standardError = new StringBuilder();

                Task standardOutputTask = PumpReaderAsync(
                    process.StandardOutput,
                    line =>
                    {
                        standardOutput.AppendLine(line);
                        if (TryParseReadyLine(
                            line,
                            out int mountPort,
                            out int nfsPort,
                            out int nfs40Port,
                            out int nfs41Port,
                            out int nfs42Port))
                        {
                            readyTcs.TrySetResult((mountPort, nfsPort, nfs40Port, nfs41Port, nfs42Port));
                        }
                    });

                Task standardErrorTask = PumpReaderAsync(
                    process.StandardError,
                    line =>
                    {
                        standardError.AppendLine(line);
                    });

                Task exitTask = process.WaitForExitAsync();
                using CancellationTokenSource timeoutTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutTokenSource.CancelAfter(TimeSpan.FromSeconds(45));
                Task delayTask = Task.Delay(Timeout.InfiniteTimeSpan, timeoutTokenSource.Token);

                try
                {
                    Task completedTask = await Task.WhenAny(readyTcs.Task, exitTask, delayTask).ConfigureAwait(false);
                    if (completedTask == readyTcs.Task)
                    {
                        (int mountPort, int nfsPort, int nfs40Port, int nfs41Port, int nfs42Port) = await readyTcs.Task.ConfigureAwait(false);
                        return new PackedOpenNfsServerProcess(
                            project,
                            process,
                            standardOutput,
                            standardError,
                            standardOutputTask,
                            standardErrorTask,
                            mountPort,
                            nfsPort,
                            nfs40Port,
                            nfs41Port,
                            nfs42Port);
                    }

                    if (completedTask == exitTask)
                    {
                        await exitTask.ConfigureAwait(false);
                        string output = await ReadOutputAsync(standardOutputTask, standardErrorTask, standardOutput, standardError, cancellationToken).ConfigureAwait(false);
                        throw new InvalidOperationException(
                            "The packed OpenNFS server consumer process exited before announcing readiness."
                            + Environment.NewLine
                            + output);
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    throw new TimeoutException("Timed out waiting for the packed OpenNFS server consumer process to announce readiness.");
                }
                catch
                {
                    await DisposeProcessAsync(process).ConfigureAwait(false);
                    await Task.WhenAll(standardOutputTask, standardErrorTask).ConfigureAwait(false);
                    await project.DisposeAsync().ConfigureAwait(false);
                    throw;
                }
            }
            catch
            {
                await project.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (!_process.HasExited)
                {
                    await _process.StandardInput.WriteLineAsync().ConfigureAwait(false);
                    await _process.StandardInput.FlushAsync().ConfigureAwait(false);
                }
            }
            catch (InvalidOperationException)
            {
            }

            await DisposeProcessAsync(_process).ConfigureAwait(false);
            await Task.WhenAll(_standardOutputTask, _standardErrorTask).ConfigureAwait(false);
            await _project.DisposeAsync().ConfigureAwait(false);
        }

        internal string GetCombinedOutput()
        {
            return _standardOutput.ToString() + Environment.NewLine + _standardError.ToString();
        }

        private static async Task DisposeProcessAsync(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    using CancellationTokenSource timeoutSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    try
                    {
                        await process.WaitForExitAsync(timeoutSource.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        if (!process.HasExited)
                        {
                            process.Kill(entireProcessTree: true);
                        }
                    }
                }
            }
            catch (InvalidOperationException)
            {
            }

            try
            {
                await process.WaitForExitAsync().ConfigureAwait(false);
            }
            catch (InvalidOperationException)
            {
            }
            finally
            {
                process.Dispose();
            }
        }

        private static async Task<string> ReadOutputAsync(
            Task standardOutputTask,
            Task standardErrorTask,
            StringBuilder standardOutput,
            StringBuilder standardError,
            CancellationToken cancellationToken)
        {
            await Task.WhenAll(standardOutputTask, standardErrorTask).WaitAsync(cancellationToken).ConfigureAwait(false);
            return "stdout:"
                + Environment.NewLine
                + standardOutput.ToString()
                + Environment.NewLine
                + "stderr:"
                + Environment.NewLine
                + standardError.ToString();
        }

        private static async Task PumpReaderAsync(System.IO.StreamReader reader, Action<string> onLine)
        {
            while (true)
            {
                string? line = await reader.ReadLineAsync().ConfigureAwait(false);
                if (line is null)
                {
                    break;
                }

                onLine(line);
            }
        }

        private static bool TryParseReadyLine(
            string line,
            out int mountPort,
            out int nfsPort,
            out int nfs40Port,
            out int nfs41Port,
            out int nfs42Port)
        {
            mountPort = 0;
            nfsPort = 0;
            nfs40Port = 0;
            nfs41Port = 0;
            nfs42Port = 0;

            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("READY ", StringComparison.Ordinal))
            {
                return false;
            }

            string[] tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 1; index < tokens.Length; index++)
            {
                int separatorIndex = tokens[index].IndexOf('=');
                if (separatorIndex <= 0 || separatorIndex >= tokens[index].Length - 1)
                {
                    continue;
                }

                values[tokens[index].Substring(0, separatorIndex)] = tokens[index].Substring(separatorIndex + 1);
            }

            return values.TryGetValue("mountPort", out string? mountPortValue)
                && values.TryGetValue("nfsPort", out string? nfsPortValue)
                && values.TryGetValue("nfs40Port", out string? nfs40PortValue)
                && values.TryGetValue("nfs41Port", out string? nfs41PortValue)
                && values.TryGetValue("nfs42Port", out string? nfs42PortValue)
                && int.TryParse(mountPortValue, out mountPort)
                && int.TryParse(nfsPortValue, out nfsPort)
                && int.TryParse(nfs40PortValue, out nfs40Port)
                && int.TryParse(nfs41PortValue, out nfs41Port)
                && int.TryParse(nfs42PortValue, out nfs42Port);
        }
    }
}
