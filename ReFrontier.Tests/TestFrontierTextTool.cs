using System.Text;

using Xunit;

using FrontierTextTool;

namespace ReFrontier.Tests
{
    /// <summary>
    /// Tests for FrontierTextTool utility methods.
    /// </summary>
    public class TestFrontierTextTool
    {
        static TestFrontierTextTool()
        {
            // Register Shift-JIS encoding provider
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        #region GetNullterminatedStringLength Tests

        [Fact]
        public void GetNullterminatedStringLength_EmptyString_ReturnsOne()
        {
            // Empty string + null terminator = 1 byte
            int result = FrontierTextTool.Program.GetNullterminatedStringLength("");
            Assert.Equal(1, result);
        }

        [Fact]
        public void GetNullterminatedStringLength_SingleAsciiChar_ReturnsTwo()
        {
            // 'A' (1 byte) + null terminator (1 byte) = 2 bytes
            int result = FrontierTextTool.Program.GetNullterminatedStringLength("A");
            Assert.Equal(2, result);
        }

        [Fact]
        public void GetNullterminatedStringLength_AsciiString_ReturnsCorrectLength()
        {
            // "Hello" = 5 bytes + 1 null = 6 bytes
            int result = FrontierTextTool.Program.GetNullterminatedStringLength("Hello");
            Assert.Equal(6, result);
        }

        [Fact]
        public void GetNullterminatedStringLength_SingleJapaneseChar_ReturnsThree()
        {
            // Japanese characters in Shift-JIS are typically 2 bytes
            // One character (2 bytes) + null (1 byte) = 3 bytes
            int result = FrontierTextTool.Program.GetNullterminatedStringLength("あ");
            Assert.Equal(3, result);
        }

        [Fact]
        public void GetNullterminatedStringLength_JapaneseString_ReturnsCorrectLength()
        {
            // "こんにちは" = 5 Japanese chars * 2 bytes each = 10 bytes + 1 null = 11 bytes
            int result = FrontierTextTool.Program.GetNullterminatedStringLength("こんにちは");
            Assert.Equal(11, result);
        }

        [Fact]
        public void GetNullterminatedStringLength_MixedString_ReturnsCorrectLength()
        {
            // "Hello世界" = 5 ASCII (5 bytes) + 2 Japanese (4 bytes) + null (1 byte) = 10 bytes
            int result = FrontierTextTool.Program.GetNullterminatedStringLength("Hello世界");
            Assert.Equal(10, result);
        }

        [Theory]
        [InlineData("Test", 5)]   // 4 + 1
        [InlineData("AB", 3)]     // 2 + 1
        [InlineData("123", 4)]    // 3 + 1
        [InlineData(" ", 2)]      // 1 + 1 (space)
        public void GetNullterminatedStringLength_VariousAscii_ReturnsCorrectLength(string input, int expected)
        {
            int result = FrontierTextTool.Program.GetNullterminatedStringLength(input);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void GetNullterminatedStringLength_StringWithNewline_IncludesNewline()
        {
            // "Hi\n" = 2 ASCII + 1 newline + 1 null = 4 bytes
            int result = FrontierTextTool.Program.GetNullterminatedStringLength("Hi\n");
            Assert.Equal(4, result);
        }

        [Fact]
        public void GetNullterminatedStringLength_StringWithTab_IncludesTab()
        {
            // "A\tB" = 3 bytes + 1 null = 4 bytes
            int result = FrontierTextTool.Program.GetNullterminatedStringLength("A\tB");
            Assert.Equal(4, result);
        }

        [Fact]
        public void GetNullterminatedStringLength_FullWidthPunctuation_ReturnsCorrectLength()
        {
            // Full-width punctuation in Shift-JIS is 2 bytes each
            // "。" (period) = 2 bytes + 1 null = 3 bytes
            int result = FrontierTextTool.Program.GetNullterminatedStringLength("。");
            Assert.Equal(3, result);
        }

        [Fact]
        public void GetNullterminatedStringLength_Katakana_ReturnsCorrectLength()
        {
            // "カタカナ" = 4 chars * 2 bytes = 8 bytes + 1 null = 9 bytes
            int result = FrontierTextTool.Program.GetNullterminatedStringLength("カタカナ");
            Assert.Equal(9, result);
        }

        #endregion

        #region CleanTradosText Tests

        [Fact]
        public void CleanTradosText_EmptyString_ReturnsEmpty()
        {
            string result = FrontierTextTool.Program.CleanTradosText("");
            Assert.Equal("", result);
        }

        [Fact]
        public void CleanTradosText_NoReplacements_ReturnsSameString()
        {
            string input = "Hello World";
            string result = FrontierTextTool.Program.CleanTradosText(input);
            Assert.Equal(input, result);
        }

        [Fact]
        public void CleanTradosText_ColonTilde_RemovesSpace()
        {
            string result = FrontierTextTool.Program.CleanTradosText(": ~test");
            Assert.Equal(":~test", result);
        }

        [Fact]
        public void CleanTradosText_JapanesePeriod_RemovesSpace()
        {
            string result = FrontierTextTool.Program.CleanTradosText("文章。 次の文");
            Assert.Equal("文章。次の文", result);
        }

        [Fact]
        public void CleanTradosText_JapaneseExclamation_RemovesSpace()
        {
            string result = FrontierTextTool.Program.CleanTradosText("やった！ 成功");
            Assert.Equal("やった！成功", result);
        }

        [Fact]
        public void CleanTradosText_JapaneseQuestion_RemovesSpace()
        {
            string result = FrontierTextTool.Program.CleanTradosText("なに？ 何が");
            Assert.Equal("なに？何が", result);
        }

        [Fact]
        public void CleanTradosText_JapaneseColon_RemovesSpace()
        {
            string result = FrontierTextTool.Program.CleanTradosText("項目： 説明");
            Assert.Equal("項目：説明", result);
        }

        [Fact]
        public void CleanTradosText_JapaneseDot_RemovesSpace()
        {
            string result = FrontierTextTool.Program.CleanTradosText("Ａ． Ｂ");
            Assert.Equal("Ａ．Ｂ", result);
        }

        [Fact]
        public void CleanTradosText_JapaneseRightQuote_RemovesSpace()
        {
            string result = FrontierTextTool.Program.CleanTradosText("言葉」 と");
            Assert.Equal("言葉」と", result);
        }

        [Fact]
        public void CleanTradosText_JapaneseLeftQuote_RemovesSpace()
        {
            string result = FrontierTextTool.Program.CleanTradosText("「 言葉");
            Assert.Equal("「言葉", result);
        }

        [Fact]
        public void CleanTradosText_JapaneseRightParen_RemovesSpace()
        {
            string result = FrontierTextTool.Program.CleanTradosText("内容） 後");
            Assert.Equal("内容）後", result);
        }

        [Fact]
        public void CleanTradosText_JapaneseLeftParen_RemovesSpace()
        {
            string result = FrontierTextTool.Program.CleanTradosText("（ 内容");
            Assert.Equal("（内容", result);
        }

        [Fact]
        public void CleanTradosText_MultipleReplacements_AppliesAll()
        {
            string input = "やった！ 成功。 次は？ 「 引用」 です";
            string expected = "やった！成功。次は？「引用」です";
            string result = FrontierTextTool.Program.CleanTradosText(input);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void CleanTradosText_PreservesNormalSpaces_BetweenAscii()
        {
            // Normal ASCII spaces should not be affected
            string input = "Hello World Test";
            string result = FrontierTextTool.Program.CleanTradosText(input);
            Assert.Equal(input, result);
        }

        [Fact]
        public void CleanTradosText_PreservesNewlines_InText()
        {
            string input = "Line1\nLine2。 Next";
            string expected = "Line1\nLine2。Next";
            string result = FrontierTextTool.Program.CleanTradosText(input);
            Assert.Equal(expected, result);
        }

        #endregion
    }
}
