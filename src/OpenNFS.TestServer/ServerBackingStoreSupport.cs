namespace OpenNFS.TestServer
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using static OpenNFS.TestServer.ServerInputSupport;
    using static OpenNFS.TestServer.ServerRuntimeState;

    internal static class ServerBackingStoreSupport
    {
        private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        private static readonly byte[] Utf8Bom = Encoding.UTF8.GetPreamble();

        internal static void ShowBackingTree()
        {
            EnsureBackingStoreReady();
            Console.WriteLine("Backing store tree:");
            PrintTree(Configuration.RootPath, depth: 0);
        }

        internal static void CreateBackingDirectory(IReadOnlyList<string> arguments)
        {
            string relativePath = arguments.Count > 1 ? arguments[1] : ReadRequiredValue("Backing directory path");
            string fullPath = ResolveBackingPath(relativePath);
            Directory.CreateDirectory(fullPath);
            Console.WriteLine("[OK] Created " + fullPath + ".");
        }

        internal static void WriteBackingFile(IReadOnlyList<string> arguments)
        {
            string relativePath = arguments.Count > 1 ? arguments[1] : ReadRequiredValue("Backing file path");
            string fullPath = ResolveBackingPath(relativePath);
            string? directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string content = ReadMultilineBlock("Enter file contents. Finish with a single line containing only '.'");
            File.WriteAllText(fullPath, content, Utf8WithoutBom);
            Console.WriteLine("[OK] Wrote " + fullPath + ".");
        }

        internal static void DeleteBackingEntry(IReadOnlyList<string> arguments)
        {
            string relativePath = arguments.Count > 1 ? arguments[1] : ReadRequiredValue("Backing file or directory path");
            string fullPath = ResolveBackingPath(relativePath);

            if (Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, recursive: true);
                Console.WriteLine("[OK] Deleted directory " + fullPath + ".");
                return;
            }

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                Console.WriteLine("[OK] Deleted file " + fullPath + ".");
                return;
            }

            throw new FileNotFoundException("The specified backing-store path does not exist.", fullPath);
        }

        internal static void EnsureBackingStoreReady()
        {
            Directory.CreateDirectory(Configuration.RootPath);
            Directory.CreateDirectory(Path.GetDirectoryName(Configuration.MappingPath) ?? Configuration.RootPath);
            Directory.CreateDirectory(Path.Combine(Configuration.RootPath, "docs"));
            Directory.CreateDirectory(Path.Combine(Configuration.RootPath, "uploads"));

            WriteSeedFileIfMissing(Path.Combine(Configuration.RootPath, "hello.txt"), "hello from OpenNFS.TestServer\n");
            WriteSeedFileIfMissing(Path.Combine(Configuration.RootPath, "docs", "readme.txt"), "This is the OpenNFS.TestServer backing store.\n");
            WriteSeedFileIfMissing(Path.Combine(Configuration.RootPath, "uploads", ".keep"), string.Empty);
        }

        internal static void CleanupBackingStore()
        {
            if (Configuration.PreserveOnExit)
            {
                Console.WriteLine("[INFO] Preserving backing store at " + Configuration.RootPath + ".");
                return;
            }

            try
            {
                if (Directory.Exists(Configuration.RootPath))
                {
                    Directory.Delete(Configuration.RootPath, recursive: true);
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine("[WARN] Failed to delete temporary backing store: " + exception.Message);
            }
        }

        internal static void PrintTree(string path, int depth)
        {
            string indent = new string(' ', depth * 2);
            string displayName = depth == 0 ? path : Path.GetFileName(path);
            Console.WriteLine(indent + displayName);

            string[] directories = Directory.GetDirectories(path);
            Array.Sort(directories, StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < directories.Length; index++)
            {
                PrintTree(directories[index], depth + 1);
            }

            string[] files = Directory.GetFiles(path);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < files.Length; index++)
            {
                FileInfo fileInfo = new FileInfo(files[index]);
                Console.WriteLine(
                    indent
                    + "  "
                    + fileInfo.Name
                    + " ("
                    + fileInfo.Length.ToString(CultureInfo.InvariantCulture)
                    + " bytes)");
            }
        }

        internal static string ResolveBackingPath(string inputPath)
        {
            if (string.IsNullOrWhiteSpace(inputPath))
            {
                throw new ArgumentException("A non-empty backing-store path is required.", nameof(inputPath));
            }

            string candidate = inputPath.Trim().Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            string combined = Path.GetFullPath(Path.Combine(Configuration.RootPath, candidate));
            string root = Path.GetFullPath(Configuration.RootPath);
            if (!combined.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Backing-store paths must remain beneath the temporary root.");
            }

            return combined;
        }

        internal static void WriteSeedFileIfMissing(string path, string content)
        {
            if (!File.Exists(path))
            {
                File.WriteAllText(path, content, Utf8WithoutBom);
                return;
            }

            if (IsLegacyBomSeedFile(path, content))
            {
                File.WriteAllText(path, content, Utf8WithoutBom);
            }
        }

        private static bool IsLegacyBomSeedFile(string path, string expectedContent)
        {
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length < Utf8Bom.Length || !bytes.AsSpan(0, Utf8Bom.Length).SequenceEqual(Utf8Bom))
            {
                return false;
            }

            string decoded = Encoding.UTF8.GetString(bytes, Utf8Bom.Length, bytes.Length - Utf8Bom.Length);
            return string.Equals(decoded, expectedContent, StringComparison.Ordinal);
        }
    }
}
