using LibReFrontier;
using LibReFrontier.Exceptions;

using ReFrontier.Jpk;
using ReFrontier.Services;
using ReFrontier.Tests.Mocks;

namespace ReFrontier.Tests.Services
{
    /// <summary>
    /// Tests for UnpackingService.
    /// </summary>
    public class UnpackingServiceTests
    {
        private readonly InMemoryFileSystem _fileSystem;
        private readonly TestLogger _logger;
        private readonly FileProcessingConfig _config;
        private readonly ICodecFactory _codecFactory;
        private readonly UnpackingService _service;

        public UnpackingServiceTests()
        {
            _fileSystem = new InMemoryFileSystem();
            _logger = new TestLogger();
            _config = FileProcessingConfig.Default();
            _codecFactory = new DefaultCodecFactory();
            _service = new UnpackingService(_fileSystem, _logger, _codecFactory, _config);
        }

        [Fact]
        public void UnpackSimpleArchive_TooSmallFile_ThrowsPackingException()
        {
            // Arrange
            byte[] smallFile = new byte[10];
            _fileSystem.AddFile("/test/small.bin", smallFile);
            using var ms = new MemoryStream(smallFile);
            using var br = new BinaryReader(ms);

            // Act & Assert
            var ex = Assert.Throws<PackingException>(() =>
                _service.UnpackSimpleArchive("/test/small.bin", br, 4, false, false, false)
            );
            Assert.Contains("too small", ex.Message);
        }

        [Fact]
        public void UnpackSimpleArchive_ValidArchive_CreatesOutputDirectory()
        {
            // Arrange - Create a simple archive with 1 entry
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            // Count
            bw.Write((int)1);
            // Entry 0: offset and size
            bw.Write((int)12); // offset (after header: 4 bytes count + 8 bytes entry)
            bw.Write((int)4);  // size
            // Entry data
            bw.Write((int)0x12345678); // some data

            byte[] archiveData = ms.ToArray();
            _fileSystem.AddFile("/test/archive.bin", archiveData);

            using var readMs = new MemoryStream(archiveData);
            using var br = new BinaryReader(readMs);

            // Act
            var result = _service.UnpackSimpleArchive("/test/archive.bin", br, 4, false, false, false);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("/test/archive.bin.unpacked", result);
            Assert.True(_fileSystem.DirectoryExists("/test/archive.bin.unpacked"));
        }

        [Fact]
        public void UnpackSimpleArchive_WithLog_CreatesLogFile()
        {
            // Arrange
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            bw.Write((int)1);
            bw.Write((int)12);
            bw.Write((int)4);
            bw.Write((int)0x12345678);

            byte[] archiveData = ms.ToArray();
            _fileSystem.AddFile("/test/archive.bin", archiveData);

            using var readMs = new MemoryStream(archiveData);
            using var br = new BinaryReader(readMs);

            // Act
            _service.UnpackSimpleArchive("/test/archive.bin", br, 4, createLog: true, cleanUp: false, autoStage: false);

            // Assert
            Assert.True(_fileSystem.FileExists("/test/archive.bin.log"));
        }

        [Fact]
        public void UnpackSimpleArchive_WithCleanup_DeletesOriginal()
        {
            // Arrange
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            bw.Write((int)1);
            bw.Write((int)12);
            bw.Write((int)4);
            bw.Write((int)0x12345678);

            byte[] archiveData = ms.ToArray();
            _fileSystem.AddFile("/test/archive.bin", archiveData);

            using var readMs = new MemoryStream(archiveData);
            using var br = new BinaryReader(readMs);

            // Act
            _service.UnpackSimpleArchive("/test/archive.bin", br, 4, createLog: false, cleanUp: true, autoStage: false);

            // Assert
            Assert.False(_fileSystem.FileExists("/test/archive.bin"));
        }

