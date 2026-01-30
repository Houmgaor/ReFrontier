using LibReFrontier;

using ReFrontier.Jpk;
using ReFrontier.Services;
using ReFrontier.Tests.Mocks;

namespace ReFrontier.Tests.Integration
{
    /// <summary>
    /// Roundtrip integration tests verifying that data survives
    /// compress→decompress and encrypt→decrypt cycles.
    /// </summary>
    public class RoundtripIntegrationTests
    {
        private readonly InMemoryFileSystem _fileSystem;
        private readonly TestLogger _logger;
        private readonly FileProcessingConfig _config;
        private readonly ICodecFactory _codecFactory;
        private readonly PackingService _packingService;
        private readonly UnpackingService _unpackingService;
        private readonly FileProcessingService _fileProcessingService;

        public RoundtripIntegrationTests()
        {
            _fileSystem = new InMemoryFileSystem();
            _logger = new TestLogger();
            _config = FileProcessingConfig.Default();
            _codecFactory = new DefaultCodecFactory();
            _packingService = new PackingService(_fileSystem, _logger, _codecFactory, _config);
            _unpackingService = new UnpackingService(_fileSystem, _logger, _codecFactory, _config);
            _fileProcessingService = new FileProcessingService(_fileSystem, _logger, _config);
        }

        #region Compression Roundtrip Tests

        [Theory]
        [InlineData(CompressionType.RW)]
        [InlineData(CompressionType.LZ)]
        [InlineData(CompressionType.HFIRW)]
        [InlineData(CompressionType.HFI)]
        public void CompressionRoundtrip_AllTypes_DataPreserved(CompressionType compressionType)
        {
            // Arrange: Create test data with some pattern
            byte[] originalData = CreateTestData(256);
            _fileSystem.AddFile("/test/input.bin", originalData);

            var compression = new Compression(compressionType, 16);

            // Act: Compress
            _packingService.JPKEncode(compression, "/test/input.bin", "/test/compressed.jkr");

            // Verify compressed file exists
            Assert.True(_fileSystem.FileExists("/test/compressed.jkr"));
            byte[] compressedData = _fileSystem.ReadAllBytes("/test/compressed.jkr");

            // Verify JKR magic
            Assert.Equal(0x4A, compressedData[0]); // 'J'
            Assert.Equal(0x4B, compressedData[1]); // 'K'
            Assert.Equal(0x52, compressedData[2]); // 'R'
            Assert.Equal(0x1A, compressedData[3]);

            // Act: Decompress
            _fileSystem.AddFile("/test/to_decompress.jkr", compressedData);
            string decompressedPath = _unpackingService.UnpackJPK("/test/to_decompress.jkr");

            // Assert: Data matches original
            byte[] decompressedData = _fileSystem.ReadAllBytes(decompressedPath);
            Assert.Equal(originalData.Length, decompressedData.Length);
            Assert.Equal(originalData, decompressedData);
        }

        [Fact]
        public void CompressionRoundtrip_LZ_LargeRepetitiveData()
        {
            // Arrange: Create large repetitive data (good for LZ compression)
            byte[] originalData = new byte[4096];
            for (int i = 0; i < originalData.Length; i++)
                originalData[i] = (byte)(i % 16); // Highly repetitive pattern
            _fileSystem.AddFile("/test/input.bin", originalData);

            var compression = new Compression(CompressionType.LZ, 50);

            // Act: Compress
            _packingService.JPKEncode(compression, "/test/input.bin", "/test/compressed.jkr");
            byte[] compressedData = _fileSystem.ReadAllBytes("/test/compressed.jkr");

            // Verify compression actually reduced size (LZ should compress well)
            Assert.True(compressedData.Length < originalData.Length,
                $"Expected compression to reduce size. Original: {originalData.Length}, Compressed: {compressedData.Length}");

            // Act: Decompress
            _fileSystem.AddFile("/test/to_decompress.jkr", compressedData);
            string decompressedPath = _unpackingService.UnpackJPK("/test/to_decompress.jkr");

            // Assert: Data matches
            byte[] decompressedData = _fileSystem.ReadAllBytes(decompressedPath);
            Assert.Equal(originalData, decompressedData);
        }

