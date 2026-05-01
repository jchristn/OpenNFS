namespace Test.Shared.Infrastructure
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;

    internal sealed class TemporaryExportRoot : IAsyncDisposable, IDisposable
    {
        private bool _disposed;

        private TemporaryExportRoot(string rootPath)
        {
            RootPath = rootPath;
            ExportRootPath = Path.Combine(rootPath, "export");
            StateDirectoryPath = Path.Combine(rootPath, "state");
            MappingFilePath = Path.Combine(StateDirectoryPath, "handles.json");

            Directory.CreateDirectory(ExportRootPath);
            Directory.CreateDirectory(StateDirectoryPath);
        }

        public string RootPath { get; }

        public string ExportRootPath { get; }

        public string StateDirectoryPath { get; }

        public string MappingFilePath { get; }

        public static TemporaryExportRoot Create(string scenarioName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(scenarioName);

            string sanitizedScenarioName = scenarioName.Replace(Path.DirectorySeparatorChar, '-')
                .Replace(Path.AltDirectorySeparatorChar, '-');
            string rootPath = Path.Combine(
                Path.GetTempPath(),
                "OpenNFS.TestExports",
                sanitizedScenarioName + "." + Guid.NewGuid().ToString("N"));
            return new TemporaryExportRoot(rootPath);
        }

        public void CreateDirectory(string relativePath)
        {
            string targetPath = ResolveRelativePath(relativePath);
            Directory.CreateDirectory(targetPath);
        }

        public string ResolveRelativePath(string relativePath)
        {
            ArgumentNullException.ThrowIfNull(relativePath);

            string trimmedRelativePath = relativePath.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return Path.Combine(ExportRootPath, trimmedRelativePath);
        }

        public void WriteAllBytes(string relativePath, byte[] contents)
        {
            ArgumentNullException.ThrowIfNull(contents);

            string targetPath = ResolveRelativePath(relativePath);
            string? parentDirectory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(parentDirectory))
            {
                Directory.CreateDirectory(parentDirectory);
            }

            File.WriteAllBytes(targetPath, contents);
        }

        public void WriteAllText(string relativePath, string contents)
        {
            ArgumentNullException.ThrowIfNull(contents);
            WriteAllBytes(relativePath, Encoding.UTF8.GetBytes(contents));
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public ValueTask DisposeAsync()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
            return ValueTask.CompletedTask;
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing && Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }

            _disposed = true;
        }
    }
}
