using System;
using System.IO;

using LibReFrontier;
using LibReFrontier.Exceptions;

using ReFrontier.Jpk;
using ReFrontier.Services;
using ReFrontier.Tests.Mocks;

namespace ReFrontier.Tests.Services
{
    /// <summary>
    /// Tests for <see cref="RestoreService"/>, which rebuilds a game file from the
    /// recipe extraction wrote, so the user does not have to remember settings.
    /// </summary>
    public class RestoreServiceTests
    {
        private readonly InMemoryFileSystem _fileSystem;
        private readonly TestLogger _logger;
        private readonly FileProcessingConfig _config;
        private readonly RestoreService _service;

        public RestoreServiceTests()
        {
            _fileSystem = new InMemoryFileSystem();
            _logger = new TestLogger();
            _config = FileProcessingConfig.Default();
            _service = new RestoreService(_fileSystem, _logger, new DefaultCodecFactory(), _config);
        }

        #region FindRecipe

        [Fact]
        public void FindRecipe_BySourceName_IsFound()
        {
            AddRecipe("/test/mhfdat.bin", JpkOnlyRecipe());

            var recipe = _service.FindRecipe("/test/mhfdat.bin", out string? recipePath);

            Assert.NotNull(recipe);
            Assert.Equal("/test/mhfdat.bin.recipe.json", recipePath?.Replace('\\', '/'));
        }

        [Fact]
        public void FindRecipe_ByExtractedName_StripsSuffixesUntilFound()
        {
            AddRecipe("/test/mhfdat.bin", JpkOnlyRecipe());

            // The user naturally points at the file they edited, not the original name.
            var recipe = _service.FindRecipe("/test/mhfdat.bin.decd.bin", out string? recipePath);

            Assert.NotNull(recipe);
            Assert.Equal("/test/mhfdat.bin.recipe.json", recipePath?.Replace('\\', '/'));
        }

        [Fact]
        public void FindRecipe_NoRecipe_ReturnsNull()
        {
            _fileSystem.AddFile("/test/lonely.bin", new byte[] { 1, 2, 3, 4 });

            var recipe = _service.FindRecipe("/test/lonely.bin", out string? recipePath);

            Assert.Null(recipe);
            Assert.Null(recipePath);
        }

        [Fact]
        public void FindRecipe_UnreadableRecipe_ReturnsNullAndWarns()
        {
            _fileSystem.AddFile("/test/broken.bin.recipe.json", "this is not json");

            var recipe = _service.FindRecipe("/test/broken.bin", out _);

            Assert.Null(recipe);
            Assert.True(_logger.AnyLineContains("not a readable recipe"));
        }

        #endregion

        #region Restore

        [Fact]
        public void Restore_CompressionOnly_RecompressesWithRecordedAlgorithm()
        {
            byte[] payload = CreateTestData(1024);
            _fileSystem.AddFile("/test/file.bin.jkr.bin", payload);
            AddRecipe("/test/file.bin", JpkOnlyRecipe(CompressionType.LZ, "file.bin.jkr.bin"));

            string result = _service.Restore("/test/file.bin.jkr.bin", levelOverride: 16);

            Assert.Equal("output/file.bin", result.Replace('\\', '/'));
            byte[] rebuilt = _fileSystem.ReadAllBytes(result);

            // A JKR header proves the recorded algorithm was applied without any CLI flag.
            Assert.Equal(FileMagic.JKR, BitConverter.ToUInt32(rebuilt, 0));
            Assert.Equal((ushort)CompressionType.LZ, BitConverter.ToUInt16(rebuilt, 6));
        }

        [Fact]
        public void Restore_CompressionOnly_RoundTripsPayloadExactly()
        {
            byte[] payload = CreateTestData(2048);
            _fileSystem.AddFile("/test/file.bin.jkr.bin", payload);
            AddRecipe("/test/file.bin", JpkOnlyRecipe(CompressionType.HFI, "file.bin.jkr.bin"));

            string result = _service.Restore("/test/file.bin.jkr.bin", levelOverride: 16);

            // Decompressing what we just rebuilt must give the payload back byte for byte.
            _fileSystem.AddFile("/test/verify.jkr", _fileSystem.ReadAllBytes(result));
            var unpacker = new UnpackingService(_fileSystem, _logger, new DefaultCodecFactory(), _config);
            string decompressed = unpacker.UnpackJPK("/test/verify.jkr");

            Assert.Equal(payload, _fileSystem.ReadAllBytes(decompressed));
        }

