using System;
using System.IO;

using ReFrontier.Jpk;

namespace ReFrontier.Tests
{
    /// <summary>
    /// Tests for HFIRW (Huffman + byte-by-byte RW) encoding and decoding.
    /// </summary>
    public class TestJpkHfirw
    {
        private const int HuffmanTableHeaderSize = 2;

        private static int[] ReadCodeLengths(byte[] encoded)
        {
            short tableLength = BitConverter.ToInt16(encoded, 0);
            short[] table = new short[tableLength];
            for (int i = 0; i < table.Length; i++)
                table[i] = BitConverter.ToInt16(encoded, HuffmanTableHeaderSize + i * 2);

            int[] codeLengths = new int[256];

            void WalkTree(int value, int depth)
            {
                if (value < 0x100)
                {
                    codeLengths[value] = depth;
                    return;
                }

                int childIndex = (value - 0x100) * 2;
                WalkTree(table[childIndex], depth + 1);
                WalkTree(table[childIndex + 1], depth + 1);
            }

            WalkTree(tableLength, 0);
            return codeLengths;
        }

        #region Decoder Tests

        [Fact]
        public void JPKDecodeHFIRW_CanBeInstantiated()
        {
            var decoder = new JPKDecodeHFIRW();
            Assert.NotNull(decoder);
        }

        [Fact]
        public void JPKDecodeHFIRW_InheritsFromJPKDecodeHFI()
        {
            var decoder = new JPKDecodeHFIRW();
            Assert.IsAssignableFrom<JPKDecodeHFI>(decoder);
        }

        [Fact]
        public void JPKDecodeHFIRW_ImplementsIJPKDecode()
        {
            var decoder = new JPKDecodeHFIRW();
            Assert.IsAssignableFrom<IJPKDecode>(decoder);
        }

        #endregion

        #region Encoder Tests

        [Fact]
        public void JPKEncodeHFIRW_CanBeInstantiated()
        {
            var encoder = new JPKEncodeHFIRW();
            Assert.NotNull(encoder);
        }

        [Fact]
        public void JPKEncodeHFIRW_InheritsFromJPKEncodeHFI()
        {
            var encoder = new JPKEncodeHFIRW();
            Assert.IsAssignableFrom<JPKEncodeHFI>(encoder);
        }

        [Fact]
        public void JPKEncodeHFIRW_ImplementsIJPKEncode()
        {
            var encoder = new JPKEncodeHFIRW();
            Assert.IsAssignableFrom<IJPKEncode>(encoder);
        }

        [Fact]
        public void HFIRW_FibonacciDistributionLimitsCodeLength()
        {
            // With the 232 unused symbols, this distribution produces a 32-bit
            // unconstrained Huffman tree and therefore exercises the 30-bit limiter.
            int[] frequencies = new int[24];
            frequencies[0] = 1;
            frequencies[1] = 1;
            int totalLength = 2;
            for (int i = 2; i < frequencies.Length; i++)
            {
                frequencies[i] = frequencies[i - 1] + frequencies[i - 2];
                totalLength += frequencies[i];
            }

            byte[] original = new byte[totalLength];
            int offset = 0;
            for (int symbol = 0; symbol < frequencies.Length; symbol++)
            {
                Array.Fill(original, (byte)symbol, offset, frequencies[symbol]);
                offset += frequencies[symbol];
            }

            var encoder = new JPKEncodeHFIRW();
            using var encodedStream = new MemoryStream();
            encoder.ProcessOnEncode(original, encodedStream);
            byte[] encoded = encodedStream.ToArray();

            int maximumCodeLength = 0;
            foreach (int codeLength in ReadCodeLengths(encoded))
                maximumCodeLength = Math.Max(maximumCodeLength, codeLength);

            Assert.Equal(30, maximumCodeLength);

            var decoder = new JPKDecodeHFIRW();
            byte[] decoded = new byte[original.Length];
            using var decodeStream = new MemoryStream(encoded);
            decoder.ProcessOnDecode(decodeStream, decoded, decoded.Length);
            Assert.Equal(original, decoded);
        }

        #endregion

        #region Round-Trip Tests

        [Fact]
        public void HFIRW_RoundTrip_SmallData_PreservesContent()
        {
            // Arrange
            byte[] original = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07 };
            var encoder = new JPKEncodeHFIRW();
            var decoder = new JPKDecodeHFIRW();

