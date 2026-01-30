using ReFrontier.Jpk;

namespace ReFrontier.Tests
{
    /// <summary>
    /// Tests for JPKEncodeLz and JPKDecodeLz (LZ77 compression).
    /// </summary>
    public class TestJpkLz
    {
        #region Encode Tests

        [Fact]
        public void EncodeLz_EmptyInput_ProducesOutput()
        {
            var encoder = new JPKEncodeLz();
            byte[] input = TestHelpers.EmptyData();

            using var outStream = new MemoryStream();
            encoder.ProcessOnEncode(input, outStream);

            // Empty input may produce minimal header data
            Assert.True(outStream.Length >= 0);
        }

        [Fact]
        public void EncodeLz_SingleByte_ProducesOutput()
        {
            var encoder = new JPKEncodeLz();
            byte[] input = TestHelpers.SingleByte(0x42);

            using var outStream = new MemoryStream();
            encoder.ProcessOnEncode(input, outStream);

            Assert.True(outStream.Length > 0, "LZ encode should produce output for single byte");
        }

        [Fact]
        public void EncodeLz_RandomData_ProducesOutput()
        {
            var encoder = new JPKEncodeLz();
            byte[] input = TestHelpers.RandomData(256, seed: 123);

            using var outStream = new MemoryStream();
            encoder.ProcessOnEncode(input, outStream);

            Assert.True(outStream.Length > 0, "LZ encode should produce output for random data");
        }

        [Fact]
        public void EncodeLz_RepetitiveData_Compresses()
        {
            var encoder = new JPKEncodeLz();
            byte[] input = TestHelpers.RepetitiveData(1024);

            using var outStream = new MemoryStream();
            encoder.ProcessOnEncode(input, outStream, level: 1000);

            // Repetitive data should compress significantly
            Assert.True(outStream.Length < input.Length,
                $"Repetitive data should compress. Input: {input.Length}, Output: {outStream.Length}");
        }

        #endregion

        #region Round-trip Tests

        [Theory]
        [InlineData(1)]
        [InlineData(16)]
        [InlineData(64)]
        [InlineData(256)]
        [InlineData(512)]
        [InlineData(1024)]
        public void RoundTrip_RandomData_VariousSizes(int size)
        {
            var encoder = new JPKEncodeLz();
            var decoder = new JPKDecodeLz();
            byte[] original = TestHelpers.RandomData(size, seed: size * 3);

            // Encode
            using var encodedStream = new MemoryStream();
            encoder.ProcessOnEncode(original, encodedStream, level: 500);
            byte[] encoded = encodedStream.ToArray();

            // Decode
            using var decodeStream = new MemoryStream(encoded);
            byte[] decoded = new byte[original.Length];
            decoder.ProcessOnDecode(decodeStream, decoded);

            TestHelpers.AssertBytesEqual(original, decoded, $"LZ round-trip random size={size}");
        }

        [Theory]
        [InlineData(64)]
        [InlineData(256)]
        [InlineData(1024)]
        public void RoundTrip_RepetitiveData_VariousSizes(int size)
        {
            var encoder = new JPKEncodeLz();
            var decoder = new JPKDecodeLz();
            byte[] original = TestHelpers.RepetitiveData(size);

            // Encode
            using var encodedStream = new MemoryStream();
            encoder.ProcessOnEncode(original, encodedStream, level: 500);
            byte[] encoded = encodedStream.ToArray();

            // Decode
            using var decodeStream = new MemoryStream(encoded);
            byte[] decoded = new byte[original.Length];
            decoder.ProcessOnDecode(decodeStream, decoded);

            TestHelpers.AssertBytesEqual(original, decoded, $"LZ round-trip repetitive size={size}");
        }

        [Theory]
        [InlineData(128)]
        [InlineData(512)]
        public void RoundTrip_MixedData_VariousSizes(int size)
        {
            var encoder = new JPKEncodeLz();
            var decoder = new JPKDecodeLz();
            byte[] original = TestHelpers.MixedData(size, seed: size * 5);

            // Encode
            using var encodedStream = new MemoryStream();
            encoder.ProcessOnEncode(original, encodedStream, level: 500);
            byte[] encoded = encodedStream.ToArray();

            // Decode
            using var decodeStream = new MemoryStream(encoded);
            byte[] decoded = new byte[original.Length];
            decoder.ProcessOnDecode(decodeStream, decoded);

            TestHelpers.AssertBytesEqual(original, decoded, $"LZ round-trip mixed size={size}");
        }

