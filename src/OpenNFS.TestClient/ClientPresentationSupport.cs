namespace OpenNFS.TestClient
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using OpenNFS.Client;
    using static OpenNFS.TestClient.ClientRuntimeState;

    internal static class ClientPresentationSupport
    {
        internal static void ShowMenu()
        {
            Console.WriteLine();
            Console.WriteLine("Available commands:");
            WriteCommandLine("? / help", "show this menu");
            WriteCommandLine("q / quit / exit", "quit");
            WriteCommandLine("cls / clear", "clear the screen");
            WriteCommandLine("show", "show current client configuration and mount state");
            Console.WriteLine();
            Console.WriteLine("Configuration:");
            WriteCommandLine("server [host] [port]", "set the primary NFS endpoint", Configuration.GetPrimaryEndpointDisplay());
            WriteCommandLine("mountendpoint [host] [port]", "set the dedicated MOUNT v3 endpoint", Configuration.GetMountEndpointDisplay());
            WriteCommandLine("mountendpoint clear", "clear the dedicated MOUNT endpoint");
            WriteCommandLine("transport [tcp|tcpudp]", "set the transport policy", Configuration.GetTransportDisplay());
            WriteCommandLine("auth [none|authsys|rpcsecgss]", "set the authentication flavor", Configuration.GetAuthenticationFlavorDisplay());
            WriteCommandLine("authsys [machine] [uid] [gid] [groups]", "set AUTH_SYS identity values", Configuration.GetAuthSysDisplay());
            Console.WriteLine();
            Console.WriteLine("Connection and export bootstrap:");
            WriteCommandLine("connect", "build and open the client");
            WriteCommandLine("disconnect", "close and dispose the client");
            WriteCommandLine("exports", "list available exports through MOUNT v3");
            WriteCommandLine("mounts", "list current server mount records through MOUNT v3");
            WriteCommandLine("mount [exportPath]", "mount an export and open a path-first session", Configuration.PreferredExportPath);
            WriteCommandLine("umount", "unmount the current export and close the session");
            Console.WriteLine();
            Console.WriteLine("Mounted-session operations:");
            WriteCommandLine("pwd", "show the current export-relative directory");
            WriteCommandLine("cd [path]", "change the current export-relative directory");
            WriteCommandLine("ls [path]", "list directory contents");
            WriteCommandLine("stat [path]", "show file or directory metadata");
            WriteCommandLine("cat [path]", "read and print a UTF-8 file");
            WriteCommandLine("write [path] [text]", "overwrite a file; prompt only when [text] is omitted");
            WriteCommandLine("touch [path]", "create a file if it does not exist");
            WriteCommandLine("mkdir [path]", "create a directory");
            WriteCommandLine("rm [path]", "delete a file");
            WriteCommandLine("rmdir [path]", "delete an empty directory");
            WriteCommandLine("put [local] [remote]", "upload a local file");
            WriteCommandLine("get [remote] [local]", "download a remote file");
            Console.WriteLine();
            Console.WriteLine("Notes:");
            Console.WriteLine("  - The current test client exercises the best-covered NFSv3 mounted-session path.");
            Console.WriteLine("  - AUTH_SYS lets you configure machine, uid, gid, and supplementary gids.");
            Console.WriteLine("  - RPCSEC_GSS remains unimplemented in the current public client surface.");
            Console.WriteLine("  - Use --script <path> to run a repeatable command file without the interactive prompt.");
            Console.WriteLine();
        }

        internal static void ShowCommandLineHelp()
        {
            Console.WriteLine("OpenNFS.TestClient options:");
            Console.WriteLine("  --script <path>   Run commands from a file and exit with a non-zero code on failure.");
            Console.WriteLine("  --help            Show this help text.");
        }

        internal static void ShowStatus()
        {
            Console.WriteLine();
            Console.WriteLine("Client configuration:");
            Console.WriteLine("  Primary endpoint : " + Configuration.GetPrimaryEndpointDisplay());
            Console.WriteLine("  MOUNT endpoint   : " + Configuration.GetMountEndpointDisplay());

            Console.WriteLine("  Transport        : " + Configuration.TransportPolicy);
            Console.WriteLine("  Auth flavor      : " + Configuration.AuthenticationFlavor);

            if (Configuration.AuthenticationFlavor == OpenNfsAuthenticationFlavor.AuthSys)
            {
                Console.WriteLine("  AUTH_SYS machine : " + Configuration.AuthSysMachineName);
                Console.WriteLine("  AUTH_SYS uid/gid : "
                    + Configuration.AuthSysUserId.ToString(CultureInfo.InvariantCulture)
                    + "/"
                    + Configuration.AuthSysGroupId.ToString(CultureInfo.InvariantCulture));
                Console.WriteLine("  AUTH_SYS groups  : " + FormatUIntList(Configuration.AuthSysSupplementaryGroupIds));
            }

            Console.WriteLine("  Connected        : " + (Client is not null ? Client.State.ToString() : "No"));
            Console.WriteLine("  Mounted export   : " + (MountedExportPath ?? "(none)"));
            Console.WriteLine("  Current path     : " + CurrentDirectory);
            Console.WriteLine();
        }

        internal static string FormatUIntList(IReadOnlyList<uint> values)
        {
            if (values.Count == 0)
            {
                return "(none)";
            }

            return string.Join(", ", values.Select(static value => value.ToString(CultureInfo.InvariantCulture)));
        }

        internal static string FormatNfsTime(OpenNfsV3Time value)
        {
            DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(value.Seconds)
                .AddTicks(value.Nanoseconds / 100U);
            return dateTimeOffset.ToString("O", CultureInfo.InvariantCulture);
        }

        private static void WriteCommandLine(string command, string description, string? currentValue = null)
        {
            string line = "  " + command.PadRight(40) + description;
            if (!string.IsNullOrWhiteSpace(currentValue))
            {
                line += " (current: " + currentValue + ")";
            }

            Console.WriteLine(line);
        }
    }
}
