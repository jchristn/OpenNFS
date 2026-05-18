namespace Test.Shared.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class SampleOpenNfsServerProcess : IAsyncDisposable
    {
        private readonly Process _process;
        private readonly StringBuilder _standardError;
        private readonly Task _standardErrorTask;
        private readonly StringBuilder _standardOutput;
        private readonly Task _standardOutputTask;

        private SampleOpenNfsServerProcess(
            Process process,
            StringBuilder standardOutput,
            StringBuilder standardError,
            Task standardOutputTask,
            Task standardErrorTask,
            int mountPort,
            int nfsPort,
            int nfs40Port,
            string kerberosTargetSpn)
        {
            _process = process;
            _standardOutput = standardOutput;
            _standardError = standardError;
            _standardOutputTask = standardOutputTask;
            _standardErrorTask = standardErrorTask;
            MountPort = mountPort;
            NfsPort = nfsPort;
            Nfs40Port = nfs40Port;
            KerberosTargetSpn = kerberosTargetSpn;
        }

        internal int MountPort { get; }

        internal int NfsPort { get; }

        internal int Nfs40Port { get; }

        internal string KerberosTargetSpn { get; }

        internal static async Task<SampleOpenNfsServerProcess> StartAsync(
            string sourcePath,
            string mappingPath,
            bool denyMounts,
            CancellationToken cancellationToken)
        {
            return await StartAsync(sourcePath, mappingPath, denyMounts, kerberosTargetSpn: null, kerberosKeytab: null, cancellationToken).ConfigureAwait(false);
        }

        internal static async Task<SampleOpenNfsServerProcess> StartAsync(
            string sourcePath,
            string mappingPath,
            bool denyMounts,
            string? kerberosTargetSpn,
            string? kerberosKeytab,
            CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
            ArgumentException.ThrowIfNullOrWhiteSpace(mappingPath);

            List<string> sampleArguments = new List<string>
            {
                "--source-path",
                Path.GetFullPath(sourcePath),
                "--mapping-path",
                Path.GetFullPath(mappingPath),
                "--listener-address",
                "0.0.0.0",
                "--mount-port",
                "0",
                "--nfs-port",
                "0",
                "--nfs40-port",
                "0",
            };

            if (denyMounts)
            {
                sampleArguments.Add("--deny-mounts");
            }

            if (!string.IsNullOrWhiteSpace(kerberosTargetSpn))
            {
                sampleArguments.Add("--kerberos-spn");
                sampleArguments.Add(kerberosTargetSpn);
            }

            if (!string.IsNullOrWhiteSpace(kerberosKeytab))
            {
                sampleArguments.Add("--kerberos-keytab");
                sampleArguments.Add(Path.GetFullPath(kerberosKeytab));
            }

            return await StartCoreAsync(sampleArguments, cancellationToken).ConfigureAwait(false);
        }

        internal static async Task<SampleOpenNfsServerProcess> StartAsync(
            string configPath,
            CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(configPath);

            List<string> sampleArguments = new List<string>
            {
                "--config",
                Path.GetFullPath(configPath),
                "--mount-port",
                "0",
                "--nfs-port",
                "0",
                "--nfs40-port",
                "0",
            };

            return await StartCoreAsync(sampleArguments, cancellationToken).ConfigureAwait(false);
        }

        private static async Task<SampleOpenNfsServerProcess> StartCoreAsync(
            IReadOnlyList<string> sampleArguments,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(sampleArguments);

            string repositoryRoot = FindRepositoryRoot();
            string sampleProjectPath = Path.Combine(repositoryRoot, "src", "Sample.OpenNfsServer", "Sample.OpenNfsServer.csproj");

            Process process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = repositoryRoot,
                },
                EnableRaisingEvents = true,
            };

            process.StartInfo.ArgumentList.Add("run");
            process.StartInfo.ArgumentList.Add("--no-build");
            process.StartInfo.ArgumentList.Add("-c");
            process.StartInfo.ArgumentList.Add("Release");
            process.StartInfo.ArgumentList.Add("--framework");
            process.StartInfo.ArgumentList.Add(InteropTargetFramework.Current);
            process.StartInfo.ArgumentList.Add("--project");
            process.StartInfo.ArgumentList.Add(sampleProjectPath);
            process.StartInfo.ArgumentList.Add("--");

            for (int index = 0; index < sampleArguments.Count; index++)
            {
                process.StartInfo.ArgumentList.Add(sampleArguments[index]);
            }

            try
            {
                if (!process.Start())
                {
                    throw new InvalidOperationException("The Sample.OpenNfsServer process could not be started.");
                }
            }
            catch (Win32Exception exception)
            {
                throw new InvalidOperationException("The dotnet CLI could not be started for Sample.OpenNfsServer.", exception);
            }

            TaskCompletionSource<(int MountPort, int NfsPort, int Nfs40Port, string Kerberos)> readyTcs =
                new TaskCompletionSource<(int MountPort, int NfsPort, int Nfs40Port, string Kerberos)>(TaskCreationOptions.RunContinuationsAsynchronously);

            StringBuilder standardOutput = new StringBuilder();
            StringBuilder standardError = new StringBuilder();

            Task standardOutputTask = PumpReaderAsync(
                process.StandardOutput,
                line =>
                {
                    standardOutput.AppendLine(line);

                    if (TryParseReadyLine(line, out int mountPort, out int nfsPort, out int nfs40Port, out string kerberos))
                    {
                        readyTcs.TrySetResult((mountPort, nfsPort, nfs40Port, kerberos));
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
                    (int mountPort, int nfsPort, int nfs40Port, string kerberos) = await readyTcs.Task.ConfigureAwait(false);
                    return new SampleOpenNfsServerProcess(
                        process,
                        standardOutput,
                        standardError,
                        standardOutputTask,
                        standardErrorTask,
                        mountPort,
                        nfsPort,
                        nfs40Port,
                        kerberos);
                }

                if (completedTask == exitTask)
                {
                    await exitTask.ConfigureAwait(false);
                    string output = await ReadOutputAsync(standardOutputTask, standardErrorTask, standardOutput, standardError, cancellationToken).ConfigureAwait(false);
                    throw new InvalidOperationException(
                        "Sample.OpenNfsServer exited before announcing readiness."
                        + Environment.NewLine
                        + output);
                }

                cancellationToken.ThrowIfCancellationRequested();
                throw new TimeoutException("Timed out waiting for Sample.OpenNfsServer to announce readiness.");
            }
            catch
            {
                await DisposeProcessAsync(process).ConfigureAwait(false);
                await Task.WhenAll(standardOutputTask, standardErrorTask).ConfigureAwait(false);
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await DisposeProcessAsync(_process).ConfigureAwait(false);
            await Task.WhenAll(_standardOutputTask, _standardErrorTask).ConfigureAwait(false);
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
                    process.Kill(entireProcessTree: true);
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

        private static string FindRepositoryRoot()
        {
            string? currentDirectory = Environment.CurrentDirectory;
            if (TryFindRepositoryRoot(currentDirectory, out string? repositoryRoot))
            {
                return repositoryRoot!;
            }

            if (TryFindRepositoryRoot(AppContext.BaseDirectory, out repositoryRoot))
            {
                return repositoryRoot!;
            }

            throw new DirectoryNotFoundException("Could not locate the OpenNFS repository root while starting the sample server process.");
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

        private static async Task PumpReaderAsync(StreamReader reader, Action<string> onLine)
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

        private static bool TryFindRepositoryRoot(string? startPath, out string? repositoryRoot)
        {
            repositoryRoot = null;
            if (string.IsNullOrWhiteSpace(startPath))
            {
                return false;
            }

            DirectoryInfo? directory = new DirectoryInfo(startPath);
            if (!directory.Exists)
            {
                directory = directory.Parent;
            }

            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "README.md"))
                    && File.Exists(Path.Combine(directory.FullName, "src", "OpenNFS.sln")))
                {
                    repositoryRoot = directory.FullName;
                    return true;
                }

                directory = directory.Parent;
            }

            return false;
        }

        private static bool TryParseReadyLine(string line, out int mountPort, out int nfsPort, out int nfs40Port, out string kerberos)
        {
            mountPort = 0;
            nfsPort = 0;
            nfs40Port = 0;
            kerberos = string.Empty;

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

            if (values.TryGetValue("kerberos", out string? kerberosValue))
            {
                kerberos = kerberosValue;
            }

            return values.TryGetValue("mountPort", out string? mountPortValue)
                && values.TryGetValue("nfsPort", out string? nfsPortValue)
                && values.TryGetValue("nfs40Port", out string? nfs40PortValue)
                && int.TryParse(mountPortValue, out mountPort)
                && int.TryParse(nfsPortValue, out nfsPort)
                && int.TryParse(nfs40PortValue, out nfs40Port);
        }
    }
}
