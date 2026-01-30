using LibReFrontier;
using LibReFrontier.Exceptions;

using ReFrontier.Jpk;
using ReFrontier.Services;
using ReFrontier.Tests.Mocks;

namespace ReFrontier.Tests.Services
{
    /// <summary>
    /// Tests for PackingService.
    /// </summary>
    public class PackingServiceTests
    {
        private readonly InMemoryFileSystem _fileSystem;
        private readonly TestLogger _logger;
        private readonly FileProcessingConfig _config;
        private readonly ICodecFactory _codecFactory;
        private readonly PackingService _service;

        public PackingServiceTests()
        {
            _fileSystem = new InMemoryFileSystem();
            _logger = new TestLogger();
            _config = FileProcessingConfig.Default();
            _codecFactory = new DefaultCodecFactory();
            _service = new PackingService(_fileSystem, _logger, _codecFactory, _config);
        }

        [Fact]
        public void JPKEncode_CreatesCompressedFile()
        {
            // Arrange
            byte[] testData = new byte[100];
            for (int i = 0; i < testData.Length; i++)
                testData[i] = (byte)(i % 256);
            _fileSystem.AddFile("/test/input.bin", testData);

            var compression = new Compression(CompressionType.LZ, 15);

            // Act
            _service.JPKEncode(compression, "/test/input.bin", "output/compressed.jkr");

            // Assert
            Assert.True(_fileSystem.FileExists("output/compressed.jkr"));
            var compressed = _fileSystem.ReadAllBytes("output/compressed.jkr");
            Assert.True(compressed.Length > 0);
            // Check JKR magic
            Assert.Equal(0x4A, compressed[0]); // 'J'
            Assert.Equal(0x4B, compressed[1]); // 'K'
            Assert.Equal(0x52, compressed[2]); // 'R'
            Assert.Equal(0x1A, compressed[3]);
            Assert.True(_logger.ContainsMessage("compressed"));
        }

        [Fact]
        public void JPKEncode_WithRWCompression_CreatesValidFile()
        {
            // Arrange
            byte[] testData = new byte[50];
            for (int i = 0; i < testData.Length; i++)
                testData[i] = (byte)i;
            _fileSystem.AddFile("/test/input.bin", testData);

            var compression = new Compression(CompressionType.RW, 10);

            // Act
            _service.JPKEncode(compression, "/test/input.bin", "output/compressed.jkr");

            // Assert
            Assert.True(_fileSystem.FileExists("output/compressed.jkr"));
        }

        [Fact]
        public void JPKEncode_WithHFICompression_CreatesValidFile()
        {
            // Arrange
            byte[] testData = new byte[100];
            for (int i = 0; i < testData.Length; i++)
                testData[i] = (byte)(i % 10); // Repetitive data for better compression
            _fileSystem.AddFile("/test/input.bin", testData);

            var compression = new Compression(CompressionType.HFI, 10);

            // Act
            _service.JPKEncode(compression, "/test/input.bin", "output/compressed.jkr");

            // Assert
            Assert.True(_fileSystem.FileExists("output/compressed.jkr"));
        }

        [Fact]
        public void JPKEncode_DeletesExistingOutputFile()
        {
            // Arrange
            _fileSystem.AddFile("output/compressed.jkr", new byte[] { 0xFF, 0xFF });
            byte[] testData = new byte[50];
            _fileSystem.AddFile("/test/input.bin", testData);

            var compression = new Compression(CompressionType.LZ, 10);

            // Act
            _service.JPKEncode(compression, "/test/input.bin", "output/compressed.jkr");

            // Assert
            var result = _fileSystem.ReadAllBytes("output/compressed.jkr");
            Assert.NotEqual(new byte[] { 0xFF, 0xFF }, result);
        }

        [Fact]
        public void ProcessPackInput_WithMissingLogFile_ThrowsFileNotFoundException()
        {
            // Arrange
            _fileSystem.AddDirectory("/test/dir.unpacked");

            // Act & Assert
            Assert.Throws<System.IO.FileNotFoundException>(() =>
                _service.ProcessPackInput("/test/dir.unpacked"));
        }

        [Fact]
        public void ProcessPackInput_SimpleArchive_CreatesPackedFile()
        {
            // Arrange
            string logContent =
                "SimpleArchive\n" +
                "test.bin\n" +
                "2\n" +
                "file1.bin,0,10,0\n" +
                "file2.bin,10,20,0";
            _fileSystem.AddFile("/test/dir.unpacked/dir.unpacked.log", logContent);
            _fileSystem.AddFile("/test/dir.unpacked/file1.bin", new byte[] { 0x01, 0x02, 0x03 });
            _fileSystem.AddFile("/test/dir.unpacked/file2.bin", new byte[] { 0x04, 0x05, 0x06, 0x07 });

            // Act
            _service.ProcessPackInput("/test/dir.unpacked");

            // Assert
            Assert.True(_fileSystem.FileExists("output/test.bin"));
            Assert.True(_logger.ContainsMessage("Simple archive"));
        }

        [Fact]
        public void ProcessPackInput_UnknownType_ThrowsPackingException()
        {
            // Arrange
            string logContent = "UnknownType\ntest.bin";
            _fileSystem.AddFile("/test/dir.unpacked/dir.unpacked.log", logContent);

            // Act & Assert
            var ex = Assert.Throws<PackingException>(() =>
                _service.ProcessPackInput("/test/dir.unpacked"));
            Assert.Contains("Unknown container type", ex.Message);
        }

        [Fact]
        public void JPKEncode_WithHFIRWCompression_CreatesValidFile()
        {
            // Arrange
            byte[] testData = new byte[100];
            for (int i = 0; i < testData.Length; i++)
                testData[i] = (byte)(i % 5);
            _fileSystem.AddFile("/test/input.bin", testData);

            var compression = new Compression(CompressionType.HFIRW, 10);

            // Act
            _service.JPKEncode(compression, "/test/input.bin", "output/compressed.jkr");

            // Assert
            Assert.True(_fileSystem.FileExists("output/compressed.jkr"));
        }




        [Fact]
        public void PackingService_CanBeCreatedWithDefaultConstructor()
        {
            // Arrange & Act
            var service = new PackingService();

            // Assert
            Assert.NotNull(service);
        }

        [Fact]
        public void JPKEncode_LogsCompressionInfo()
        {
            // Arrange
            byte[] testData = new byte[30];
            _fileSystem.AddFile("/test/input.bin", testData);

            var compression = new Compression(CompressionType.LZ, 10);

            // Act
            _service.JPKEncode(compression, "/test/input.bin", "output/compressed.jkr");

            // Assert
            Assert.True(_logger.ContainsMessage("Starting file compression"));
            Assert.True(_logger.ContainsMessage("LZ"));
            Assert.True(_logger.ContainsMessage("level 10"));
        }
    }
}
