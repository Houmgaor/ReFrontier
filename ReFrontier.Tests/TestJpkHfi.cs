using System;
using System.IO;

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
        /// SHA-256 of the 1022-byte Huffman table emitted for the fixed input below
        /// (2-byte length plus 510 entries). Pinning it detects platform or runtime
        /// differences in tree construction that the multi-OS CI would otherwise miss.
        /// Update this only when the tree-building algorithm is meant to change.
        /// </summary>
        private const string ExpectedTableHash =
            "64ba8bb6a45f2973ccd49045de80618880643f483824ef465f94e75959a991e2";

        private const int HuffmanTableSize = 1022;

        private static byte[] Encode(byte[] input, int level = 50)
        {
            var encoder = new JPKEncodeHFI();
            using var outStream = new MemoryStream();
            encoder.ProcessOnEncode(input, outStream, level);
            return outStream.ToArray();
        }

        private static byte[] EncodeLz(byte[] input, int level = 50)
        {
            var encoder = new JPKEncodeLz();
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
        public void EncodeHFI_TableDependsOnLzOutputFrequencies()
        {
            byte[] fromOneInput = Encode(TestHelpers.RandomData(2048, seed: 1))[..HuffmanTableSize];
            byte[] fromAnother = Encode(TestHelpers.RandomData(3000, seed: 2))[..HuffmanTableSize];

            Assert.False(fromOneInput.AsSpan().SequenceEqual(fromAnother));
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
        public void EncodeHFI_SkewedInputUsesVariableLengthCodes()
        {
            byte[] encoded = Encode(new byte[4096], level: 200);

            int[] codeLengths = TestHelpers.ReadHuffmanCodeLengths(encoded);

            Assert.Contains(codeLengths, length => length != codeLengths[0]);
            Assert.All(codeLengths, length => Assert.InRange(length, 1, 30));
        }

        [Fact]
        public void EncodeHFI_SkewedInputShrinksLzPayload()
        {
            byte[] input = new byte[4096];

            byte[] lz = EncodeLz(input, level: 200);
            byte[] hfi = Encode(input, level: 200);
            int huffmanPayloadLength = hfi.Length - HuffmanTableSize;

            Assert.True(
                huffmanPayloadLength < lz.Length,
                $"Huffman payload ({huffmanPayloadLength}) should be smaller than LZ output ({lz.Length})."
            );
        }

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
