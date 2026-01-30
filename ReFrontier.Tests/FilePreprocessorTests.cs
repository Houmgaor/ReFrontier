using System;
using System.IO;
using System.Text;
using LibReFrontier;
using LibReFrontier.Abstractions;
using ReFrontier.Jpk;
using ReFrontier.Services;
using ReFrontier.Tests.Mocks;
using Xunit;

namespace ReFrontier.Tests
{
    /// <summary>
    /// Tests for FilePreprocessor class with round-trip scenarios.
    /// </summary>
    public class FilePreprocessorTests
    {
        [Fact]
        public void IsEncrypted_EcdFile_ReturnsTrue()
        {
            var fileSystem = new InMemoryFileSystem();
            var logger = new TestLogger();
            var preprocessor = new FilePreprocessor(fileSystem, logger, new DefaultCodecFactory());

            // Create a mock ECD file (magic: 0x1A646365)
            byte[] ecdData = new byte[100];
            ecdData[0] = 0x65; // "ece" in little-endian
            ecdData[1] = 0x63;
            ecdData[2] = 0x64;
            ecdData[3] = 0x1A;
            fileSystem.AddFile("/test/file.bin", ecdData);

            bool result = preprocessor.IsEncrypted("/test/file.bin");

            Assert.True(result);
        }

        [Fact]
        public void IsEncrypted_ExfFile_ReturnsTrue()
        {
            var fileSystem = new InMemoryFileSystem();
            var logger = new TestLogger();
            var preprocessor = new FilePreprocessor(fileSystem, logger, new DefaultCodecFactory());

            // Create a mock EXF file (magic: 0x1A667865)
            byte[] exfData = new byte[100];
            exfData[0] = 0x65; // "exf" in little-endian
            exfData[1] = 0x78;
            exfData[2] = 0x66;
            exfData[3] = 0x1A;
            fileSystem.AddFile("/test/file.bin", exfData);

            bool result = preprocessor.IsEncrypted("/test/file.bin");

            Assert.True(result);
        }

        [Fact]
        public void IsEncrypted_PlainFile_ReturnsFalse()
        {
            var fileSystem = new InMemoryFileSystem();
            var logger = new TestLogger();
            var preprocessor = new FilePreprocessor(fileSystem, logger, new DefaultCodecFactory());

            byte[] plainData = Encoding.UTF8.GetBytes("Plain text data");
            fileSystem.AddFile("/test/file.bin", plainData);

            bool result = preprocessor.IsEncrypted("/test/file.bin");

            Assert.False(result);
        }

        [Fact]
        public void IsJpkCompressed_JkrFile_ReturnsTrue()
        {
            var fileSystem = new InMemoryFileSystem();
            var logger = new TestLogger();
            var preprocessor = new FilePreprocessor(fileSystem, logger, new DefaultCodecFactory());

            // Create a mock JKR file (magic: 0x1A524B4A)
            byte[] jkrData = new byte[100];
            jkrData[0] = 0x4A; // "JKR" in little-endian
            jkrData[1] = 0x4B;
            jkrData[2] = 0x52;
            jkrData[3] = 0x1A;
            fileSystem.AddFile("/test/file.bin", jkrData);

            bool result = preprocessor.IsJpkCompressed("/test/file.bin");

            Assert.True(result);
        }

        [Fact]
        public void IsJpkCompressed_PlainFile_ReturnsFalse()
        {
            var fileSystem = new InMemoryFileSystem();
            var logger = new TestLogger();
            var preprocessor = new FilePreprocessor(fileSystem, logger, new DefaultCodecFactory());

            byte[] plainData = Encoding.UTF8.GetBytes("Plain text data");
            fileSystem.AddFile("/test/file.bin", plainData);

            bool result = preprocessor.IsJpkCompressed("/test/file.bin");

            Assert.False(result);
        }

        [Fact]
        public void AutoDecrypt_PlainFile_ReturnsOriginalPath()
        {
            var fileSystem = new InMemoryFileSystem();
            var logger = new TestLogger();
            var preprocessor = new FilePreprocessor(fileSystem, logger, new DefaultCodecFactory());

            byte[] plainData = Encoding.UTF8.GetBytes("Plain text data");
            fileSystem.AddFile("/test/file.bin", plainData);

            string result = preprocessor.AutoDecrypt("/test/file.bin", createMetaFile: false);

            TestHelpers.AssertPathsEqual("/test/file.bin", result);
        }

