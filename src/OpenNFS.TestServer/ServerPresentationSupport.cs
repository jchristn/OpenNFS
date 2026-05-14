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
            Console.WriteLine("  ? / help              show this menu");
            Console.WriteLine("  q / quit / exit       quit");
            Console.WriteLine("  cls / clear           clear the screen");
            Console.WriteLine("  show                  show server configuration and runtime state");
            Console.WriteLine();
            Console.WriteLine("Listener and export configuration:");
            Console.WriteLine("  server [name]         set the server display name");
            Console.WriteLine("  address [value]       set the listener address");
            Console.WriteLine("  mountport [port]      set the MOUNT v3 TCP port (0 allowed)");
            Console.WriteLine("  nfsport [port]        set the NFSv3 TCP port (0 allowed)");
            Console.WriteLine("  nfs40port [port]      set the NFSv4.0 TCP port (0 allowed)");
            Console.WriteLine("  nfs41port [port]      set the NFSv4.1 TCP port (0 allowed)");
            Console.WriteLine("  nfs42port [port]      set the NFSv4.2 TCP port (0 allowed)");
            Console.WriteLine("  export [path]         set the client-visible export path");
            Console.WriteLine("  readonly [on|off]     toggle read-only export mode");
            Console.WriteLine("  denymounts [on|off]   toggle explicit mount denial");
            Console.WriteLine("  v3 [on|off]           enable or disable the NFSv3-era listener surface");
            Console.WriteLine("  v40 [on|off]          enable or disable the NFSv4.0 listener surface");
            Console.WriteLine("  v41 [on|off]          enable or disable the NFSv4.1 session-management surface");
            Console.WriteLine("  v42 [on|off]          enable or disable the initial NFSv4.2 listener surface");
            Console.WriteLine("  preserve [on|off]     preserve the temporary backing store on exit");
            Console.WriteLine();
            Console.WriteLine("Backing store helpers:");
            Console.WriteLine("  root                  show the temporary backing-store path");
            Console.WriteLine("  seed                  ensure sample seed content exists");
            Console.WriteLine("  tree                  show the current backing-store tree");
            Console.WriteLine("  mkdir [path]          create a backing-store directory");
            Console.WriteLine("  write [path]          write a UTF-8 backing-store file");
            Console.WriteLine("  delete [path]         delete a backing-store file or directory");
            Console.WriteLine("  resetroot             clear and reseed the temporary backing store");
            Console.WriteLine();
            Console.WriteLine("Server lifecycle:");
            Console.WriteLine("  start                 start the server");
            Console.WriteLine("  stop                  stop the server");
            Console.WriteLine("  restart               restart the server");
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
    }
}
