namespace Test.Shared.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    internal static class DotnetCli
    {
        public static Task<DotnetCommandResult> RunAsync(
            IReadOnlyList<string> arguments,
            string workingDirectory,
            CancellationToken cancellationToken,
            TimeSpan? timeout = null)
        {
            ArgumentNullException.ThrowIfNull(arguments);
            ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);

            return RunCoreAsync(arguments, workingDirectory, cancellationToken, timeout ?? TimeSpan.FromMinutes(3));
        }

        public static async Task<DotnetCommandResult> RunCheckedAsync(
            IReadOnlyList<string> arguments,
            string workingDirectory,
            CancellationToken cancellationToken,
            TimeSpan? timeout = null)
        {
            DotnetCommandResult result =
                await RunAsync(arguments, workingDirectory, cancellationToken, timeout).ConfigureAwait(false);
            if (result.ExitCode == 0)
            {
                return result;
            }

            throw new InvalidOperationException(
                "The dotnet command '" + FormatArguments(arguments) + "' failed with exit code " + result.ExitCode + "."
                + Environment.NewLine
                + "stdout:"
                + Environment.NewLine
                + result.StandardOutput
                + Environment.NewLine
                + "stderr:"
                + Environment.NewLine
                + result.StandardError);
        }

        private static string FormatArguments(IReadOnlyList<string> arguments)
        {
            StringBuilder builder = new StringBuilder();

            for (int index = 0; index < arguments.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(arguments[index]);
            }

            return builder.ToString();
        }

        private static async Task<DotnetCommandResult> RunCoreAsync(
            IReadOnlyList<string> arguments,
            string workingDirectory,
            CancellationToken cancellationToken,
            TimeSpan timeout)
        {
            using Process process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            for (int index = 0; index < arguments.Count; index++)
            {
                process.StartInfo.ArgumentList.Add(arguments[index]);
            }

            try
            {
                if (!process.Start())
                {
                    throw new InvalidOperationException("The dotnet CLI process could not be started.");
                }
            }
            catch (Win32Exception exception)
            {
                throw new InvalidOperationException("The dotnet CLI could not be started from PATH.", exception);
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
                    "The dotnet command '" + FormatArguments(arguments) + "' did not complete within " + timeout + ".");
            }

            string standardOutput = await standardOutputTask.ConfigureAwait(false);
            string standardError = await standardErrorTask.ConfigureAwait(false);
            return new DotnetCommandResult(process.ExitCode, standardOutput, standardError);
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
