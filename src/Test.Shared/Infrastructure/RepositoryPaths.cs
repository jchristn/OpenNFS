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
                string readmePath = Path.Combine(directory.FullName, "README.md");
                string solutionPath = Path.Combine(directory.FullName, "src", "OpenNFS.sln");
                if (File.Exists(readmePath) && File.Exists(solutionPath))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Unable to locate the OpenNFS repository root from the current test host base directory.");
        }
    }
}
