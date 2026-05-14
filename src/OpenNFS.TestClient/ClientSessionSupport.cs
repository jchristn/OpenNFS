namespace OpenNFS.TestClient
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client;
    using static OpenNFS.TestClient.ClientConfigurationSupport;
    using static OpenNFS.TestClient.ClientInputSupport;
    using static OpenNFS.TestClient.ClientPresentationSupport;
    using static OpenNFS.TestClient.ClientRuntimeState;

    internal static class ClientSessionSupport
    {
        internal static async Task ChangeDirectoryAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            OpenNfsMountSession session = RequireMountedSession();
            string inputPath = arguments.Count > 1 ? arguments[1] : ReadRequiredValue("Directory path");
            string resolvedPath = ResolveRemotePath(inputPath, allowCurrentDirectory: true);
            OpenNfsV3Attributes attributes = await session.Metadata.GetAttributesAsync(resolvedPath, cancellationToken).ConfigureAwait(false);
            if (attributes.FileType != OpenNfsV3FileType.Directory)
            {
                throw new InvalidOperationException("Path '" + resolvedPath + "' is not a directory.");
            }

            CurrentDirectory = resolvedPath;
            Console.WriteLine("[OK] Current directory is now " + CurrentDirectory + ".");
        }

        internal static async Task ListDirectoryAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            OpenNfsMountSession session = RequireMountedSession();
            string targetPath = arguments.Count > 1 ? ResolveRemotePath(arguments[1], allowCurrentDirectory: true) : CurrentDirectory;
            IReadOnlyList<OpenNfsV3DirectoryEntry> entries = await session.Directories.ListAsync(targetPath, cancellationToken).ConfigureAwait(false);

            if (entries.Count == 0)
            {
                Console.WriteLine("[INFO] Directory is empty.");
                return;
            }

            Console.WriteLine("Listing " + targetPath + ":");
            for (int index = 0; index < entries.Count; index++)
            {
                OpenNfsV3DirectoryEntry entry = entries[index];
                string childPath = CombineRemotePath(targetPath, entry.Name);
                string detail = string.Empty;

                try
                {
                    OpenNfsV3Attributes attributes = await session.Metadata.GetAttributesAsync(childPath, cancellationToken).ConfigureAwait(false);
                    detail = " [" + attributes.FileType + "] " + attributes.SizeBytes.ToString(CultureInfo.InvariantCulture) + " bytes";
                }
                catch (Exception exception)
                {
                    detail = " [unknown] " + exception.Message;
                }

                Console.WriteLine("  " + entry.Name + detail);
            }
        }

        internal static async Task ShowMetadataAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            OpenNfsMountSession session = RequireMountedSession();
            string targetPath = arguments.Count > 1 ? ResolveRemotePath(arguments[1], allowCurrentDirectory: true) : CurrentDirectory;
            OpenNfsV3Attributes attributes = await session.Metadata.GetAttributesAsync(targetPath, cancellationToken).ConfigureAwait(false);

            Console.WriteLine("Metadata for " + targetPath + ":");
            Console.WriteLine("  Type        : " + attributes.FileType);
            Console.WriteLine("  Mode        : 0" + Convert.ToString(attributes.Mode, 8));
            Console.WriteLine("  Links       : " + attributes.LinkCount.ToString(CultureInfo.InvariantCulture));
            Console.WriteLine("  Uid/Gid     : " + attributes.UserId.ToString(CultureInfo.InvariantCulture) + "/" + attributes.GroupId.ToString(CultureInfo.InvariantCulture));
            Console.WriteLine("  Size        : " + attributes.SizeBytes.ToString(CultureInfo.InvariantCulture) + " bytes");
            Console.WriteLine("  Used        : " + attributes.UsedBytes.ToString(CultureInfo.InvariantCulture) + " bytes");
            Console.WriteLine("  FileSystemId: " + attributes.FileSystemId.ToString(CultureInfo.InvariantCulture));
            Console.WriteLine("  FileId      : " + attributes.FileId.ToString(CultureInfo.InvariantCulture));
            Console.WriteLine("  AccessTime  : " + FormatNfsTime(attributes.AccessTime));
            Console.WriteLine("  ModifyTime  : " + FormatNfsTime(attributes.ModifyTime));
            Console.WriteLine("  ChangeTime  : " + FormatNfsTime(attributes.ChangeTime));
        }

        internal static async Task ReadFileAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            OpenNfsMountSession session = RequireMountedSession();
            string targetPath = arguments.Count > 1 ? ResolveRemotePath(arguments[1], allowCurrentDirectory: false) : ReadRequiredRemotePath("Remote file path");
            byte[] bytes = await session.Files.ReadAllBytesAsync(targetPath, cancellationToken).ConfigureAwait(false);
            Console.WriteLine("Read " + bytes.Length.ToString(CultureInfo.InvariantCulture) + " byte(s) from " + targetPath + ".");
            Console.WriteLine();
            Console.WriteLine(Encoding.UTF8.GetString(bytes));
        }

        internal static async Task WriteTextFileAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            OpenNfsMountSession session = RequireMountedSession();
            string targetPath = arguments.Count > 1 ? ResolveRemotePath(arguments[1], allowCurrentDirectory: false) : ReadRequiredRemotePath("Remote file path");
            string content = arguments.Count > 2
                ? string.Join(" ", arguments.Skip(2))
                : ReadMultilineBlock("Enter file contents. Finish with a single line containing only '.'");
            await EnsureRemoteFileExistsAsync(session, targetPath, cancellationToken).ConfigureAwait(false);
            await session.Files.WriteAllBytesAsync(targetPath, Encoding.UTF8.GetBytes(content), OpenNfsWriteStability.FileSync, cancellationToken).ConfigureAwait(false);
            Console.WriteLine("[OK] Wrote " + targetPath + ".");
        }

        internal static async Task CreateFileAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            OpenNfsMountSession session = RequireMountedSession();
            string targetPath = arguments.Count > 1 ? ResolveRemotePath(arguments[1], allowCurrentDirectory: false) : ReadRequiredRemotePath("Remote file path");
            await session.Directories.CreateFileAsync(targetPath, failIfExists: false, cancellationToken).ConfigureAwait(false);
            Console.WriteLine("[OK] Created file " + targetPath + ".");
        }

        internal static async Task CreateDirectoryAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            OpenNfsMountSession session = RequireMountedSession();
            string targetPath = arguments.Count > 1 ? ResolveRemotePath(arguments[1], allowCurrentDirectory: false) : ReadRequiredRemotePath("Remote directory path");
            await session.Directories.CreateDirectoryAsync(targetPath, cancellationToken).ConfigureAwait(false);
            Console.WriteLine("[OK] Created directory " + targetPath + ".");
        }

        internal static async Task DeleteFileAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            OpenNfsMountSession session = RequireMountedSession();
            string targetPath = arguments.Count > 1 ? ResolveRemotePath(arguments[1], allowCurrentDirectory: false) : ReadRequiredRemotePath("Remote file path");
            await session.Directories.DeleteFileAsync(targetPath, cancellationToken).ConfigureAwait(false);
            Console.WriteLine("[OK] Deleted file " + targetPath + ".");
        }

        internal static async Task DeleteDirectoryAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            OpenNfsMountSession session = RequireMountedSession();
            string targetPath = arguments.Count > 1 ? ResolveRemotePath(arguments[1], allowCurrentDirectory: false) : ReadRequiredRemotePath("Remote directory path");
            await session.Directories.DeleteDirectoryAsync(targetPath, cancellationToken).ConfigureAwait(false);
            Console.WriteLine("[OK] Deleted directory " + targetPath + ".");
        }

        internal static async Task UploadFileAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            OpenNfsMountSession session = RequireMountedSession();
            string localPath = arguments.Count > 1 ? arguments[1] : ReadRequiredValue("Local file path");
            string remotePath = arguments.Count > 2 ? ResolveRemotePath(arguments[2], allowCurrentDirectory: false) : ReadRequiredRemotePath("Remote file path");
            byte[] bytes = File.ReadAllBytes(localPath);
            await EnsureRemoteFileExistsAsync(session, remotePath, cancellationToken).ConfigureAwait(false);
            await session.Files.WriteAllBytesAsync(remotePath, bytes, OpenNfsWriteStability.FileSync, cancellationToken).ConfigureAwait(false);
            Console.WriteLine("[OK] Uploaded " + bytes.Length.ToString(CultureInfo.InvariantCulture) + " byte(s) to " + remotePath + ".");
        }

        internal static async Task DownloadFileAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            OpenNfsMountSession session = RequireMountedSession();
            string remotePath = arguments.Count > 1 ? ResolveRemotePath(arguments[1], allowCurrentDirectory: false) : ReadRequiredRemotePath("Remote file path");
            string localPath = arguments.Count > 2 ? arguments[2] : ReadRequiredValue("Local destination path");
            byte[] bytes = await session.Files.ReadAllBytesAsync(remotePath, cancellationToken).ConfigureAwait(false);
            string? localDirectory = Path.GetDirectoryName(localPath);
            if (!string.IsNullOrWhiteSpace(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            File.WriteAllBytes(localPath, bytes);
            Console.WriteLine("[OK] Downloaded " + bytes.Length.ToString(CultureInfo.InvariantCulture) + " byte(s) to " + localPath + ".");
        }

        internal static async Task EnsureRemoteFileExistsAsync(OpenNfsMountSession session, string remotePath, CancellationToken cancellationToken)
        {
            try
            {
                _ = await session.Metadata.GetAttributesAsync(remotePath, cancellationToken).ConfigureAwait(false);
            }
            catch (InvalidOperationException exception) when (exception.Message.Contains("NoEnt", StringComparison.Ordinal))
            {
                await session.Directories.CreateFileAsync(remotePath, failIfExists: false, cancellationToken).ConfigureAwait(false);
            }
        }

        internal static string ResolveRemotePath(string inputPath, bool allowCurrentDirectory)
        {
            string candidate = string.IsNullOrWhiteSpace(inputPath)
                ? (allowCurrentDirectory ? CurrentDirectory : throw new ArgumentException("A non-empty remote path is required.", nameof(inputPath)))
                : inputPath.Trim().Replace('\\', '/');

            bool absolute = candidate.StartsWith("/", StringComparison.Ordinal);
            List<string> segments = absolute
                ? new List<string>()
                : SplitSegments(CurrentDirectory);

            foreach (string segment in candidate.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (string.Equals(segment, ".", StringComparison.Ordinal))
                {
                    continue;
                }

                if (string.Equals(segment, "..", StringComparison.Ordinal))
                {
                    if (segments.Count > 0)
                    {
                        segments.RemoveAt(segments.Count - 1);
                    }

                    continue;
                }

                segments.Add(segment);
            }

            return segments.Count == 0 ? "/" : "/" + string.Join('/', segments);
        }

        internal static string ReadRequiredRemotePath(string prompt)
        {
            return ResolveRemotePath(ReadRequiredValue(prompt), allowCurrentDirectory: false);
        }

        internal static string CombineRemotePath(string basePath, string name)
        {
            if (string.Equals(basePath, "/", StringComparison.Ordinal))
            {
                return "/" + name;
            }

            return basePath.TrimEnd('/') + "/" + name;
        }

        internal static List<string> SplitSegments(string path)
        {
            return path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }
    }
}
