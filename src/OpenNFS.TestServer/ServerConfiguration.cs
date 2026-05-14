namespace OpenNFS.TestServer
{
    using System;
    using System.IO;

    internal sealed class ServerConfiguration
    {
        private ServerConfiguration(string rootPath)
        {
            RootPath = rootPath;
            MappingPath = Path.Combine(rootPath, "handles.json");
        }

        public bool DenyMounts { get; set; }

        public bool EnableNfsV3 { get; set; } = true;

        public bool EnableNfsV40 { get; set; } = true;

        public bool EnableNfsV41 { get; set; }

        public bool EnableNfsV42 { get; set; }

        public string ExportPath { get; set; } = "/exports/test";

        public string ListenerAddress { get; set; } = "0.0.0.0";

        public string MappingPath { get; }

        public int MountPort { get; set; } = 20048;

        public int Nfs40Port { get; set; } = 3049;

        public int Nfs41Port { get; set; } = 4049;

        public int Nfs42Port { get; set; } = 5049;

        public int NfsPort { get; set; } = 2049;

        public bool PreserveOnExit { get; set; }

        public bool ReadOnly { get; set; }

        public string RootPath { get; }

        public string ServerName { get; set; } = "OpenNFS.TestServer";

        public static ServerConfiguration CreateDefault()
        {
            string rootPath = Path.Combine(Path.GetTempPath(), "OpenNFS.TestServer", Guid.NewGuid().ToString("N"));
            return new ServerConfiguration(rootPath);
        }
    }
}
