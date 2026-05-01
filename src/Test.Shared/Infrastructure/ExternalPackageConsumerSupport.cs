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
        public static async Task<ExternalPackageConsumerProject> CreateSinglePackageConsoleAppAsync(
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
            string uniquePackageVersion = BuildUniquePackageVersion(repositoryRoot);

            Directory.CreateDirectory(feedDirectory);
            Directory.CreateDirectory(projectDirectory);

            try
            {
                string packageProjectPath = Path.Combine(repositoryRoot, packageProjectRelativePath);
                await DotnetCli.RunCheckedAsync(
                    new[]
                    {
                        "pack",
                        "--disable-build-servers",
                        "--no-build",
                        packageProjectPath,
                        "-c",
                        "Release",
                        "-o",
                        feedDirectory,
                        "/p:BuildProjectReferences=false",
                        "/p:PackageVersion=" + uniquePackageVersion,
                    },
                    repositoryRoot,
                    cancellationToken,
                    timeout: TimeSpan.FromMinutes(5)).ConfigureAwait(false);

                string packageVersion = ResolvePackedPackageVersion(feedDirectory, packageId, uniquePackageVersion);
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
                        "--disable-build-servers",
                        projectPath,
                        "--configfile",
                        nuGetConfigPath,
                    },
                    projectDirectory,
                    cancellationToken,
                    timeout: TimeSpan.FromMinutes(3)).ConfigureAwait(false);

                return new ExternalPackageConsumerProject(tempRoot, projectDirectory, projectPath, nuGetConfigPath);
            }
            catch
            {
                TryDeleteDirectory(tempRoot);
                throw;
            }
        }

        public static async Task<DotnetCommandResult> RunSinglePackageConsoleAppAsync(
            string packageProjectRelativePath,
            string packageId,
            string programSource,
            CancellationToken cancellationToken)
        {
            await using ExternalPackageConsumerProject project = await CreateSinglePackageConsoleAppAsync(
                packageProjectRelativePath,
                packageId,
                programSource,
                cancellationToken).ConfigureAwait(false);

            return await DotnetCli.RunCheckedAsync(
                    new[]
                    {
                        "run",
                        "--disable-build-servers",
                        "--project",
                        project.ProjectPath,
                        "-c",
                        "Release",
                        "--no-restore",
                    },
                    project.ProjectDirectory,
                    cancellationToken,
                    timeout: TimeSpan.FromMinutes(3)).ConfigureAwait(false);
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

        private static string BuildUniquePackageVersion(string repositoryRoot)
        {
            string directoryBuildPropsPath = Path.Combine(repositoryRoot, "src", "Directory.Build.props");
            XDocument document = XDocument.Load(directoryBuildPropsPath);
            string? baseVersion = document.Root?
                .Elements("PropertyGroup")
                .Elements("Version")
                .Select(static element => element.Value.Trim())
                .FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value));

            if (string.IsNullOrWhiteSpace(baseVersion))
            {
                throw new InvalidOperationException(
                    "Could not determine the repository package version from '" + directoryBuildPropsPath + "'.");
            }

            string uniqueSuffix = Guid.NewGuid().ToString("N");
            return baseVersion.Contains('-', StringComparison.Ordinal)
                ? baseVersion + ".external." + uniqueSuffix
                : baseVersion + "-external." + uniqueSuffix;
        }

        private static string ResolvePackedPackageVersion(string feedDirectory, string packageId, string expectedPackageVersion)
        {
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
            string expectedFileName = packageId + "." + expectedPackageVersion + ".nupkg";
            if (!string.Equals(fileName, expectedFileName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Expected the packed package file name to be '" + expectedFileName + "', but found '" + fileName + "'.");
            }

            return expectedPackageVersion;
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