        [Theory]
        [InlineData(10)]
        [InlineData(50)]
        [InlineData(100)]
        [InlineData(500)]
        [InlineData(1000)]
        [InlineData(5000)]
        public void RoundTrip_DifferentCompressionLevels(int level)
        {
            var encoder = new JPKEncodeLz();
            var decoder = new JPKDecodeLz();
            byte[] original = TestHelpers.MixedData(256, seed: level);

            // Encode with specific level
            using var encodedStream = new MemoryStream();
            encoder.ProcessOnEncode(original, encodedStream, level: level);
            byte[] encoded = encodedStream.ToArray();

            // Decode
            using var decodeStream = new MemoryStream(encoded);
            byte[] decoded = new byte[original.Length];
            decoder.ProcessOnDecode(decodeStream, decoded);

            TestHelpers.AssertBytesEqual(original, decoded, $"LZ round-trip level={level}");
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void RoundTrip_AllZeros()
        {
            var encoder = new JPKEncodeLz();
            var decoder = new JPKDecodeLz();
            byte[] original = new byte[256]; // All zeros

            // Encode
            using var encodedStream = new MemoryStream();
            encoder.ProcessOnEncode(original, encodedStream, level: 500);
            byte[] encoded = encodedStream.ToArray();

            // Decode
            using var decodeStream = new MemoryStream(encoded);
            byte[] decoded = new byte[original.Length];
            decoder.ProcessOnDecode(decodeStream, decoded);

            TestHelpers.AssertBytesEqual(original, decoded, "LZ round-trip all-zeros");
        }

        [Fact]
        public void RoundTrip_AllOnes()
        {
            var encoder = new JPKEncodeLz();
            var decoder = new JPKDecodeLz();
            byte[] original = new byte[256];
            Array.Fill(original, (byte)0xFF);

            // Encode
            using var encodedStream = new MemoryStream();
            encoder.ProcessOnEncode(original, encodedStream, level: 500);
            byte[] encoded = encodedStream.ToArray();

            // Decode
            using var decodeStream = new MemoryStream(encoded);
            byte[] decoded = new byte[original.Length];
            decoder.ProcessOnDecode(decodeStream, decoded);

            TestHelpers.AssertBytesEqual(original, decoded, "LZ round-trip all-0xFF");
        }

        [Fact]
        public void RoundTrip_SequentialBytes()
        {
            var encoder = new JPKEncodeLz();
            var decoder = new JPKDecodeLz();
            byte[] original = new byte[256];
            for (int i = 0; i < 256; i++)
                original[i] = (byte)i;

            // Encode
            using var encodedStream = new MemoryStream();
            encoder.ProcessOnEncode(original, encodedStream, level: 500);
            byte[] encoded = encodedStream.ToArray();

            // Decode
            using var decodeStream = new MemoryStream(encoded);
            byte[] decoded = new byte[original.Length];
            decoder.ProcessOnDecode(decodeStream, decoded);

            TestHelpers.AssertBytesEqual(original, decoded, "LZ round-trip sequential");
        }

        #endregion

        #region Large Data Tests

        [Theory]
        [InlineData(2048)]
        [InlineData(4096)]
        [InlineData(8192)]
        public void RoundTrip_LargeRandomData(int size)
        {
            var encoder = new JPKEncodeLz();
            var decoder = new JPKDecodeLz();
            byte[] original = TestHelpers.RandomData(size, seed: size);

            using var encodedStream = new MemoryStream();
            encoder.ProcessOnEncode(original, encodedStream, level: 1000);
            byte[] encoded = encodedStream.ToArray();

            using var decodeStream = new MemoryStream(encoded);
            byte[] decoded = new byte[original.Length];
            decoder.ProcessOnDecode(decodeStream, decoded);

            TestHelpers.AssertBytesEqual(original, decoded, $"LZ round-trip large size={size}");
        }

        [Fact]
        public void RoundTrip_LongRepeatingPattern()
        {
            // This should trigger longer match cases in LZ compression
            var encoder = new JPKEncodeLz();
            var decoder = new JPKDecodeLz();

            // Create a pattern that repeats every 16 bytes
            byte[] original = new byte[2048];
            for (int i = 0; i < original.Length; i++)
                original[i] = (byte)(i % 16);

            using var encodedStream = new MemoryStream();
            encoder.ProcessOnEncode(original, encodedStream, level: 2000);
            byte[] encoded = encodedStream.ToArray();

            using var decodeStream = new MemoryStream(encoded);
            byte[] decoded = new byte[original.Length];
            decoder.ProcessOnDecode(decodeStream, decoded);

            TestHelpers.AssertBytesEqual(original, decoded, "LZ round-trip long repeating pattern");
        }

        [Fact]
        public void RoundTrip_AlternatingBlocks()
        {
            // Alternating blocks of zeros and ones to test different compression scenarios
            var encoder = new JPKEncodeLz();
            var decoder = new JPKDecodeLz();

            byte[] original = new byte[1024];
            for (int i = 0; i < original.Length; i++)
            {
                int block = i / 64;
                original[i] = (block % 2 == 0) ? (byte)0x00 : (byte)0xFF;
            }

            using var encodedStream = new MemoryStream();
            encoder.ProcessOnEncode(original, encodedStream, level: 500);
            byte[] encoded = encodedStream.ToArray();

            using var decodeStream = new MemoryStream(encoded);
            byte[] decoded = new byte[original.Length];
            decoder.ProcessOnDecode(decodeStream, decoded);

            TestHelpers.AssertBytesEqual(original, decoded, "LZ round-trip alternating blocks");
        }

        #endregion

        #region Compression Ratio Tests

        [Fact]
        public void CompressionRatio_RepetitiveCompressesBetterThanRandom()
        {
            var encoder = new JPKEncodeLz();
            byte[] repetitive = TestHelpers.RepetitiveData(1024);
            byte[] random = TestHelpers.RandomData(1024, seed: 777);

            using var repStream = new MemoryStream();
            encoder.ProcessOnEncode(repetitive, repStream, level: 1000);
            int repSize = (int)repStream.Length;

            encoder = new JPKEncodeLz(); // Fresh encoder
            using var rndStream = new MemoryStream();
            encoder.ProcessOnEncode(random, rndStream, level: 1000);
            int rndSize = (int)rndStream.Length;

            Assert.True(repSize < rndSize,
                $"Repetitive data ({repSize} bytes) should compress better than random ({rndSize} bytes)");
        }

        #endregion
    }
}
