namespace Test.Shared.Infrastructure
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Xml.Linq;

    internal static class ExternalPackageConsumerSupport
    {
        public static async Task<DotnetCommandResult> RunSinglePackageConsoleAppAsync(
            string packageProjectRelativePath,
            string packageId,
            string programSource,
            CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(packageProjectRelativePath);
            ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
            ArgumentException.ThrowIfNullOrWhiteSpace(programSource);

            string repositoryRoot = RepositoryPaths.GetRepositoryRoot();
            string tempRoot = Path.Combine(Path.GetTempPath(), "OpenNFS.ExternalConsumer", Guid.NewGuid().ToString("N"));
            string feedDirectory = Path.Combine(tempRoot, "feed");
            string projectDirectory = Path.Combine(tempRoot, "consumer");

            Directory.CreateDirectory(feedDirectory);
            Directory.CreateDirectory(projectDirectory);

            try
            {
                string packageProjectPath = Path.Combine(repositoryRoot, packageProjectRelativePath);
                await DotnetCli.RunCheckedAsync(
                    new[]
                    {
                        "pack",
                        packageProjectPath,
                        "-c",
                        "Release",
                        "-o",
                        feedDirectory,
                    },
                    repositoryRoot,
                    cancellationToken,
                    timeout: TimeSpan.FromMinutes(5)).ConfigureAwait(false);

                string packageVersion = ResolvePackedPackageVersion(feedDirectory, packageId);
                string projectPath = Path.Combine(projectDirectory, "Consumer.csproj");
                string programPath = Path.Combine(projectDirectory, "Program.cs");
                string nuGetConfigPath = Path.Combine(projectDirectory, "NuGet.Config");

                File.WriteAllText(projectPath, BuildProjectFile(packageId, packageVersion));
                File.WriteAllText(programPath, programSource);
                WriteNuGetConfig(nuGetConfigPath, feedDirectory);

                await DotnetCli.RunCheckedAsync(
                    new[]
                    {
                        "restore",
                        projectPath,
                        "--configfile",
                        nuGetConfigPath,
                    },
                    projectDirectory,
                    cancellationToken,
                    timeout: TimeSpan.FromMinutes(3)).ConfigureAwait(false);

                return await DotnetCli.RunCheckedAsync(
                    new[]
                    {
                        "run",
                        "--project",
                        projectPath,
                        "-c",
                        "Release",
                        "--no-restore",
                    },
                    projectDirectory,
                    cancellationToken,
                    timeout: TimeSpan.FromMinutes(3)).ConfigureAwait(false);
            }
            finally
            {
                TryDeleteDirectory(tempRoot);
            }
        }

        private static string BuildProjectFile(string packageId, string packageVersion)
        {
            return "<Project Sdk=\"Microsoft.NET.Sdk\">" + Environment.NewLine
                + "  <PropertyGroup>" + Environment.NewLine
                + "    <OutputType>Exe</OutputType>" + Environment.NewLine
                + "    <TargetFramework>net8.0</TargetFramework>" + Environment.NewLine
                + "    <ImplicitUsings>disable</ImplicitUsings>" + Environment.NewLine
                + "    <Nullable>enable</Nullable>" + Environment.NewLine
                + "  </PropertyGroup>" + Environment.NewLine
                + "  <ItemGroup>" + Environment.NewLine
                + "    <PackageReference Include=\"" + packageId + "\" Version=\"" + packageVersion + "\" />" + Environment.NewLine
                + "  </ItemGroup>" + Environment.NewLine
                + "</Project>" + Environment.NewLine;
        }

        private static string ResolvePackedPackageVersion(string feedDirectory, string packageId)
        {
            string packagePrefix = packageId + ".";
            string[] packagePaths = Directory.GetFiles(feedDirectory, packageId + ".*.nupkg", SearchOption.TopDirectoryOnly)
                .Where(static path => !path.EndsWith(".snupkg", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (packagePaths.Length != 1)
            {
                throw new InvalidOperationException(
                    "Expected exactly one packed '" + packageId + "' package in '" + feedDirectory + "', but found "
                    + packagePaths.Length + ".");
            }

            string fileName = Path.GetFileName(packagePaths[0]);
            if (!fileName.StartsWith(packagePrefix, StringComparison.Ordinal)
                || !fileName.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Could not parse the packed package version from '" + fileName + "'.");
            }

            return fileName.Substring(
                packagePrefix.Length,
                fileName.Length - packagePrefix.Length - ".nupkg".Length);
        }

        private static void TryDeleteDirectory(string directoryPath)
        {
            try
            {
                if (Directory.Exists(directoryPath))
                {
                    Directory.Delete(directoryPath, recursive: true);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static void WriteNuGetConfig(string configPath, string feedDirectory)
        {
            XDocument document = new XDocument(
                new XElement(
                    "configuration",
                    new XElement(
                        "packageSources",
                        new XElement("clear"),
                        new XElement(
                            "add",
                            new XAttribute("key", "local"),
                            new XAttribute("value", Path.GetFullPath(feedDirectory))))));
            document.Save(configPath);
        }
    }
}