        [Fact]
        public void CompressionRoundtrip_HFI_DataWithFrequentBytes()
        {
            // Arrange: Create data with many repeated byte values (good for Huffman)
            byte[] originalData = new byte[1024];
            Random rnd = new(42); // Fixed seed for reproducibility
            // Fill with mostly low values (will compress well with Huffman)
            for (int i = 0; i < originalData.Length; i++)
                originalData[i] = (byte)(rnd.Next(16));
            _fileSystem.AddFile("/test/input.bin", originalData);

            var compression = new Compression(CompressionType.HFI, 20);

            // Act: Compress then decompress
            _packingService.JPKEncode(compression, "/test/input.bin", "/test/compressed.jkr");
            byte[] compressedData = _fileSystem.ReadAllBytes("/test/compressed.jkr");

            _fileSystem.AddFile("/test/to_decompress.jkr", compressedData);
            string decompressedPath = _unpackingService.UnpackJPK("/test/to_decompress.jkr");

            // Assert
            byte[] decompressedData = _fileSystem.ReadAllBytes(decompressedPath);
            Assert.Equal(originalData, decompressedData);
        }

        [Fact]
        public void CompressionRoundtrip_RW_NoCompression_ExactCopy()
        {
            // Arrange: RW (raw) should store data as-is
            byte[] originalData = CreateTestData(100);
            _fileSystem.AddFile("/test/input.bin", originalData);

            var compression = new Compression(CompressionType.RW, 10);

            // Act
            _packingService.JPKEncode(compression, "/test/input.bin", "/test/compressed.jkr");
            byte[] compressedData = _fileSystem.ReadAllBytes("/test/compressed.jkr");

            _fileSystem.AddFile("/test/to_decompress.jkr", compressedData);
            string decompressedPath = _unpackingService.UnpackJPK("/test/to_decompress.jkr");

            // Assert: Exact match
            byte[] decompressedData = _fileSystem.ReadAllBytes(decompressedPath);
            Assert.Equal(originalData, decompressedData);
        }

        [Theory]
        [InlineData(1)]    // Minimum size
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        [InlineData(8192)]
        public void CompressionRoundtrip_VariousSizes_AllPreserved(int size)
        {
            // Arrange
            byte[] originalData = CreateTestData(size);
            _fileSystem.AddFile("/test/input.bin", originalData);

            var compression = new Compression(CompressionType.LZ, 16);

            // Act
            _packingService.JPKEncode(compression, "/test/input.bin", "/test/compressed.jkr");
            byte[] compressedData = _fileSystem.ReadAllBytes("/test/compressed.jkr");

            _fileSystem.AddFile("/test/to_decompress.jkr", compressedData);
            string decompressedPath = _unpackingService.UnpackJPK("/test/to_decompress.jkr");

            // Assert
            byte[] decompressedData = _fileSystem.ReadAllBytes(decompressedPath);
            Assert.Equal(originalData, decompressedData);
        }

        #endregion

        #region Encryption Roundtrip Tests

