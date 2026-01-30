using ReFrontier.Jpk;
using Xunit;

namespace ReFrontier.Tests
{
    /// <summary>
    /// Tests for JPKEncodeRW and JPKDecodeRW (raw/no compression).
    /// </summary>
    public class TestJpkRW
    {
        #region Encode Tests

        [Fact]
        public void EncodeRW_EmptyInput_ProducesEmptyOutput()
        {
            var encoder = new JPKEncodeRW();
            byte[] input = TestHelpers.EmptyData();

            using var outStream = new MemoryStream();
            encoder.ProcessOnEncode(input, outStream);

            Assert.Equal(0, outStream.Length);
        }

        [Fact]
        public void EncodeRW_SingleByte_OutputMatchesInput()
        {
            var encoder = new JPKEncodeRW();
            byte[] input = TestHelpers.SingleByte(0xAB);

            using var outStream = new MemoryStream();
            encoder.ProcessOnEncode(input, outStream);

            byte[] output = outStream.ToArray();
            TestHelpers.AssertBytesEqual(input, output, "RW encode single byte");
        }

        [Theory]
        [InlineData(16)]
        [InlineData(64)]
        [InlineData(256)]
        [InlineData(1024)]
        public void EncodeRW_VariousSizes_OutputMatchesInput(int size)
        {
            var encoder = new JPKEncodeRW();
            byte[] input = TestHelpers.RandomData(size, seed: size);

            using var outStream = new MemoryStream();
            encoder.ProcessOnEncode(input, outStream);

            byte[] output = outStream.ToArray();
            TestHelpers.AssertBytesEqual(input, output, $"RW encode size={size}");
        }

        #endregion

        #region Decode Tests

        [Fact]
        public void DecodeRW_EmptyInput_ProducesEmptyOutput()
        {
            var decoder = new JPKDecodeRW();
            using var inStream = new MemoryStream(TestHelpers.EmptyData());
            byte[] output = TestHelpers.EmptyData();

            decoder.ProcessOnDecode(inStream, output);

            Assert.Empty(output);
        }

        [Fact]
        public void DecodeRW_SingleByte_OutputMatchesInput()
        {
            var decoder = new JPKDecodeRW();
            byte[] input = TestHelpers.SingleByte(0xCD);

            using var inStream = new MemoryStream(input);
            byte[] output = new byte[1];
            decoder.ProcessOnDecode(inStream, output);

            TestHelpers.AssertBytesEqual(input, output, "RW decode single byte");
        }

        [Theory]
        [InlineData(16)]
        [InlineData(64)]
        [InlineData(256)]
        [InlineData(1024)]
        public void DecodeRW_VariousSizes_OutputMatchesInput(int size)
        {
            var decoder = new JPKDecodeRW();
            byte[] input = TestHelpers.RandomData(size, seed: size);

            using var inStream = new MemoryStream(input);
            byte[] output = new byte[size];
            decoder.ProcessOnDecode(inStream, output);

            TestHelpers.AssertBytesEqual(input, output, $"RW decode size={size}");
        }

        [Fact]
        public void DecodeRW_OutputSmallerThanInput_TruncatesCorrectly()
        {
            var decoder = new JPKDecodeRW();
            byte[] input = TestHelpers.RandomData(100, seed: 999);

            using var inStream = new MemoryStream(input);
            byte[] output = new byte[50]; // Only read first 50 bytes
            decoder.ProcessOnDecode(inStream, output);

            byte[] expectedPart = new byte[50];
            Array.Copy(input, 0, expectedPart, 0, 50);
            TestHelpers.AssertBytesEqual(expectedPart, output, "RW decode truncated");
        }

        #endregion

        #region Round-trip Tests

        [Theory]
        [InlineData(1)]
        [InlineData(16)]
        [InlineData(64)]
        [InlineData(256)]
        [InlineData(1024)]
        public void RoundTrip_VariousSizes_RecoversOriginal(int size)
        {
            var encoder = new JPKEncodeRW();
            var decoder = new JPKDecodeRW();
            byte[] original = TestHelpers.RandomData(size, seed: size * 2);

            // Encode
            using var encodedStream = new MemoryStream();
            encoder.ProcessOnEncode(original, encodedStream);
            byte[] encoded = encodedStream.ToArray();

            // Decode
            using var decodeStream = new MemoryStream(encoded);
            byte[] decoded = new byte[original.Length];
            decoder.ProcessOnDecode(decodeStream, decoded);

            TestHelpers.AssertBytesEqual(original, decoded, $"RW round-trip size={size}");
        }

        [Fact]
        public void RoundTrip_RepetitiveData_RecoversOriginal()
        {
            var encoder = new JPKEncodeRW();
            var decoder = new JPKDecodeRW();
            byte[] original = TestHelpers.RepetitiveData(512);

            // Encode
            using var encodedStream = new MemoryStream();
            encoder.ProcessOnEncode(original, encodedStream);
            byte[] encoded = encodedStream.ToArray();

            // Decode
            using var decodeStream = new MemoryStream(encoded);
            byte[] decoded = new byte[original.Length];
            decoder.ProcessOnDecode(decodeStream, decoded);

            TestHelpers.AssertBytesEqual(original, decoded, "RW round-trip repetitive");
        }

        #endregion
    }
}
