using System.IO;

using LibReFrontier;

using ReFrontier.Jpk;
using ReFrontier.Services;
using ReFrontier.Tests.Mocks;

namespace ReFrontier.Tests
{
    /// <summary>
    /// Tests that extraction records how a file was taken apart, which is what
    /// lets it be rebuilt later without the user re-specifying any settings.
    /// </summary>
    public class RecipeWritingTests
    {
        private readonly InMemoryFileSystem _fileSystem;
        private readonly TestLogger _logger;
        private readonly FileProcessingConfig _config;
        private readonly ICodecFactory _codecFactory;
        private readonly Program _program;

        public RecipeWritingTests()
        {
            _fileSystem = new InMemoryFileSystem();
            _logger = new TestLogger();
            _config = FileProcessingConfig.Default();
            _codecFactory = new DefaultCodecFactory();
            _program = new Program(_fileSystem, _logger, _codecFactory, _config);
        }

        [Theory]
        [InlineData(CompressionType.RW)]
        [InlineData(CompressionType.HFIRW)]
        [InlineData(CompressionType.LZ)]
        [InlineData(CompressionType.HFI)]
        public void ProcessFile_CompressedFile_RecordsTheAlgorithmItWasCompressedWith(CompressionType algorithm)
        {
            byte[] payload = CreateTestData(512);
            AddCompressedFile("/test/file.jkr", payload, algorithm);

            _program.ProcessFile("/test/file.jkr", ExtractArgs());

            var recipe = ReadRecipe("/test/file.jkr");
            Assert.NotNull(recipe);
            var jpkLayer = Assert.Single(recipe.Layers);
            Assert.Equal(RecipeLayerKind.Jpk, jpkLayer.Kind);
            Assert.Equal(algorithm, jpkLayer.Algorithm);
        }

        [Fact]
        public void ProcessFile_CompressedFile_RecordsSourceAndExtractedNames()
        {
            AddCompressedFile("/test/file.jkr", CreateTestData(512), CompressionType.LZ);

            var result = _program.ProcessFile("/test/file.jkr", ExtractArgs());

            var recipe = ReadRecipe("/test/file.jkr");
            Assert.NotNull(recipe);
            Assert.Equal("file.jkr", recipe.SourceFile);
            Assert.Equal(Path.GetFileName(result.OutputPath), recipe.ExtractedFile);
        }

        [Fact]
        public void ProcessFile_CompressedFile_RecordsOriginalSize()
        {
            AddCompressedFile("/test/file.jkr", CreateTestData(512), CompressionType.LZ);
            long packedSize = _fileSystem.GetFileLength("/test/file.jkr");

            _program.ProcessFile("/test/file.jkr", ExtractArgs());

            var recipe = ReadRecipe("/test/file.jkr");
            Assert.NotNull(recipe);
            Assert.Equal(packedSize, recipe.Layers[0].OriginalSize);
        }

        [Fact]
        public void ProcessFile_WithoutSaveMeta_WritesNoRecipe()
        {
            AddCompressedFile("/test/file.jkr", CreateTestData(512), CompressionType.LZ);

            var args = ExtractArgs();
            args.createLog = false;
            _program.ProcessFile("/test/file.jkr", args);

            // Recipes follow the same opt-in as .meta files, so extraction stays
            // side-effect free for users who only want to look at a file.
            Assert.False(_fileSystem.FileExists($"/test/file.jkr{ExtractionRecipe.FileSuffix}"));
        }

        [Fact]
        public void ProcessFile_PlainFile_WritesNoRecipe()
        {
            // Nothing was undone, so there is nothing to reverse. A plain file is probed as
            // a simple archive and skipped; the probe must leave no recipe behind.
            _fileSystem.AddFile("/test/plain.bin", CreateTestData(64));

            var result = _program.ProcessFile("/test/plain.bin", ExtractArgs());

            Assert.False(result.WasProcessed);
            Assert.False(_fileSystem.FileExists($"/test/plain.bin{ExtractionRecipe.FileSuffix}"));
        }

        [Fact]
        public void ProcessFile_IgnoreJpk_WritesNoRecipe()
        {
            AddCompressedFile("/test/file.jkr", CreateTestData(512), CompressionType.LZ);

            var args = ExtractArgs();
            args.ignoreJPK = true;
            _program.ProcessFile("/test/file.jkr", args);

            Assert.False(_fileSystem.FileExists($"/test/file.jkr{ExtractionRecipe.FileSuffix}"));
        }

        [Fact]
        public void ProcessFile_RecipeIsRoundTrippedByRestore()
        {
            byte[] payload = CreateTestData(2048);
            AddCompressedFile("/test/file.jkr", payload, CompressionType.HFI);

            var result = _program.ProcessFile("/test/file.jkr", ExtractArgs());

            // The whole point: extraction hands restore everything it needs.
            var restoreService = new RestoreService(_fileSystem, _logger, _codecFactory, _config);
            string rebuilt = restoreService.Restore(result.OutputPath!, levelOverride: 16);

            _fileSystem.AddFile("/test/verify.jkr", _fileSystem.ReadAllBytes(rebuilt));
            var unpacker = new UnpackingService(_fileSystem, _logger, _codecFactory, _config);
            string decompressed = unpacker.UnpackJPK("/test/verify.jkr");

            Assert.Equal(payload, _fileSystem.ReadAllBytes(decompressed));
        }

        #region Helpers

        private static InputArguments ExtractArgs()
        {
            return new InputArguments
            {
                createLog = true,
                recursive = true,
            };
        }

        private void AddCompressedFile(string path, byte[] payload, CompressionType algorithm)
        {
            _fileSystem.AddFile("/staging/payload.bin", payload);
            var packer = new PackingService(_fileSystem, _logger, _codecFactory, _config);
            packer.JPKEncode(new Compression(algorithm, 16), "/staging/payload.bin", "/staging/packed.jkr");
            _fileSystem.AddFile(path, _fileSystem.ReadAllBytes("/staging/packed.jkr"));
        }

        private ExtractionRecipe? ReadRecipe(string sourcePath)
        {
            string recipePath = $"{sourcePath}{ExtractionRecipe.FileSuffix}";
            return _fileSystem.FileExists(recipePath)
                ? ExtractionRecipe.Deserialize(_fileSystem.ReadAllBytes(recipePath))
                : null;
        }

        private static byte[] CreateTestData(int size)
        {
            byte[] data = new byte[size];
            for (int i = 0; i < size; i++)
                data[i] = (byte)(i % 251);
            return data;
        }

        #endregion
    }
}
