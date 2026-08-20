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

            // Guard against rebuilding from a file that is still packed. Extraction leaves
            // the original next to the recipe, and pointing at it by mistake would compress
            // and encrypt already-compressed, already-encrypted bytes into a broken file.
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
                    $"{recipePath} records no encryption or compression, so there is nothing to rebuild. " +
                    "Extract the original file again with --saveMeta to write a usable recipe.",
                    inputPath
                );
            }

            // Layers are recorded outermost first, so reverse them to rebuild.
            RecipeLayer? encryption = null;
            RecipeLayer? compression = null;
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
                    default:
                        break;
                }
            }

            _fileSystem.CreateDirectory(_config.OutputDirectory);

            // When the file is encrypted, compression has to write to the name the
            // encryption step expects, since it derives its output from its input name.
            string encryptedSuffix = encryption?.Kind == RecipeLayerKind.Exf
                ? _config.DecryptedExfSuffix
                : _config.DecryptedSuffix;
            string finalPath = Path.Combine(_config.OutputDirectory, sourceName);
            string intermediatePath = encryption == null ? finalPath : $"{finalPath}{encryptedSuffix}";

            _logger.WriteLine($"Restoring {sourceName} from {recipePath}.");

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
                _packingService.JPKEncode(new Compression(algorithm, level), inputPath, intermediatePath);
            }
            else
            {
                _fileSystem.WriteAllBytes(intermediatePath, _fileSystem.ReadAllBytes(inputPath));
            }

            if (encryption != null)
            {
                string metaPath = Path.Combine(recipeDir, encryption.MetaFile ?? $"{sourceName}{_config.MetaSuffix}");

                // cleanUp is false on purpose: it would delete the user's .meta file,
                // which they need for every later rebuild.
                finalPath = encryption.Kind == RecipeLayerKind.Exf
                    ? _fileProcessingService.EncryptExfFile(intermediatePath, metaPath, cleanUp: false, verbose)
                    : _fileProcessingService.EncryptEcdFile(intermediatePath, metaPath, cleanUp: false, verbose);

                _fileSystem.DeleteFile(intermediatePath);
            }

            ReportResult(finalPath, recipe);
            return finalPath;
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
