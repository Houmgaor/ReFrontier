using System;
using System.Globalization;
using System.IO;

using LibReFrontier;
using LibReFrontier.Abstractions;
using LibReFrontier.Exceptions;

using ReFrontier.Jpk;

namespace ReFrontier.Services
{
    /// <summary>
    /// Rebuilds a game file from an <see cref="ExtractionRecipe"/>, reversing every
    /// transformation extraction applied, in the order it applied them.
    ///
    /// <para>This is what makes the round trip stateless-free for the user: the settings
    /// come from the recipe rather than from flags they have to remember.</para>
    /// </summary>
    public class RestoreService
    {
        /// <summary>
        /// How many trailing extensions to strip when looking for the recipe of an
        /// extracted file. Extraction appends at most two (".decd" then the detected
        /// extension), one spare covers manual renames.
        /// </summary>
        private const int MaxSuffixProbes = 3;

        /// <summary>
        /// How deep container nesting may go before a recipe is assumed to be cyclic.
        /// Real game files nest two or three levels.
        /// </summary>
        private const int MaxNestingDepth = 16;

        private readonly IFileSystem _fileSystem;
        private readonly ILogger _logger;
        private readonly PackingService _packingService;
        private readonly FileProcessingService _fileProcessingService;
        private readonly FileProcessingConfig _config;

        /// <summary>
        /// Create a new RestoreService with default dependencies.
        /// </summary>
        public RestoreService()
            : this(new RealFileSystem(), new ConsoleLogger(), new DefaultCodecFactory(), FileProcessingConfig.Default())
        {
        }

        /// <summary>
        /// Create a new RestoreService with injectable dependencies.
        /// </summary>
        /// <param name="fileSystem">File system abstraction.</param>
        /// <param name="logger">Logger abstraction.</param>
        /// <param name="codecFactory">Codec factory for encoders.</param>
        /// <param name="config">Configuration settings.</param>
        public RestoreService(IFileSystem fileSystem, ILogger logger, ICodecFactory codecFactory, FileProcessingConfig config)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _packingService = new PackingService(fileSystem, logger, codecFactory, config);
            _fileProcessingService = new FileProcessingService(fileSystem, logger, config);
        }

        /// <summary>
        /// Locate the recipe describing how <paramref name="inputPath"/> was extracted.
        ///
        /// <para>Accepts either the original file name (<c>mhfdat.bin</c>) or the extracted
        /// one (<c>mhfdat.bin.decd.bin</c>), by stripping trailing extensions until a
        /// recipe file turns up.</para>
        /// </summary>
        /// <param name="inputPath">File the user pointed at.</param>
        /// <param name="recipePath">Path of the recipe that was found, null when none.</param>
        /// <returns>The parsed recipe, or null if no readable recipe was found.</returns>
        public ExtractionRecipe? FindRecipe(string inputPath, out string? recipePath)
        {
            string? candidate = inputPath;
            for (int i = 0; i <= MaxSuffixProbes && !string.IsNullOrEmpty(candidate); i++)
            {
                string probe = $"{candidate}{ExtractionRecipe.FileSuffix}";
                if (_fileSystem.FileExists(probe))
                {
                    var recipe = ExtractionRecipe.Deserialize(_fileSystem.ReadAllBytes(probe));
                    if (recipe != null)
                    {
                        recipePath = probe;
                        return recipe;
                    }

                    _logger.WriteLine($"Warning: {probe} is not a readable recipe, ignoring it.");
                }

                if (string.IsNullOrEmpty(Path.GetExtension(candidate)))
                    break;
                candidate = Path.Combine(
                    Path.GetDirectoryName(candidate) ?? "",
                    Path.GetFileNameWithoutExtension(candidate)
                );
            }

            recipePath = null;
            return null;
        }

