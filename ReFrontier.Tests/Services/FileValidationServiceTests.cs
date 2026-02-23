using LibReFrontier;

using ReFrontier.Jpk;
using ReFrontier.Services;
using ReFrontier.Tests.Mocks;

namespace ReFrontier.Tests.Services
{
    /// <summary>
    /// Tests for FileValidationService.
    /// </summary>
    public class FileValidationServiceTests
    {
        private readonly TestLogger _logger = new();
        private readonly FileValidationService _service;

        public FileValidationServiceTests()
        {
            _service = new FileValidationService(_logger, new DefaultCodecFactory());
        }

        #region Helper Methods

        /// <summary>
        /// Build a valid ECD buffer from plaintext data.
        /// </summary>
        private static byte[] BuildEcdBuffer(byte[] plaintext, int keyIndex = 4)
        {
            return Crypto.EncodeEcd(plaintext, keyIndex);
        }

        /// <summary>
        /// Build a valid EXF buffer from plaintext data.
        /// </summary>
        private static byte[] BuildExfBuffer(byte[] plaintext)
        {
            byte[] meta = new byte[16];
            // EXF magic: 0x1A667865
            meta[0] = 0x65;
            meta[1] = 0x78;
            meta[2] = 0x66;
            meta[3] = 0x1A;
            // Key index 0
            meta[4] = 0x00;
            meta[5] = 0x00;
            // Seed value at offset 12
            meta[12] = 0x42;
            meta[13] = 0x00;
            meta[14] = 0x00;
            meta[15] = 0x00;
            return Crypto.EncodeExf(plaintext, meta);
        }

        /// <summary>
        /// Build a valid JPK RW buffer from plaintext data.
        /// </summary>
        private static byte[] BuildJpkRwBuffer(byte[] plaintext)
        {
            // Compress using RW encoder
            var encoder = new JPKEncodeRW();
            using var compressedStream = new System.IO.MemoryStream();
            encoder.ProcessOnEncode(plaintext, compressedStream, 16);
            byte[] compressed = compressedStream.ToArray();

            // Build JPK header + compressed data
            using var ms = new System.IO.MemoryStream();
            using var bw = new System.IO.BinaryWriter(ms);
            bw.Write((uint)FileMagic.JKR);     // Magic
            bw.Write((ushort)0);                 // Skip
            bw.Write((ushort)0);                 // CompressionType = RW (0)
            bw.Write((int)0x10);                 // Start offset
            bw.Write(plaintext.Length);           // Decompressed size
            bw.Write(compressed);
            return ms.ToArray();
        }

        /// <summary>
        /// Build a valid simple archive buffer.
        /// Simple archives have: 4 bytes "magic" (non-matching), 4 bytes count, then entry table + data.
        /// </summary>
        private static byte[] BuildSimpleArchive(int entryCount)
        {
            int magicSize = 4; // 4 bytes skipped before count
            int entryTableSize = entryCount * 8;
            int dataPerEntry = 16; // minimum viable entry
            int dataStart = magicSize + 4 + entryTableSize; // magic + count + entries
            int totalSize = dataStart + entryCount * dataPerEntry;

            using var ms = new System.IO.MemoryStream();
            using var bw = new System.IO.BinaryWriter(ms);

            bw.Write((uint)0x00000001); // non-matching "magic" (first 4 bytes)
            bw.Write(entryCount);        // count at offset 4

            for (int i = 0; i < entryCount; i++)
            {
                bw.Write(dataStart + i * dataPerEntry); // offset
                bw.Write(dataPerEntry);                   // size
            }

            // Write dummy data
            for (int i = 0; i < entryCount * dataPerEntry; i++)
            {
                bw.Write((byte)(i & 0xFF));
            }

            return ms.ToArray();
        }

        /// <summary>
        /// Build a valid MOMO archive buffer.
        /// </summary>
        private static byte[] BuildMomoArchive(int entryCount)
        {
            // MOMO header: 4 bytes magic + 4 bytes padding
            // Then: 4 bytes count, entry table, data
            int momoHeaderSize = 8;
            int entryTableSize = entryCount * 8;
            int dataPerEntry = 16;
            int dataStart = momoHeaderSize + 4 + entryTableSize;
            int totalSize = dataStart + entryCount * dataPerEntry;

            using var ms = new System.IO.MemoryStream();
            using var bw = new System.IO.BinaryWriter(ms);

            bw.Write(FileMagic.MOMO);       // Magic
            bw.Write((uint)0);               // Padding
            bw.Write(entryCount);            // Count

            for (int i = 0; i < entryCount; i++)
            {
                bw.Write(dataStart + i * dataPerEntry);
                bw.Write(dataPerEntry);
            }

            for (int i = 0; i < entryCount * dataPerEntry; i++)
            {
                bw.Write((byte)(i & 0xFF));
            }

            return ms.ToArray();
        }

