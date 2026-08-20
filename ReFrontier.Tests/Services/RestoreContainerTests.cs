using LibReFrontier;
using LibReFrontier.Exceptions;

using ReFrontier.Jpk;
using ReFrontier.Services;
using ReFrontier.Tests.Mocks;

namespace ReFrontier.Tests.Services
{
    /// <summary>
    /// Tests for restoring container archives, where rebuilding means packing a directory
    /// back through its log rather than compressing a single file, and any entry that was
    /// itself unpacked has to be rebuilt first.
    /// </summary>
    public class RestoreContainerTests
    {
        private readonly InMemoryFileSystem _fileSystem;
        private readonly TestLogger _logger;
        private readonly FileProcessingConfig _config;
        private readonly ICodecFactory _codecFactory;
        private readonly RestoreService _service;

        public RestoreContainerTests()
        {
            _fileSystem = new InMemoryFileSystem();
            _logger = new TestLogger();
            _config = FileProcessingConfig.Default();
            _codecFactory = new DefaultCodecFactory();
            _service = new RestoreService(_fileSystem, _logger, _codecFactory, _config);
        }

        [Fact]
        public void Restore_ContainerFromOriginalName_PacksTheUnpackedDirectory()
        {
            SetupContainer();

            string result = _service.Restore("/test/arc.bin", levelOverride: null);

            Assert.Equal("output/arc.bin", result.Replace('\\', '/'));
            byte[] packed = _fileSystem.ReadAllBytes(result);
            Assert.Equal(2, BitConverter.ToInt32(packed, 0));
        }

        [Fact]
        public void Restore_ContainerFromUnpackedDirectory_Works()
        {
            SetupContainer();

            // The user naturally points at the directory they edited.
            string result = _service.Restore("/test/arc.bin.unpacked", levelOverride: null);

            Assert.Equal("output/arc.bin", result.Replace('\\', '/'));
        }

        [Fact]
        public void Restore_ContainerWithUnpackedEntry_RebuildsItBeforePacking()
        {
            SetupContainer();

            // Extraction decompressed b.jkr in place, so the log no longer matches disk.
            _fileSystem.DeleteFile("/test/arc.bin.unpacked/b.jkr");
            byte[] payload = CreateTestData(512);
            _fileSystem.AddFile("/test/arc.bin.unpacked/b.jkr.bin", payload);
            AddJpkRecipe("/test/arc.bin.unpacked/b.jkr", "b.jkr", "b.jkr.bin", CompressionType.LZ);

            string result = _service.Restore("/test/arc.bin.unpacked", levelOverride: 16);

            // The entry is back under the name the log uses...
            Assert.True(_fileSystem.FileExists("/test/arc.bin.unpacked/b.jkr"));
            Assert.Equal(
                FileMagic.JKR,
                BitConverter.ToUInt32(_fileSystem.ReadAllBytes("/test/arc.bin.unpacked/b.jkr"), 0)
            );
            // ...and the container packed successfully rather than reporting it missing.
            Assert.True(_fileSystem.FileExists(result));
            Assert.True(_logger.AnyLineContains("Rebuilt 1 nested entry"));
        }

        [Fact]
        public void Restore_ContainerWithUnpackedEntry_PreservesEntryPayload()
        {
            SetupContainer();
            _fileSystem.DeleteFile("/test/arc.bin.unpacked/b.jkr");
            byte[] payload = CreateTestData(1024);
            _fileSystem.AddFile("/test/arc.bin.unpacked/b.jkr.bin", payload);
            AddJpkRecipe("/test/arc.bin.unpacked/b.jkr", "b.jkr", "b.jkr.bin", CompressionType.HFI);

            _service.Restore("/test/arc.bin.unpacked", levelOverride: 16);

            _fileSystem.AddFile("/verify/entry.jkr", _fileSystem.ReadAllBytes("/test/arc.bin.unpacked/b.jkr"));
            var unpacker = new UnpackingService(_fileSystem, _logger, _codecFactory, _config);
            string decompressed = unpacker.UnpackJPK("/verify/entry.jkr");

            Assert.Equal(payload, _fileSystem.ReadAllBytes(decompressed));
        }

        [Fact]
        public void Restore_NestedContainers_RebuildsDepthFirst()
        {
            SetupContainer();

            // Entry b is itself a container, unpacked one level further down.
            _fileSystem.DeleteFile("/test/arc.bin.unpacked/b.jkr");
            AddSimpleArchive(
                directory: "/test/arc.bin.unpacked/b.jkr.unpacked",
                logPath: "/test/arc.bin.unpacked/b.jkr.log",
                packedName: "b.jkr",
                entries: [("inner1.bin", CreateTestData(32)), ("inner2.bin", CreateTestData(48))]
            );
            AddContainerRecipe(
                "/test/arc.bin.unpacked/b.jkr", "b.jkr", "b.jkr.unpacked", "SimpleArchive"
            );

            string result = _service.Restore("/test/arc.bin.unpacked", levelOverride: null);

            // The inner container was packed back into the entry the outer log names.
            Assert.True(_fileSystem.FileExists("/test/arc.bin.unpacked/b.jkr"));
            Assert.Equal(
                2,
                BitConverter.ToInt32(_fileSystem.ReadAllBytes("/test/arc.bin.unpacked/b.jkr"), 0)
            );
            Assert.True(_fileSystem.FileExists(result));
        }

