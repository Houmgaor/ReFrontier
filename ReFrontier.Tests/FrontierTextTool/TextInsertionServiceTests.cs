using System.Text;

using FrontierTextTool.Services;

using ReFrontier.Tests.Mocks;

namespace ReFrontier.Tests.TextToolTests
{
    /// <summary>
    /// Tests for TextInsertionService.
    /// </summary>
    public class TextInsertionServiceTests
    {
        private readonly InMemoryFileSystem _fileSystem;
        private readonly TestLogger _logger;
        private readonly TextInsertionService _service;

        static TextInsertionServiceTests()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public TextInsertionServiceTests()
        {
            _fileSystem = new InMemoryFileSystem();
            _logger = new TestLogger();
            _service = new TextInsertionService(_fileSystem, _logger);
        }

        #region LoadCsvToStringDatabase Tests

        [Fact]
        public void LoadCsvToStringDatabase_ParsesEntries()
        {
            // Arrange
            string csv = TestDataFactory.CreateStringDatabaseCsv(
                (0, "Japanese1", "English1"),
                (10, "Japanese2", "English2")
            );
            _fileSystem.AddFile("/test/strings.csv", Encoding.GetEncoding("shift-jis").GetBytes(csv));

            // Act
            var result = _service.LoadCsvToStringDatabase("/test/strings.csv");

            // Assert
            Assert.Equal(2, result.Length);
            Assert.Equal((uint)0, result[0].Offset);
            Assert.Equal("English1", result[0].EString);
            Assert.Equal((uint)10, result[1].Offset);
            Assert.Equal("English2", result[1].EString);
        }

        [Fact]
        public void LoadCsvToStringDatabase_ReplacesTabMarker()
        {
            // Arrange
            string csv = "Offset\tHash\tJString\tEString\n0\t123\tTest\tHello\\tWorld\n";
            _fileSystem.AddFile("/test/strings.csv", Encoding.GetEncoding("shift-jis").GetBytes(csv));

            // Act
            var result = _service.LoadCsvToStringDatabase("/test/strings.csv");

            // Assert
            Assert.Single(result);
            Assert.Equal("Hello\tWorld", result[0].EString);
        }

        [Fact]
        public void LoadCsvToStringDatabase_ReplacesNewlineMarker()
        {
            // Arrange
            string csv = "Offset\tHash\tJString\tEString\n0\t123\tTest\tLine1\\nLine2\n";
            _fileSystem.AddFile("/test/strings.csv", Encoding.GetEncoding("shift-jis").GetBytes(csv));

            // Act
            var result = _service.LoadCsvToStringDatabase("/test/strings.csv");

            // Assert
            Assert.Single(result);
            Assert.Equal("Line1\nLine2", result[0].EString);
        }

        [Fact]
        public void LoadCsvToStringDatabase_ReplacesCarriageReturnMarker()
        {
            // Arrange
            string csv = "Offset\tHash\tJString\tEString\n0\t123\tTest\tLine1\\r\\nLine2\n";
            _fileSystem.AddFile("/test/strings.csv", Encoding.GetEncoding("shift-jis").GetBytes(csv));

            // Act
            var result = _service.LoadCsvToStringDatabase("/test/strings.csv");

            // Assert
            Assert.Single(result);
            Assert.Equal("Line1\r\nLine2", result[0].EString);
        }

        #endregion

        #region UpdateBinaryStrings Tests

        [Fact]
        public void UpdateBinaryStrings_AppendsTranslatedStrings()
        {
            // Arrange
            byte[] originalData = new byte[100];
            var stringDb = new StringDatabase[]
            {
                new() { Offset = 0, EString = "Hello" },
                new() { Offset = 10, EString = "World" }
            };

            // Act
            byte[] result = _service.UpdateBinaryStrings(stringDb, originalData, false, false);

            // Assert - result should be larger than original
            Assert.True(result.Length > originalData.Length);

            // Translations should be at the end
            int helloOffset = originalData.Length;
            int worldOffset = helloOffset + 6; // "Hello" + null

            string extractedHello = Encoding.GetEncoding("shift-jis").GetString(result, helloOffset, 5);
            string extractedWorld = Encoding.GetEncoding("shift-jis").GetString(result, worldOffset, 5);

            Assert.Equal("Hello", extractedHello);
            Assert.Equal("World", extractedWorld);
        }

