using ReFrontier.Jpk;

namespace ReFrontier.Tests
{
    /// <summary>
    /// Tests for JPKEncodeHFI and JPKDecodeHFI (Huffman + LZ compression).
    /// </summary>
    public class TestJpkHfi
    {
        private const int HuffmanTableHeaderSize = 2; // Int16 for table length
        private const short ExpectedTableLength = 0x1FE;

        /// <summary>
        /// SHA-256 of the 1022-byte Huffman table the encoder emits (2-byte length plus
        /// 510 entries). The table does not depend on the input, so pinning it detects any
        /// change in the permutation — including a platform or runtime difference in
        /// seeded Random or in OrderBy, which the multi-OS CI would otherwise not catch.
        /// Update this only when the table is meant to change.
        /// </summary>
        private const string ExpectedTableHash =
            "3f48199120871acbb432cc02e47760ac6f89e617731a7f5efa68ec994fead6cf";

        private const int HuffmanTableSize = 1022;

        private static byte[] Encode(byte[] input, int level = 50)
        {
            var encoder = new JPKEncodeHFI();
            using var outStream = new MemoryStream();
            encoder.ProcessOnEncode(input, outStream, level);
            return outStream.ToArray();
        }

        #region Determinism Tests

        [Fact]
        public void EncodeHFI_IsDeterministic()
        {
            // The leaf permutation used to be seeded from the clock, so the same input
            // compressed to a different file on every run and output could not be
            // compared, cached or checksummed.
            byte[] input = TestHelpers.RandomData(4096, seed: 4242);

            byte[] first = Encode(input);
            byte[] second = Encode(input);

            Assert.Equal(first, second);
        }

        [Fact]
        public void EncodeHFIRW_IsDeterministic()
        {
            // HFIRW builds its table with the same FillTable, so it shared the problem.
            byte[] input = TestHelpers.RandomData(4096, seed: 2424);

            static byte[] EncodeRw(byte[] data)
            {
                var encoder = new JPKEncodeHFIRW();
                using var outStream = new MemoryStream();
                encoder.ProcessOnEncode(data, outStream, level: 50);
                return outStream.ToArray();
            }

            Assert.Equal(EncodeRw(input), EncodeRw(input));
        }

        [Fact]
        public void EncodeHFI_TableDoesNotDependOnInput()
        {
            byte[] fromOneInput = Encode(TestHelpers.RandomData(2048, seed: 1))[..HuffmanTableSize];
            byte[] fromAnother = Encode(TestHelpers.RandomData(3000, seed: 2))[..HuffmanTableSize];

            Assert.Equal(fromOneInput, fromAnother);
        }

        [Fact]
        public void EncodeHFI_TableMatchesPinnedValue()
        {
            byte[] table = Encode(TestHelpers.RandomData(1024, seed: 7))[..HuffmanTableSize];

            string actual = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(table))
                .ToLowerInvariant();

            Assert.Equal(ExpectedTableHash, actual);
        }

        #endregion

        #region Encode Tests

        [Fact]
        public void EncodeHFI_ProducesHuffmanTableHeader()
        {
            var encoder = new JPKEncodeHFI();
            byte[] input = TestHelpers.RandomData(64, seed: 100);

            using var outStream = new MemoryStream();
            encoder.ProcessOnEncode(input, outStream, level: 100);

            byte[] output = outStream.ToArray();

            // First 2 bytes should be the table length (0x1FE)
            Assert.True(output.Length >= HuffmanTableHeaderSize, "Output should contain header");
            short tableLen = BitConverter.ToInt16(output, 0);
            Assert.Equal(ExpectedTableLength, tableLen);
        }

        [Fact]
        public void EncodeHFI_OutputContainsTable()
        {
            var encoder = new JPKEncodeHFI();
            byte[] input = TestHelpers.RandomData(64, seed: 200);

            using var outStream = new MemoryStream();
            encoder.ProcessOnEncode(input, outStream, level: 100);

            byte[] output = outStream.ToArray();

            // Output should contain: 2 bytes header + (0x1FE * 2 bytes table) + compressed data
            int expectedMinSize = HuffmanTableHeaderSize + ExpectedTableLength * 2;
            Assert.True(output.Length >= expectedMinSize,
                $"Output ({output.Length}) should be at least {expectedMinSize} bytes (header + table)");
        }