        /// <summary>
        /// Build a valid MHA archive buffer.
        /// </summary>
        private static byte[] BuildMhaArchive(int entryCount)
        {
            // MHA header: 4 magic + 4 pointerMeta + 4 count + 4 pointerNames + 4 namesLen + 2 unk1 + 2 unk2 = 24
            int headerSize = 24;
            int metaBlockSize = entryCount * 0x14; // MhaEntryMetadataSize
            int metaStart = headerSize;
            int namesStart = metaStart + metaBlockSize;

            // Build names
            var names = new string[entryCount];
            var nameBytes = new byte[entryCount][];
            int namesSize = 0;
            for (int i = 0; i < entryCount; i++)
            {
                names[i] = $"entry_{i:D4}.bin";
                nameBytes[i] = System.Text.Encoding.UTF8.GetBytes(names[i]);
                namesSize += nameBytes[i].Length + 1; // +1 null terminator
            }

            int dataStart = namesStart + namesSize;
            int dataPerEntry = 32;

            using var ms = new System.IO.MemoryStream();
            using var bw = new System.IO.BinaryWriter(ms);

            // Header
            bw.Write(FileMagic.MHA);           // Magic
            bw.Write(metaStart);               // Pointer to metadata
            bw.Write(entryCount);              // Count
            bw.Write(namesStart);              // Pointer to names
            bw.Write(namesSize);               // Names block length
            bw.Write((short)0);                // unk1
            bw.Write((short)0);                // unk2

            // Metadata entries
            int currentNameOffset = 0;
            for (int i = 0; i < entryCount; i++)
            {
                bw.Write(currentNameOffset);                   // stringOffset
                bw.Write(dataStart + i * dataPerEntry);        // entryOffset
                bw.Write(dataPerEntry);                         // entrySize
                bw.Write(dataPerEntry);                         // paddedSize
                bw.Write(i);                                    // fileId
                currentNameOffset += nameBytes[i].Length + 1;
            }

            // Names
            for (int i = 0; i < entryCount; i++)
            {
                bw.Write(nameBytes[i]);
                bw.Write((byte)0);
            }

            // Data
            for (int i = 0; i < entryCount * dataPerEntry; i++)
            {
                bw.Write((byte)(i & 0xFF));
            }

            return ms.ToArray();
        }

        #endregion

        #region ECD Tests

        [Fact]
        public void ValidateEcd_ValidFile_PassesCrc32()
        {
            byte[] plaintext = TestHelpers.RandomData(256, seed: 42);
            byte[] ecdBuffer = BuildEcdBuffer(plaintext);

            var checks = _service.ValidateEcd(ecdBuffer);

            Assert.All(checks, c => Assert.True(c.Passed, $"{c.Layer}:{c.CheckName} failed: {c.Detail}"));
            Assert.Contains(checks, c => c.CheckName == "CRC32" && c.Passed);
        }

        [Fact]
        public void ValidateEcd_CorruptPayload_FailsCrc32()
        {
            byte[] plaintext = TestHelpers.RandomData(256, seed: 42);
            byte[] ecdBuffer = BuildEcdBuffer(plaintext);

            // Flip a byte in the encrypted payload
            ecdBuffer[20] ^= 0xFF;

            var checks = _service.ValidateEcd(ecdBuffer);

            Assert.Contains(checks, c => c.CheckName == "CRC32" && !c.Passed);
        }

        [Fact]
        public void ValidateEcd_TooSmall_ReportsError()
        {
            byte[] buffer = new byte[8]; // Less than 16-byte header
            buffer[0] = 0x65; buffer[1] = 0x63; buffer[2] = 0x64; buffer[3] = 0x1A; // ECD magic

            var checks = _service.ValidateEcd(buffer);

            Assert.Contains(checks, c => c.CheckName == "HeaderSize" && !c.Passed);
        }

        [Fact]
        public void ValidateEcd_InvalidKeyIndex_ReportsError()
        {
            byte[] plaintext = TestHelpers.RandomData(32, seed: 1);
            byte[] ecdBuffer = BuildEcdBuffer(plaintext);

            // Set key index to 6 (invalid, valid range 0-5)
            ecdBuffer[4] = 6;
            ecdBuffer[5] = 0;

            var checks = _service.ValidateEcd(ecdBuffer);

            Assert.Contains(checks, c => c.CheckName == "KeyIndex" && !c.Passed);
        }