            // Act - Encode
            using var encodedStream = new MemoryStream();
            encoder.ProcessOnEncode(original, encodedStream);
            byte[] encoded = encodedStream.ToArray();

            // Act - Decode
            byte[] decoded = new byte[original.Length];
            using var decodeStream = new MemoryStream(encoded);
            decoder.ProcessOnDecode(decodeStream, decoded, decoded.Length);

            // Assert
            Assert.Equal(original, decoded);
        }

        [Fact]
        public void HFIRW_RoundTrip_RepeatingPattern_PreservesContent()
        {
            // Arrange - Data with repeating pattern
            byte[] original = new byte[64];
            for (int i = 0; i < original.Length; i++)
                original[i] = (byte)(i % 16);

            var encoder = new JPKEncodeHFIRW();
            var decoder = new JPKDecodeHFIRW();

            // Act - Encode
            using var encodedStream = new MemoryStream();
            encoder.ProcessOnEncode(original, encodedStream);
            byte[] encoded = encodedStream.ToArray();

            // Act - Decode
            byte[] decoded = new byte[original.Length];
            using var decodeStream = new MemoryStream(encoded);
            decoder.ProcessOnDecode(decodeStream, decoded, decoded.Length);

            // Assert
            Assert.Equal(original, decoded);
        }

        [Fact]
        public void HFIRW_RoundTrip_RandomData_PreservesContent()
        {
            // Arrange - Random-like data
            byte[] original = TestHelpers.RandomData(128, seed: 42);
            var encoder = new JPKEncodeHFIRW();
            var decoder = new JPKDecodeHFIRW();

            // Act - Encode
            using var encodedStream = new MemoryStream();
            encoder.ProcessOnEncode(original, encodedStream);
            byte[] encoded = encodedStream.ToArray();

            // Act - Decode
            byte[] decoded = new byte[original.Length];
            using var decodeStream = new MemoryStream(encoded);
            decoder.ProcessOnDecode(decodeStream, decoded, decoded.Length);

            // Assert
            Assert.Equal(original, decoded);
        }

        [Fact]
        public void HFIRW_RoundTrip_AllByteValues_PreservesContent()
        {
            // Arrange - All possible byte values
            byte[] original = new byte[256];
            for (int i = 0; i < 256; i++)
                original[i] = (byte)i;

            var encoder = new JPKEncodeHFIRW();
            var decoder = new JPKDecodeHFIRW();

            // Act - Encode
            using var encodedStream = new MemoryStream();
            encoder.ProcessOnEncode(original, encodedStream);
            byte[] encoded = encodedStream.ToArray();

            // Act - Decode
            byte[] decoded = new byte[original.Length];
            using var decodeStream = new MemoryStream(encoded);
            decoder.ProcessOnDecode(decodeStream, decoded, decoded.Length);

            // Assert
            Assert.Equal(original, decoded);
        }

        [Fact]
        public void HFIRW_RoundTrip_EmptyData_PreservesContent()
        {
            // Arrange
            byte[] original = new byte[0];
            var encoder = new JPKEncodeHFIRW();
            var decoder = new JPKDecodeHFIRW();

            // Act - Encode
            using var encodedStream = new MemoryStream();
            encoder.ProcessOnEncode(original, encodedStream);
            byte[] encoded = encodedStream.ToArray();

            // Act - Decode
            byte[] decoded = new byte[original.Length];
            using var decodeStream = new MemoryStream(encoded);
            decoder.ProcessOnDecode(decodeStream, decoded, decoded.Length);

            // Assert
            Assert.Equal(original, decoded);
        }

        [Fact]
        public void HFIRW_RoundTrip_SingleByte_PreservesContent()
        {
            // Arrange
            byte[] original = new byte[] { 0x42 };
            var encoder = new JPKEncodeHFIRW();
            var decoder = new JPKDecodeHFIRW();

            // Act - Encode
            using var encodedStream = new MemoryStream();
            encoder.ProcessOnEncode(original, encodedStream);
            byte[] encoded = encodedStream.ToArray();

            // Act - Decode
            byte[] decoded = new byte[original.Length];
            using var decodeStream = new MemoryStream(encoded);
            decoder.ProcessOnDecode(decodeStream, decoded, decoded.Length);

            // Assert
            Assert.Equal(original, decoded);
        }

        #endregion
    }
}
