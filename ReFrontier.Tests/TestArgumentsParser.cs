using LibReFrontier;
using Xunit;

namespace ReFrontier.Tests
{
    /// <summary>
    /// Tests for LibReFrontier.ArgumentsParser class.
    /// </summary>
    public class TestArgumentsParser
    {
        #region ParseCompression Tests (New API with separate type and level)

        [Theory]
        [InlineData("rw", 50, CompressionType.RW, 50)]
        [InlineData("RW", 100, CompressionType.RW, 100)]
        [InlineData("hfirw", 75, CompressionType.HFIRW, 75)]
        [InlineData("HFIRW", 75, CompressionType.HFIRW, 75)]
        [InlineData("lz", 100, CompressionType.LZ, 100)]
        [InlineData("LZ", 200, CompressionType.LZ, 200)]
        [InlineData("hfi", 150, CompressionType.HFI, 150)]
        [InlineData("HFI", 150, CompressionType.HFI, 150)]
        public void ParseCompression_NamedType_ReturnsCorrectCompression(
            string type, int level, CompressionType expectedType, int expectedLevel)
        {
            var result = ArgumentsParser.ParseCompression(type, level);

            Assert.Equal(expectedType, result.type);
            Assert.Equal(expectedLevel, result.level);
        }

        [Theory]
        [InlineData("0", 50, CompressionType.RW, 50)]
        [InlineData("0", 100, CompressionType.RW, 100)]
        [InlineData("2", 75, CompressionType.HFIRW, 75)]
        [InlineData("3", 100, CompressionType.LZ, 100)]
        [InlineData("3", 200, CompressionType.LZ, 200)]
        [InlineData("4", 150, CompressionType.HFI, 150)]
        public void ParseCompression_NumericType_ReturnsCorrectCompression(
            string type, int level, CompressionType expectedType, int expectedLevel)
        {
            var result = ArgumentsParser.ParseCompression(type, level);

            Assert.Equal(expectedType, result.type);
            Assert.Equal(expectedLevel, result.level);
        }

        [Fact]
        public void ParseCompression_TypeOne_ThrowsInvalidCastException()
        {
            // Type 1 is "None" which is not allowed for compression
            var exception = Assert.Throws<InvalidCastException>(
                () => ArgumentsParser.ParseCompression("1", 50)
            );
            Assert.Contains("compression type", exception.Message.ToLower());
        }

        [Theory]
        [InlineData("lz", 0)]
        [InlineData("rw", -1)]
        [InlineData("hfi", -100)]
        public void ParseCompression_InvalidLevel_ThrowsArgumentException(string type, int level)
        {
            var exception = Assert.Throws<ArgumentException>(
                () => ArgumentsParser.ParseCompression(type, level)
            );
            Assert.Contains("compression level", exception.Message.ToLower());
        }

        [Fact]
        public void ParseCompression_InvalidNamedType_ThrowsInvalidCastException()
        {
            var exception = Assert.Throws<InvalidCastException>(
                () => ArgumentsParser.ParseCompression("invalid", 50)
            );
            Assert.Contains("invalid compression type", exception.Message.ToLower());
        }

        [Fact]
        public void ParseCompression_OutOfRangeNumericType_ThrowsInvalidCastException()
        {
            var exception = Assert.Throws<InvalidCastException>(
                () => ArgumentsParser.ParseCompression("5", 50)
            );
            Assert.Contains("invalid compression type", exception.Message.ToLower());
        }

        [Fact]
        public void ParseCompression_NegativeNumericType_ThrowsInvalidCastException()
        {
            var exception = Assert.Throws<InvalidCastException>(
                () => ArgumentsParser.ParseCompression("-1", 50)
            );
            Assert.Contains("invalid compression type", exception.Message.ToLower());
        }

        #endregion

        #region ParseCompression Tests (Legacy API - backward compatibility)

#pragma warning disable CS0618 // Type or member is obsolete
        [Theory]
        [InlineData("0,50", CompressionType.RW, 50)]
        [InlineData("0,100", CompressionType.RW, 100)]
        [InlineData("2,75", CompressionType.HFIRW, 75)]
        [InlineData("3,100", CompressionType.LZ, 100)]
        [InlineData("3,200", CompressionType.LZ, 200)]
        [InlineData("4,150", CompressionType.HFI, 150)]
        public void ParseCompression_LegacyFormat_ReturnsCorrectCompression(
            string input, CompressionType expectedType, int expectedLevel)
        {
            var result = ArgumentsParser.ParseCompression(input);

            Assert.Equal(expectedType, result.type);
            Assert.Equal(expectedLevel, result.level);
        }

        [Theory]
        [InlineData("rw,50", CompressionType.RW, 50)]
        [InlineData("lz,100", CompressionType.LZ, 100)]
        [InlineData("hfirw,75", CompressionType.HFIRW, 75)]
        [InlineData("hfi,150", CompressionType.HFI, 150)]
        public void ParseCompression_LegacyFormatNamedType_ReturnsCorrectCompression(
            string input, CompressionType expectedType, int expectedLevel)
        {
            var result = ArgumentsParser.ParseCompression(input);

            Assert.Equal(expectedType, result.type);
            Assert.Equal(expectedLevel, result.level);
        }

        [Fact]
        public void ParseCompression_LegacyTypeOne_ThrowsInvalidCastException()
        {
            var exception = Assert.Throws<InvalidCastException>(
                () => ArgumentsParser.ParseCompression("1,50")
            );
            Assert.Contains("compression type", exception.Message.ToLower());
        }

        [Fact]
        public void ParseCompression_LegacyLevelZero_ThrowsArgumentException()
        {
            var exception = Assert.Throws<ArgumentException>(
                () => ArgumentsParser.ParseCompression("3,0")
            );
            Assert.Contains("compression level", exception.Message.ToLower());
        }

        [Fact]
        public void ParseCompression_LegacyMissingComma_ThrowsArgumentException()
        {
            var exception = Assert.Throws<ArgumentException>(
                () => ArgumentsParser.ParseCompression("3")
            );
            Assert.Contains("compress", exception.Message.ToLower());
        }

        [Fact]
        public void ParseCompression_LegacyTooManyParts_ThrowsArgumentException()
        {
            var exception = Assert.Throws<ArgumentException>(
                () => ArgumentsParser.ParseCompression("3,50,extra")
            );
            Assert.Contains("compress", exception.Message.ToLower());
        }

        [Fact]
        public void ParseCompression_LegacyNonNumericLevel_ThrowsFormatException()
        {
            Assert.Throws<FormatException>(
                () => ArgumentsParser.ParseCompression("3,abc")
            );
        }

        [Fact]
        public void ParseCompression_LegacyEmptyString_ThrowsArgumentException()
        {
            var exception = Assert.Throws<ArgumentException>(
                () => ArgumentsParser.ParseCompression("")
            );
            Assert.Contains("compress", exception.Message.ToLower());
        }
#pragma warning restore CS0618 // Type or member is obsolete

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