        [Fact]
        public void UpdateBinaryStrings_SkipsEmptyTranslations()
        {
            // Arrange
            byte[] originalData = new byte[100];
            var stringDb = new StringDatabase[]
            {
                new() { Offset = 0, EString = "" },         // Empty - skip
                new() { Offset = 10, EString = "Translated" }
            };

            // Act
            byte[] result = _service.UpdateBinaryStrings(stringDb, originalData, false, false);

            // Assert - only one translation added
            int expectedSize = originalData.Length + 11; // "Translated" (10) + null (1)
            Assert.Equal(expectedSize, result.Length);
        }

        [Fact]
        public void UpdateBinaryStrings_WithTrueOffsets_ModifiesPointerAtOffset()
        {
            // Arrange
            byte[] originalData = new byte[100];
            var stringDb = new StringDatabase[]
            {
                new() { Offset = 0, EString = "Test" }
            };

            // Act
            byte[] result = _service.UpdateBinaryStrings(stringDb, originalData, false, true);

            // Assert - pointer at offset 0 should be updated to new location
            int newPointer = System.BitConverter.ToInt32(result, 0);
            Assert.Equal(100, newPointer); // Points to end of original data
        }

        [Fact]
        public void UpdateBinaryStrings_HandlesJapaneseText()
        {
            // Arrange
            byte[] originalData = new byte[100];
            var stringDb = new StringDatabase[]
            {
                new() { Offset = 0, EString = "日本語テスト" }
            };

            // Act
            byte[] result = _service.UpdateBinaryStrings(stringDb, originalData, false, false);

            // Assert
            int japaneseLength = Encoding.GetEncoding("shift-jis").GetBytes("日本語テスト").Length;
            int expectedSize = originalData.Length + japaneseLength + 1;
            Assert.Equal(expectedSize, result.Length);

            // Extract and verify
            string extracted = Encoding.GetEncoding("shift-jis").GetString(
                result, originalData.Length, japaneseLength);
            Assert.Equal("日本語テスト", extracted);
        }

        [Fact]
        public void UpdateBinaryStrings_VerboseMode_LogsStrings()
        {
            // Arrange
            byte[] originalData = new byte[100];
            var stringDb = new StringDatabase[]
            {
                new() { Offset = 0, EString = "Test" }
            };

            // Act
            _service.UpdateBinaryStrings(stringDb, originalData, true, false);

            // Assert
            Assert.True(_logger.ContainsMessage("Test"));
            Assert.True(_logger.ContainsMessage("Filling array"));
        }

        #endregion

        #region GetNullterminatedStringLength Tests

        [Fact]
        public void GetNullterminatedStringLength_EmptyString_ReturnsOne()
        {
            int result = TextInsertionService.GetNullterminatedStringLength("");
            Assert.Equal(1, result);
        }

        [Fact]
        public void GetNullterminatedStringLength_AsciiString_ReturnsCorrectLength()
        {
            int result = TextInsertionService.GetNullterminatedStringLength("Hello");
            Assert.Equal(6, result); // 5 + 1 null
        }

        [Fact]
        public void GetNullterminatedStringLength_JapaneseString_ReturnsCorrectLength()
        {
            // Japanese characters are 2 bytes each in Shift-JIS
            int result = TextInsertionService.GetNullterminatedStringLength("あいう");
            Assert.Equal(7, result); // 6 + 1 null
        }

        [Theory]
        [InlineData("A", 2)]
        [InlineData("AB", 3)]
        [InlineData("ABC", 4)]
        public void GetNullterminatedStringLength_VariousLengths(string input, int expected)
        {
            int result = TextInsertionService.GetNullterminatedStringLength(input);
            Assert.Equal(expected, result);
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullFileSystem_ThrowsArgumentNullException()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
                new TextInsertionService(null!, _logger));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
                new TextInsertionService(_fileSystem, null!));
        }

        [Fact]
        public void DefaultConstructor_CreatesValidInstance()
        {
            var service = new TextInsertionService();
            Assert.NotNull(service);
        }

        #endregion
    }
}
