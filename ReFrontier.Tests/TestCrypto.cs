using LibReFrontier;

namespace ReFrontier.Tests
{
    /// <summary>
    /// Tests for LibReFrontier.Crypto class.
    /// </summary>
    public class TestCrypto
    {
        #region GetCrc32 Tests

        [Fact]
        public void GetCrc32_EmptyArray_ReturnsZero()
        {
            byte[] empty = TestHelpers.EmptyData();
            uint result = Crypto.GetCrc32(empty);
            Assert.Equal(0u, result);
        }

        [Fact]
        public void GetCrc32_KnownValue_ReturnsExpected()
        {
            // "123456789" has a well-known CRC32 value of 0xCBF43926
            byte[] data = "123456789"u8.ToArray();
            uint result = Crypto.GetCrc32(data);
            Assert.Equal(0xCBF43926u, result);
        }

        [Fact]
        public void GetCrc32_IsDeterministic()
        {
            byte[] data = TestHelpers.RandomData(100, seed: 42);
            uint result1 = Crypto.GetCrc32(data);
            uint result2 = Crypto.GetCrc32(data);
            Assert.Equal(result1, result2);
        }

        [Fact]
        public void GetCrc32_DifferentData_DifferentResults()
        {
            byte[] data1 = TestHelpers.RandomData(100, seed: 1);
            byte[] data2 = TestHelpers.RandomData(100, seed: 2);
            uint result1 = Crypto.GetCrc32(data1);
            uint result2 = Crypto.GetCrc32(data2);
            Assert.NotEqual(result1, result2);
        }

        #endregion

        #region ECD Round-trip Tests

        [Theory]
        [InlineData(16, 0)]
        [InlineData(64, 1)]
        [InlineData(256, 2)]
        [InlineData(1024, 3)]
        [InlineData(16, 4)]
        [InlineData(64, 5)]
        public void EcdRoundTrip_VariousSizesAndKeys_RecoversOriginal(int size, int keyIndex)
        {
            byte[] original = TestHelpers.RandomData(size, seed: size + keyIndex);

            // Create meta buffer with the key index
            byte[] meta = CreateEcdMeta(keyIndex);

            // Encode
            byte[] encoded = Crypto.EncodeEcd(original, meta);

            // Decode in place
            Crypto.DecodeEcd(encoded);

            // Extract payload (skip 16-byte header)
            byte[] decoded = new byte[original.Length];
            Array.Copy(encoded, 16, decoded, 0, original.Length);

            TestHelpers.AssertBytesEqual(original, decoded, $"ECD round-trip size={size}, key={keyIndex}");
        }

        [Fact]
        public void EcdRoundTrip_SingleByte_Works()
        {
            byte[] original = TestHelpers.SingleByte(0x42);
            byte[] meta = CreateEcdMeta(0);

            byte[] encoded = Crypto.EncodeEcd(original, meta);
            Crypto.DecodeEcd(encoded);

            Assert.Equal(original[0], encoded[16]);
        }

        [Fact]
        public void EcdRoundTrip_AllZeros_Works()
        {
            byte[] original = new byte[64]; // All zeros
            byte[] meta = CreateEcdMeta(1);

            byte[] encoded = Crypto.EncodeEcd(original, meta);
            Crypto.DecodeEcd(encoded);

            byte[] decoded = new byte[original.Length];
            Array.Copy(encoded, 16, decoded, 0, original.Length);

            TestHelpers.AssertBytesEqual(original, decoded, "ECD all-zeros");
        }

        [Fact]
        public void EcdRoundTrip_AllOnes_Works()
        {
            byte[] original = new byte[64];
            Array.Fill(original, (byte)0xFF);
            byte[] meta = CreateEcdMeta(2);

            byte[] encoded = Crypto.EncodeEcd(original, meta);
            Crypto.DecodeEcd(encoded);

            byte[] decoded = new byte[original.Length];
            Array.Copy(encoded, 16, decoded, 0, original.Length);

            TestHelpers.AssertBytesEqual(original, decoded, "ECD all-0xFF");
        }

        #endregion

        #region ECD Encoding Structure Tests

        [Fact]
        public void EncodeEcd_ProducesCorrectHeaderMagic()
        {
            byte[] original = TestHelpers.RandomData(32, seed: 123);
            byte[] meta = CreateEcdMeta(0);

            byte[] encoded = Crypto.EncodeEcd(original, meta);

            // First 4 bytes should contain magic from meta
            uint magic = BitConverter.ToUInt32(encoded, 0);
            Assert.Equal(0x1A646365u, magic); // ECD magic
        }

