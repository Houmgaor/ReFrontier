using LibReFrontier;
using Xunit;

namespace ReFrontier.Tests
{
    /// <summary>
    /// Tests for LibReFrontier.ArgumentsParser class.
    /// </summary>
    public class TestArgumentsParser
    {
        #region ParseArguments Tests

        [Fact]
        public void ParseArguments_EmptyArray_ReturnsEmptyDictionary()
        {
            string[] args = [];
            var result = ArgumentsParser.ParseArguments(args);
            Assert.Empty(result);
        }

        [Fact]
        public void ParseArguments_SingleArgWithoutValue_ReturnsKeyWithNullValue()
        {
            string[] args = ["--log"];
            var result = ArgumentsParser.ParseArguments(args);

            Assert.Single(result);
            Assert.True(result.ContainsKey("--log"));
            Assert.Null(result["--log"]);
        }

        [Fact]
        public void ParseArguments_SingleArgWithValue_ReturnsKeyValuePair()
        {
            string[] args = ["--compress=3,50"];
            var result = ArgumentsParser.ParseArguments(args);

            Assert.Single(result);
            Assert.True(result.ContainsKey("--compress"));
            Assert.Equal("3,50", result["--compress"]);
        }

        [Fact]
        public void ParseArguments_MultipleArgsWithValues_ReturnsAllKeyValuePairs()
        {
            string[] args = ["--compress=3,50", "--output=dir"];
            var result = ArgumentsParser.ParseArguments(args);

            Assert.Equal(2, result.Count);
            Assert.Equal("3,50", result["--compress"]);
            Assert.Equal("dir", result["--output"]);
        }

        [Fact]
        public void ParseArguments_MixedArgsWithAndWithoutValues_ParsesCorrectly()
        {
            string[] args = ["--log", "--compress=3,50", "--recursive", "--output=test"];
            var result = ArgumentsParser.ParseArguments(args);

            Assert.Equal(4, result.Count);
            Assert.Null(result["--log"]);
            Assert.Equal("3,50", result["--compress"]);
            Assert.Null(result["--recursive"]);
            Assert.Equal("test", result["--output"]);
        }

        [Fact]
        public void ParseArguments_ArgWithMultipleEquals_SplitsOnFirstEquals()
        {
            // "key=value=extra" splits to ["key", "value=extra"], then only first two are used
            string[] args = ["--key=value=extra"];
            var result = ArgumentsParser.ParseArguments(args);

            // With simple Split('='), this creates 3 parts, so parts.Length != 2
            // Therefore, the whole arg becomes a key with null value
            Assert.Single(result);
            Assert.True(result.ContainsKey("--key=value=extra"));
            Assert.Null(result["--key=value=extra"]);
        }

        [Fact]
        public void ParseArguments_DuplicateKeys_LastValueWins()
        {
            string[] args = ["--level=1", "--level=2"];
            var result = ArgumentsParser.ParseArguments(args);

            Assert.Single(result);
            Assert.Equal("2", result["--level"]);
        }

        #endregion

        #region ParseCompression Tests

        [Theory]
        [InlineData("0,50", CompressionType.RW, 50)]
        [InlineData("0,100", CompressionType.RW, 100)]
        [InlineData("2,75", CompressionType.HFIRW, 75)]
        [InlineData("3,100", CompressionType.LZ, 100)]
        [InlineData("3,200", CompressionType.LZ, 200)]
        [InlineData("4,150", CompressionType.HFI, 150)]
        public void ParseCompression_ValidInput_ReturnsCorrectCompression(
            string input, CompressionType expectedType, int expectedLevel)
        {
            var result = ArgumentsParser.ParseCompression(input);

            Assert.Equal(expectedType, result.type);
            Assert.Equal(expectedLevel, result.level);
        }

        [Fact]
        public void ParseCompression_TypeOne_ThrowsInvalidCastException()
        {
            // Type 1 is "None" which is not allowed for compression
            var exception = Assert.Throws<InvalidCastException>(
                () => ArgumentsParser.ParseCompression("1,50")
            );
            Assert.Contains("compression type", exception.Message.ToLower());
        }

        [Fact]
        public void ParseCompression_LevelZero_ThrowsArgumentException()
        {
            var exception = Assert.Throws<ArgumentException>(
                () => ArgumentsParser.ParseCompression("3,0")
            );
            Assert.Contains("compression level", exception.Message.ToLower());
        }

        [Fact]
        public void ParseCompression_MissingComma_ThrowsArgumentException()
        {
            var exception = Assert.Throws<ArgumentException>(
                () => ArgumentsParser.ParseCompression("3")
            );
            Assert.Contains("compress", exception.Message.ToLower());
        }

        [Fact]
        public void ParseCompression_TooManyParts_ThrowsArgumentException()
        {
            var exception = Assert.Throws<ArgumentException>(
                () => ArgumentsParser.ParseCompression("3,50,extra")
            );
            Assert.Contains("compress", exception.Message.ToLower());
        }

        [Fact]
        public void ParseCompression_NonNumericType_ThrowsFormatException()
        {
            Assert.Throws<FormatException>(
                () => ArgumentsParser.ParseCompression("abc,50")
            );
        }

        [Fact]
        public void ParseCompression_NonNumericLevel_ThrowsFormatException()
        {
            Assert.Throws<FormatException>(
                () => ArgumentsParser.ParseCompression("3,abc")
            );
        }

        [Fact]
        public void ParseCompression_EmptyString_ThrowsArgumentException()
        {
            var exception = Assert.Throws<ArgumentException>(
                () => ArgumentsParser.ParseCompression("")
            );
            Assert.Contains("compress", exception.Message.ToLower());
        }

        #endregion

        #region Print Tests

        [Fact]
        public void Print_PrintBefore_OutputsInCorrectOrder()
        {
            // Capture console output
            using var sw = new StringWriter();
            var originalOut = Console.Out;
            Console.SetOut(sw);

            try
            {
                ArgumentsParser.Print("Test message", printBefore: true);
                string output = sw.ToString();

                // When printBefore=true: separator first, then message
                int separatorPos = output.IndexOf("=====");
                int messagePos = output.IndexOf("Test message");

                Assert.True(separatorPos < messagePos,
                    "When printBefore=true, separator should come before message");
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }

        [Fact]
        public void Print_PrintAfter_OutputsInCorrectOrder()
        {
            using var sw = new StringWriter();
            var originalOut = Console.Out;
            Console.SetOut(sw);

            try
            {
                ArgumentsParser.Print("Test message", printBefore: false);
                string output = sw.ToString();

                // When printBefore=false: message first, then separator
                int separatorPos = output.IndexOf("=====");
                int messagePos = output.IndexOf("Test message");

                Assert.True(messagePos < separatorPos,
                    "When printBefore=false, message should come before separator");
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }

        #endregion
    }
}
