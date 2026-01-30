using LibReFrontier;
using LibReFrontier.Exceptions;

using ReFrontier.Jpk;
using ReFrontier.Services;
using ReFrontier.Tests.Mocks;

namespace ReFrontier.Tests.Services
{
    /// <summary>
    /// Tests for error handling and exception throwing in services.
    /// </summary>
    public class ErrorHandlingTests
    {
        private readonly InMemoryFileSystem _fileSystem;
        private readonly TestLogger _logger;
        private readonly FileProcessingConfig _config;
        private readonly ICodecFactory _codecFactory;
        private readonly UnpackingService _unpackingService;

        public ErrorHandlingTests()
        {
            _fileSystem = new InMemoryFileSystem();
            _logger = new TestLogger();
            _config = FileProcessingConfig.Default();
            _codecFactory = new DefaultCodecFactory();
            _unpackingService = new UnpackingService(_fileSystem, _logger, _codecFactory, _config);
        }

        #region UnpackSimpleArchive Error Tests

        [Fact]
        public void UnpackSimpleArchive_FileTooSmall_ThrowsPackingException()
        {
            // Arrange
            byte[] smallFile = new byte[10];
            _fileSystem.AddFile("/test/small.bin", smallFile);
            using var ms = new MemoryStream(smallFile);
            using var br = new BinaryReader(ms);

            // Act & Assert
            var ex = Assert.Throws<PackingException>(() =>
                _unpackingService.UnpackSimpleArchive("/test/small.bin", br, 4, false, false, false)
            );
            Assert.Contains("too small", ex.Message);
            Assert.Equal("/test/small.bin", ex.FilePath);
        }

        [Fact]
        public void UnpackSimpleArchive_InvalidContainer_ThrowsPackingException()
        {
            // Arrange - Create a file that passes size check but has invalid container data
            // Need at least 16 bytes to pass the initial size check
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            // Write high count that would result in completeSize > fileLength
            bw.Write((int)10000); // Very high count (triggers tempCount > 9999 check)
            bw.Write((int)0);     // Entry 0 offset
            bw.Write((int)999999); // Entry 0 size (way too big)
            // Add padding to reach 16+ bytes
            bw.Write((int)0);
            bw.Write((int)0);

            byte[] invalidData = ms.ToArray();
            _fileSystem.AddFile("/test/invalid.bin", invalidData);

            using var readMs = new MemoryStream(invalidData);
            using var br = new BinaryReader(readMs);

            // Act & Assert
            var ex = Assert.Throws<PackingException>(() =>
                _unpackingService.UnpackSimpleArchive("/test/invalid.bin", br, 4, false, false, false)
            );
            Assert.Contains("Not a valid simple container", ex.Message);
        }

        #endregion

        #region UnpackJPK Error Tests

        [Fact]
        public void UnpackJPK_InvalidHeader_ThrowsPackingException()
        {
            // Arrange - File without JKR header
            byte[] invalidData = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07 };
            _fileSystem.AddFile("/test/invalid.bin", invalidData);

            // Act & Assert
            var ex = Assert.Throws<PackingException>(() =>
                _unpackingService.UnpackJPK("/test/invalid.bin")
            );
            Assert.Contains("Invalid JKR header", ex.Message);
            Assert.Contains("0x03020100", ex.Message); // The actual header value in hex
            Assert.Equal("/test/invalid.bin", ex.FilePath);
        }

        [Fact]
        public void UnpackJPK_InvalidCompressionType_ThrowsPackingException()
        {
            // Arrange - JKR file with invalid compression type
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            bw.Write((uint)0x1A524B4A); // JKR magic
            bw.Write((ushort)0);        // padding
            bw.Write((ushort)99);       // Invalid compression type
            bw.Write((int)16);          // start offset
            bw.Write((int)4);           // output size

            byte[] invalidData = ms.ToArray();
            _fileSystem.AddFile("/test/invalid_type.jkr", invalidData);

            // Act & Assert
            var ex = Assert.Throws<PackingException>(() =>
                _unpackingService.UnpackJPK("/test/invalid_type.jkr")
            );
            Assert.Contains("Invalid compression type", ex.Message);
        }

        #endregion

        #region Crypto DecodeEcd Error Tests

