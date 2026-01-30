using ReFrontier.Jpk;
using Xunit;

namespace ReFrontier.Tests
{
    /// <summary>
    /// Tests for JPKEncodeHFI and JPKDecodeHFI (Huffman + LZ compression).
    /// Note: HFI uses randomization in table generation, so some tests verify
    /// structural properties rather than exact byte equality across runs.
    /// </summary>
    public class TestJpkHfi
    {
        private const int HuffmanTableHeaderSize = 2; // Int16 for table length
        private const short ExpectedTableLength = 0x1FE;

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
            decoder.ProcessOnDecode(decodeStream, decoded);

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
            decoder.ProcessOnDecode(decodeStream, decoded);

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
            decoder.ProcessOnDecode(decodeStream, decoded);

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
            decoder.ProcessOnDecode(decodeStream, decoded);

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
            decoder.ProcessOnDecode(decodeStream, decoded);

            TestHelpers.AssertBytesEqual(original, decoded, "HFI round-trip sequential");
        }

        #endregion
    }
}
