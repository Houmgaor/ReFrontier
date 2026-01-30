using ReFrontier.Jpk;

namespace ReFrontier.Tests
{
    /// <summary>
    /// Tests for HFIRW (Huffman + byte-by-byte RW) encoding and decoding.
    /// </summary>
    public class TestJpkHfirw
    {
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
            decoder.ProcessOnDecode(decodeStream, decoded);

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
            decoder.ProcessOnDecode(decodeStream, decoded);

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
            decoder.ProcessOnDecode(decodeStream, decoded);

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
            decoder.ProcessOnDecode(decodeStream, decoded);

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
            decoder.ProcessOnDecode(decodeStream, decoded);

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
            decoder.ProcessOnDecode(decodeStream, decoded);

            // Assert
            Assert.Equal(original, decoded);
        }

        #endregion
    }
}
