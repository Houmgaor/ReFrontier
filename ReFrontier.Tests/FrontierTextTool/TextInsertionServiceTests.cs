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
            Assert.Equal("English1", result[0].Translation);
            Assert.Equal((uint)10, result[1].Offset);
            Assert.Equal("English2", result[1].Translation);
        }

        [Fact]
        public void LoadCsvToStringDatabase_HandlesTabInQuotedField()
        {
            // Arrange - RFC 4180 CSV with tab character in quoted field
            string csv = "Offset,Hash,Original,Translation\n0,123,Test,\"Hello\tWorld\"\n";
            _fileSystem.AddFile("/test/strings.csv", Encoding.GetEncoding("shift-jis").GetBytes(csv));

            // Act
            var result = _service.LoadCsvToStringDatabase("/test/strings.csv");

            // Assert
            Assert.Single(result);
            Assert.Equal("Hello\tWorld", result[0].Translation);
        }

        [Fact]
        public void LoadCsvToStringDatabase_HandlesNewlineInQuotedField()
        {
            // Arrange - RFC 4180 CSV with newline in quoted field
            string csv = "Offset,Hash,Original,Translation\n0,123,Test,\"Line1\nLine2\"\n";
            _fileSystem.AddFile("/test/strings.csv", Encoding.GetEncoding("shift-jis").GetBytes(csv));

            // Act
            var result = _service.LoadCsvToStringDatabase("/test/strings.csv");

            // Assert
            Assert.Single(result);
            Assert.Equal("Line1\nLine2", result[0].Translation);
        }

        [Fact]
        public void LoadCsvToStringDatabase_HandlesCarriageReturnInQuotedField()
        {
            // Arrange - RFC 4180 CSV with CRLF in quoted field
            string csv = "Offset,Hash,Original,Translation\n0,123,Test,\"Line1\r\nLine2\"\n";
            _fileSystem.AddFile("/test/strings.csv", Encoding.GetEncoding("shift-jis").GetBytes(csv));

            // Act
            var result = _service.LoadCsvToStringDatabase("/test/strings.csv");

            // Assert
            Assert.Single(result);
            Assert.Equal("Line1\r\nLine2", result[0].Translation);
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
                new() { Offset = 0, Translation = "Hello" },
                new() { Offset = 10, Translation = "World" }
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
                new() { Offset = 0, Translation = "" },         // Empty - skip
                new() { Offset = 10, Translation = "Translated" }
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
                new() { Offset = 0, Translation = "Test" }
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
                new() { Offset = 0, Translation = "日本語テスト" }
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
                new() { Offset = 0, Translation = "Test" }
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

        #region Additional LoadCsvToStringDatabase Tests

        [Fact]
        public void LoadCsvToStringDatabase_HandlesBackslash()
        {
            // Arrange - backslash is just regular text in RFC 4180
            string csv = "Offset,Hash,Original,Translation\n0,123,Test,Path\\File\n";
            _fileSystem.AddFile("/test/strings.csv", Encoding.GetEncoding("shift-jis").GetBytes(csv));

            // Act
            var result = _service.LoadCsvToStringDatabase("/test/strings.csv");

            // Assert
            Assert.Single(result);
            Assert.Equal("Path\\File", result[0].Translation);
        }

        [Fact]
        public void LoadCsvToStringDatabase_HandlesUtf8BomEncoding()
        {
            // Arrange - UTF-8 with BOM
            string csv = "Offset,Hash,Original,Translation\n0,123,Test,English\n";
            byte[] utf8Bom = new byte[] { 0xEF, 0xBB, 0xBF };
            byte[] content = Encoding.UTF8.GetBytes(csv);
            byte[] withBom = new byte[utf8Bom.Length + content.Length];
            utf8Bom.CopyTo(withBom, 0);
            content.CopyTo(withBom, utf8Bom.Length);
            _fileSystem.AddFile("/test/strings.csv", withBom);

            // Act
            var result = _service.LoadCsvToStringDatabase("/test/strings.csv");

            // Assert
            Assert.Single(result);
            Assert.Equal("English", result[0].Translation);
        }

        [Fact]
        public void LoadCsvToStringDatabase_WithEmptyTranslation_ReturnsEmptyString()
        {
            // Arrange
            string csv = "Offset,Hash,Original,Translation\n0,123,Test,\n";
            _fileSystem.AddFile("/test/strings.csv", Encoding.GetEncoding("shift-jis").GetBytes(csv));

            // Act
            var result = _service.LoadCsvToStringDatabase("/test/strings.csv");

            // Assert
            Assert.Single(result);
            Assert.Equal("", result[0].Translation);
        }

        [Fact]
        public void LoadCsvToStringDatabase_ParsesHashCorrectly()
        {
            // Arrange
            string csv = "Offset,Hash,Original,Translation\n0,4294967295,Test,English\n";
            _fileSystem.AddFile("/test/strings.csv", Encoding.GetEncoding("shift-jis").GetBytes(csv));

            // Act
            var result = _service.LoadCsvToStringDatabase("/test/strings.csv");

            // Assert
            Assert.Single(result);
            Assert.Equal(uint.MaxValue, result[0].Hash);
        }

        #endregion

        #region Additional UpdateBinaryStrings Tests

        [Fact]
        public void UpdateBinaryStrings_WithoutTrueOffsets_ScansForPointers()
        {
            // Arrange - create data with a pointer value that matches an offset
            // The pointer value must be at position > 10000 for the scan to work
            byte[] originalData = new byte[11000];
            // Write a pointer value at position 10004 that points to offset 0
            BitConverter.GetBytes(0).CopyTo(originalData, 10004);

            var stringDb = new StringDatabase[]
            {
                new() { Offset = 0, Translation = "Test" }
            };

            // Act
            byte[] result = _service.UpdateBinaryStrings(stringDb, originalData, false, false);

            // Assert - pointer at position 10004 should be updated to new location
            int newPointer = BitConverter.ToInt32(result, 10004);
            Assert.Equal(11000, newPointer); // Points to end of original data
        }

        [Fact]
        public void UpdateBinaryStrings_SkipsPointersBeforePosition10000()
        {
            // Arrange
            byte[] originalData = new byte[11000];
            // Write a pointer at position 100 (< 10000, should not be updated)
            BitConverter.GetBytes(0).CopyTo(originalData, 100);

            var stringDb = new StringDatabase[]
            {
                new() { Offset = 0, Translation = "Test" }
            };

            // Act
            byte[] result = _service.UpdateBinaryStrings(stringDb, originalData, false, false);

            // Assert - pointer at position 100 should NOT be updated
            int pointerValue = BitConverter.ToInt32(result, 100);
            Assert.Equal(0, pointerValue); // Should remain unchanged
        }

        [Fact]
        public void UpdateBinaryStrings_MultipleTranslations_AppendsInOrder()
        {
            // Arrange
            byte[] originalData = new byte[100];
            var stringDb = new StringDatabase[]
            {
                new() { Offset = 0, Translation = "First" },
                new() { Offset = 10, Translation = "Second" },
                new() { Offset = 20, Translation = "Third" }
            };

            // Act
            byte[] result = _service.UpdateBinaryStrings(stringDb, originalData, false, false);

            // Assert
            int firstOffset = originalData.Length;
            int secondOffset = firstOffset + 6; // "First" + null
            int thirdOffset = secondOffset + 7; // "Second" + null

            string first = Encoding.GetEncoding("shift-jis").GetString(result, firstOffset, 5);
            string second = Encoding.GetEncoding("shift-jis").GetString(result, secondOffset, 6);
            string third = Encoding.GetEncoding("shift-jis").GetString(result, thirdOffset, 5);

            Assert.Equal("First", first);
            Assert.Equal("Second", second);
            Assert.Equal("Third", third);
        }

        [Fact]
        public void UpdateBinaryStrings_HandlesNullStringsInMiddle()
        {
            // Arrange
            byte[] originalData = new byte[100];
            var stringDb = new StringDatabase[]
            {
                new() { Offset = 0, Translation = "First" },
                new() { Offset = 10, Translation = null },     // null - skip
                new() { Offset = 20, Translation = "Third" }
            };

            // Act
            byte[] result = _service.UpdateBinaryStrings(stringDb, originalData, false, false);

            // Assert - only two translations added
            int expectedSize = originalData.Length + 6 + 6; // "First" + null + "Third" + null
            Assert.Equal(expectedSize, result.Length);
        }

        #endregion
    }
}
