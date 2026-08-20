using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LibReFrontier
{
    /// <summary>
    /// A transformation that was undone while extracting a file.
    /// </summary>
    public enum RecipeLayerKind
    {
        /// <summary>
        /// ECD encryption. The original header is kept in the companion meta file.
        /// </summary>
        Ecd = 0,

        /// <summary>
        /// EXF encryption. The original header is kept in the companion meta file.
        /// </summary>
        Exf = 1,

        /// <summary>
        /// JPK (JKR) compression.
        /// </summary>
        Jpk = 2,
    }

    /// <summary>
    /// One layer of an <see cref="ExtractionRecipe"/>, describing a single
    /// decryption or decompression step that has to be reversed to rebuild the file.
    /// </summary>
    public sealed class RecipeLayer
    {
        /// <summary>
        /// Kind of transformation that was undone.
        /// </summary>
        public RecipeLayerKind Kind { get; set; }

        /// <summary>
        /// Path to the meta file holding the original encryption header.
        /// Only set for <see cref="RecipeLayerKind.Ecd"/> and <see cref="RecipeLayerKind.Exf"/>.
        /// </summary>
        public string? MetaFile { get; set; }

        /// <summary>
        /// JPK compression algorithm read from the JKR header.
        /// Only set for <see cref="RecipeLayerKind.Jpk"/>.
        /// </summary>
        public CompressionType? Algorithm { get; set; }

        /// <summary>
        /// Compression level to re-encode with.
        /// <para>Always null when written during extraction: the level is an encoder-side
        /// parameter and is not recorded in the JKR header, so it cannot be recovered from
        /// the game file. Restoring falls back to <see cref="ExtractionRecipe.DefaultCompressionLevel"/>.</para>
        /// </summary>
        public int? Level { get; set; }

        /// <summary>
        /// Size in bytes of this layer as it was in the original file.
        /// Used to report how closely a rebuilt file matches the original.
        /// </summary>
        public long? OriginalSize { get; set; }
    }

    /// <summary>
    /// Records how a game file was taken apart, so that it can be put back
    /// together without the user having to re-specify encryption and compression settings.
    ///
    /// <para>Written next to the source file as <c>&lt;sourceFile&gt;.recipe.json</c> when
    /// extracting with the save-meta option, and consumed by the restore option.</para>
    /// </summary>
    public sealed class ExtractionRecipe
    {
        /// <summary>
        /// Schema version of recipes written by this build.
        /// </summary>
        public const int CurrentVersion = 1;

        /// <summary>
        /// Suffix appended to the source file name to build the recipe file name.
        /// </summary>
        public const string FileSuffix = ".recipe.json";

        /// <summary>
        /// Compression level used when a recipe does not record one.
        /// Levels above this offer diminishing returns for much longer compression times.
        /// </summary>
        public const int DefaultCompressionLevel = 80;

        /// <summary>
        /// Schema version this recipe was written with.
        /// </summary>
        public int Version { get; set; } = CurrentVersion;

        /// <summary>
        /// File name of the original game file, e.g. <c>mhfdat.bin</c>.
        /// This is the name a restored file is given.
        /// </summary>
        public string SourceFile { get; set; } = "";

        /// <summary>
        /// File name of the editable file produced by extraction,
        /// e.g. <c>mhfdat.bin.decd.bin</c>.
        /// </summary>
        public string ExtractedFile { get; set; } = "";

        /// <summary>
        /// Layers that were peeled off, outermost first.
        /// Restoring applies them in reverse order.
        /// </summary>
        /// <remarks>
        /// The creation handling attribute is required: without it the deserializer
        /// silently leaves this read-only collection empty, which would make a restore
        /// look like it succeeded while skipping compression and encryption entirely.
        /// </remarks>
        [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
        public Collection<RecipeLayer> Layers { get; } = [];

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() },
        };

        /// <summary>
        /// Serialize this recipe to UTF-8 JSON.
        /// </summary>
        /// <returns>UTF-8 encoded JSON bytes.</returns>
        public byte[] Serialize()
        {
            return JsonSerializer.SerializeToUtf8Bytes(this, SerializerOptions);
        }

        /// <summary>
        /// Parse a recipe from UTF-8 JSON.
        /// </summary>
        /// <param name="data">UTF-8 encoded JSON bytes.</param>
        /// <returns>The parsed recipe, or null if the content is not a readable recipe.</returns>
        public static ExtractionRecipe? Deserialize(byte[] data)
        {
            try
            {
                return JsonSerializer.Deserialize<ExtractionRecipe>(data, SerializerOptions);
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
