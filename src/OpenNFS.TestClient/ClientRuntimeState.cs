namespace OpenNFS.TestClient
{
    using OpenNFS.Client;

    internal static class ClientRuntimeState
    {
        internal static readonly ClientConfiguration Configuration = new ClientConfiguration();
        internal static OpenNfsClient? Client;
        internal static string CurrentDirectory = "/";
        internal static bool IsScripted = false;
        internal static string? MountedExportPath;
        internal static bool RunForever = true;
        internal static OpenNfsMountSession? Session;
    }
}
