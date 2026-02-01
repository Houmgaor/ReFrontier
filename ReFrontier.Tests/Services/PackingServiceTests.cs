using System.Text;

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

        #region FTXT Packing Tests

        [Fact]
        public void PackFTXT_CreatesPackedFile()
        {
            // Arrange
            byte[] meta = CreateFtxtMeta(2); // 2 strings
            _fileSystem.AddFile("/test/file.ftxt.meta", meta);
            _fileSystem.AddFile("/test/file.ftxt.txt", "Hello\nWorld");

            // Act
            string result = _service.PackFTXT("/test/file.ftxt.txt", "/test/file.ftxt.meta", false);

            // Assert
            Assert.Equal("/test/file.ftxt", result);
            Assert.True(_fileSystem.FileExists("/test/file.ftxt"));
        }

        [Fact]
        public void PackFTXT_WritesCorrectHeader()
        {
            // Arrange
            byte[] meta = CreateFtxtMeta(2);
            _fileSystem.AddFile("/test/file.ftxt.meta", meta);
            _fileSystem.AddFile("/test/file.ftxt.txt", "Hello\nWorld");

            // Act
            _service.PackFTXT("/test/file.ftxt.txt", "/test/file.ftxt.meta", false);

            // Assert
            byte[] result = _fileSystem.ReadAllBytes("/test/file.ftxt");

            // First 10 bytes should match meta
            for (int i = 0; i < 10; i++)
            {
                Assert.Equal(meta[i], result[i]);
            }

            // String count at offset 10 (2 bytes, little-endian)
            short stringCount = BitConverter.ToInt16(result, 10);
            Assert.Equal(2, stringCount);
        }

        [Fact]
        public void PackFTXT_WritesStringsWithNullTerminators()
        {
            // Arrange
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            byte[] meta = CreateFtxtMeta(2);
            _fileSystem.AddFile("/test/file.ftxt.meta", meta);
            _fileSystem.AddFile("/test/file.ftxt.txt", "ABC\nXYZ");

            // Act
            _service.PackFTXT("/test/file.ftxt.txt", "/test/file.ftxt.meta", false);

            // Assert
            byte[] result = _fileSystem.ReadAllBytes("/test/file.ftxt");

            // Skip 16-byte header, check strings
            // "ABC" + null + "XYZ" + null = 8 bytes
            Assert.Equal((byte)'A', result[16]);
            Assert.Equal((byte)'B', result[17]);
            Assert.Equal((byte)'C', result[18]);
            Assert.Equal((byte)0, result[19]); // null terminator
            Assert.Equal((byte)'X', result[20]);
            Assert.Equal((byte)'Y', result[21]);
            Assert.Equal((byte)'Z', result[22]);
            Assert.Equal((byte)0, result[23]); // null terminator
        }

        [Fact]
        public void PackFTXT_ReplacesNewlineMarkers()
        {
            // Arrange
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            byte[] meta = CreateFtxtMeta(1);
            _fileSystem.AddFile("/test/file.ftxt.meta", meta);
            _fileSystem.AddFile("/test/file.ftxt.txt", "Hello\\nWorld");

            // Act
            _service.PackFTXT("/test/file.ftxt.txt", "/test/file.ftxt.meta", false);

            // Assert
            byte[] result = _fileSystem.ReadAllBytes("/test/file.ftxt");

            // Find the newline character in the output (0x0A)
            bool foundNewline = false;
            for (int i = 16; i < result.Length; i++)
            {
                if (result[i] == 0x0A)
                {
                    foundNewline = true;
                    break;
                }
            }
            Assert.True(foundNewline, "Should contain actual newline character");
        }

        [Fact]
        public void PackFTXT_CalculatesCorrectTextBlockSize()
        {
            // Arrange
            byte[] meta = CreateFtxtMeta(2);
            _fileSystem.AddFile("/test/file.ftxt.meta", meta);
            _fileSystem.AddFile("/test/file.ftxt.txt", "AB\nCD");

            // Act
            _service.PackFTXT("/test/file.ftxt.txt", "/test/file.ftxt.meta", false);

            // Assert
            byte[] result = _fileSystem.ReadAllBytes("/test/file.ftxt");

            // Text block size at offset 12 (4 bytes, little-endian)
            // "AB" + null + "CD" + null = 6 bytes
            int textBlockSize = BitConverter.ToInt32(result, 12);
            Assert.Equal(6, textBlockSize);
        }

        [Fact]
        public void PackFTXT_WithMissingMetaFile_ThrowsFileNotFoundException()
        {
            // Arrange
            _fileSystem.AddFile("/test/file.ftxt.txt", "Hello");

            // Act & Assert
            Assert.Throws<FileNotFoundException>(() =>
                _service.PackFTXT("/test/file.ftxt.txt", "/test/file.ftxt.meta", false));
        }

        [Fact]
        public void PackFTXT_WithTooSmallMetaFile_ThrowsPackingException()
        {
            // Arrange
            byte[] tooSmallMeta = new byte[10]; // Less than 16 bytes
            _fileSystem.AddFile("/test/file.ftxt.meta", tooSmallMeta);
            _fileSystem.AddFile("/test/file.ftxt.txt", "Hello");

            // Act & Assert
            var ex = Assert.Throws<PackingException>(() =>
                _service.PackFTXT("/test/file.ftxt.txt", "/test/file.ftxt.meta", false));
            Assert.Contains("too small", ex.Message);
        }

        [Fact]
        public void PackFTXT_WithCleanUp_DeletesInputFiles()
        {
            // Arrange
            byte[] meta = CreateFtxtMeta(1);
            _fileSystem.AddFile("/test/file.ftxt.meta", meta);
            _fileSystem.AddFile("/test/file.ftxt.txt", "Hello");

            // Act
            _service.PackFTXT("/test/file.ftxt.txt", "/test/file.ftxt.meta", cleanUp: true);

            // Assert
            Assert.False(_fileSystem.FileExists("/test/file.ftxt.txt"));
            Assert.False(_fileSystem.FileExists("/test/file.ftxt.meta"));
            Assert.True(_fileSystem.FileExists("/test/file.ftxt"));
        }

        [Fact]
        public void PackFTXT_LogsPackingInfo()
        {
            // Arrange
            byte[] meta = CreateFtxtMeta(2);
            _fileSystem.AddFile("/test/file.ftxt.meta", meta);
            _fileSystem.AddFile("/test/file.ftxt.txt", "Hello\nWorld");

            // Act
            _service.PackFTXT("/test/file.ftxt.txt", "/test/file.ftxt.meta", false, verbose: true);

            // Assert
            Assert.True(_logger.ContainsMessage("FTXT packed"));
            Assert.True(_logger.ContainsMessage("2 strings"));
        }

        /// <summary>
        /// Creates a valid FTXT meta buffer.
        /// </summary>
        private static byte[] CreateFtxtMeta(int stringCount)
        {
            byte[] meta = new byte[16];
            // First 10 bytes can be any padding/unknown values
            meta[0] = 0x01;
            meta[1] = 0x02;
            // Offset 10-11: string count (will be overwritten during packing)
            meta[10] = (byte)(stringCount & 0xFF);
            meta[11] = (byte)((stringCount >> 8) & 0xFF);
            // Offset 12-15: text block size (will be overwritten during packing)
            return meta;
        }

        #endregion
    }
}
