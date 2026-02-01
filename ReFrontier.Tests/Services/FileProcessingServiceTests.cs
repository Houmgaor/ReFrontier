using ReFrontier.Services;
using ReFrontier.Tests.Mocks;

namespace ReFrontier.Tests.Services
{
    /// <summary>
    /// Tests for FileProcessingService.
    /// </summary>
    public class FileProcessingServiceTests
    {
        private readonly InMemoryFileSystem _fileSystem;
        private readonly TestLogger _logger;
        private readonly FileProcessingConfig _config;
        private readonly FileProcessingService _service;

        public FileProcessingServiceTests()
        {
            _fileSystem = new InMemoryFileSystem();
            _logger = new TestLogger();
            _config = FileProcessingConfig.Default();
            _service = new FileProcessingService(_fileSystem, _logger, _config);
        }

        [Fact]
        public void DecryptEcdFile_CreatesDecryptedFile()
        {
            // Arrange
            // Create a minimal ECD file (0x10 byte header + data)
            byte[] ecdHeader = new byte[0x10];
            ecdHeader[0] = 0x65; // 'e'
            ecdHeader[1] = 0x63; // 'c'
            ecdHeader[2] = 0x64; // 'd'
            ecdHeader[3] = 0x1A;
            byte[] data = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            byte[] fullFile = new byte[ecdHeader.Length + data.Length];
            Array.Copy(ecdHeader, 0, fullFile, 0, ecdHeader.Length);
            Array.Copy(data, 0, fullFile, ecdHeader.Length, data.Length);

            _fileSystem.AddFile("/test/file.bin", fullFile);

            // Act
            string result = _service.DecryptEcdFile("/test/file.bin", createLog: false, cleanUp: false, verbose: true);

            // Assert
            TestHelpers.AssertPathsEqual("/test/file.bin.decd", result);
            Assert.True(_fileSystem.FileExists("/test/file.bin.decd"));
            Assert.True(_fileSystem.FileExists("/test/file.bin")); // Original not deleted
            Assert.True(_logger.ContainsMessage("decrypted"));
        }

        [Fact]
        public void DecryptEcdFile_WithLog_CreatesMetaFile()
        {
            // Arrange
            byte[] fullFile = new byte[0x14];
            fullFile[0] = 0x65;
            fullFile[1] = 0x63;
            fullFile[2] = 0x64;
            fullFile[3] = 0x1A;
            _fileSystem.AddFile("/test/file.bin", fullFile);

            // Act
            _service.DecryptEcdFile("/test/file.bin", createLog: true, cleanUp: false, verbose: true);

            // Assert
            Assert.True(_fileSystem.FileExists("/test/file.bin.meta"));
            Assert.True(_logger.ContainsMessage("log file"));
        }

        [Fact]
        public void DecryptEcdFile_WithCleanUp_DeletesOriginal()
        {
            // Arrange
            byte[] fullFile = new byte[0x14];
            _fileSystem.AddFile("/test/file.bin", fullFile);

            // Act
            _service.DecryptEcdFile("/test/file.bin", createLog: false, cleanUp: true);

            // Assert
            Assert.False(_fileSystem.FileExists("/test/file.bin"));
            Assert.True(_fileSystem.FileExists("/test/file.bin.decd"));
        }

        [Fact]
        public void DecryptExfFile_CreatesDecryptedFile()
        {
            // Arrange
            byte[] fullFile = new byte[0x14];
            fullFile[0] = 0x65;
            fullFile[1] = 0x78;
            fullFile[2] = 0x66;
            fullFile[3] = 0x1A;
            _fileSystem.AddFile("/test/file.bin", fullFile);

            // Act
            string result = _service.DecryptExfFile("/test/file.bin", createLog: false, cleanUp: false);

            // Assert
            TestHelpers.AssertPathsEqual("/test/file.bin.dexf", result);
            Assert.True(_fileSystem.FileExists("/test/file.bin.dexf"));
        }

        [Fact]
        public void EncryptEcdFile_WithMissingMetaFile_UsesDefaultKeyAndWarns()
        {
            // Arrange
            byte[] data = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            _fileSystem.AddFile("/test/file.bin.decd", data, DateTime.Now);

            // Act
            string result = _service.EncryptEcdFile("/test/file.bin.decd", "/test/file.bin.meta", cleanUp: false, verbose: true);

            // Assert - should succeed with warning about using default key
            TestHelpers.AssertPathsEqual("/test/file.bin", result);
            Assert.True(_fileSystem.FileExists("/test/file.bin"));
            Assert.True(_logger.ContainsMessage("WARNING"));
            Assert.True(_logger.ContainsMessage("default ECD key index"));
        }

        [Fact]
        public void EncryptEcdFile_WithValidMetaFile_CreatesEncryptedFile()
        {
            // Arrange
            byte[] data = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            byte[] metaData = new byte[0x10];
            metaData[0] = 0x65;
            metaData[1] = 0x63;
            metaData[2] = 0x64;
            metaData[3] = 0x1A;

            _fileSystem.AddFile("/test/file.bin.decd", data, DateTime.Now);
            _fileSystem.AddFile("/test/file.bin.meta", metaData);

            // Act
            string result = _service.EncryptEcdFile("/test/file.bin.decd", "/test/file.bin.meta", cleanUp: false, verbose: true);

            // Assert
            TestHelpers.AssertPathsEqual("/test/file.bin", result);
            Assert.True(_fileSystem.FileExists("/test/file.bin"));
            Assert.True(_logger.ContainsMessage("encrypted"));
        }
    }
}