        [Fact]
        public void UnpackJPK_ValidJKRFile_CreatesDecompressedFile()
        {
            // Arrange - First compress some data, then decompress
            var packingService = new PackingService(_fileSystem, _logger, _codecFactory, _config);
            byte[] originalData = new byte[50];
            for (int i = 0; i < originalData.Length; i++)
                originalData[i] = (byte)i;
            _fileSystem.AddFile("/test/original.bin", originalData);

            packingService.JPKEncode(
                new Compression(CompressionType.LZ, 10),
                "/test/original.bin",
                "/test/compressed.jkr"
            );

            _logger.Clear();

            // Act
            var result = _service.UnpackJPK("/test/compressed.jkr");

            // Assert
            Assert.NotNull(result);
            Assert.True(_fileSystem.FileExists(result));
            Assert.False(_fileSystem.FileExists("/test/compressed.jkr")); // Original deleted
            Assert.True(_logger.ContainsMessage("JPK LZ"));
        }

        [Fact]
        public void UnpackJPK_InvalidFile_ThrowsPackingException()
        {
            // Arrange - File without JKR header
            byte[] invalidData = new byte[] { 0x00, 0x01, 0x02, 0x03 };
            _fileSystem.AddFile("/test/invalid.bin", invalidData);

            // Act & Assert
            var ex = Assert.Throws<PackingException>(() =>
                _service.UnpackJPK("/test/invalid.bin")
            );
            Assert.Contains("Invalid JKR header", ex.Message);
        }

        [Fact]
        public void UnpackingService_CanBeCreatedWithDependencies()
        {
            // Arrange & Act
            var service = new UnpackingService(_fileSystem, _logger, _codecFactory, _config);

            // Assert
            Assert.NotNull(service);
        }

        [Fact]
        public void UnpackingService_DefaultConstructor_Works()
        {
            // Arrange & Act
            var service = new UnpackingService();

            // Assert
            Assert.NotNull(service);
        }

        [Fact]
        public void UnpackJPK_HFIRWCompression_Decompresses()
        {
            // Arrange - Compress with HFIRW
            var packingService = new PackingService(_fileSystem, _logger, _codecFactory, _config);
            byte[] originalData = new byte[100];
            for (int i = 0; i < originalData.Length; i++)
                originalData[i] = (byte)(i % 10);
            _fileSystem.AddFile("/test/original.bin", originalData);

            packingService.JPKEncode(
                new Compression(CompressionType.HFIRW, 10),
                "/test/original.bin",
                "/test/compressed.jkr"
            );

            _logger.Clear();

            // Act
            var result = _service.UnpackJPK("/test/compressed.jkr");

            // Assert
            Assert.NotNull(result);
            Assert.True(_fileSystem.FileExists(result));
            Assert.True(_logger.ContainsMessage("JPK HFIRW"));
        }

        [Fact]
        public void UnpackJPK_RWCompression_Decompresses()
        {
            // Arrange - Compress with RW
            var packingService = new PackingService(_fileSystem, _logger, _codecFactory, _config);
            byte[] originalData = new byte[30];
            for (int i = 0; i < originalData.Length; i++)
                originalData[i] = (byte)i;
            _fileSystem.AddFile("/test/original.bin", originalData);

            packingService.JPKEncode(
                new Compression(CompressionType.RW, 10),
                "/test/original.bin",
                "/test/compressed.jkr"
            );

            _logger.Clear();

            // Act
            var result = _service.UnpackJPK("/test/compressed.jkr");

            // Assert
            Assert.NotNull(result);
            Assert.True(_fileSystem.FileExists(result));
            Assert.True(_logger.ContainsMessage("JPK RW"));
        }

        [Fact]
        public void UnpackJPK_HFICompression_Decompresses()
        {
            // Arrange - Compress with HFI
            var packingService = new PackingService(_fileSystem, _logger, _codecFactory, _config);
            byte[] originalData = new byte[50];
            for (int i = 0; i < originalData.Length; i++)
                originalData[i] = (byte)(i * 2);
            _fileSystem.AddFile("/test/original.bin", originalData);

            packingService.JPKEncode(
                new Compression(CompressionType.HFI, 20),
                "/test/original.bin",
                "/test/compressed.jkr"
            );

            _logger.Clear();

            // Act
            var result = _service.UnpackJPK("/test/compressed.jkr");

            // Assert
            Assert.NotNull(result);
            Assert.True(_fileSystem.FileExists(result));
            Assert.True(_logger.ContainsMessage("JPK HFI"));
        }



    }
}
