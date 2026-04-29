namespace Test.Shared.Infrastructure
{
    using System;
    using System.IO;

    internal static class RepositoryPaths
    {
        public static string GetRepositoryRoot()
        {
            DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory != null)
            {
                string planPath = Path.Combine(directory.FullName, "OPENNFS.md");
                string sourceDirectory = Path.Combine(directory.FullName, "src");
                if (File.Exists(planPath) && Directory.Exists(sourceDirectory))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Unable to locate the OpenNFS repository root from the current test host base directory.");
        }
    }
}

