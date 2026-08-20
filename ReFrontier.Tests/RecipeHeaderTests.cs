using LibReFrontier;
using LibReFrontier.Exceptions;

using ReFrontier.Jpk;
using ReFrontier.Services;
using ReFrontier.Tests.Mocks;

namespace ReFrontier.Tests
{
    /// <summary>
    /// Tests for the encryption header carried inside the recipe from version 2 on.
    ///
    /// <para>The header used to live only in the companion meta file, which meant a recipe
    /// was useless on its own. It is now recorded in the recipe as well, while the meta file
    /// keeps being written so <c>--encrypt</c>, FrontierTextTool and older versions still
    /// work.</para>
    /// </summary>
    public class RecipeHeaderTests
    {
        private readonly InMemoryFileSystem _fileSystem;
        private readonly TestLogger _logger;
        private readonly FileProcessingConfig _config;
        private readonly ICodecFactory _codecFactory;
        private readonly RestoreService _restore;

        public RecipeHeaderTests()
        {
            _fileSystem = new InMemoryFileSystem();
            _logger = new TestLogger();
            _config = FileProcessingConfig.Default();
            _codecFactory = new DefaultCodecFactory();
            _restore = new RestoreService(_fileSystem, _logger, _codecFactory, _config);
        }

        [Fact]
        public void Extraction_WritesTheHeaderIntoTheRecipe()
        {
            var recipe = ExtractEncryptedFile();

            var layer = recipe.Layers[0];
            Assert.Equal(RecipeLayerKind.Ecd, layer.Kind);
            Assert.NotNull(layer.Header);
            Assert.Equal(
                FileFormatConstants.EncryptionHeaderLength,
                Convert.FromBase64String(layer.Header).Length
            );
        }

        [Fact]
        public void Extraction_StillWritesTheMetaFile()
        {
            ExtractEncryptedFile();

            // --encrypt, FrontierTextTool and older versions all read this file.
            Assert.True(_fileSystem.FileExists("/test/file.bin.meta"));
        }

        [Fact]
        public void Extraction_RecordsVersionTwo()
        {
            Assert.Equal(2, ExtractEncryptedFile().Version);
        }

        [Fact]
        public void Restore_WithoutAnyMetaFile_UsesTheEmbeddedHeader()
        {
            byte[] payload = CreateTestData(256);
            var recipe = ExtractEncryptedFile(payload, out string extracted);

            // The whole point: the recipe alone is enough.
            _fileSystem.DeleteFile("/test/file.bin.meta");

            string result = _restore.Restore(extracted, levelOverride: 16);

            Assert.Equal(FileMagic.ECD, BitConverter.ToUInt32(_fileSystem.ReadAllBytes(result), 0));
            Assert.Equal(recipe.Layers[0].Header, ReadRecipe().Layers[0].Header);
        }

        [Fact]
        public void Restore_VersionOneRecipe_FallsBackToTheMetaFile()
        {
            ExtractEncryptedFile(CreateTestData(256), out string extracted);

            // Recipes written before the header was embedded name a meta file instead.
            var recipe = ReadRecipe();
            recipe.Version = 1;
            recipe.Layers[0].Header = null;
            WriteRecipe(recipe);

            string result = _restore.Restore(extracted, levelOverride: 16);

            Assert.Equal(FileMagic.ECD, BitConverter.ToUInt32(_fileSystem.ReadAllBytes(result), 0));
        }

        [Fact]
        public void Restore_UnreadableHeader_WarnsAndFallsBackToTheMetaFile()
        {
            ExtractEncryptedFile(CreateTestData(256), out string extracted);

            var recipe = ReadRecipe();
            recipe.Layers[0].Header = "this is not base64 !!";
            WriteRecipe(recipe);

            string result = _restore.Restore(extracted, levelOverride: 16);

            Assert.True(_fileSystem.FileExists(result));
            Assert.True(_logger.AnyLineContains("not readable"));
        }

        [Fact]
        public void Restore_NoHeaderAnywhere_StillRebuildsWithTheDefaultKey()
        {
            ExtractEncryptedFile(CreateTestData(256), out string extracted);

            var recipe = ReadRecipe();
            recipe.Layers[0].Header = null;
            WriteRecipe(recipe);
            _fileSystem.DeleteFile("/test/file.bin.meta");

            string result = _restore.Restore(extracted, levelOverride: 16);

            // ECD can fall back to the default key index; the result is valid but its
            // header cannot reproduce fields the original carried.
            Assert.Equal(FileMagic.ECD, BitConverter.ToUInt32(_fileSystem.ReadAllBytes(result), 0));
        }