        [Fact]
        public void Restore_CompressionAndEcd_ProducesEncryptedFile()
        {
            byte[] payload = CreateTestData(512);
            _fileSystem.AddFile("/test/mhfdat.bin.decd.bin", payload);
            _fileSystem.AddFile("/test/mhfdat.bin.meta", CreateEcdMetaHeader(keyIndex: 4));

            var recipe = new ExtractionRecipe
            {
                SourceFile = "mhfdat.bin",
                ExtractedFile = "mhfdat.bin.decd.bin",
            };
            recipe.Layers.Add(new RecipeLayer
            {
                Kind = RecipeLayerKind.Ecd,
                MetaFile = "mhfdat.bin.meta",
                OriginalSize = 4096,
            });
            recipe.Layers.Add(new RecipeLayer { Kind = RecipeLayerKind.Jpk, Algorithm = CompressionType.LZ });
            AddRecipe("/test/mhfdat.bin", recipe);

            string result = _service.Restore("/test/mhfdat.bin.decd.bin", levelOverride: 16);

            Assert.Equal("output/mhfdat.bin", result.Replace('\\', '/'));
            byte[] rebuilt = _fileSystem.ReadAllBytes(result);
            Assert.Equal(FileMagic.ECD, BitConverter.ToUInt32(rebuilt, 0));
        }

        [Fact]
        public void Restore_CompressionAndEcd_KeepsMetaFileForLaterRebuilds()
        {
            SetupEcdScenario();

            _service.Restore("/test/mhfdat.bin.decd.bin", levelOverride: 16);

            // The user rebuilds repeatedly while iterating on a mod; deleting their
            // meta file after the first rebuild would break every later one.
            Assert.True(_fileSystem.FileExists("/test/mhfdat.bin.meta"));
        }

        [Fact]
        public void Restore_CompressionAndEcd_RemovesIntermediateFile()
        {
            SetupEcdScenario();

            _service.Restore("/test/mhfdat.bin.decd.bin", levelOverride: 16);

            Assert.False(_fileSystem.FileExists("output/mhfdat.bin.decd"));
        }

        [Fact]
        public void Restore_LevelOverride_TakesPrecedenceOverRecipe()
        {
            byte[] payload = CreateTestData(4096);
            _fileSystem.AddFile("/test/a.bin.jkr.bin", payload);
            _fileSystem.AddFile("/test/b.bin.jkr.bin", payload);

            var lowLevel = JpkOnlyRecipe(CompressionType.LZ, "a.bin.jkr.bin");
            lowLevel.Layers[0].Level = 1;
            AddRecipe("/test/a.bin", lowLevel);

            var alsoLowLevel = JpkOnlyRecipe(CompressionType.LZ, "b.bin.jkr.bin");
            alsoLowLevel.Layers[0].Level = 1;
            AddRecipe("/test/b.bin", alsoLowLevel);

            string fromRecipe = _service.Restore("/test/a.bin.jkr.bin", levelOverride: null);
            byte[] recipeResult = _fileSystem.ReadAllBytes(fromRecipe);

            string overridden = _service.Restore("/test/b.bin.jkr.bin", levelOverride: 64);
            byte[] overrideResult = _fileSystem.ReadAllBytes(overridden);

            // A higher level searches harder, so the two runs cannot produce the same bytes.
            Assert.NotEqual(recipeResult.Length, overrideResult.Length);
        }

