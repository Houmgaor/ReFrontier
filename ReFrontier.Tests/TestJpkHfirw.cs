using ReFrontier.Jpk;
using Xunit;

namespace ReFrontier.Tests
{
    /// <summary>
    /// Tests for JPKDecodeHFIRW (Huffman + byte-by-byte RW decompression).
    ///
    /// Note: HFIRW is a decode-only format with no corresponding encoder in this tool.
    /// It's used for decompressing specific game files. Full round-trip tests would
    /// require actual game data. These tests verify the decoder can be instantiated
    /// and inherits correctly from JPKDecodeHFI.
    /// </summary>
    public class TestJpkHfirw
    {
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
    }
}
