namespace Sample.OpenNfsServer
{
    internal sealed class SampleServerConfigurationFile
    {
        public bool? DenyMounts { get; set; }

        public string? ExportPath { get; set; }

        public string? ListenerAddress { get; set; }

        public string? MappingPath { get; set; }

        public int? MountPort { get; set; }

        public int? NfsPort { get; set; }

        public int? Nfs40Port { get; set; }

        public string? Owner { get; set; }

        public string? OwnerGroup { get; set; }

        public string? ServerName { get; set; }

        public string? SourcePath { get; set; }

        public string? KerberosTargetSpn { get; set; }

        public string? KerberosKeytab { get; set; }
    }
}