        [Fact]
        public void AutoDecompress_PlainFile_ReturnsOriginalPath()
        {
            var fileSystem = new InMemoryFileSystem();
            var logger = new TestLogger();
            var preprocessor = new FilePreprocessor(fileSystem, logger, new DefaultCodecFactory());

            byte[] plainData = Encoding.UTF8.GetBytes("Plain text data");
            fileSystem.AddFile("/test/file.bin", plainData);

            string result = preprocessor.AutoDecompress("/test/file.bin");

            TestHelpers.AssertPathsEqual("/test/file.bin", result);
        }

        [Fact]
        public void RoundTrip_EncryptDecrypt_PreservesData()
        {
            var fileSystem = new InMemoryFileSystem();
            var logger = new TestLogger();
            var codecFactory = new DefaultCodecFactory();
            var config = FileProcessingConfig.Default();
            var preprocessor = new FilePreprocessor(fileSystem, logger, codecFactory);
            var processingService = new FileProcessingService(fileSystem, logger, config);

            // Create test data
            byte[] originalData = new byte[256];
            for (int i = 0; i < originalData.Length; i++)
                originalData[i] = (byte)(i % 256);

            // Encrypt the data first
            fileSystem.AddFile("/test/original.bin", originalData);
            
            // Create a proper ECD meta header for encryption
            byte[] metaHeader = new byte[16];
            metaHeader[0] = 0x65; metaHeader[1] = 0x63; metaHeader[2] = 0x64; metaHeader[3] = 0x1A; // "ece" magic
            BitConverter.GetBytes((ushort)1).CopyTo(metaHeader, 4); // key index
            BitConverter.GetBytes(originalData.Length).CopyTo(metaHeader, 8); // size
            uint crc32 = Crypto.GetCrc32(originalData);
            BitConverter.GetBytes(crc32).CopyTo(metaHeader, 12); // CRC32

            fileSystem.AddFile("/test/original.bin.meta", metaHeader);

            // Encrypt using the processing service
            string encryptedPath = processingService.EncryptEcdFile("/test/original.bin", "/test/original.bin.meta", cleanUp: false);

            // Verify encryption created output
            Assert.True(fileSystem.FileExists(encryptedPath));
            byte[] encryptedData = fileSystem.ReadAllBytes(encryptedPath);
            
            // Verify it's encrypted (first 4 bytes should be ECD magic)
            Assert.Equal((byte)0x65, encryptedData[0]);
            Assert.Equal((byte)0x63, encryptedData[1]);
            Assert.Equal((byte)0x64, encryptedData[2]);
            Assert.Equal((byte)0x1A, encryptedData[3]);

            // Now use FilePreprocessor to decrypt
            var (decryptedPath, cleanup) = preprocessor.AutoPreprocess(encryptedPath, createMetaFile: true);

            try
            {
                // Verify decryption worked
                Assert.NotEqual(encryptedPath, decryptedPath);
                Assert.True(fileSystem.FileExists(decryptedPath));

                byte[] decryptedData = fileSystem.ReadAllBytes(decryptedPath);

                // Verify data matches original
                Assert.Equal(originalData.Length, decryptedData.Length);
                Assert.Equal(originalData, decryptedData);
            }
            finally
            {
                cleanup();
            }
        }

        [Fact]
        public void RoundTrip_CompressDecompress_PreservesData()
        {
            var fileSystem = new InMemoryFileSystem();
            var logger = new TestLogger();
            var codecFactory = new DefaultCodecFactory();
            var config = FileProcessingConfig.Default();
            var preprocessor = new FilePreprocessor(fileSystem, logger, codecFactory);
            var packingService = new PackingService(fileSystem, logger, codecFactory, config);

            // Create test data with some patterns (compresses well)
            byte[] originalData = new byte[1024];
            for (int i = 0; i < originalData.Length; i++)
                originalData[i] = (byte)(i % 32); // Repeating pattern

            fileSystem.AddFile("/test/original.bin", originalData);

            // Compress using JPK RW compression
            Compression compression = new()
            {
                type = CompressionType.RW,
                level = 15
            };

            packingService.JPKEncode(compression, "/test/original.bin", "/test/compressed.jkr");

            // Verify compression created output
            Assert.True(fileSystem.FileExists("/test/compressed.jkr"));
            byte[] compressedData = fileSystem.ReadAllBytes("/test/compressed.jkr");

            // Verify it's JPK compressed (first 4 bytes should be JKR magic)
            Assert.Equal((byte)0x4A, compressedData[0]);
            Assert.Equal((byte)0x4B, compressedData[1]);
            Assert.Equal((byte)0x52, compressedData[2]);
            Assert.Equal((byte)0x1A, compressedData[3]);

            // Now use FilePreprocessor to decompress
            var (decompressedPath, cleanup) = preprocessor.AutoPreprocess("/test/compressed.jkr", createMetaFile: false);

            try
            {
                // Verify decompression worked
                Assert.NotEqual("/test/compressed.jkr", decompressedPath);
                Assert.True(fileSystem.FileExists(decompressedPath));

                byte[] decompressedData = fileSystem.ReadAllBytes(decompressedPath);

                // Verify data matches original
                Assert.Equal(originalData.Length, decompressedData.Length);
                Assert.Equal(originalData, decompressedData);
            }
            finally
            {
                cleanup();
            }
        }

