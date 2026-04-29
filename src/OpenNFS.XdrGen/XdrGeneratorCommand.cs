namespace OpenNFS.XdrGen
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.XdrGen.Emission;
    using OpenNFS.XdrGen.Model;
    using OpenNFS.XdrGen.Parsing;

    /// <summary>
    /// Validates the XDR generator manifest, parses vendored XDR, and emits generated C# output.
    /// </summary>
    public sealed class XdrGeneratorCommand
    {
        private static readonly SemaphoreSlim GenerationGate = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Loads and validates a manifest, parses XDR input, and optionally writes target <c>Generated</c> files.
        /// </summary>
        /// <param name="options">Command options.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The validated generation result.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
        /// <exception cref="FileNotFoundException">Thrown when the manifest file does not exist.</exception>
        /// <exception cref="InvalidDataException">Thrown when the manifest is malformed or references missing paths.</exception>
        public async Task<XdrGenerationResult> RunAsync(XdrGeneratorOptions options, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(options);
            cancellationToken.ThrowIfCancellationRequested();

            await GenerationGate.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                string configurationPath = Path.GetFullPath(options.ConfigurationPath);
                if (!File.Exists(configurationPath))
                {
                    throw new FileNotFoundException("The XDR generator configuration file was not found.", configurationPath);
                }

                string configurationDirectory = Path.GetDirectoryName(configurationPath)
                    ?? throw new InvalidDataException("The XDR generator configuration path did not resolve to a parent directory.");

                string json = await File.ReadAllTextAsync(configurationPath, cancellationToken).ConfigureAwait(false);

                using JsonDocument document = JsonDocument.Parse(json);
                JsonElement root = document.RootElement;
                if (!root.TryGetProperty("projects", out JsonElement projectsElement) || projectsElement.ValueKind != JsonValueKind.Array)
                {
                    throw new InvalidDataException("The XDR generator configuration must contain a 'projects' array.");
                }

                List<XdrProjectMapping> mappings = new List<XdrProjectMapping>();
                List<string> ensuredDirectories = new List<string>();

                foreach (JsonElement projectElement in projectsElement.EnumerateArray())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string id = ReadRequiredString(projectElement, "id");
                    string inputDirectory = ResolvePath(configurationDirectory, ReadRequiredString(projectElement, "inputDirectory"));
                    string outputProjectPath = ResolvePath(configurationDirectory, ReadRequiredString(projectElement, "outputProjectPath"));
                    string outputNamespace = ReadRequiredString(projectElement, "outputNamespace");
                    List<string> configuredEntryPointFiles = ReadRequiredStringArray(projectElement, "entryPointFiles");

                    if (!Directory.Exists(inputDirectory))
                    {
                        throw new InvalidDataException("The XDR input directory does not exist for mapping '" + id + "': " + inputDirectory);
                    }

                    if (!File.Exists(outputProjectPath))
                    {
                        throw new InvalidDataException("The XDR output project path does not exist for mapping '" + id + "': " + outputProjectPath);
                    }

                    List<string> sourceFiles = Directory
                        .EnumerateFiles(inputDirectory, "*", SearchOption.AllDirectories)
                        .Where(file => IsSupportedSourceFile(file))
                        .Select(Path.GetFullPath)
                        .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    if (sourceFiles.Count < 1)
                    {
                        throw new InvalidDataException("The XDR input directory does not contain any supported source files for mapping '" + id + "': " + inputDirectory);
                    }

                    List<string> entryPointFiles = configuredEntryPointFiles
                        .Select(relativePath => ResolvePath(inputDirectory, relativePath))
                        .ToList();

                    foreach (string entryPointFile in entryPointFiles)
                    {
                        EnsurePathIsWithinDirectory(inputDirectory, entryPointFile, "entryPointFiles");

                        if (!File.Exists(entryPointFile))
                        {
                            throw new InvalidDataException("The XDR generation entry-point file was not found for mapping '" + id + "': " + entryPointFile);
                        }

                        if (!IsSupportedEntryPointFile(entryPointFile))
                        {
                            throw new InvalidDataException("The XDR generation entry-point file must be a '.x' file for mapping '" + id + "': " + entryPointFile);
                        }
                    }

                    List<string> duplicateEntryPoints = entryPointFiles
                        .GroupBy(file => file, StringComparer.OrdinalIgnoreCase)
                        .Where(group => group.Count() > 1)
                        .Select(group => group.Key)
                        .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    if (duplicateEntryPoints.Count > 0)
                    {
                        throw new InvalidDataException("The XDR generator mapping '" + id + "' contains duplicate generation entry-point files: " + string.Join(", ", duplicateEntryPoints));
                    }

                    string outputProjectDirectory = Path.GetDirectoryName(outputProjectPath)
                        ?? throw new InvalidDataException("The XDR output project path did not resolve to a parent directory for mapping '" + id + "'.");

                    string generatedDirectoryPath = Path.Combine(outputProjectDirectory, "Generated");

                    if (!options.CheckOnly)
                    {
                        Directory.CreateDirectory(generatedDirectoryPath);
                    }

                    XdrProjectMapping mapping = new XdrProjectMapping(
                        id: id,
                        inputDirectory: inputDirectory,
                        outputProjectPath: outputProjectPath,
                        outputNamespace: outputNamespace,
                        generatedDirectoryPath: generatedDirectoryPath,
                        sourceFiles: sourceFiles,
                        entryPointFiles: entryPointFiles);

                    mappings.Add(mapping);
                    ensuredDirectories.Add(generatedDirectoryPath);
                }

                XdrParser parser = new XdrParser();
                List<XdrDocument> parsedDocuments = new List<XdrDocument>();
                Dictionary<string, XdrDocument> parsedDocumentByPath = new Dictionary<string, XdrDocument>(StringComparer.OrdinalIgnoreCase);
                List<string> uniqueXdrSourceFiles = mappings
                    .SelectMany(mapping => mapping.SourceFiles)
                    .Where(IsSupportedEntryPointFile)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                foreach (string sourceFile in uniqueXdrSourceFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string source = await File.ReadAllTextAsync(sourceFile, cancellationToken).ConfigureAwait(false);
                    XdrDocument parsedDocument = parser.ParseDocument(sourceFile, source);
                    parsedDocuments.Add(parsedDocument);
                    parsedDocumentByPath.Add(Path.GetFullPath(sourceFile), parsedDocument);
                }

                XdrCSharpEmitter emitter = new XdrCSharpEmitter();
                List<GeneratedCSharpFile> generatedFiles = new List<GeneratedCSharpFile>();
                foreach (XdrProjectMapping mapping in mappings)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    IReadOnlyList<GeneratedCSharpFile> emittedFiles = emitter.EmitProject(mapping, parsedDocumentByPath);
                    generatedFiles.AddRange(emittedFiles);

                    if (options.CheckOnly)
                    {
                        await VerifyGeneratedOutputAsync(mapping, emittedFiles, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        await WriteGeneratedOutputAsync(mapping, emittedFiles, cancellationToken).ConfigureAwait(false);
                    }
                }

                XdrGeneratorConfiguration configuration = new XdrGeneratorConfiguration(configurationPath, mappings);
                return new XdrGenerationResult(configuration, options.CheckOnly, ensuredDirectories, parsedDocuments, generatedFiles);
            }
            finally
            {
                GenerationGate.Release();
            }
        }

        private static async Task WriteGeneratedOutputAsync(
            XdrProjectMapping mapping,
            IReadOnlyList<GeneratedCSharpFile> emittedFiles,
            CancellationToken cancellationToken)
        {
            if (Directory.Exists(mapping.GeneratedDirectoryPath))
            {
                foreach (string generatedFilePath in Directory.EnumerateFiles(mapping.GeneratedDirectoryPath, "*.g.cs", SearchOption.TopDirectoryOnly))
                {
                    File.Delete(generatedFilePath);
                }
            }

            UTF8Encoding encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            foreach (GeneratedCSharpFile generatedFile in emittedFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await File.WriteAllTextAsync(generatedFile.FilePath, generatedFile.SourceText, encoding, cancellationToken).ConfigureAwait(false);
            }
        }

        private static async Task VerifyGeneratedOutputAsync(
            XdrProjectMapping mapping,
            IReadOnlyList<GeneratedCSharpFile> emittedFiles,
            CancellationToken cancellationToken)
        {
            if (!Directory.Exists(mapping.GeneratedDirectoryPath))
            {
                throw new InvalidDataException(
                    "The generated output directory is missing for mapping '" + mapping.Id + "': " + mapping.GeneratedDirectoryPath + ". Run powershell -ExecutionPolicy Bypass -File .\\scripts\\Generate-Xdr.ps1 to regenerate the checked-in corpus.");
            }

            Dictionary<string, GeneratedCSharpFile> expectedFiles = emittedFiles.ToDictionary(
                file => Path.GetFullPath(file.FilePath),
                StringComparer.OrdinalIgnoreCase);
            List<string> actualFiles = Directory
                .EnumerateFiles(mapping.GeneratedDirectoryPath, "*.g.cs", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFullPath)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            List<string> missingFiles = expectedFiles.Keys
                .Except(actualFiles, StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (missingFiles.Count > 0)
            {
                throw new InvalidDataException(
                    "The checked-in generated output is missing file(s) for mapping '" + mapping.Id + "': " + string.Join(", ", missingFiles) + ". Run powershell -ExecutionPolicy Bypass -File .\\scripts\\Generate-Xdr.ps1 to regenerate the corpus.");
            }

            List<string> extraFiles = actualFiles
                .Except(expectedFiles.Keys, StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (extraFiles.Count > 0)
            {
                throw new InvalidDataException(
                    "The checked-in generated output contains unexpected file(s) for mapping '" + mapping.Id + "': " + string.Join(", ", extraFiles) + ". Run powershell -ExecutionPolicy Bypass -File .\\scripts\\Generate-Xdr.ps1 to normalize the corpus.");
            }

            foreach (string actualFile in actualFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string actualSource = await File.ReadAllTextAsync(actualFile, cancellationToken).ConfigureAwait(false);
                string expectedSource = expectedFiles[actualFile].SourceText;
                if (!string.Equals(actualSource, expectedSource, StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        "The checked-in generated output is stale for mapping '" + mapping.Id + "': " + actualFile + ". Run powershell -ExecutionPolicy Bypass -File .\\scripts\\Generate-Xdr.ps1 to regenerate the corpus.");
                }
            }
        }

        private static string ReadRequiredString(JsonElement parent, string propertyName)
        {
            if (!parent.TryGetProperty(propertyName, out JsonElement propertyValue) || propertyValue.ValueKind != JsonValueKind.String)
            {
                throw new InvalidDataException("The XDR generator configuration property '" + propertyName + "' is required and must be a string.");
            }

            string? value = propertyValue.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidDataException("The XDR generator configuration property '" + propertyName + "' must contain a non-empty string.");
            }

            return value;
        }

        private static List<string> ReadRequiredStringArray(JsonElement parent, string propertyName)
        {
            if (!parent.TryGetProperty(propertyName, out JsonElement propertyValue) || propertyValue.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException("The XDR generator configuration property '" + propertyName + "' is required and must be an array of strings.");
            }

            List<string> values = new List<string>();
            foreach (JsonElement item in propertyValue.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String)
                {
                    throw new InvalidDataException("The XDR generator configuration property '" + propertyName + "' must contain only string values.");
                }

                string? value = item.GetString();
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new InvalidDataException("The XDR generator configuration property '" + propertyName + "' must not contain empty strings.");
                }

                values.Add(value);
            }

            if (values.Count < 1)
            {
                throw new InvalidDataException("The XDR generator configuration property '" + propertyName + "' must contain at least one entry.");
            }

            return values;
        }

        private static string ResolvePath(string configurationDirectory, string relativeOrAbsolutePath)
        {
            if (Path.IsPathRooted(relativeOrAbsolutePath))
            {
                return Path.GetFullPath(relativeOrAbsolutePath);
            }

            return Path.GetFullPath(Path.Combine(configurationDirectory, relativeOrAbsolutePath));
        }

        private static void EnsurePathIsWithinDirectory(string rootDirectory, string candidatePath, string propertyName)
        {
            string normalizedRoot = Path.GetFullPath(rootDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            string normalizedCandidate = Path.GetFullPath(candidatePath);

            if (!normalizedCandidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The XDR generator configuration property '" + propertyName + "' must resolve within the input directory: " + candidatePath);
            }
        }

        private static bool IsSupportedSourceFile(string path)
        {
            string extension = Path.GetExtension(path);
            return string.Equals(extension, ".x", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".txt", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSupportedEntryPointFile(string path)
        {
            string extension = Path.GetExtension(path);
            return string.Equals(extension, ".x", StringComparison.OrdinalIgnoreCase);
        }
    }
}
