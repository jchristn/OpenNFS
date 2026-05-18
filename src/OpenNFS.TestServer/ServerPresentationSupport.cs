namespace OpenNFS.TestServer
{
    using System;
    using System.Globalization;
    using static OpenNFS.TestServer.ServerRuntimeState;

    internal static class ServerPresentationSupport
    {
        internal static void ShowMenu()
        {
            Console.WriteLine();
            Console.WriteLine("Available commands:");
            WriteCommandLine("? / help", "show this menu");
            WriteCommandLine("q / quit / exit", "quit");
            WriteCommandLine("cls / clear", "clear the screen");
            WriteCommandLine("show", "show server configuration and runtime state");
            Console.WriteLine();
            Console.WriteLine("Listener and export configuration:");
            WriteCommandLine("server [name]", "set the server display name", Configuration.ServerName);
            WriteCommandLine("address [value]", "set the listener address", Configuration.ListenerAddress);
            WriteCommandLine("mountport [port]", "set the MOUNT v3 TCP port (0 allowed)", Configuration.MountPort.ToString(CultureInfo.InvariantCulture));
            WriteCommandLine("nfsport [port]", "set the NFSv3 TCP port (0 allowed)", Configuration.NfsPort.ToString(CultureInfo.InvariantCulture));
            WriteCommandLine("nfs40port [port]", "set the NFSv4.0 TCP port (0 allowed)", Configuration.Nfs40Port.ToString(CultureInfo.InvariantCulture));
            WriteCommandLine("nfs41port [port]", "set the NFSv4.1 TCP port (0 allowed)", Configuration.Nfs41Port.ToString(CultureInfo.InvariantCulture));
            WriteCommandLine("nfs42port [port]", "set the NFSv4.2 TCP port (0 allowed)", Configuration.Nfs42Port.ToString(CultureInfo.InvariantCulture));
            WriteCommandLine("export [path]", "set the client-visible export path", Configuration.ExportPath);
            WriteCommandLine("readonly [on|off]", "toggle read-only export mode", FormatToggle(Configuration.ReadOnly));
            WriteCommandLine("denymounts [on|off]", "toggle explicit mount denial", FormatToggle(Configuration.DenyMounts));
            WriteCommandLine("v3 [on|off]", "enable or disable the NFSv3-era listener surface", FormatToggle(Configuration.EnableNfsV3));
            WriteCommandLine("v40 [on|off]", "enable or disable the NFSv4.0 listener surface", FormatToggle(Configuration.EnableNfsV40));
            WriteCommandLine("v41 [on|off]", "enable or disable the NFSv4.1 session-management surface", FormatToggle(Configuration.EnableNfsV41));
            WriteCommandLine("v42 [on|off]", "enable or disable the initial NFSv4.2 listener surface", FormatToggle(Configuration.EnableNfsV42));
            WriteCommandLine("preserve [on|off]", "preserve the temporary backing store on exit", FormatToggle(Configuration.PreserveOnExit));
            Console.WriteLine();
            Console.WriteLine("Backing store helpers:");
            WriteCommandLine("root", "show the temporary backing-store path");
            WriteCommandLine("seed", "ensure sample seed content exists");
            WriteCommandLine("tree", "show the current backing-store tree");
            WriteCommandLine("mkdir [path]", "create a backing-store directory");
            WriteCommandLine("write [path]", "write a UTF-8 backing-store file");
            WriteCommandLine("delete [path]", "delete a backing-store file or directory");
            WriteCommandLine("resetroot", "clear and reseed the temporary backing store");
            Console.WriteLine();
            Console.WriteLine("Server lifecycle:");
            WriteCommandLine("start", "start the server");
            WriteCommandLine("stop", "stop the server");
            WriteCommandLine("restart", "restart the server");
            Console.WriteLine();
            Console.WriteLine("Notes:");
            Console.WriteLine("  - The backing store is a temporary local directory with persistent filehandle mappings.");
            Console.WriteLine("  - NFSv3-era and NFSv4.0 listeners are available by default; the NFSv4.1 and initial NFSv4.2 listeners are opt-in.");
            Console.WriteLine("  - This tool is meant for manual exercise and remote-client integration, not conformance claims.");
            Console.WriteLine();
        }

        internal static void ShowStatus()
        {
            Console.WriteLine();
            Console.WriteLine("Server configuration:");
            Console.WriteLine("  Name           : " + Configuration.ServerName);
            Console.WriteLine("  Address        : " + Configuration.ListenerAddress);
            Console.WriteLine("  Export path    : " + Configuration.ExportPath);
            Console.WriteLine("  Backing root   : " + Configuration.RootPath);
            Console.WriteLine("  Handle map     : " + Configuration.MappingPath);
            Console.WriteLine("  Read-only      : " + Configuration.ReadOnly);
            Console.WriteLine("  Deny mounts    : " + Configuration.DenyMounts);
            Console.WriteLine("  Preserve root  : " + Configuration.PreserveOnExit);
            Console.WriteLine("  Enable NFSv3   : " + Configuration.EnableNfsV3);
            Console.WriteLine("  Enable NFSv4.0 : " + Configuration.EnableNfsV40);
            Console.WriteLine("  Enable NFSv4.1 : " + Configuration.EnableNfsV41);
            Console.WriteLine("  Enable NFSv4.2 : " + Configuration.EnableNfsV42);

            if (Application is null || !Application.IsRunning)
            {
                Console.WriteLine("  Mount port     : " + Configuration.MountPort.ToString(CultureInfo.InvariantCulture) + " (configured)");
                Console.WriteLine("  NFSv3 port     : " + Configuration.NfsPort.ToString(CultureInfo.InvariantCulture) + " (configured)");
                Console.WriteLine("  NFSv4.0 port   : " + Configuration.Nfs40Port.ToString(CultureInfo.InvariantCulture) + " (configured)");
                Console.WriteLine("  NFSv4.1 port   : " + Configuration.Nfs41Port.ToString(CultureInfo.InvariantCulture) + " (configured)");
                Console.WriteLine("  NFSv4.2 port   : " + Configuration.Nfs42Port.ToString(CultureInfo.InvariantCulture) + " (configured)");
                Console.WriteLine("  Running        : false");
            }
            else
            {
                Console.WriteLine("  Mount port     : " + Application.MountPort.ToString(CultureInfo.InvariantCulture) + " (bound)");
                Console.WriteLine("  NFSv3 port     : " + Application.NfsPort.ToString(CultureInfo.InvariantCulture) + " (bound)");
                Console.WriteLine("  NFSv4.0 port   : " + Application.Nfs40Port.ToString(CultureInfo.InvariantCulture) + " (bound)");
                Console.WriteLine("  NFSv4.1 port   : " + Application.Nfs41Port.ToString(CultureInfo.InvariantCulture) + " (bound)");
                Console.WriteLine("  NFSv4.2 port   : " + Application.Nfs42Port.ToString(CultureInfo.InvariantCulture) + " (bound)");
                Console.WriteLine("  Running        : true");
            }

            Console.WriteLine();
        }

        private static string FormatToggle(bool value)
        {
            return value ? "on" : "off";
        }

        private static void WriteCommandLine(string command, string description, string? currentValue = null)
        {
            string line = "  " + command.PadRight(30) + description;
            if (!string.IsNullOrWhiteSpace(currentValue))
            {
                line += " (current: " + currentValue + ")";
            }

            Console.WriteLine(line);
        }
    }
}