        [Fact]
        public void RoundTrip_EncryptAndCompress_ThenDecryptAndDecompress_PreservesData()
        {
            var fileSystem = new InMemoryFileSystem();
            var logger = new TestLogger();
            var codecFactory = new DefaultCodecFactory();
            var config = FileProcessingConfig.Default();
            var preprocessor = new FilePreprocessor(fileSystem, logger, codecFactory);
            var processingService = new FileProcessingService(fileSystem, logger, config);
            var packingService = new PackingService(fileSystem, logger, codecFactory, config);

            // Create test data
            byte[] originalData = new byte[512];
            for (int i = 0; i < originalData.Length; i++)
                originalData[i] = (byte)((i * 7 + 13) % 256);

            fileSystem.AddFile("/test/original.bin", originalData);

            // Step 1: Compress the data
            Compression compression = new()
            {
                type = CompressionType.RW,
                level = 15
            };
            packingService.JPKEncode(compression, "/test/original.bin", "/test/compressed.jkr");

            // Step 2: Encrypt the compressed data
            byte[] compressedData = fileSystem.ReadAllBytes("/test/compressed.jkr");
            byte[] metaHeader = new byte[16];
            metaHeader[0] = 0x65; metaHeader[1] = 0x63; metaHeader[2] = 0x64; metaHeader[3] = 0x1A;
            BitConverter.GetBytes((ushort)1).CopyTo(metaHeader, 4);
            BitConverter.GetBytes(compressedData.Length).CopyTo(metaHeader, 8);
            uint crc32 = Crypto.GetCrc32(compressedData);
            BitConverter.GetBytes(crc32).CopyTo(metaHeader, 12);
            fileSystem.AddFile("/test/compressed.jkr.meta", metaHeader);

            string encryptedPath = processingService.EncryptEcdFile("/test/compressed.jkr", "/test/compressed.jkr.meta", cleanUp: false);

            // Verify we have an encrypted, compressed file
            byte[] encryptedCompressedData = fileSystem.ReadAllBytes(encryptedPath);
            Assert.Equal((byte)0x65, encryptedCompressedData[0]); // ECD magic
            Assert.Equal((byte)0x63, encryptedCompressedData[1]);

            // Step 3: Use FilePreprocessor to auto-decrypt and auto-decompress
            var (processedPath, cleanup) = preprocessor.AutoPreprocess(encryptedPath, createMetaFile: true);

            try
            {
                // Verify preprocessing worked
                Assert.NotEqual(encryptedPath, processedPath);
                Assert.True(fileSystem.FileExists(processedPath));

                byte[] processedData = fileSystem.ReadAllBytes(processedPath);

                // Verify data matches original
                Assert.Equal(originalData.Length, processedData.Length);
                Assert.Equal(originalData, processedData);

                // Verify logger shows both operations
                Assert.Contains("Detected ECD encryption", logger.Output);
                Assert.Contains("Detected JPK compression", logger.Output);
            }
            finally
            {
                cleanup();
            }
        }

        [Fact]
        public void AutoPreprocess_Cleanup_RemovesTemporaryFiles()
        {
            var fileSystem = new InMemoryFileSystem();
            var logger = new TestLogger();
            var codecFactory = new DefaultCodecFactory();
            var preprocessor = new FilePreprocessor(fileSystem, logger, codecFactory);

            // Create a simple file
            byte[] plainData = Encoding.UTF8.GetBytes("Test data");
            fileSystem.AddFile("/test/file.bin", plainData);

            var (processedPath, cleanup) = preprocessor.AutoPreprocess("/test/file.bin", createMetaFile: false);

            // Since it's not encrypted/compressed, path should be the same
            TestHelpers.AssertPathsEqual("/test/file.bin", processedPath);

            // Cleanup should not remove the original file
            cleanup();
            Assert.True(fileSystem.FileExists("/test/file.bin"));
        }
    }
}
