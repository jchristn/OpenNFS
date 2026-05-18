namespace OpenNFS.TestClient
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client;
    using static OpenNFS.TestClient.ClientConfigurationSupport;
    using static OpenNFS.TestClient.ClientInputSupport;
    using static OpenNFS.TestClient.ClientRuntimeState;

    internal static class ClientConnectionSupport
    {
        internal static async Task ConnectAsync(CancellationToken cancellationToken)
        {
            if (Client is not null && Client.State == OpenNfsClientState.Open)
            {
                Console.WriteLine("[INFO] Client is already connected.");
                return;
            }

            await DisconnectAsync(unmountIfNeeded: false, CancellationToken.None).ConfigureAwait(false);

            Client = BuildClient();
            await Client.ConnectAsync(cancellationToken).ConfigureAwait(false);
            Console.WriteLine("[OK] Client connected.");
        }

        internal static async Task DisconnectAsync(bool unmountIfNeeded, CancellationToken cancellationToken)
        {
            if (unmountIfNeeded && Session is not null && Client is not null && !string.IsNullOrWhiteSpace(MountedExportPath))
            {
                try
                {
                    await Client.Exports.UnmountV3Async(MountedExportPath, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    Console.WriteLine("[WARN] Unmount cleanup failed: " + exception.Message);
                }
            }

            if (Session is not null)
            {
                await Session.DisposeAsync().ConfigureAwait(false);
                Session = null;
                MountedExportPath = null;
                CurrentDirectory = "/";
            }

            if (Client is null)
            {
                return;
            }

            try
            {
                if (Client.State == OpenNfsClientState.Open)
                {
                    await Client.DisconnectAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                await Client.DisposeAsync().ConfigureAwait(false);
                Client = null;
            }
        }

        internal static async Task ListExportsAsync(CancellationToken cancellationToken)
        {
            OpenNfsClient client = RequireConnectedClient();
            IReadOnlyList<OpenNfsExportV3Entry> exports = await client.Exports.ListExportsV3Async(cancellationToken).ConfigureAwait(false);
            if (exports.Count == 0)
            {
                Console.WriteLine("[INFO] No exports were returned.");
                return;
            }

            Console.WriteLine("Exports:");
            for (int index = 0; index < exports.Count; index++)
            {
                OpenNfsExportV3Entry export = exports[index];
                Console.WriteLine("  " + export.ExportPath);
                if (export.AuthorizedClientGroups.Count > 0)
                {
                    Console.WriteLine("    groups: " + string.Join(", ", export.AuthorizedClientGroups));
                }
            }
        }

        internal static async Task ListMountsAsync(CancellationToken cancellationToken)
        {
            OpenNfsClient client = RequireConnectedClient();
            IReadOnlyList<OpenNfsMountedExportV3Entry> mounts = await client.Exports.ListMountsV3Async(cancellationToken).ConfigureAwait(false);
            if (mounts.Count == 0)
            {
                Console.WriteLine("[INFO] No mounted exports were returned.");
                return;
            }

            Console.WriteLine("Mounted exports:");
            for (int index = 0; index < mounts.Count; index++)
            {
                OpenNfsMountedExportV3Entry mount = mounts[index];
                Console.WriteLine("  " + mount.HostName + " -> " + mount.ExportPath);
            }
        }

        internal static async Task MountAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            OpenNfsClient client = RequireConnectedClient();
            if (Session is not null)
            {
                throw new InvalidOperationException("An export is already mounted. Use umount before mounting another export.");
            }

            string exportPath = arguments.Count > 1 ? arguments[1] : Configuration.PreferredExportPath;
            Session = await client.MountAsync(exportPath, cancellationToken).ConfigureAwait(false);
            MountedExportPath = Session.ExportPath;
            Configuration.PreferredExportPath = Session.ExportPath;
            CurrentDirectory = "/";
            Console.WriteLine("[OK] Mounted " + MountedExportPath + ".");
        }

        internal static async Task UnmountAsync(CancellationToken cancellationToken)
        {
            OpenNfsClient client = RequireConnectedClient();
            if (Session is null || string.IsNullOrWhiteSpace(MountedExportPath))
            {
                throw new InvalidOperationException("No export is currently mounted.");
            }

            await client.Exports.UnmountV3Async(MountedExportPath, cancellationToken).ConfigureAwait(false);
            await Session.DisposeAsync().ConfigureAwait(false);
            Console.WriteLine("[OK] Unmounted " + MountedExportPath + ".");
            Session = null;
            MountedExportPath = null;
            CurrentDirectory = "/";
        }
    }
}
