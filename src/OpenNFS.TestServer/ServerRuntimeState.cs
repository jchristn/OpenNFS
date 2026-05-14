namespace OpenNFS.TestServer
{
    using OpenNFS.Server;

    internal static class ServerRuntimeState
    {
        internal static readonly ServerConfiguration Configuration = ServerConfiguration.CreateDefault();
        internal static OpenNfsServerApplication? Application;
        internal static bool RunForever = true;
    }
}
