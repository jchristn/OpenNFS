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
            Console.WriteLine("  ? / help                show this menu");
            Console.WriteLine("  q / quit / exit         quit");
            Console.WriteLine("  cls / clear             clear the screen");
            Console.WriteLine("  show                    show current client configuration and mount state");
            Console.WriteLine();
            Console.WriteLine("Configuration:");
            Console.WriteLine("  server [host] [port]                    set the primary NFS endpoint");
            Console.WriteLine("  mountendpoint [host] [port]             set the dedicated MOUNT v3 endpoint");
            Console.WriteLine("  mountendpoint clear                     clear the dedicated MOUNT endpoint");
            Console.WriteLine("  transport [tcp|tcpudp]                  set the transport policy");
            Console.WriteLine("  auth [none|authsys|rpcsecgss]           set the authentication flavor");
            Console.WriteLine("  authsys [machine] [uid] [gid] [groups]  set AUTH_SYS identity values");
            Console.WriteLine();
            Console.WriteLine("Connection and export bootstrap:");
            Console.WriteLine("  connect                  build and open the client");
            Console.WriteLine("  disconnect               close and dispose the client");
            Console.WriteLine("  exports                  list available exports through MOUNT v3");
            Console.WriteLine("  mounts                   list current server mount records through MOUNT v3");
            Console.WriteLine("  mount [exportPath]       mount an export and open a path-first session");
            Console.WriteLine("  umount                   unmount the current export and close the session");
            Console.WriteLine();
            Console.WriteLine("Mounted-session operations:");
            Console.WriteLine("  pwd                      show the current export-relative directory");
            Console.WriteLine("  cd [path]                change the current export-relative directory");
            Console.WriteLine("  ls [path]                list directory contents");
            Console.WriteLine("  stat [path]              show file or directory metadata");
            Console.WriteLine("  cat [path]               read and print a UTF-8 file");
            Console.WriteLine("  write [path] [text]      overwrite a file; prompt only when [text] is omitted");
            Console.WriteLine("  touch [path]             create a file if it does not exist");
            Console.WriteLine("  mkdir [path]             create a directory");
            Console.WriteLine("  rm [path]                delete a file");
            Console.WriteLine("  rmdir [path]             delete an empty directory");
            Console.WriteLine("  put [local] [remote]     upload a local file");
            Console.WriteLine("  get [remote] [local]     download a remote file");
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
            Console.WriteLine("  Primary endpoint : " + Configuration.ServerHost + ":" + Configuration.ServerPort.ToString(CultureInfo.InvariantCulture));
            if (Configuration.HasExplicitMountEndpoint)
            {
                Console.WriteLine("  MOUNT endpoint   : " + Configuration.MountServerHost + ":" + Configuration.MountServerPort!.Value.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                Console.WriteLine("  MOUNT endpoint   : primary endpoint");
            }

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
    }
}
