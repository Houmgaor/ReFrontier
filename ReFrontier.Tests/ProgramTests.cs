using System.Linq;

using LibReFrontier;
using LibReFrontier.Exceptions;

using ReFrontier.Jpk;
using ReFrontier.Services;
using ReFrontier.Tests.Mocks;

namespace ReFrontier.Tests
{
    /// <summary>
    /// Tests for Program class methods.
    /// </summary>
    public class ProgramTests
    {
        private readonly InMemoryFileSystem _fileSystem;
        private readonly TestLogger _logger;
        private readonly FileProcessingConfig _config;
        private readonly ICodecFactory _codecFactory;
        private readonly Program _program;

        public ProgramTests()
        {
            _fileSystem = new InMemoryFileSystem();
            _logger = new TestLogger();
            _config = FileProcessingConfig.Default();
            _codecFactory = new DefaultCodecFactory();
            _program = new Program(_fileSystem, _logger, _codecFactory, _config);
        }

        #region Constructor Tests

        [Fact]
        public void Program_DefaultConstructor_CreatesValidInstance()
        {
            // Act
            var program = new Program();

            // Assert
            Assert.NotNull(program);
        }

        [Fact]
        public void Program_InjectedDependencies_CreatesValidInstance()
        {
            // Assert
            Assert.NotNull(_program);
        }

        #endregion

        #region StartProcessingFile Tests

        [Fact]
        public void StartProcessingFile_WithCompression_CreatesCompressedOutput()
        {
            // Arrange
            byte[] testData = new byte[100];
            for (int i = 0; i < testData.Length; i++)
                testData[i] = (byte)(i % 256);
            _fileSystem.AddFile("/test/input.bin", testData);

            var args = new InputArguments
            {
                compression = new Compression(CompressionType.LZ, 10)
            };

            // Act
            _program.StartProcessingFile("/test/input.bin", args);

            // Assert
            Assert.True(_fileSystem.FileExists("output/input"));
            Assert.True(_logger.ContainsMessage("compressed"));
        }

        [Fact]
        public void StartProcessingFile_NoCompressionNoEncrypt_LogsAndContinues()
        {
            // Arrange - File with no recognized format (50 bytes of zeros)
            byte[] testData = new byte[50];
            _fileSystem.AddFile("/test/input.bin", testData);

            var args = new InputArguments
            {
                compression = new Compression(CompressionType.RW, 0),
                encrypt = false
            };

            // Act - Should not throw; exceptions are caught and logged
            _program.StartProcessingFile("/test/input.bin", args);

            // Assert - Should have logged a skip message
            Assert.True(_logger.ContainsMessage("Skipping") || _logger.ContainsMessage("stage-specific"));
        }

        [Fact]
        public void StartProcessingFile_WithEncrypt_RequiresMetaFile()
        {
            // Arrange
            byte[] testData = new byte[50];
            _fileSystem.AddFile("/test/input.bin.decd", testData);

            var args = new InputArguments
            {
                encrypt = true
            };

            // Act & Assert - Should throw because meta file doesn't exist
            Assert.Throws<FileNotFoundException>(() =>
                _program.StartProcessingFile("/test/input.bin.decd", args));
        }

        #endregion

        #region StartProcessingDirectory Tests

        [Fact]
        public void StartProcessingDirectory_WithRepack_CallsPackInput()
        {
            // Arrange - Create directory with log file
            string logContent =
                "SimpleArchive\n" +
                "test.bin\n" +
                "1\n" +
                "file1.bin,0,10,0";
            _fileSystem.AddDirectory("/test/dir.unpacked");
            _fileSystem.AddFile("/test/dir.unpacked/dir.unpacked.log", logContent);
            _fileSystem.AddFile("/test/dir.unpacked/file1.bin", new byte[] { 0x01, 0x02 });

            var args = new InputArguments
            {
                repack = true
            };

            // Act
            _program.StartProcessingDirectory("/test/dir.unpacked", args);

            // Assert
            Assert.True(_fileSystem.FileExists("output/test.bin"));
            Assert.True(_logger.ContainsMessage("Simple archive"));
        }