        /// <summary>
        /// Rebuild a game file from its recipe.
        /// </summary>
        /// <param name="inputPath">Edited file to rebuild from.</param>
        /// <param name="levelOverride">Compression level to use instead of the recipe's, if any.</param>
        /// <param name="verbose">Show per-file processing messages.</param>
        /// <returns>Path of the rebuilt file.</returns>
        /// <exception cref="FileNotFoundException">Thrown when no recipe can be found.</exception>
        /// <exception cref="ReFrontierException">Thrown when compression or encryption fails.</exception>
        public string Restore(string inputPath, int? levelOverride, bool verbose = false)
        {
            return Restore(inputPath, levelOverride, null, verbose);
        }

        /// <summary>
        /// Rebuild a game file from its recipe, into a chosen directory.
        /// </summary>
        /// <param name="inputPath">Edited file or unpacked directory to rebuild from.</param>
        /// <param name="levelOverride">Compression level to use instead of the recipe's, if any.</param>
        /// <param name="outputDirectory">Directory to write the rebuilt file into, or null
        /// for the configured output directory. Nested entries are rebuilt beside their
        /// siblings so that the container above them can be packed from a complete directory.</param>
        /// <param name="verbose">Show per-file processing messages.</param>
        /// <returns>Path of the rebuilt file.</returns>
        /// <exception cref="FileNotFoundException">Thrown when no recipe can be found.</exception>
        /// <exception cref="ReFrontierException">Thrown when packing, compression or encryption fails.</exception>
        public string Restore(string inputPath, int? levelOverride, string? outputDirectory, bool verbose = false)
        {
            return RestoreInternal(inputPath, levelOverride, outputDirectory, verbose, depth: 0);
        }

