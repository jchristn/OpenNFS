namespace Test.Shared.Infrastructure
{
    using System;
    using System.IO;
    using System.Threading.Tasks;

    internal sealed class ExternalPackageConsumerProject : IAsyncDisposable
    {
        private readonly string _tempRoot;

        internal ExternalPackageConsumerProject(
            string tempRoot,
            string projectDirectory,
            string projectPath,
            string nuGetConfigPath)
        {
            _tempRoot = tempRoot;
            ProjectDirectory = projectDirectory;
            ProjectPath = projectPath;
            NuGetConfigPath = nuGetConfigPath;
        }

        internal string NuGetConfigPath { get; }

        internal string ProjectDirectory { get; }

        internal string ProjectPath { get; }

        public ValueTask DisposeAsync()
        {
            try
            {
                if (Directory.Exists(_tempRoot))
                {
                    Directory.Delete(_tempRoot, recursive: true);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            return ValueTask.CompletedTask;
        }
    }
}