        [Fact]
        public void Restore_StillPackedInput_ThrowsAndNamesTheRightFile()
        {
            // Extraction leaves the original next to the recipe: pointing at it by mistake
            // would compress and encrypt already-packed bytes into a broken file.
            byte[] encrypted = CreateTestData(64);
            BitConverter.GetBytes(FileMagic.ECD).CopyTo(encrypted, 0);
            _fileSystem.AddFile("/test/mhfdat.bin", encrypted);
            AddRecipe("/test/mhfdat.bin", JpkOnlyRecipe(CompressionType.LZ, "mhfdat.bin.decd.bin"));

            var ex = Assert.Throws<ReFrontierException>(
                () => _service.Restore("/test/mhfdat.bin", levelOverride: 16)
            );

            Assert.Contains("still encrypted or compressed", ex.Message, StringComparison.Ordinal);
            Assert.Contains("mhfdat.bin.decd.bin", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Restore_StillCompressedInput_Throws()
        {
            byte[] compressed = CreateTestData(64);
            BitConverter.GetBytes(FileMagic.JKR).CopyTo(compressed, 0);
            _fileSystem.AddFile("/test/file.bin", compressed);
            AddRecipe("/test/file.bin", JpkOnlyRecipe(CompressionType.LZ, "file.bin.jkr.bin"));

            Assert.Throws<ReFrontierException>(() => _service.Restore("/test/file.bin", levelOverride: 16));
        }

        [Fact]
        public void Restore_RecipeWithNoLayers_ThrowsRatherThanCopying()
        {
            _fileSystem.AddFile("/test/file.bin.jkr.bin", CreateTestData(64));
            AddRecipe("/test/file.bin", new ExtractionRecipe
            {
                SourceFile = "file.bin",
                ExtractedFile = "file.bin.jkr.bin",
            });

            var ex = Assert.Throws<ReFrontierException>(
                () => _service.Restore("/test/file.bin.jkr.bin", levelOverride: 16)
            );

            Assert.Contains("nothing to rebuild", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Restore_NoRecipe_ThrowsWithRecoveryInstructions()
        {
            _fileSystem.AddFile("/test/lonely.bin", CreateTestData(64));

            var ex = Assert.Throws<FileNotFoundException>(
                () => _service.Restore("/test/lonely.bin", levelOverride: 16)
            );

            Assert.Contains("--saveMeta", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Restore_DifferentFileThanRecorded_WarnsButProceeds()
        {
            _fileSystem.AddFile("/test/file.bin.renamed", CreateTestData(256));
            AddRecipe("/test/file.bin", JpkOnlyRecipe(CompressionType.LZ, "file.bin.jkr.bin"));

            string result = _service.Restore("/test/file.bin.renamed", levelOverride: 16);

            Assert.True(_fileSystem.FileExists(result));
            Assert.True(_logger.AnyLineContains("rebuilding from"));
        }

        [Fact]
        public void Restore_NewerRecipeVersion_WarnsButProceeds()
        {
            _fileSystem.AddFile("/test/file.bin.jkr.bin", CreateTestData(256));
            var recipe = JpkOnlyRecipe(CompressionType.LZ, "file.bin.jkr.bin");
            recipe.Version = ExtractionRecipe.CurrentVersion + 1;
            AddRecipe("/test/file.bin", recipe);

            string result = _service.Restore("/test/file.bin.jkr.bin", levelOverride: 16);

            Assert.True(_fileSystem.FileExists(result));
            Assert.True(_logger.AnyLineContains("newer version of ReFrontier"));
        }

        [Fact]
        public void Restore_ReportsSizeAgainstTheOriginal()
        {
            _fileSystem.AddFile("/test/file.bin.jkr.bin", CreateTestData(1024));
            var recipe = JpkOnlyRecipe(CompressionType.LZ, "file.bin.jkr.bin");
            recipe.Layers[0].OriginalSize = 900;
            AddRecipe("/test/file.bin", recipe);

            _service.Restore("/test/file.bin.jkr.bin", levelOverride: 16);

            Assert.True(_logger.AnyLineContains("originally"));
        }

        #endregion

        #region Helpers

        private void SetupEcdScenario()
        {
            _fileSystem.AddFile("/test/mhfdat.bin.decd.bin", CreateTestData(512));
            _fileSystem.AddFile("/test/mhfdat.bin.meta", CreateEcdMetaHeader(keyIndex: 4));

            var recipe = new ExtractionRecipe
            {
                SourceFile = "mhfdat.bin",
                ExtractedFile = "mhfdat.bin.decd.bin",
            };
            recipe.Layers.Add(new RecipeLayer { Kind = RecipeLayerKind.Ecd, MetaFile = "mhfdat.bin.meta" });
            recipe.Layers.Add(new RecipeLayer { Kind = RecipeLayerKind.Jpk, Algorithm = CompressionType.LZ });
            AddRecipe("/test/mhfdat.bin", recipe);
        }

        private void AddRecipe(string sourcePath, ExtractionRecipe recipe)
        {
            _fileSystem.AddFile($"{sourcePath}{ExtractionRecipe.FileSuffix}", recipe.Serialize());
        }

        private static ExtractionRecipe JpkOnlyRecipe(
            CompressionType algorithm = CompressionType.LZ,
            string extractedFile = "file.bin.jkr.bin")
        {
            var recipe = new ExtractionRecipe
            {
                SourceFile = extractedFile.Split('.')[0] + ".bin",
                ExtractedFile = extractedFile,
            };
            recipe.Layers.Add(new RecipeLayer { Kind = RecipeLayerKind.Jpk, Algorithm = algorithm });
            return recipe;
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
                0x65, 0x63, 0x64, 0x1A, // Magic: ecd\x1A
                (byte)keyIndex, 0x00,   // Key index (little-endian UInt16)
                0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, // Payload size, rewritten on encrypt
                0x00, 0x00, 0x00, 0x00, // CRC32, rewritten on encrypt
            };
        }

        #endregion
    }
}