        /// <summary>
        /// Rebuild one artifact, recursing into nested containers.
        /// </summary>
        /// <param name="inputPath">Edited file or unpacked directory to rebuild from.</param>
        /// <param name="levelOverride">Compression level to use instead of the recipe's, if any.</param>
        /// <param name="outputDirectory">Directory to write the rebuilt file into.</param>
        /// <param name="verbose">Show per-file processing messages.</param>
        /// <param name="depth">Current nesting depth, guarding against a recipe that points at itself.</param>
        /// <returns>Path of the rebuilt file.</returns>
        private string RestoreInternal(
            string inputPath, int? levelOverride, string? outputDirectory, bool verbose, int depth)
        {
            if (depth > MaxNestingDepth)
            {
                throw new ReFrontierException(
                    $"Gave up rebuilding {inputPath} after {MaxNestingDepth} levels of nesting. " +
                    "A recipe most likely points back at a container that contains it.",
                    inputPath
                );
            }

            var recipe = FindRecipe(inputPath, out string? recipePath)
                ?? throw new FileNotFoundException(
                    $"No extraction recipe found for {inputPath}. " +
                    "A recipe is written when you extract with the --saveMeta option, " +
                    $"as <original file>{ExtractionRecipe.FileSuffix} next to the original file. " +
                    "Without one, rebuild manually with --compress and --encrypt."
                );

            if (recipe.Version > ExtractionRecipe.CurrentVersion)
            {
                _logger.WriteLine(
                    $"Warning: {recipePath} was written by a newer version of ReFrontier " +
                    $"(recipe version {recipe.Version}, this build understands {ExtractionRecipe.CurrentVersion}). " +
                    "Restoring anyway, the result may be incomplete."
                );
            }

            string recipeDir = Path.GetDirectoryName(recipePath) ?? "";
            string sourceName = string.IsNullOrEmpty(recipe.SourceFile)
                ? Path.GetFileName(inputPath)
                : recipe.SourceFile;

            if (!string.IsNullOrEmpty(recipe.ExtractedFile)
                && !string.Equals(recipe.ExtractedFile, Path.GetFileName(inputPath), StringComparison.OrdinalIgnoreCase))
            {
                _logger.WriteLine(
                    $"Note: the recipe was written for {recipe.ExtractedFile}, rebuilding from {Path.GetFileName(inputPath)} instead."
                );
            }

            if (recipe.Layers.Count == 0)
            {
                throw new ReFrontierException(
                    $"{recipePath} records no layer to reverse, so there is nothing to rebuild. " +
                    "Extract the original file again with --saveMeta to write a usable recipe.",
                    inputPath
                );
            }

            // Layers are recorded outermost first, so reverse them to rebuild.
            RecipeLayer? encryption = null;
            RecipeLayer? compression = null;
            RecipeLayer? container = null;
            foreach (var layer in recipe.Layers)
            {
                switch (layer.Kind)
                {
                    case RecipeLayerKind.Ecd:
                    case RecipeLayerKind.Exf:
                        encryption ??= layer;
                        break;
                    case RecipeLayerKind.Jpk:
                        compression ??= layer;
                        break;
                    case RecipeLayerKind.Container:
                        container ??= layer;
                        break;
                    default:
                        break;
                }
            }

            string targetDirectory = outputDirectory ?? _config.OutputDirectory;
            _fileSystem.CreateDirectory(targetDirectory);

            // When the file is encrypted, compression has to write to the name the
            // encryption step expects, since it derives its output from its input name.
            string encryptedSuffix = encryption?.Kind == RecipeLayerKind.Exf
                ? _config.DecryptedExfSuffix
                : _config.DecryptedSuffix;
            string finalPath = Path.Combine(targetDirectory, sourceName);
            string intermediatePath = encryption == null ? finalPath : $"{finalPath}{encryptedSuffix}";

            _logger.WriteLine($"Restoring {sourceName} from {recipePath}.");

            // Produce the payload the compression and encryption layers apply to. For a
            // container that means rebuilding its entries and packing the directory;
            // otherwise it is simply the file the user edited.
            string payloadPath;
            string? scratchPath = null;
            if (container != null)
            {
                string containerDir = ResolveContainerDirectory(inputPath, recipe, container, recipeDir);
                int rebuilt = RestoreContainerEntries(containerDir, levelOverride, verbose, depth);
                if (rebuilt > 0)
                    _logger.WriteLine($"Rebuilt {rebuilt} nested {(rebuilt == 1 ? "entry" : "entries")} in {containerDir}.");

                payloadPath = _packingService.ProcessPackInput(containerDir, targetDirectory);
                scratchPath = payloadPath;
            }
            else
            {
                if (_fileSystem.DirectoryExists(inputPath))
                {
                    throw new ReFrontierException(
                        $"{Path.GetFileName(inputPath)} is a directory, but {recipePath} does not " +
                        "describe a container archive. Point --restore at the extracted file, " +
                        "or repack the directory with --pack.",
                        inputPath
                    );
                }

                // Extraction leaves the original next to the recipe, and pointing at it by
                // mistake would compress and encrypt already-packed bytes into a broken file.
                uint magic = ReadMagic(inputPath);
                if (FileMagic.IsEncrypted(magic) || FileMagic.IsJpkCompressed(magic))
                {
                    string hint = string.IsNullOrEmpty(recipe.ExtractedFile)
                        ? "the extracted file"
                        : recipe.ExtractedFile;
                    throw new ReFrontierException(
                        $"{Path.GetFileName(inputPath)} is still encrypted or compressed, " +
                        $"so there is nothing to rebuild from it. Point --restore at {hint} instead.",
                        inputPath
                    );
                }
                payloadPath = inputPath;
            }

            if (compression != null)
            {
                var algorithm = compression.Algorithm ?? CompressionType.HFI;
                int level = levelOverride ?? compression.Level ?? ExtractionRecipe.DefaultCompressionLevel;
                if (levelOverride == null && compression.Level == null && verbose)
                {
                    _logger.WriteLine(
                        $"Compression level is not recorded in the JKR header, using {level}. " +
                        "Override it with --level."
                    );
                }
                _packingService.JPKEncode(new Compression(algorithm, level), payloadPath, intermediatePath);
            }
            else if (!SamePath(payloadPath, intermediatePath))
            {
                _fileSystem.WriteAllBytes(intermediatePath, _fileSystem.ReadAllBytes(payloadPath));
            }

            // Packing names its output from the log, which may not be the name the next
            // layer expects; drop the copy once its contents have moved on.
            if (scratchPath != null
                && !SamePath(scratchPath, intermediatePath)
                && _fileSystem.FileExists(scratchPath))
            {
                _fileSystem.DeleteFile(scratchPath);
            }

            if (encryption != null)
            {
                byte[]? header = ReadEncryptionHeader(encryption, recipeDir, sourceName, verbose);

                // cleanUp is false on purpose: it would delete the user's .meta file,
                // which they need for every later rebuild.
                if (encryption.Kind == RecipeLayerKind.Exf)
                {
                    finalPath = header != null
                        ? _fileProcessingService.EncryptExfFile(intermediatePath, header, cleanUp: false, verbose)
                        : throw new ReFrontierException(
                            $"Cannot rebuild {sourceName}: EXF encryption needs the original header, " +
                            $"and neither {recipePath} nor its meta file has it. Extract the original again.",
                            inputPath
                        );
                }
                else
                {
                    finalPath = _fileProcessingService.EncryptEcdFile(intermediatePath, header, cleanUp: false, verbose);
                }

                _fileSystem.DeleteFile(intermediatePath);
            }

            ReportResult(finalPath, recipe);
            return finalPath;
        }