        #endregion

        #region JPK Tests

        [Fact]
        public void ValidateJpk_ValidRw_Passes()
        {
            byte[] plaintext = TestHelpers.RandomData(128, seed: 10);
            byte[] jpkBuffer = BuildJpkRwBuffer(plaintext);

            var checks = _service.ValidateJpk(jpkBuffer);

            Assert.All(checks, c => Assert.True(c.Passed, $"{c.Layer}:{c.CheckName} failed: {c.Detail}"));
            Assert.Contains(checks, c => c.CheckName == "Decompression" && c.Passed);
        }

        [Fact]
        public void ValidateJpk_ZeroDecompressedSize_Fails()
        {
            byte[] plaintext = TestHelpers.RandomData(128, seed: 10);
            byte[] jpkBuffer = BuildJpkRwBuffer(plaintext);

            // Set declared decompressed size to 0
            byte[] sizeBytes = BitConverter.GetBytes(0);
            Array.Copy(sizeBytes, 0, jpkBuffer, 12, 4);

            var checks = _service.ValidateJpk(jpkBuffer);

            Assert.Contains(checks, c => c.CheckName == "DeclaredSize" && !c.Passed);
        }

        [Fact]
        public void ValidateJpk_InvalidCompressionType_Fails()
        {
            byte[] jpkBuffer = TestDataFactory.CreateJpkHeader(99, 100);

            var checks = _service.ValidateJpk(jpkBuffer);

            Assert.Contains(checks, c => c.CheckName == "CompressionType" && !c.Passed);
        }

        #endregion

        #region SimpleArchive Tests

        [Fact]
        public void ValidateSimpleArchive_ValidContainer_Passes()
        {
            byte[] buffer = BuildSimpleArchive(5);

            var checks = _service.ValidateSimpleArchive(buffer);

            Assert.All(checks, c => Assert.True(c.Passed, $"{c.CheckName} failed: {c.Detail}"));
        }

        [Fact]
        public void ValidateSimpleArchive_EntryOutOfBounds_Fails()
        {
            byte[] buffer = BuildSimpleArchive(2);

            // Corrupt first entry offset to point beyond buffer
            // Entry table starts at offset 8 (4 magic + 4 count), first entry offset is at 8
            byte[] badOffset = BitConverter.GetBytes(buffer.Length + 100);
            Array.Copy(badOffset, 0, buffer, 8, 4);

            var checks = _service.ValidateSimpleArchive(buffer);

            Assert.Contains(checks, c => c.CheckName == "EntryBounds" && !c.Passed);
        }

        #endregion

        #region MHA Tests

        [Fact]
        public void ValidateMha_ValidContainer_Passes()
        {
            byte[] buffer = BuildMhaArchive(3);

            var checks = _service.ValidateMha(buffer);

            Assert.All(checks, c => Assert.True(c.Passed, $"{c.CheckName} failed: {c.Detail}"));
        }

        #endregion

        #region EXF Tests

        [Fact]
        public void ValidateExf_ValidFile_Passes()
        {
            byte[] plaintext = TestHelpers.RandomData(64, seed: 7);
            byte[] exfBuffer = BuildExfBuffer(plaintext);

            var checks = _service.ValidateExf(exfBuffer);

            Assert.All(checks, c => Assert.True(c.Passed, $"{c.CheckName} failed: {c.Detail}"));
            Assert.Contains(checks, c => c.CheckName == "Decryption" && c.Passed);
        }

        #endregion

        #region Recursive Tests

        [Fact]
        public void ValidateRecursive_EcdContainingJpk_ValidatesBothLayers()
        {
            // Create JPK data first
            byte[] innerPlaintext = TestHelpers.RandomData(64, seed: 99);
            byte[] jpkBuffer = BuildJpkRwBuffer(innerPlaintext);

            // Wrap in ECD
            byte[] ecdBuffer = BuildEcdBuffer(jpkBuffer);

            var result = _service.ValidateBuffer(ecdBuffer, "test.bin");

            Assert.True(result.IsValid);
            Assert.Contains(result.Checks, c => c.Layer == "ECD" && c.CheckName == "CRC32" && c.Passed);
            Assert.Contains(result.Checks, c => c.Layer == "JPK" && c.CheckName == "Decompression" && c.Passed);
        }

        #endregion

        #region Unknown Format Tests

