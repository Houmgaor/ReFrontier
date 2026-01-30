using System.Text;

using LibReFrontier;

namespace ReFrontier.Tests
{
    /// <summary>
    /// Tests for LibReFrontier.FileOperations class.
    /// Uses MemoryStream to avoid actual file I/O.
    /// </summary>
    public class TestFileOperations
    {
        #region ReadNullterminatedString Tests

        [Fact]
        public void ReadNullterminatedString_EmptyStream_ReturnsEmptyString()
        {
            using var ms = new MemoryStream([]);
            using var br = new BinaryReader(ms);

            string result = FileOperations.ReadNullterminatedString(br, Encoding.UTF8);

            Assert.Equal("", result);
        }

        [Fact]
        public void ReadNullterminatedString_SingleCharThenNull_ReturnsSingleChar()
        {
            byte[] data = [(byte)'A', 0x00];
            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);

            string result = FileOperations.ReadNullterminatedString(br, Encoding.UTF8);

            Assert.Equal("A", result);
        }

        [Fact]
        public void ReadNullterminatedString_StringWithoutNullTerminator_DropsLastByte()
        {
            // Stream reaches EOF before null terminator.
            // Implementation: reads first byte, then loops while byte != 0 AND pos != length.
            // When pos == length, the last byte read is NOT added to the list.
            byte[] data = [(byte)'H', (byte)'i'];
            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);

            string result = FileOperations.ReadNullterminatedString(br, Encoding.UTF8);

            // Only 'H' is added; 'i' is read but not added because pos == length
            Assert.Equal("H", result);
        }

        [Fact]
        public void ReadNullterminatedString_NullTerminatedString_ReturnsUpToNull()
        {
            byte[] data = [(byte)'H', (byte)'e', (byte)'l', (byte)'l', (byte)'o', 0x00, (byte)'X'];
            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);

            string result = FileOperations.ReadNullterminatedString(br, Encoding.UTF8);

            Assert.Equal("Hello", result);
        }

        [Fact]
        public void ReadNullterminatedString_JustNull_ReturnsEmptyString()
        {
            byte[] data = [0x00];
            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);

            string result = FileOperations.ReadNullterminatedString(br, Encoding.UTF8);

            Assert.Equal("", result);
        }

        [Fact]
        public void ReadNullterminatedString_UTF8MultibyteChars_DecodesCorrectly()
        {
            // UTF-8 encoded "é" is 0xC3 0xA9
            byte[] data = [0xC3, 0xA9, 0x00];
            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);

            string result = FileOperations.ReadNullterminatedString(br, Encoding.UTF8);

            Assert.Equal("é", result);
        }

        [Fact]
        public void ReadNullterminatedString_ShiftJIS_DecodesCorrectly()
        {
            // Register encoding provider for Shift-JIS
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Encoding shiftJis = Encoding.GetEncoding("shift_jis");

            // Shift-JIS encoded "あ" is 0x82 0xA0
            byte[] data = [0x82, 0xA0, 0x00];
            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);

            string result = FileOperations.ReadNullterminatedString(br, shiftJis);

            Assert.Equal("あ", result);
        }

        [Fact]
        public void ReadNullterminatedString_ASCIIString_ReturnsCorrectly()
        {
            byte[] data = Encoding.ASCII.GetBytes("Test123\0");
            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);

            string result = FileOperations.ReadNullterminatedString(br, Encoding.ASCII);

            Assert.Equal("Test123", result);
        }

        [Fact]
        public void ReadNullterminatedString_ConsecutiveNulls_ReturnsEmptyOnFirst()
        {
            byte[] data = [0x00, 0x00, (byte)'A'];
            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);

            string result = FileOperations.ReadNullterminatedString(br, Encoding.UTF8);

            Assert.Equal("", result);
        }

        [Fact]
        public void ReadNullterminatedString_LongerStringWithNull_StopsAtNull()
        {
            // Longer string to test the loop properly
            byte[] data = [(byte)'T', (byte)'e', (byte)'s', (byte)'t', 0x00, (byte)'!'];
            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);

            string result = FileOperations.ReadNullterminatedString(br, Encoding.UTF8);

            Assert.Equal("Test", result);
        }

        #endregion
    }
}