        /// <summary>
        /// Obtain the encryption header for a layer.
        ///
        /// <para>Recipes carry it from version 2 on, which keeps them usable on their own.
        /// Version 1 recipes name a meta file instead, so fall back to reading that.</para>
        /// </summary>
        /// <param name="encryption">The encryption layer being reversed.</param>
        /// <param name="recipeDir">Directory holding the recipe.</param>
        /// <param name="sourceName">Name of the file being rebuilt.</param>
        /// <param name="verbose">Show per-file processing messages.</param>
        /// <returns>The header, or null when neither source has one.</returns>
        private byte[]? ReadEncryptionHeader(
            RecipeLayer encryption, string recipeDir, string sourceName, bool verbose)
        {
            if (!string.IsNullOrEmpty(encryption.Header))
            {
                try
                {
                    return Convert.FromBase64String(encryption.Header);
                }
                catch (FormatException)
                {
                    _logger.WriteLine(
                        "Warning: the encryption header recorded in the recipe is not readable, " +
                        "falling back to the meta file."
                    );
                }
            }

            string metaPath = Path.Combine(recipeDir, encryption.MetaFile ?? $"{sourceName}{_config.MetaSuffix}");
            if (_fileSystem.FileExists(metaPath))
                return _fileSystem.ReadAllBytes(metaPath);

            if (verbose)
                _logger.WriteLine($"No encryption header in the recipe and no meta file at {metaPath}.");
            return null;
        }

        /// <summary>
        /// Locate the unpacked directory of a container layer.
        ///
        /// <para>Accepts the directory itself, or the original file name, in which case the
        /// recipe says which directory the container was unpacked into.</para>
        /// </summary>
        /// <param name="inputPath">Path the user pointed at.</param>
        /// <param name="recipe">Recipe being restored.</param>
        /// <param name="container">The container layer.</param>
        /// <param name="recipeDir">Directory holding the recipe.</param>
        /// <returns>Path of the unpacked directory.</returns>
        /// <exception cref="ReFrontierException">Thrown when the directory cannot be found.</exception>
        private string ResolveContainerDirectory(
            string inputPath, ExtractionRecipe recipe, RecipeLayer container, string recipeDir)
        {
            if (_fileSystem.DirectoryExists(inputPath))
                return inputPath;

            string? directoryName = container.Directory;
            if (string.IsNullOrEmpty(directoryName))
                directoryName = recipe.ExtractedFile;

            if (!string.IsNullOrEmpty(directoryName))
            {
                string candidate = Path.Combine(recipeDir, directoryName);
                if (_fileSystem.DirectoryExists(candidate))
                    return candidate;
            }

            throw new ReFrontierException(
                $"Cannot rebuild {recipe.SourceFile}: its unpacked directory " +
                $"{directoryName ?? "(unknown)"} is missing. Extract the original file again " +
                "with --saveMeta, or point --restore at the unpacked directory.",
                inputPath
            );
        }