        [Fact]
        public void StartProcessingDirectory_WithoutRepack_ProcessesFiles()
        {
            // Arrange - Create directory with files
            _fileSystem.AddDirectory("/test/dir");
            // Add a file large enough to pass size check (16+ bytes), but still invalid
            // Use .bin extension since .txt is excluded as an output file type
            _fileSystem.AddFile("/test/dir/file1.bin", new byte[20] { 0x48, 0x65, 0x6C, 0x6C, 0x6F, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });

            var args = new InputArguments
            {
                repack = false,
                recursive = false
            };

            // Act - Should not throw; exceptions are caught and logged
            _program.StartProcessingDirectory("/test/dir", args);

            // Assert - Files should be processed (or at least attempted with skip message)
            // The logger should have some output
            Assert.True(_logger.Lines.Count > 0 || _logger.Messages.Count > 0);
        }

        [Fact]
        public void StartProcessingDirectory_FiltersOutputFiles()
        {
            // Arrange - Create directory with both source and output files
            _fileSystem.AddDirectory("/test/dir");
            // Source file that should be processed
            _fileSystem.AddFile("/test/dir/file1.bin", new byte[20] { 0x48, 0x65, 0x6C, 0x6C, 0x6F, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
            // Output files that should be filtered out
            _fileSystem.AddFile("/test/dir/file1.decd", new byte[20]);
            _fileSystem.AddFile("/test/dir/file1.dexf", new byte[20]);
            _fileSystem.AddFile("/test/dir/file1.meta", new byte[20]);
            _fileSystem.AddFile("/test/dir/file1.log", new byte[20]);
            _fileSystem.AddFile("/test/dir/file1.txt", new byte[20]);
            _fileSystem.AddFile("/test/dir/image.png", new byte[20]);

            var args = new InputArguments
            {
                repack = false,
                recursive = false
            };

            // Act
            _program.StartProcessingDirectory("/test/dir", args);

            // Assert - Only the .bin file should trigger processing messages
            // The output files should be silently skipped by the filter
            var processingMessages = _logger.Messages.Where(m => m.Contains("Processing")).ToList();
            Assert.Single(processingMessages);
            Assert.Contains("file1.bin", processingMessages[0]);
        }

        [Fact]
        public void StartProcessingDirectory_FiltersUnpackedDirectories()
        {
            // Arrange - Create directory structure with .unpacked subdirectory
            _fileSystem.AddDirectory("/test/dir");
            _fileSystem.AddDirectory("/test/dir/archive.bin.unpacked");
            // Source file that should be processed
            _fileSystem.AddFile("/test/dir/file1.bin", new byte[20] { 0x48, 0x65, 0x6C, 0x6C, 0x6F, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
            // Files inside .unpacked directory should be filtered out
            _fileSystem.AddFile("/test/dir/archive.bin.unpacked/extracted.bin", new byte[20]);

            var args = new InputArguments
            {
                repack = false,
                recursive = false
            };

            // Act
            _program.StartProcessingDirectory("/test/dir", args);

            // Assert - Only the root .bin file should trigger processing
            var processingMessages = _logger.Messages.Where(m => m.Contains("Processing")).ToList();
            Assert.Single(processingMessages);
            Assert.Contains("file1.bin", processingMessages[0]);
            Assert.DoesNotContain(processingMessages, m => m.Contains("extracted.bin"));
        }

        #endregion

        #region ProcessFile Tests

        [Fact]
        public void ProcessFile_RecognizesEcdMagic()
        {
            // Arrange - Create a file with ECD magic (0x1A646365)
            // ECD file structure: magic (4) + key (2) + unk (2) + size (4) + crc32 (4) + payload
            byte[] ecdFile = new byte[32];
            // Magic: "ecd\x1A" in little-endian
            ecdFile[0] = 0x65; // 'e'
            ecdFile[1] = 0x63; // 'c'
            ecdFile[2] = 0x64; // 'd'
            ecdFile[3] = 0x1A;
            // Key index (2 bytes)
            ecdFile[4] = 0x00;
            ecdFile[5] = 0x00;
            // Unknown (2 bytes)
            ecdFile[6] = 0x00;
            ecdFile[7] = 0x00;
            // Payload size (4 bytes) - 16 bytes of payload
            ecdFile[8] = 0x10;
            ecdFile[9] = 0x00;
            ecdFile[10] = 0x00;
            ecdFile[11] = 0x00;
            // CRC32 (4 bytes) - placeholder
            ecdFile[12] = 0x00;
            ecdFile[13] = 0x00;
            ecdFile[14] = 0x00;
            ecdFile[15] = 0x00;
            // Payload (16 bytes)
            for (int i = 16; i < 32; i++)
                ecdFile[i] = 0x00;

            _fileSystem.AddFile("/test/encrypted.ecd", ecdFile);

            var args = new InputArguments
            {
                createLog = true,
                recursive = false,
                decryptOnly = true
            };

            // Act
            _program.ProcessFile("/test/encrypted.ecd", args);

            // Assert - Should recognize ECD format
            Assert.True(_logger.ContainsMessage("ECD"));
        }

        [Fact]
        public void ProcessFile_RecognizesJkrMagic()
        {
            // Arrange - Create a simple JKR file
            // JKR magic is 0x1A524B4A ("JKR\x1A" in little-endian)
            // Structure: magic (4) + version (2) + type (2) + offset (4) + size (4) + compressed data
            var packingService = new PackingService(_fileSystem, _logger, _codecFactory, _config);
            byte[] testData = new byte[50];
            for (int i = 0; i < testData.Length; i++)
                testData[i] = (byte)i;
            _fileSystem.AddFile("/test/original.bin", testData);

            packingService.JPKEncode(
                new Compression(CompressionType.LZ, 10),
                "/test/original.bin",
                "/test/compressed.jkr"
            );

            _logger.Clear();

            var args = new InputArguments
            {
                recursive = false,
                ignoreJPK = false
            };

            // Act
            _program.ProcessFile("/test/compressed.jkr", args);

            // Assert - Should recognize JKR format and decompress
            Assert.True(_logger.ContainsMessage("JPK"));
        }

        [Fact]
        public void ProcessFile_WithIgnoreJPK_SkipsJkrProcessing()
        {
            // Arrange - Create a JKR file
            var packingService = new PackingService(_fileSystem, _logger, _codecFactory, _config);
            byte[] testData = new byte[50];
            _fileSystem.AddFile("/test/original.bin", testData);

            packingService.JPKEncode(
                new Compression(CompressionType.LZ, 10),
                "/test/original.bin",
                "/test/compressed.jkr"
            );

            _logger.Clear();

            var args = new InputArguments
            {
                recursive = false,
                ignoreJPK = true
            };

            // Act
            _program.ProcessFile("/test/compressed.jkr", args);

            // Assert - Should skip JPK processing
            Assert.False(_logger.ContainsMessage("decompressed"));
        }

        [Fact]
        public void ProcessFile_UnrecognizedMagic_ThrowsPackingException()
        {
            // Arrange - Create a file with unrecognized magic (too small to be valid)
            byte[] unknownFile = new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0 };
            _fileSystem.AddFile("/test/unknown.bin", unknownFile);

            var args = new InputArguments
            {
                recursive = false
            };

            // Act & Assert - Should throw PackingException for invalid file
            var ex = Assert.Throws<PackingException>(() =>
                _program.ProcessFile("/test/unknown.bin", args)
            );
            Assert.Contains("too small", ex.Message);
            // File should still exist
            Assert.True(_fileSystem.FileExists("/test/unknown.bin"));
        }

        #endregion

        #region InputArguments Equality Tests

        [Fact]
        public void InputArguments_Equals_ConsidersParallelism()
        {
            var args1 = new InputArguments
            {
                parallelism = 4,
                createLog = true
            };

            var args2 = new InputArguments
            {
                parallelism = 8,
                createLog = true
            };

            var args3 = new InputArguments
            {
                parallelism = 4,
                createLog = true
            };

            // Different parallelism should not be equal
            Assert.NotEqual(args1, args2);

            // Same parallelism should be equal
            Assert.Equal(args1, args3);
        }

        [Fact]
        public void InputArguments_GetHashCode_ConsidersParallelism()
        {
            var args1 = new InputArguments
            {
                parallelism = 4,
                createLog = true
            };

            var args2 = new InputArguments
            {
                parallelism = 8,
                createLog = true
            };

            // Different parallelism should produce different hash codes (most of the time)
            Assert.NotEqual(args1.GetHashCode(), args2.GetHashCode());
        }

        #endregion
    }
}