        [Fact]
        public void EncodeEcd_StoresCorrectPayloadSize()
        {
            byte[] original = TestHelpers.RandomData(100, seed: 456);
            byte[] meta = CreateEcdMeta(0);

            byte[] encoded = Crypto.EncodeEcd(original, meta);

            uint storedSize = BitConverter.ToUInt32(encoded, 8);
            Assert.Equal((uint)original.Length, storedSize);
        }

        [Fact]
        public void EncodeEcd_OutputLengthIsInputPlusHeader()
        {
            byte[] original = TestHelpers.RandomData(50, seed: 789);
            byte[] meta = CreateEcdMeta(0);

            byte[] encoded = Crypto.EncodeEcd(original, meta);

            Assert.Equal(16 + original.Length, encoded.Length);
        }

        #endregion

        #region EXF Decoding Tests

        [Fact]
        public void DecodeExf_InvalidMagic_NoModification()
        {
            // Create buffer without EXF magic
            byte[] buffer = TestHelpers.RandomData(64, seed: 111);
            byte[] original = (byte[])buffer.Clone();

            Crypto.DecodeExf(buffer);

            // Buffer should be unchanged (no EXF magic)
            TestHelpers.AssertBytesEqual(original, buffer, "EXF no-magic unchanged");
        }

        [Fact]
        public void DecodeExf_ValidMagic_ModifiesData()
        {
            // Create buffer with EXF magic and valid key index
            byte[] buffer = new byte[64];
            TestHelpers.RandomData(64, seed: 222).CopyTo(buffer, 0);

            // Set EXF magic at offset 0
            byte[] magic = BitConverter.GetBytes(0x1a667865u);
            Array.Copy(magic, 0, buffer, 0, 4);
            // Set valid key index (0-4) at offset 4
            buffer[4] = 0;
            buffer[5] = 0;

            byte[] original = (byte[])buffer.Clone();

            Crypto.DecodeExf(buffer);

            // Data after header should be different (decoded)
            bool anyDifferent = false;
            for (int i = 16; i < buffer.Length; i++)
            {
                if (buffer[i] != original[i])
                {
                    anyDifferent = true;
                    break;
                }
            }
            Assert.True(anyDifferent, "EXF decode should modify payload data");
        }

        [Fact]
        public void DecodeExf_HeaderUnchanged()
        {
            byte[] buffer = new byte[64];
            TestHelpers.RandomData(64, seed: 333).CopyTo(buffer, 0);

            // Set EXF magic
            byte[] magic = BitConverter.GetBytes(0x1a667865u);
            Array.Copy(magic, 0, buffer, 0, 4);
            // Set valid key index (0-4) at offset 4
            buffer[4] = 0;
            buffer[5] = 0;

            byte[] originalHeader = new byte[16];
            Array.Copy(buffer, 0, originalHeader, 0, 16);

            Crypto.DecodeExf(buffer);

            // Header (first 16 bytes) should be unchanged
            byte[] newHeader = new byte[16];
            Array.Copy(buffer, 0, newHeader, 0, 16);

            TestHelpers.AssertBytesEqual(originalHeader, newHeader, "EXF header unchanged");
        }

        #endregion

        #region EXF Round-trip Tests

        [Theory]
        [InlineData(16, 0)]
        [InlineData(64, 1)]
        [InlineData(256, 2)]
        [InlineData(1024, 3)]
        [InlineData(16, 4)]
        public void ExfRoundTrip_VariousSizesAndKeys_RecoversOriginal(int size, int keyIndex)
        {
            byte[] original = TestHelpers.RandomData(size, seed: size + keyIndex + 1000);

            // Create meta buffer with the key index
            byte[] meta = CreateExfMeta(keyIndex);

            // Encode
            byte[] encoded = Crypto.EncodeExf(original, meta);

            // Decode in place
            Crypto.DecodeExf(encoded);

            // Extract payload (skip 16-byte header)
            byte[] decoded = new byte[original.Length];
            Array.Copy(encoded, 16, decoded, 0, original.Length);

            TestHelpers.AssertBytesEqual(original, decoded, $"EXF round-trip size={size}, key={keyIndex}");
        }

        [Fact]
        public void ExfRoundTrip_SingleByte_Works()
        {
            byte[] original = TestHelpers.SingleByte(0x42);
            byte[] meta = CreateExfMeta(0);

            byte[] encoded = Crypto.EncodeExf(original, meta);
            Crypto.DecodeExf(encoded);

            Assert.Equal(original[0], encoded[16]);
        }

