namespace OpenNFS.Server.FileHandles
{
    internal sealed class PersistentHandleTargetRecord
    {
        public string ExportPath { get; set; } = string.Empty;

        public string SourcePath { get; set; } = string.Empty;

        public string? IdentityScheme { get; set; }

        public string? IdentityValue { get; set; }
    }
}
