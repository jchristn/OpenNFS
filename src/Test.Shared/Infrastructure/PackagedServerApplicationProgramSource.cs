namespace Test.Shared.Infrastructure
{
    using System;

    internal static class PackagedServerApplicationProgramSource
    {
        internal static string Create(bool denyMounts)
        {
            string mountAuthorizationLines = denyMounts
                ? """
                .UseMountAuthorization(new DenyAllMountAuthorization())
"""
                : string.Empty;
            string extraTypes = denyMounts
                ? """

internal sealed class DenyAllMountAuthorization : INfsMountAuthorization
{
    public Task<NfsAuthorizeMountResponse> AuthorizeAsync(NfsAuthorizeMountRequest request)
    {
        return Task.FromResult(new NfsAuthorizeMountResponse(NfsMountAccessDisposition.Deny));
    }
}
"""
                : string.Empty;
            string helloContents = denyMounts ? "denied-from-packed-server" : "hello-from-packed-server";

            string template = """
using System;
using System.IO;
using System.Threading.Tasks;
using OpenNFS.Server;
using OpenNFS.Server.FileHandles;
__MOUNT_AUTH_USINGS__

public static class Program
{
    public static async Task<int> Main()
    {
        string rootDirectory = Path.Combine(Path.GetTempPath(), "OpenNfsPackServerRuntime", Guid.NewGuid().ToString("N"));
        string exportRoot = Path.Combine(rootDirectory, "export");
        string docsDirectory = Path.Combine(exportRoot, "docs");
        string mappingPath = Path.Combine(rootDirectory, "handles.json");

        try
        {
            Directory.CreateDirectory(docsDirectory);
            File.WriteAllText(Path.Combine(exportRoot, "hello.txt"), "__HELLO_CONTENTS__");
            File.WriteAllText(Path.Combine(docsDirectory, "readme.txt"), "readme-from-packed-server");

            await using OpenNfsServerApplication application = new OpenNfsServerBuilder()
                .WithServerName("Packed Runtime")
                .WithListenerAddress("0.0.0.0")
                .UseLocalFileSystem()
                .UseFileHandleProvider(new PersistentMappingHandleProvider(mappingPath))
__MOUNT_AUTH_LINES__                .AddExport("/data", exportRoot)
                .BuildApplication(
                    new OpenNfsServerApplicationOptions
                    {
                        ListenerAddress = "0.0.0.0",
                        EnableNfs41 = true,
                        EnableNfs42 = true,
                        MountPort = 0,
                        NfsPort = 0,
                        Nfs40Port = 0,
                        Nfs41Port = 0,
                        Nfs42Port = 0,
                        NlmPort = 0,
                        NsmPort = 0,
                    });

            await application.StartAsync().ConfigureAwait(false);
            Console.WriteLine("READY mountPort=" + application.MountPort + " nfsPort=" + application.NfsPort + " nfs40Port=" + application.Nfs40Port + " nfs41Port=" + application.Nfs41Port + " nfs42Port=" + application.Nfs42Port + " exportPath=/data");
            await Console.In.ReadLineAsync().ConfigureAwait(false);
            return 0;
        }
        finally
        {
            if (Directory.Exists(rootDirectory))
            {
                Directory.Delete(rootDirectory, recursive: true);
            }
        }
    }
}
__EXTRA_TYPES__
""";

            string mountAuthUsings = denyMounts
                ? "using OpenNFS.Server.Abstractions;" + Environment.NewLine
                    + "using OpenNFS.Server.Requests;" + Environment.NewLine
                    + "using OpenNFS.Server.Responses;"
                : string.Empty;

            return template
                .Replace("__MOUNT_AUTH_USINGS__", mountAuthUsings, StringComparison.Ordinal)
                .Replace("__HELLO_CONTENTS__", helloContents, StringComparison.Ordinal)
                .Replace("__MOUNT_AUTH_LINES__", mountAuthorizationLines, StringComparison.Ordinal)
                .Replace("__EXTRA_TYPES__", extraTypes, StringComparison.Ordinal);
        }
    }
}