        [Fact]
        public void Restore_ContainerUnderEncryption_PacksThenEncrypts()
        {
            SetupContainer(sourceName: "arc.bin", logPackedName: "arc.bin.decd");
            _fileSystem.AddFile("/test/arc.bin.meta", CreateEcdMetaHeader(keyIndex: 4));

            var recipe = new ExtractionRecipe
            {
                SourceFile = "arc.bin",
                ExtractedFile = "arc.bin.unpacked",
            };
            recipe.Layers.Add(new RecipeLayer
            {
                Kind = RecipeLayerKind.Ecd,
                MetaFile = "arc.bin.meta",
                OriginalSize = 4096,
            });
            recipe.Layers.Add(new RecipeLayer
            {
                Kind = RecipeLayerKind.Container,
                ContainerType = "SimpleArchive",
                Directory = "arc.bin.unpacked",
            });
            _fileSystem.AddFile($"/test/arc.bin{ExtractionRecipe.FileSuffix}", recipe.Serialize());

            string result = _service.Restore("/test/arc.bin.unpacked", levelOverride: null);

            Assert.Equal("output/arc.bin", result.Replace('\\', '/'));
            Assert.Equal(FileMagic.ECD, BitConverter.ToUInt32(_fileSystem.ReadAllBytes(result), 0));
            // The packed intermediate must not be left lying next to the result.
            Assert.False(_fileSystem.FileExists("output/arc.bin.decd"));
        }

        [Fact]
        public void Restore_MissingUnpackedDirectory_ExplainsWhatIsWrong()
        {
            AddContainerRecipe("/test/arc.bin", "arc.bin", "arc.bin.unpacked", "SimpleArchive");

            var ex = Assert.Throws<ReFrontierException>(
                () => _service.Restore("/test/arc.bin", levelOverride: null)
            );

            Assert.Contains("arc.bin.unpacked", ex.Message, StringComparison.Ordinal);
            Assert.Contains("missing", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Restore_DirectoryButRecipeIsNotAContainer_ExplainsWhatIsWrong()
        {
            _fileSystem.AddFile("/test/thing.bin.unpacked/entry.bin", CreateTestData(16));
            var recipe = new ExtractionRecipe { SourceFile = "thing.bin", ExtractedFile = "thing.bin.bin" };
            recipe.Layers.Add(new RecipeLayer { Kind = RecipeLayerKind.Jpk, Algorithm = CompressionType.LZ });
            _fileSystem.AddFile($"/test/thing.bin{ExtractionRecipe.FileSuffix}", recipe.Serialize());

            var ex = Assert.Throws<ReFrontierException>(
                () => _service.Restore("/test/thing.bin.unpacked", levelOverride: 16)
            );

            Assert.Contains("--pack", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Restore_ContainerNestedTooDeep_StopsRatherThanRecursingForever()
        {
            // Guards against a recipe chain that never bottoms out.
            const int levels = 20;
            string path = "/test/d0";
            AddContainerRecipe(path, "d0", "d0.unpacked", "SimpleArchive");
            string dir = "/test/d0.unpacked";
            for (int i = 1; i <= levels; i++)
            {
                _fileSystem.AddDirectory($"{dir}/d{i}.unpacked");
                AddContainerRecipe($"{dir}/d{i}", $"d{i}", $"d{i}.unpacked", "SimpleArchive");
                dir = $"{dir}/d{i}.unpacked";
            }

            var ex = Assert.Throws<ReFrontierException>(
                () => _service.Restore("/test/d0", levelOverride: null)
            );

            Assert.Contains("levels of nesting", ex.Message, StringComparison.Ordinal);
        }

        #region Helpers

        private void SetupContainer(string sourceName = "arc.bin", string? logPackedName = null)
        {
            AddSimpleArchive(
                directory: $"/test/{sourceName}.unpacked",
                logPath: $"/test/{sourceName}.log",
                packedName: logPackedName ?? sourceName,
                entries: [("a.bin", CreateTestData(64)), ("b.jkr", CreateTestData(96))]
            );
            if (logPackedName == null)
            {
                AddContainerRecipe($"/test/{sourceName}", sourceName, $"{sourceName}.unpacked", "SimpleArchive");
            }
        }

        private void AddSimpleArchive(
            string directory, string logPath, string packedName, (string Name, byte[] Data)[] entries)
        {
            var lines = new List<string> { "SimpleArchive", packedName, entries.Length.ToString() };
            foreach (var (name, data) in entries)
            {
                lines.Add($"{name},0,{data.Length},0");
                _fileSystem.AddFile($"{directory}/{name}", data);
            }
            _fileSystem.AddFile(logPath, string.Join("\n", lines));
        }

        private void AddContainerRecipe(
            string sourcePath, string sourceName, string directoryName, string containerType)
        {
            var recipe = new ExtractionRecipe
            {
                SourceFile = sourceName,
                ExtractedFile = directoryName,
            };
            recipe.Layers.Add(new RecipeLayer
            {
                Kind = RecipeLayerKind.Container,
                ContainerType = containerType,
                Directory = directoryName,
            });
            _fileSystem.AddFile($"{sourcePath}{ExtractionRecipe.FileSuffix}", recipe.Serialize());
        }

        private void AddJpkRecipe(
            string sourcePath, string sourceName, string extractedName, CompressionType algorithm)
        {
            var recipe = new ExtractionRecipe
            {
                SourceFile = sourceName,
                ExtractedFile = extractedName,
            };
            recipe.Layers.Add(new RecipeLayer { Kind = RecipeLayerKind.Jpk, Algorithm = algorithm });
            _fileSystem.AddFile($"{sourcePath}{ExtractionRecipe.FileSuffix}", recipe.Serialize());
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
                0x00, 0x00,
                0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00,
            };
        }

        #endregion
    }
}