        [Fact]
        public void ExfRoundTrip_AllZeros_Works()
        {
            byte[] original = new byte[64]; // All zeros
            byte[] meta = CreateExfMeta(1);

            byte[] encoded = Crypto.EncodeExf(original, meta);
            Crypto.DecodeExf(encoded);

            byte[] decoded = new byte[original.Length];
            Array.Copy(encoded, 16, decoded, 0, original.Length);

            TestHelpers.AssertBytesEqual(original, decoded, "EXF all-zeros");
        }

        [Fact]
        public void ExfRoundTrip_AllOnes_Works()
        {
            byte[] original = new byte[64];
            Array.Fill(original, (byte)0xFF);
            byte[] meta = CreateExfMeta(2);

            byte[] encoded = Crypto.EncodeExf(original, meta);
            Crypto.DecodeExf(encoded);

            byte[] decoded = new byte[original.Length];
            Array.Copy(encoded, 16, decoded, 0, original.Length);

            TestHelpers.AssertBytesEqual(original, decoded, "EXF all-0xFF");
        }

        [Fact]
        public void ExfRoundTrip_LargeFile_Works()
        {
            // Test with a larger file to ensure the algorithm scales
            byte[] original = TestHelpers.RandomData(4096, seed: 9999);
            byte[] meta = CreateExfMeta(3);

            byte[] encoded = Crypto.EncodeExf(original, meta);
            Crypto.DecodeExf(encoded);

            byte[] decoded = new byte[original.Length];
            Array.Copy(encoded, 16, decoded, 0, original.Length);

            TestHelpers.AssertBytesEqual(original, decoded, "EXF large file");
        }

        #endregion

        #region EXF Encoding Structure Tests

        [Fact]
        public void EncodeExf_ProducesCorrectHeaderMagic()
        {
            byte[] original = TestHelpers.RandomData(32, seed: 123);
            byte[] meta = CreateExfMeta(0);

            byte[] encoded = Crypto.EncodeExf(original, meta);

            // First 4 bytes should contain magic from meta
            uint magic = BitConverter.ToUInt32(encoded, 0);
            Assert.Equal(0x1a667865u, magic); // EXF magic
        }

        [Fact]
        public void EncodeExf_OutputLengthIsInputPlusHeader()
        {
            byte[] original = TestHelpers.RandomData(50, seed: 789);
            byte[] meta = CreateExfMeta(0);

            byte[] encoded = Crypto.EncodeExf(original, meta);

            Assert.Equal(16 + original.Length, encoded.Length);
        }

        [Fact]
        public void EncodeExf_PreservesHeaderFromMeta()
        {
            byte[] original = TestHelpers.RandomData(32, seed: 456);
            byte[] meta = CreateExfMeta(2);

            byte[] encoded = Crypto.EncodeExf(original, meta);

            // Header should match meta
            byte[] header = new byte[16];
            Array.Copy(encoded, 0, header, 0, 16);

            TestHelpers.AssertBytesEqual(meta, header, "EXF header preserved");
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Creates an ECD meta buffer with the specified key index.
        /// </summary>
        private static byte[] CreateEcdMeta(int keyIndex)
        {
            byte[] meta = new byte[16];
            // Set ECD magic (0x1A646365 in little-endian)
            meta[0] = 0x65;
            meta[1] = 0x63;
            meta[2] = 0x64;
            meta[3] = 0x1A;
            // Set key index at offset 4 (16-bit)
            meta[4] = (byte)(keyIndex & 0xFF);
            meta[5] = (byte)((keyIndex >> 8) & 0xFF);
            return meta;
        }

        /// <summary>
        /// Creates an EXF meta buffer with the specified key index.
        /// </summary>
        private static byte[] CreateExfMeta(int keyIndex)
        {
            byte[] meta = new byte[16];
            // Set EXF magic (0x1a667865 in little-endian)
            meta[0] = 0x65;
            meta[1] = 0x78;
            meta[2] = 0x66;
            meta[3] = 0x1A;
            // Set key index at offset 4 (16-bit)
            meta[4] = (byte)(keyIndex & 0xFF);
            meta[5] = (byte)((keyIndex >> 8) & 0xFF);
            // Set seed value at offset 12 (used for XOR key generation)
            // Use a deterministic seed based on key index
            uint seed = (uint)(0x12345678 + keyIndex * 0x11111111);
            byte[] seedBytes = BitConverter.GetBytes(seed);
            Array.Copy(seedBytes, 0, meta, 12, 4);
            return meta;
        }

        #endregion
    }
}