        #endregion

        #region Round-trip Tests

        [Theory]
        [InlineData(32)]
        [InlineData(64)]
        [InlineData(128)]
        [InlineData(256)]
        public void RoundTrip_RandomData_VariousSizes(int size)
        {
            var encoder = new JPKEncodeHFI();
            var decoder = new JPKDecodeHFI();
            byte[] original = TestHelpers.RandomData(size, seed: size * 7);

            // Encode
            using var encodedStream = new MemoryStream();
            encoder.ProcessOnEncode(original, encodedStream, level: 200);
            byte[] encoded = encodedStream.ToArray();

            // Decode
            using var decodeStream = new MemoryStream(encoded);
            byte[] decoded = new byte[original.Length];
            decoder.ProcessOnDecode(decodeStream, decoded, decoded.Length);

            TestHelpers.AssertBytesEqual(original, decoded, $"HFI round-trip random size={size}");
        }

        [Theory]
        [InlineData(64)]
        [InlineData(256)]
        [InlineData(512)]
        public void RoundTrip_RepetitiveData_VariousSizes(int size)
        {
            var encoder = new JPKEncodeHFI();
            var decoder = new JPKDecodeHFI();
            byte[] original = TestHelpers.RepetitiveData(size);

            // Encode
            using var encodedStream = new MemoryStream();
            encoder.ProcessOnEncode(original, encodedStream, level: 200);
            byte[] encoded = encodedStream.ToArray();

            // Decode
            using var decodeStream = new MemoryStream(encoded);
            byte[] decoded = new byte[original.Length];
            decoder.ProcessOnDecode(decodeStream, decoded, decoded.Length);

            TestHelpers.AssertBytesEqual(original, decoded, $"HFI round-trip repetitive size={size}");
        }

        [Theory]
        [InlineData(128)]
        [InlineData(256)]
        public void RoundTrip_MixedData_VariousSizes(int size)
        {
            var encoder = new JPKEncodeHFI();
            var decoder = new JPKDecodeHFI();
            byte[] original = TestHelpers.MixedData(size, seed: size * 11);

            // Encode
            using var encodedStream = new MemoryStream();
            encoder.ProcessOnEncode(original, encodedStream, level: 200);
            byte[] encoded = encodedStream.ToArray();

            // Decode
            using var decodeStream = new MemoryStream(encoded);
            byte[] decoded = new byte[original.Length];
            decoder.ProcessOnDecode(decodeStream, decoded, decoded.Length);

            TestHelpers.AssertBytesEqual(original, decoded, $"HFI round-trip mixed size={size}");
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void RoundTrip_AllZeros()
        {
            var encoder = new JPKEncodeHFI();
            var decoder = new JPKDecodeHFI();
            byte[] original = new byte[128];

            // Encode
            using var encodedStream = new MemoryStream();
            encoder.ProcessOnEncode(original, encodedStream, level: 200);
            byte[] encoded = encodedStream.ToArray();

            // Decode
            using var decodeStream = new MemoryStream(encoded);
            byte[] decoded = new byte[original.Length];
            decoder.ProcessOnDecode(decodeStream, decoded, decoded.Length);

            TestHelpers.AssertBytesEqual(original, decoded, "HFI round-trip all-zeros");
        }

        [Fact]
        public void RoundTrip_SequentialBytes()
        {
            var encoder = new JPKEncodeHFI();
            var decoder = new JPKDecodeHFI();
            byte[] original = new byte[256];
            for (int i = 0; i < 256; i++)
                original[i] = (byte)i;

            // Encode
            using var encodedStream = new MemoryStream();
            encoder.ProcessOnEncode(original, encodedStream, level: 200);
            byte[] encoded = encodedStream.ToArray();

            // Decode
            using var decodeStream = new MemoryStream(encoded);
            byte[] decoded = new byte[original.Length];
            decoder.ProcessOnDecode(decodeStream, decoded, decoded.Length);

            TestHelpers.AssertBytesEqual(original, decoded, "HFI round-trip sequential");
        }

        #endregion
    }
}
