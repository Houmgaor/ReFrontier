using ReFrontier.Jpk;

namespace ReFrontier.Tests
{
    /// <summary>
    /// Tests for JPKEncodeRW and JPKDecodeRW (raw/no-compression codec).
    /// RW is a passthrough codec that reads/writes bytes without compression.
    /// </summary>
    public class TestJpkRw
    {
        #region Encode Tests

        [Fact]
        public void EncodeRW_EmptyInput_ProducesEmptyOutput()
        {
            var encoder = new JPKEncodeRW();
            byte[] input = TestHelpers.EmptyData();

            using var outStream = new MemoryStream();
            encoder.ProcessOnEncode(input, outStream, level: 100);

            Assert.Equal(0, outStream.Length);
        }

        [Fact]
        public void EncodeRW_SingleByte_ProducesIdenticalOutput()
        {
            var encoder = new JPKEncodeRW();
            byte[] input = TestHelpers.SingleByte(0x42);

            using var outStream = new MemoryStream();
            encoder.ProcessOnEncode(input, outStream, level: 100);

            byte[] output = outStream.ToArray();
            TestHelpers.AssertBytesEqual(input, output, "RW encode single byte");
        }

        [Fact]
        public void EncodeRW_RandomData_ProducesIdenticalOutput()
        {
            var encoder = new JPKEncodeRW();
            byte[] input = TestHelpers.RandomData(256, seed: 42);

            using var outStream = new MemoryStream();
            encoder.ProcessOnEncode(input, outStream, level: 100);

            byte[] output = outStream.ToArray();
            TestHelpers.AssertBytesEqual(input, output, "RW encode random data");
        }

        [Fact]
        public void EncodeRW_LevelParameterIgnored()
        {
            // RW doesn't use the level parameter, so different levels should produce identical output
            var encoder1 = new JPKEncodeRW();
            var encoder2 = new JPKEncodeRW();
            byte[] input = TestHelpers.RandomData(128, seed: 99);

            using var stream1 = new MemoryStream();
            encoder1.ProcessOnEncode(input, stream1, level: 1);

            using var stream2 = new MemoryStream();
            encoder2.ProcessOnEncode(input, stream2, level: 10000);

            TestHelpers.AssertBytesEqual(stream1.ToArray(), stream2.ToArray(), "RW level independence");
        }

        #endregion

        #region Decode Tests

        [Fact]
        public void DecodeRW_EmptyStream_ProducesEmptyBuffer()
        {
            var decoder = new JPKDecodeRW();
            byte[] outBuffer = new byte[0];

            using var inStream = new MemoryStream([]);
            decoder.ProcessOnDecode(inStream, outBuffer);

            Assert.Empty(outBuffer);
        }

        [Fact]
        public void DecodeRW_SingleByte_ReadsCorrectly()
        {
            var decoder = new JPKDecodeRW();
            byte[] input = [0x42];
            byte[] outBuffer = new byte[1];

            using var inStream = new MemoryStream(input);
            decoder.ProcessOnDecode(inStream, outBuffer);

            Assert.Equal(0x42, outBuffer[0]);
        }

        [Fact]
        public void DecodeRW_StreamShorterThanBuffer_FillsPartially()
        {
            // When stream is shorter than output buffer, decoder should stop at stream end
            var decoder = new JPKDecodeRW();
            byte[] input = [0x01, 0x02, 0x03];
            byte[] outBuffer = new byte[10];

            using var inStream = new MemoryStream(input);
            decoder.ProcessOnDecode(inStream, outBuffer);

            // First 3 bytes should match input
            Assert.Equal(0x01, outBuffer[0]);
            Assert.Equal(0x02, outBuffer[1]);
            Assert.Equal(0x03, outBuffer[2]);
            // Remaining bytes should be 0 (unmodified)
            for (int i = 3; i < outBuffer.Length; i++)
                Assert.Equal(0, outBuffer[i]);
        }

        [Fact]
        public void DecodeRW_StreamLongerThanBuffer_ReadsOnlyBufferSize()
        {
            var decoder = new JPKDecodeRW();
            byte[] input = [0x01, 0x02, 0x03, 0x04, 0x05];
            byte[] outBuffer = new byte[3];

            using var inStream = new MemoryStream(input);
            decoder.ProcessOnDecode(inStream, outBuffer);

            Assert.Equal(0x01, outBuffer[0]);
            Assert.Equal(0x02, outBuffer[1]);
            Assert.Equal(0x03, outBuffer[2]);
        }

        #endregion

        #region Round-trip Tests

        [Theory]
        [InlineData(1)]
        [InlineData(16)]
        [InlineData(64)]
        [InlineData(256)]
        [InlineData(1024)]
        public void RoundTrip_VariousSizes(int size)
        {
            var encoder = new JPKEncodeRW();
            var decoder = new JPKDecodeRW();
            byte[] original = TestHelpers.RandomData(size, seed: size);

            // Encode
            using var encodedStream = new MemoryStream();
            encoder.ProcessOnEncode(original, encodedStream, level: 100);
            byte[] encoded = encodedStream.ToArray();

            // Decode
            using var decodeStream = new MemoryStream(encoded);
            byte[] decoded = new byte[original.Length];
            decoder.ProcessOnDecode(decodeStream, decoded);

            TestHelpers.AssertBytesEqual(original, decoded, $"RW round-trip size={size}");
        }

        [Fact]
        public void RoundTrip_AllByteValues()
        {
            var encoder = new JPKEncodeRW();
            var decoder = new JPKDecodeRW();
            byte[] original = new byte[256];
            for (int i = 0; i < 256; i++)
                original[i] = (byte)i;

            // Encode
            using var encodedStream = new MemoryStream();
            encoder.ProcessOnEncode(original, encodedStream, level: 100);
            byte[] encoded = encodedStream.ToArray();

            // Decode
            using var decodeStream = new MemoryStream(encoded);
            byte[] decoded = new byte[original.Length];
            decoder.ProcessOnDecode(decodeStream, decoded);

            TestHelpers.AssertBytesEqual(original, decoded, "RW round-trip all byte values");
        }

        #endregion
    }
}