        [Fact]
        public void EncryptionRoundtrip_ECD_DataPreserved()
        {
            // Arrange: Create original data and meta header
            byte[] originalData = CreateTestData(256);
            byte[] metaHeader = CreateEcdMetaHeader(keyIndex: 0);

            _fileSystem.AddFile("/test/data.bin.decd", originalData);
            _fileSystem.AddFile("/test/data.bin.meta", metaHeader);

            // Act: Encrypt
            string encryptedPath = _fileProcessingService.EncryptEcdFile(
                "/test/data.bin.decd",
                "/test/data.bin.meta",
                cleanUp: false
            );

            Assert.True(_fileSystem.FileExists(encryptedPath));
            byte[] encryptedData = _fileSystem.ReadAllBytes(encryptedPath);

            // Verify ECD magic
            Assert.Equal(0x65, encryptedData[0]); // 'e'
            Assert.Equal(0x63, encryptedData[1]); // 'c'
            Assert.Equal(0x64, encryptedData[2]); // 'd'
            Assert.Equal(0x1A, encryptedData[3]);

            // Act: Decrypt
            _fileSystem.AddFile("/test/encrypted.bin", encryptedData);
            string decryptedPath = _fileProcessingService.DecryptEcdFile(
                "/test/encrypted.bin",
                createLog: true,
                cleanUp: false,
                rewriteOldFile: false
            );

            // Assert: Data matches original
            byte[] decryptedData = _fileSystem.ReadAllBytes(decryptedPath);
            Assert.Equal(originalData.Length, decryptedData.Length);
            Assert.Equal(originalData, decryptedData);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        public void EncryptionRoundtrip_ECD_AllKeyIndices(int keyIndex)
        {
            // Arrange
            byte[] originalData = CreateTestData(128);
            byte[] metaHeader = CreateEcdMetaHeader(keyIndex);

            _fileSystem.AddFile("/test/data.bin.decd", originalData);
            _fileSystem.AddFile("/test/data.bin.meta", metaHeader);

            // Act: Encrypt then decrypt
            string encryptedPath = _fileProcessingService.EncryptEcdFile(
                "/test/data.bin.decd",
                "/test/data.bin.meta",
                cleanUp: false
            );
            byte[] encryptedData = _fileSystem.ReadAllBytes(encryptedPath);

            _fileSystem.AddFile("/test/to_decrypt.bin", encryptedData);
            string decryptedPath = _fileProcessingService.DecryptEcdFile(
                "/test/to_decrypt.bin",
                createLog: false,
                cleanUp: false,
                rewriteOldFile: false
            );

            // Assert
            byte[] decryptedData = _fileSystem.ReadAllBytes(decryptedPath);
            Assert.Equal(originalData, decryptedData);
        }

        [Fact]
        public void EncryptionRoundtrip_ECD_LargeFile()
        {
            // Arrange: Test with larger file
            byte[] originalData = CreateTestData(16384);
            byte[] metaHeader = CreateEcdMetaHeader(keyIndex: 2);

            _fileSystem.AddFile("/test/large.bin.decd", originalData);
            _fileSystem.AddFile("/test/large.bin.meta", metaHeader);

            // Act
            string encryptedPath = _fileProcessingService.EncryptEcdFile(
                "/test/large.bin.decd",
                "/test/large.bin.meta",
                cleanUp: false
            );
            byte[] encryptedData = _fileSystem.ReadAllBytes(encryptedPath);

            _fileSystem.AddFile("/test/to_decrypt.bin", encryptedData);
            string decryptedPath = _fileProcessingService.DecryptEcdFile(
                "/test/to_decrypt.bin",
                createLog: false,
                cleanUp: false,
                rewriteOldFile: false
            );

            // Assert
            byte[] decryptedData = _fileSystem.ReadAllBytes(decryptedPath);
            Assert.Equal(originalData, decryptedData);
        }

        #endregion

        #region Combined Workflow Tests

        [Theory]
        [InlineData(CompressionType.LZ)]
        [InlineData(CompressionType.HFI)]
        public void CombinedWorkflow_EncryptThenCompress_RoundtripPreserved(CompressionType compressionType)
        {
            // Arrange: Original data
            byte[] originalData = CreateTestData(512);
            byte[] metaHeader = CreateEcdMetaHeader(keyIndex: 1);

            _fileSystem.AddFile("/test/original.bin.decd", originalData);
            _fileSystem.AddFile("/test/original.bin.meta", metaHeader);

            // Step 1: Encrypt
            string encryptedPath = _fileProcessingService.EncryptEcdFile(
                "/test/original.bin.decd",
                "/test/original.bin.meta",
                cleanUp: false
            );
            byte[] encryptedData = _fileSystem.ReadAllBytes(encryptedPath);

            // Step 2: Compress the encrypted file
            _fileSystem.AddFile("/test/encrypted_for_compress.bin", encryptedData);
            var compression = new Compression(compressionType, 16);
            _packingService.JPKEncode(compression, "/test/encrypted_for_compress.bin", "/test/compressed.jkr");
            byte[] compressedData = _fileSystem.ReadAllBytes("/test/compressed.jkr");

            // Now reverse: Step 3: Decompress
            _fileSystem.AddFile("/test/to_decompress.jkr", compressedData);
            string decompressedPath = _unpackingService.UnpackJPK("/test/to_decompress.jkr");
            byte[] decompressedData = _fileSystem.ReadAllBytes(decompressedPath);

            // Verify we got back the encrypted data
            Assert.Equal(encryptedData, decompressedData);

            // Step 4: Decrypt
            _fileSystem.AddFile("/test/to_decrypt.bin", decompressedData);
            string finalPath = _fileProcessingService.DecryptEcdFile(
                "/test/to_decrypt.bin",
                createLog: false,
                cleanUp: false,
                rewriteOldFile: false
            );

            // Assert: Final data matches original
            byte[] finalData = _fileSystem.ReadAllBytes(finalPath);
            Assert.Equal(originalData, finalData);
        }

        [Fact]
        public void CombinedWorkflow_CompressThenEncrypt_RoundtripPreserved()
        {
            // Arrange
            byte[] originalData = CreateTestData(512);
            byte[] metaHeader = CreateEcdMetaHeader(keyIndex: 3);

            _fileSystem.AddFile("/test/original.bin", originalData);

            // Step 1: Compress first
            var compression = new Compression(CompressionType.LZ, 16);
            _packingService.JPKEncode(compression, "/test/original.bin", "/test/compressed.jkr");
            byte[] compressedData = _fileSystem.ReadAllBytes("/test/compressed.jkr");

            // Step 2: Encrypt the compressed file
            _fileSystem.AddFile("/test/compressed.jkr.decd", compressedData);
            _fileSystem.AddFile("/test/compressed.jkr.meta", metaHeader);
            string encryptedPath = _fileProcessingService.EncryptEcdFile(
                "/test/compressed.jkr.decd",
                "/test/compressed.jkr.meta",
                cleanUp: false
            );
            byte[] encryptedData = _fileSystem.ReadAllBytes(encryptedPath);

            // Reverse: Step 3: Decrypt
            _fileSystem.AddFile("/test/to_decrypt.bin", encryptedData);
            string decryptedPath = _fileProcessingService.DecryptEcdFile(
                "/test/to_decrypt.bin",
                createLog: false,
                cleanUp: false,
                rewriteOldFile: false
            );
            byte[] decryptedData = _fileSystem.ReadAllBytes(decryptedPath);

            // Verify we got back the compressed data
            Assert.Equal(compressedData, decryptedData);

            // Step 4: Decompress
            _fileSystem.AddFile("/test/to_decompress.jkr", decryptedData);
            string finalPath = _unpackingService.UnpackJPK("/test/to_decompress.jkr");

            // Assert
            byte[] finalData = _fileSystem.ReadAllBytes(finalPath);
            Assert.Equal(originalData, finalData);
        }

        [Fact]
        public void CombinedWorkflow_MultipleCompressionCycles_DataPreserved()
        {
            // Arrange: Test compressing already compressed data
            byte[] originalData = CreateTestData(256);
            _fileSystem.AddFile("/test/original.bin", originalData);

            // Compress with LZ
            var compression1 = new Compression(CompressionType.LZ, 16);
            _packingService.JPKEncode(compression1, "/test/original.bin", "/test/compressed1.jkr");
            byte[] compressed1 = _fileSystem.ReadAllBytes("/test/compressed1.jkr");

            // Compress again with HFI (nested compression)
            _fileSystem.AddFile("/test/compressed1_for_hfi.bin", compressed1);
            var compression2 = new Compression(CompressionType.HFI, 16);
            _packingService.JPKEncode(compression2, "/test/compressed1_for_hfi.bin", "/test/compressed2.jkr");
            byte[] compressed2 = _fileSystem.ReadAllBytes("/test/compressed2.jkr");

            // Decompress outer layer (HFI)
            _fileSystem.AddFile("/test/to_decompress2.jkr", compressed2);
            string path1 = _unpackingService.UnpackJPK("/test/to_decompress2.jkr");
            byte[] decompressed1 = _fileSystem.ReadAllBytes(path1);
            Assert.Equal(compressed1, decompressed1);

            // Decompress inner layer (LZ)
            _fileSystem.AddFile("/test/to_decompress1.jkr", decompressed1);
            string path2 = _unpackingService.UnpackJPK("/test/to_decompress1.jkr");
            byte[] finalData = _fileSystem.ReadAllBytes(path2);

            Assert.Equal(originalData, finalData);
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void CompressionRoundtrip_AllZeros_Preserved()
        {
            // Arrange: All zeros (highly compressible)
            byte[] originalData = new byte[500];
            _fileSystem.AddFile("/test/zeros.bin", originalData);

            var compression = new Compression(CompressionType.LZ, 16);
            _packingService.JPKEncode(compression, "/test/zeros.bin", "/test/compressed.jkr");
            byte[] compressedData = _fileSystem.ReadAllBytes("/test/compressed.jkr");

            _fileSystem.AddFile("/test/to_decompress.jkr", compressedData);
            string decompressedPath = _unpackingService.UnpackJPK("/test/to_decompress.jkr");

            byte[] decompressedData = _fileSystem.ReadAllBytes(decompressedPath);
            Assert.Equal(originalData, decompressedData);
        }

        [Fact]
        public void CompressionRoundtrip_AllSameValue_Preserved()
        {
            // Arrange: All 0xFF (run-length encoding test)
            byte[] originalData = new byte[500];
            Array.Fill<byte>(originalData, 0xFF);
            _fileSystem.AddFile("/test/ones.bin", originalData);

            var compression = new Compression(CompressionType.LZ, 16);
            _packingService.JPKEncode(compression, "/test/ones.bin", "/test/compressed.jkr");
            byte[] compressedData = _fileSystem.ReadAllBytes("/test/compressed.jkr");

            _fileSystem.AddFile("/test/to_decompress.jkr", compressedData);
            string decompressedPath = _unpackingService.UnpackJPK("/test/to_decompress.jkr");

            byte[] decompressedData = _fileSystem.ReadAllBytes(decompressedPath);
            Assert.Equal(originalData, decompressedData);
        }

        [Fact]
        public void CompressionRoundtrip_RandomData_Preserved()
        {
            // Arrange: Random data (incompressible)
            byte[] originalData = new byte[500];
            new Random(12345).NextBytes(originalData);
            _fileSystem.AddFile("/test/random.bin", originalData);

            var compression = new Compression(CompressionType.LZ, 16);
            _packingService.JPKEncode(compression, "/test/random.bin", "/test/compressed.jkr");
            byte[] compressedData = _fileSystem.ReadAllBytes("/test/compressed.jkr");

            _fileSystem.AddFile("/test/to_decompress.jkr", compressedData);
            string decompressedPath = _unpackingService.UnpackJPK("/test/to_decompress.jkr");

            byte[] decompressedData = _fileSystem.ReadAllBytes(decompressedPath);
            Assert.Equal(originalData, decompressedData);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Create test data with a predictable pattern.
        /// </summary>
        private static byte[] CreateTestData(int size)
        {
            byte[] data = new byte[size];
            for (int i = 0; i < size; i++)
                data[i] = (byte)(i % 256);
            return data;
        }

        /// <summary>
        /// Create a valid ECD meta header for encryption.
        /// </summary>
        /// <param name="keyIndex">Key index (0-5).</param>
        private static byte[] CreateEcdMetaHeader(int keyIndex)
        {
            // ECD header format:
            // Bytes 0-3: Magic "ecd\x1A"
            // Bytes 4-5: Key index as UInt16 (little-endian, so low byte first)
            // Bytes 8-11: Payload size (will be updated)
            // Bytes 12-15: CRC32 (will be updated)
            return new byte[]
            {
                0x65, 0x63, 0x64, 0x1A, // Magic: ecd\x1A
                (byte)keyIndex, 0x00,    // Bytes 4-5: Key index (little-endian UInt16)
                0x00, 0x00,              // Bytes 6-7
                0x00, 0x00, 0x00, 0x00,  // Bytes 8-11: Payload size
                0x00, 0x00, 0x00, 0x00   // Bytes 12-15: CRC32
            };
        }

        #endregion
    }
}
