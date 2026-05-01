namespace Test.Shared.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    internal static class PowerShellCli
    {
        public static Task<PowerShellCommandResult> RunScriptAsync(
            string scriptPath,
            IReadOnlyList<string> scriptArguments,
            string workingDirectory,
            CancellationToken cancellationToken,
            TimeSpan? timeout = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(scriptPath);
            ArgumentNullException.ThrowIfNull(scriptArguments);
            ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);

            return RunCoreAsync(scriptPath, scriptArguments, workingDirectory, cancellationToken, timeout ?? TimeSpan.FromMinutes(3));
        }

        private static async Task<PowerShellCommandResult> RunCoreAsync(
            string scriptPath,
            IReadOnlyList<string> scriptArguments,
            string workingDirectory,
            CancellationToken cancellationToken,
            TimeSpan timeout)
        {
            string[] candidateExecutables = OperatingSystem.IsWindows()
                ? new[] { "pwsh", "powershell" }
                : new[] { "pwsh" };

            Exception? lastStartException = null;
            for (int index = 0; index < candidateExecutables.Length; index++)
            {
                try
                {
                    return await RunWithExecutableAsync(
                        candidateExecutables[index],
                        scriptPath,
                        scriptArguments,
                        workingDirectory,
                        cancellationToken,
                        timeout).ConfigureAwait(false);
                }
                catch (Win32Exception exception)
                {
                    lastStartException = exception;
                }
            }

            throw new InvalidOperationException("A PowerShell executable could not be started from PATH.", lastStartException);
        }

        private static async Task<PowerShellCommandResult> RunWithExecutableAsync(
            string executableName,
            string scriptPath,
            IReadOnlyList<string> scriptArguments,
            string workingDirectory,
            CancellationToken cancellationToken,
            TimeSpan timeout)
        {
            using Process process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = executableName,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            process.StartInfo.ArgumentList.Add("-NoLogo");
            process.StartInfo.ArgumentList.Add("-NoProfile");
            if (string.Equals(executableName, "powershell", StringComparison.OrdinalIgnoreCase))
            {
                process.StartInfo.ArgumentList.Add("-ExecutionPolicy");
                process.StartInfo.ArgumentList.Add("Bypass");
            }

            process.StartInfo.ArgumentList.Add("-File");
            process.StartInfo.ArgumentList.Add(scriptPath);

            for (int index = 0; index < scriptArguments.Count; index++)
            {
                process.StartInfo.ArgumentList.Add(scriptArguments[index]);
            }

            if (!process.Start())
            {
                throw new InvalidOperationException("The PowerShell process could not be started.");
            }

            Task<string> standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            Task<string> standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            using CancellationTokenSource timeoutTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutTokenSource.CancelAfter(timeout);

            try
            {
                await process.WaitForExitAsync(timeoutTokenSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                TryKill(process);
                throw new TimeoutException(
                    "The PowerShell command '" + executableName + " " + FormatArguments(scriptPath, scriptArguments) + "' did not complete within " + timeout + ".");
            }

            string standardOutput = await standardOutputTask.ConfigureAwait(false);
            string standardError = await standardErrorTask.ConfigureAwait(false);
            return new PowerShellCommandResult(process.ExitCode, standardOutput, standardError);
        }

        private static string FormatArguments(string scriptPath, IReadOnlyList<string> scriptArguments)
        {
            StringBuilder builder = new StringBuilder(scriptPath);
            for (int index = 0; index < scriptArguments.Count; index++)
            {
                builder.Append(' ');
                builder.Append(scriptArguments[index]);
            }

            return builder.ToString();
        }

        private static void TryKill(Process process)
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
        }
    }
}