        [Fact]
        public void DecodeEcd_NullBuffer_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => Crypto.DecodeEcd(null));
        }

        [Fact]
        public void DecodeEcd_BufferTooSmall_ThrowsDecryptionException()
        {
            // Arrange
            byte[] smallBuffer = new byte[8];

            // Act & Assert
            var ex = Assert.Throws<DecryptionException>(() => Crypto.DecodeEcd(smallBuffer));
            Assert.Contains("too small", ex.Message);
            Assert.Contains("16 bytes", ex.Message);
        }

        [Fact]
        public void DecodeEcd_ExactlyMinimumSize_DoesNotThrow()
        {
            // Arrange - 16 bytes minimum with valid-ish header
            byte[] buffer = new byte[16];
            buffer[0] = 0x65; // 'e'
            buffer[1] = 0x63; // 'c'
            buffer[2] = 0x64; // 'd'
            buffer[3] = 0x1A; // magic terminator

            // Act & Assert - Should not throw (even if decryption produces garbage)
            var exception = Record.Exception(() => Crypto.DecodeEcd(buffer));
            Assert.Null(exception);
        }

        #endregion

        #region Crypto DecodeExf Error Tests

        [Fact]
        public void DecodeExf_NullBuffer_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => Crypto.DecodeExf(null));
        }

        [Fact]
        public void DecodeExf_BufferTooSmall_ThrowsDecryptionException()
        {
            // Arrange
            byte[] smallBuffer = new byte[8];

            // Act & Assert
            var ex = Assert.Throws<DecryptionException>(() => Crypto.DecodeExf(smallBuffer));
            Assert.Contains("too small", ex.Message);
            Assert.Contains("16 bytes", ex.Message);
        }

        [Fact]
        public void DecodeExf_ExactlyMinimumSize_DoesNotThrow()
        {
            // Arrange - 16 bytes minimum
            byte[] buffer = new byte[16];

            // Act & Assert - Should not throw (magic won't match so it's a no-op)
            var exception = Record.Exception(() => Crypto.DecodeExf(buffer));
            Assert.Null(exception);
        }

        [Fact]
        public void DecodeExf_ValidMagicMinimumSize_DoesNotThrow()
        {
            // Arrange - 16 bytes with valid EXF magic
            byte[] buffer = new byte[16];
            buffer[0] = 0x65; // 'e'
            buffer[1] = 0x78; // 'x'
            buffer[2] = 0x66; // 'f'
            buffer[3] = 0x1A; // magic terminator (0x1a667865 in little-endian)

            // Act & Assert - Should not throw
            var exception = Record.Exception(() => Crypto.DecodeExf(buffer));
            Assert.Null(exception);
        }

        #endregion

        #region Crypto EncodeEcd Error Tests

        [Fact]
        public void EncodeEcd_NullBuffer_ThrowsArgumentNullException()
        {
            // Arrange
            byte[] meta = new byte[16];

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => Crypto.EncodeEcd(null, meta));
        }

        [Fact]
        public void EncodeEcd_NullMeta_ThrowsArgumentNullException()
        {
            // Arrange
            byte[] buffer = new byte[10];

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => Crypto.EncodeEcd(buffer, null));
        }

        [Fact]
        public void EncodeEcd_MetaTooSmall_ThrowsDecryptionException()
        {
            // Arrange
            byte[] buffer = new byte[10];
            byte[] smallMeta = new byte[4]; // Needs at least 6 bytes

            // Act & Assert
            var ex = Assert.Throws<DecryptionException>(() => Crypto.EncodeEcd(buffer, smallMeta));
            Assert.Contains("too small", ex.Message);
            Assert.Contains("6 bytes", ex.Message);
        }

        [Fact]
        public void EncodeEcd_ValidInputs_ReturnsEncryptedData()
        {
            // Arrange
            byte[] buffer = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            byte[] meta = new byte[16];
            meta[0] = 0x65; // 'e'
            meta[1] = 0x63; // 'c'
            meta[2] = 0x64; // 'd'
            meta[3] = 0x1A; // magic

            // Act
            byte[] result = Crypto.EncodeEcd(buffer, meta);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(16 + buffer.Length, result.Length); // Header + payload
        }

        #endregion

        #region Exception Properties Tests

        [Fact]
        public void PackingException_ContainsFilePath()
        {
            // Arrange
            const string testPath = "/test/file.bin";
            const string testMessage = "Test error message";

            // Act
            var ex = new PackingException(testMessage, testPath);

            // Assert
            Assert.Equal(testMessage, ex.Message);
            Assert.Equal(testPath, ex.FilePath);
        }

        [Fact]
        public void DecryptionException_ContainsFilePath()
        {
            // Arrange
            const string testPath = "/test/encrypted.bin";
            const string testMessage = "Decryption failed";

            // Act
            var ex = new DecryptionException(testMessage, testPath);

            // Assert
            Assert.Equal(testMessage, ex.Message);
            Assert.Equal(testPath, ex.FilePath);
        }

        #endregion

        #region WithFilePath Tests

        [Fact]
        public void WithFilePath_SetsFilePathWhenNull()
        {
            // Arrange
            var ex = new DecryptionException("Test error");
            Assert.Null(ex.FilePath);

            // Act
            var result = ex.WithFilePath("/test/file.bin");

            // Assert
            Assert.Same(ex, result); // Returns same instance
            Assert.Equal("/test/file.bin", ex.FilePath);
        }

        [Fact]
        public void WithFilePath_DoesNotOverwriteExistingPath()
        {
            // Arrange
            var ex = new DecryptionException("Test error", "/original/path.bin");
            Assert.Equal("/original/path.bin", ex.FilePath);

            // Act
            var result = ex.WithFilePath("/new/path.bin");

            // Assert
            Assert.Same(ex, result);
            Assert.Equal("/original/path.bin", ex.FilePath); // Unchanged
        }

        [Fact]
        public void WithFilePath_IgnoresNullPath()
        {
            // Arrange
            var ex = new DecryptionException("Test error");
            Assert.Null(ex.FilePath);

            // Act
            var result = ex.WithFilePath(null);

            // Assert
            Assert.Same(ex, result);
            Assert.Null(ex.FilePath); // Still null
        }

        [Fact]
        public void CompressionException_WithFilePath_SetsPath()
        {
            // Arrange
            var ex = new CompressionException("Decompression failed");
            Assert.Null(ex.FilePath);

            // Act
            var result = ex.WithFilePath("/test/compressed.jkr");

            // Assert
            Assert.Same(ex, result);
            Assert.Equal("/test/compressed.jkr", ex.FilePath);
        }

        #endregion

        #region Service Layer Exception Enrichment Tests

        [Fact]
        public void FileProcessingService_DecryptEcd_EnrichesExceptionWithFilePath()
        {
            // Arrange
            byte[] tooSmall = new byte[8]; // Too small to be valid ECD
            _fileSystem.AddFile("/test/small.ecd", tooSmall);
            var service = new FileProcessingService(_fileSystem, _logger, _config);

            // Act & Assert
            var ex = Assert.Throws<DecryptionException>(() =>
                service.DecryptEcdFile("/test/small.ecd", false, false, false)
            );
            Assert.Equal("/test/small.ecd", ex.FilePath);
            Assert.Contains("too small", ex.Message);
        }

        [Fact]
        public void FileProcessingService_DecryptExf_EnrichesExceptionWithFilePath()
        {
            // Arrange
            byte[] tooSmall = new byte[8]; // Too small to be valid EXF
            _fileSystem.AddFile("/test/small.exf", tooSmall);
            var service = new FileProcessingService(_fileSystem, _logger, _config);

            // Act & Assert
            var ex = Assert.Throws<DecryptionException>(() =>
                service.DecryptExfFile("/test/small.exf", false)
            );
            Assert.Equal("/test/small.exf", ex.FilePath);
            Assert.Contains("too small", ex.Message);
        }

        [Fact]
        public void UnpackingService_UnpackJPK_EnrichesDecoderExceptionWithFilePath()
        {
            // Arrange - Create a JKR file with valid header and some data but not enough
            // to satisfy the output size, causing decoder to fail with unexpected end of stream
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            bw.Write((uint)0x1A524B4A); // JKR magic
            bw.Write((ushort)0x108);    // padding
            bw.Write((ushort)3);        // LZ compression type (LZ needs to read flag bits)
            bw.Write((int)16);          // start offset
            bw.Write((int)1000);        // output size (large, will fail)
            // Add a few bytes of "compressed" data - not enough for 1000 byte output
            bw.Write((byte)0xFF);       // flag byte
            bw.Write((byte)0xFF);       // Will try to read more but hit EOF

            byte[] truncatedJkr = ms.ToArray();
            _fileSystem.AddFile("/test/truncated.jkr", truncatedJkr);

            // Act & Assert
            var ex = Assert.Throws<CompressionException>(() =>
                _unpackingService.UnpackJPK("/test/truncated.jkr")
            );
            Assert.Equal("/test/truncated.jkr", ex.FilePath);
            Assert.Contains("unexpected end of stream", ex.Message);
        }

        #endregion
    }
}
