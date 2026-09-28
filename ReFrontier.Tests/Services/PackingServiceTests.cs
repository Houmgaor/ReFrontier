using System;
using System.IO;
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
            _fileSystem.AddFile("/test/file.ftxt.meta", TestDataFactory.CreateFtxt("A", "B"));
            _fileSystem.AddFile("/test/file.ftxt.txt", "Hello\nWorld");

            // Act
            string result = _service.PackFTXT("/test/file.ftxt.txt", "/test/file.ftxt.meta", false);

            // Assert
            Assert.Equal("/test/file.ftxt", result.Replace('\\', '/'));
            Assert.True(_fileSystem.FileExists("/test/file.ftxt"));
        }

        [Fact]
        public void PackFTXT_UnchangedText_RebuildsOriginal()
        {
            // Arrange
            byte[] original = TestDataFactory.CreateFtxt("駆け抜けろ！", "AB");
            _fileSystem.AddFile("/test/file.ftxt.meta", original);
            _fileSystem.AddFile("/test/file.ftxt.txt",
                TextFileConfiguration.Cp932Encoding.GetBytes("駆け抜けろ！\r\nAB\r\n"));

            // Act
            _service.PackFTXT("/test/file.ftxt.txt", "/test/file.ftxt.meta", false);

            // Assert
            Assert.Equal(original, _fileSystem.ReadAllBytes("/test/file.ftxt"));
        }

        [Fact]
        public void PackFTXT_LongerText_KeepsTailAndUpdatesSizes()
        {
            // Arrange
            byte[] original = TestDataFactory.CreateFtxt("A", "B");
            _fileSystem.AddFile("/test/file.ftxt.meta", original);
            _fileSystem.AddFile("/test/file.ftxt.txt", "Run through!\nB\nNew");

            // Act
            _service.PackFTXT("/test/file.ftxt.txt", "/test/file.ftxt.meta", false);

            // Assert
            byte[] result = _fileSystem.ReadAllBytes("/test/file.ftxt");
            byte[] strings = TestDataFactory.CreateBinaryWithStrings("Run through!", "B", "New");
            Assert.Equal(original[..4], result[..4]);
            Assert.Equal(original[0x08..0x0E], result[0x08..0x0E]);
            Assert.Equal(3, BitConverter.ToUInt16(result, FileFormatConstants.FtxtStringCountOffset));
            Assert.Equal(strings.Length + TestDataFactory.FtxtBlockTail.Length,
                BitConverter.ToInt32(result, FileFormatConstants.FtxtTextBlockSizeOffset));
            Assert.Equal(result.Length, BitConverter.ToInt32(result, FileFormatConstants.FtxtFileSizeOffset));
            Assert.Equal(
                [.. strings, .. TestDataFactory.FtxtBlockTail, .. TestDataFactory.FtxtDataAfterBlock],
                result[FileFormatConstants.FtxtHeaderLength..]);
        }

        [Fact]
        public void PackFTXT_OtherFileSizeValue_IsKept()
        {
            // Arrange
            byte[] original = TestDataFactory.CreateFtxt("A");
            BitConverter.GetBytes(0x1234).CopyTo(original, FileFormatConstants.FtxtFileSizeOffset);
            _fileSystem.AddFile("/test/file.ftxt.meta", original);
            _fileSystem.AddFile("/test/file.ftxt.txt", "Longer");

            // Act
            _service.PackFTXT("/test/file.ftxt.txt", "/test/file.ftxt.meta", false);

            // Assert
            byte[] result = _fileSystem.ReadAllBytes("/test/file.ftxt");
            Assert.Equal(0x1234, BitConverter.ToInt32(result, FileFormatConstants.FtxtFileSizeOffset));
        }

        [Fact]
        public void PackFTXT_ReplacesNewlineMarkers()
        {
            // Arrange
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            _fileSystem.AddFile("/test/file.ftxt.meta", TestDataFactory.CreateFtxt("A"));
            _fileSystem.AddFile("/test/file.ftxt.txt", "Hello\\nWorld");

            // Act
            _service.PackFTXT("/test/file.ftxt.txt", "/test/file.ftxt.meta", false);

            // Assert
            byte[] result = _fileSystem.ReadAllBytes("/test/file.ftxt");
            byte[] expected = [.. Encoding.ASCII.GetBytes("Hello\nWorld"), 0];
            Assert.Equal(expected, result[FileFormatConstants.FtxtHeaderLength..(FileFormatConstants.FtxtHeaderLength + expected.Length)]);
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
        public void PackFTXT_WithOldHeaderOnlyMeta_ThrowsPackingException()
        {
            // Arrange: older versions saved 16 bytes of header only
            _fileSystem.AddFile("/test/file.ftxt.meta", TestDataFactory.CreateFtxt("A")[..16]);
            _fileSystem.AddFile("/test/file.ftxt.txt", "Hello");

            // Act & Assert
            var ex = Assert.Throws<PackingException>(() =>
                _service.PackFTXT("/test/file.ftxt.txt", "/test/file.ftxt.meta", false));
            Assert.Contains("too small", ex.Message);
        }

        [Fact]
        public void PackFTXT_WithMetaThatIsNotTheFile_ThrowsPackingException()
        {
            // Arrange: a header whose text block is too small for its strings
            byte[] meta = TestDataFactory.CreateFtxt("Hello");
            BitConverter.GetBytes(2).CopyTo(meta, FileFormatConstants.FtxtTextBlockSizeOffset);
            _fileSystem.AddFile("/test/file.ftxt.meta", meta);
            _fileSystem.AddFile("/test/file.ftxt.txt", "Hello");

            // Act & Assert
            var ex = Assert.Throws<PackingException>(() =>
                _service.PackFTXT("/test/file.ftxt.txt", "/test/file.ftxt.meta", false));
            Assert.Contains("not the original FTXT file", ex.Message);
        }

        [Fact]
        public void PackFTXT_WithCleanUp_DeletesInputFiles()
        {
            // Arrange
            _fileSystem.AddFile("/test/file.ftxt.meta", TestDataFactory.CreateFtxt("A"));
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
            _fileSystem.AddFile("/test/file.ftxt.meta", TestDataFactory.CreateFtxt("A", "B"));
            _fileSystem.AddFile("/test/file.ftxt.txt", "Hello\nWorld");

            // Act
            _service.PackFTXT("/test/file.ftxt.txt", "/test/file.ftxt.meta", false, verbose: true);

            // Assert
            Assert.True(_logger.ContainsMessage("FTXT packed"));
            Assert.True(_logger.ContainsMessage("2 strings"));
        }

        #endregion
    }
}