        [Fact]
        public void ValidateUnknownFormat_ReportsUnrecognized()
        {
            // Random data that doesn't match any magic
            byte[] buffer = [0xDE, 0xAD, 0xBE, 0xEF, 0x00, 0x00, 0x00, 0x00,
                             0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];

            var result = _service.ValidateBuffer(buffer, "unknown.bin");

            Assert.False(result.IsRecognized);
            Assert.Contains(result.Checks, c => c.Layer == "Unknown");
        }

        #endregion

        #region MOMO Tests

        [Fact]
        public void ValidateMomo_ValidContainer_Passes()
        {
            byte[] buffer = BuildMomoArchive(3);

            var checks = _service.ValidateMomo(buffer);

            Assert.All(checks, c => Assert.True(c.Passed, $"{c.CheckName} failed: {c.Detail}"));
        }

        #endregion

        #region FTXT Tests

        [Fact]
        public void ValidateFtxt_ValidFile_Passes()
        {
            // Build a minimal FTXT file
            // Header: 10 bytes padding, 2 bytes string count, 4 bytes text block size
            using var ms = new System.IO.MemoryStream();
            using var bw = new System.IO.BinaryWriter(ms);

            // Magic at offset 0 (FTXT = 0x000B0000)
            bw.Write((uint)FileMagic.FTXT);
            bw.Write(new byte[6]);  // padding to offset 10
            bw.Write((short)2);     // 2 strings
            bw.Write((int)20);      // text block size (doesn't matter for validation)

            // Two null-terminated strings
            bw.Write(System.Text.Encoding.ASCII.GetBytes("hello"));
            bw.Write((byte)0);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("world"));
            bw.Write((byte)0);

            byte[] buffer = ms.ToArray();
            var checks = _service.ValidateFtxt(buffer);

            Assert.All(checks, c => Assert.True(c.Passed, $"{c.CheckName} failed: {c.Detail}"));
        }

        #endregion

        #region StageContainer Tests

        [Fact]
        public void ValidateStageContainer_ValidContainer_Passes()
        {
            // Build a minimal stage container
            int dataStart = 0x18 + 0x08 + 12; // header + rest header + 1 rest entry
            int segmentSize = 32;
            int totalSize = dataStart + 4 * segmentSize; // 3 segments + 1 rest entry

            using var ms = new System.IO.MemoryStream();
            using var bw = new System.IO.BinaryWriter(ms);

            // 3 segments (offset, size)
            int currentData = dataStart;
            for (int i = 0; i < 3; i++)
            {
                bw.Write(currentData);
                bw.Write(segmentSize);
                currentData += segmentSize;
            }

            // Rest header
            bw.Write(1);  // restCount
            bw.Write(0);  // unkHeader

            // 1 rest entry (offset, size, unk)
            bw.Write(currentData);
            bw.Write(segmentSize);
            bw.Write(0);

            // Data
            for (int i = 0; i < 4 * segmentSize; i++)
            {
                bw.Write((byte)0x42);
            }

            byte[] buffer = ms.ToArray();
            var checks = _service.ValidateStageContainer(buffer);

            Assert.All(checks, c => Assert.True(c.Passed, $"{c.CheckName} failed: {c.Detail}"));
        }

        #endregion

        #region ValidationResult Properties

        [Fact]
        public void ValidationResult_FormatChain_ShowsAllLayers()
        {
            var result = new ValidationResult
            {
                FilePath = "test.bin",
                Checks = new System.Collections.Generic.List<ValidationCheck>
                {
                    new() { Layer = "ECD", CheckName = "Magic", Passed = true },
                    new() { Layer = "ECD", CheckName = "CRC32", Passed = true },
                    new() { Layer = "JPK", CheckName = "Decompression", Passed = true },
                    new() { Layer = "SimpleArchive", CheckName = "EntryBounds", Passed = true },
                }
            };

            Assert.Equal("ECD > JPK > SimpleArchive", result.FormatChain);
            Assert.True(result.IsValid);
            Assert.True(result.IsRecognized);
        }

        [Fact]
        public void ValidationResult_FirstFailure_ReturnsCorrectCheck()
        {
            var result = new ValidationResult
            {
                FilePath = "test.bin",
                Checks = new System.Collections.Generic.List<ValidationCheck>
                {
                    new() { Layer = "ECD", CheckName = "Magic", Passed = true },
                    new() { Layer = "ECD", CheckName = "CRC32", Passed = false, Detail = "mismatch" },
                }
            };

            Assert.False(result.IsValid);
            Assert.NotNull(result.FirstFailure);
            Assert.Equal("CRC32", result.FirstFailure!.CheckName);
        }

        #endregion
    }
}