        /// <summary>
        /// Rebuild every entry of a container that extraction unpacked in place.
        ///
        /// <para>Each such entry left a recipe of its own next to it, so the whole nested
        /// structure is rebuilt depth first: an entry that is itself a container recurses
        /// through this method again before its parent is packed.</para>
        /// </summary>
        /// <param name="containerDir">Unpacked directory of the container.</param>
        /// <param name="levelOverride">Compression level to use instead of each recipe's, if any.</param>
        /// <param name="verbose">Show per-file processing messages.</param>
        /// <param name="depth">Current nesting depth.</param>
        /// <returns>How many entries were rebuilt.</returns>
        private int RestoreContainerEntries(string containerDir, int? levelOverride, bool verbose, int depth)
        {
            string[] recipePaths = _fileSystem.GetFiles(
                containerDir, $"*{ExtractionRecipe.FileSuffix}", SearchOption.TopDirectoryOnly
            );
            Array.Sort(recipePaths, StringComparer.Ordinal);

            int rebuilt = 0;
            foreach (string recipePath in recipePaths)
            {
                var entryRecipe = ExtractionRecipe.Deserialize(_fileSystem.ReadAllBytes(recipePath));
                if (entryRecipe == null || string.IsNullOrEmpty(entryRecipe.ExtractedFile))
                {
                    _logger.WriteLine($"Warning: {recipePath} is not a readable recipe, ignoring it.");
                    continue;
                }

                // Nothing to rebuild from means the entry is either already in its packed
                // form or genuinely gone; packing reports the latter with full context.
                string extracted = Path.Combine(containerDir, entryRecipe.ExtractedFile);
                if (!_fileSystem.FileExists(extracted) && !_fileSystem.DirectoryExists(extracted))
                    continue;

                RestoreInternal(extracted, levelOverride, containerDir, verbose, depth + 1);
                rebuilt++;
            }

            return rebuilt;
        }

        /// <summary>
        /// Compare two paths that may have been built with different separators.
        /// </summary>
        /// <param name="left">First path.</param>
        /// <param name="right">Second path.</param>
        /// <returns>true if both denote the same location.</returns>
        private static bool SamePath(string left, string right)
        {
            return string.Equals(
                left.Replace('\\', '/').TrimEnd('/'),
                right.Replace('\\', '/').TrimEnd('/'),
                StringComparison.OrdinalIgnoreCase
            );
        }

        /// <summary>
        /// Read the 4-byte magic number at the start of a file.
        /// </summary>
        /// <param name="path">File to read.</param>
        /// <returns>The magic number, or 0 if the file is shorter than 4 bytes.</returns>
        private uint ReadMagic(string path)
        {
            using var stream = _fileSystem.OpenRead(path);
            Span<byte> header = stackalloc byte[4];
            return stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false) < header.Length
                ? 0
                : BitConverter.ToUInt32(header);
        }

        /// <summary>
        /// Print the rebuilt file's size next to the original's, when the recipe recorded it.
        /// </summary>
        /// <param name="finalPath">Path of the rebuilt file.</param>
        /// <param name="recipe">Recipe the file was rebuilt from.</param>
        private void ReportResult(string finalPath, ExtractionRecipe recipe)
        {
            long size = _fileSystem.GetFileLength(finalPath);
            long? originalSize = recipe.Layers.Count > 0 ? recipe.Layers[0].OriginalSize : null;

            string sizeText = size.ToString("N0", CultureInfo.InvariantCulture);
            if (originalSize is > 0)
            {
                string originalText = originalSize.Value.ToString("N0", CultureInfo.InvariantCulture);
                decimal ratio = (decimal)size / originalSize.Value;
                _logger.PrintWithSeparator(
                    $"Restored to {finalPath}: {sizeText} bytes " +
                    $"({originalText} originally, {ratio:P1} of it).",
                    false
                );
            }
            else
            {
                _logger.PrintWithSeparator($"Restored to {finalPath}: {sizeText} bytes.", false);
            }
        }
    }
}