        [Fact]
        public void Restore_ExfWithNoHeaderAnywhere_ReportsThatItCannot()
        {
            // EXF needs the seed from the header, so there is no fallback.
            _fileSystem.AddFile("/test/file.exf.dexf", CreateTestData(128));
            var recipe = new ExtractionRecipe { SourceFile = "file.exf", ExtractedFile = "file.exf.dexf" };
            recipe.Layers.Add(new RecipeLayer { Kind = RecipeLayerKind.Exf });
            _fileSystem.AddFile($"/test/file.exf{ExtractionRecipe.FileSuffix}", recipe.Serialize());

            var ex = Assert.Throws<ReFrontierException>(
                () => _restore.Restore("/test/file.exf.dexf", levelOverride: null)
            );

            Assert.Contains("needs the original header", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void EncryptEcdFile_FromHeaderBytes_MatchesEncryptingFromTheMetaFile()
        {
            byte[] header = CreateEcdMetaHeader(keyIndex: 4);
            var service = new FileProcessingService(_fileSystem, _logger, _config);

            _fileSystem.AddFile("/a/file.bin.decd", CreateTestData(512));
            _fileSystem.AddFile("/a/file.bin.meta", header);
            string fromFile = service.EncryptEcdFile("/a/file.bin.decd", "/a/file.bin.meta", cleanUp: false);

            _fileSystem.AddFile("/b/file.bin.decd", CreateTestData(512));
            string fromBytes = service.EncryptEcdFile("/b/file.bin.decd", header, cleanUp: false);

            Assert.Equal(_fileSystem.ReadAllBytes(fromFile), _fileSystem.ReadAllBytes(fromBytes));
        }

        [Fact]
        public void EncryptEcdFile_FromMetaFile_StillDeletesItOnCleanUp()
        {
            var service = new FileProcessingService(_fileSystem, _logger, _config);
            _fileSystem.AddFile("/a/file.bin.decd", CreateTestData(64));
            _fileSystem.AddFile("/a/file.bin.meta", CreateEcdMetaHeader(keyIndex: 4));

            service.EncryptEcdFile("/a/file.bin.decd", "/a/file.bin.meta", cleanUp: true);

            Assert.False(_fileSystem.FileExists("/a/file.bin.meta"));
            Assert.False(_fileSystem.FileExists("/a/file.bin.decd"));
        }

        #region Helpers

        private ExtractionRecipe ExtractEncryptedFile() => ExtractEncryptedFile(CreateTestData(256), out _);

        private ExtractionRecipe ExtractEncryptedFile(byte[] payload, out string extractedPath)
        {
            // Build a real ECD file: compress the payload, then encrypt it.
            var packing = new PackingService(_fileSystem, _logger, _codecFactory, _config);
            var processing = new FileProcessingService(_fileSystem, _logger, _config);

            _fileSystem.AddFile("/staging/payload.bin", payload);
            packing.JPKEncode(new Compression(CompressionType.LZ, 16), "/staging/payload.bin", "/staging/packed.jkr");
            _fileSystem.AddFile("/staging/packed.jkr.decd", _fileSystem.ReadAllBytes("/staging/packed.jkr"));
            string encrypted = processing.EncryptEcdFile(
                "/staging/packed.jkr.decd", CreateEcdMetaHeader(keyIndex: 4), cleanUp: false
            );
            _fileSystem.AddFile("/test/file.bin", _fileSystem.ReadAllBytes(encrypted));

            var program = new Program(_fileSystem, _logger, _codecFactory, _config);
            var result = program.ProcessFile("/test/file.bin", new InputArguments { createLog = true, recursive = true });
            extractedPath = result.OutputPath!;

            return ReadRecipe();
        }

        private ExtractionRecipe ReadRecipe()
        {
            return ExtractionRecipe.Deserialize(
                _fileSystem.ReadAllBytes($"/test/file.bin{ExtractionRecipe.FileSuffix}")
            )!;
        }

        private void WriteRecipe(ExtractionRecipe recipe)
        {
            _fileSystem.WriteAllBytes($"/test/file.bin{ExtractionRecipe.FileSuffix}", recipe.Serialize());
        }

        private static byte[] CreateTestData(int size)
        {
            byte[] data = new byte[size];
            for (int i = 0; i < size; i++)
                data[i] = (byte)(i % 251);
            return data;
        }

        private static byte[] CreateEcdMetaHeader(int keyIndex)
        {
            return new byte[]
            {
                0x65, 0x63, 0x64, 0x1A,
                (byte)keyIndex, 0x00,
                0x37, 0x13,             // the unknown field the header preserves
                0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00,
            };
        }

        #endregion
    }
}
