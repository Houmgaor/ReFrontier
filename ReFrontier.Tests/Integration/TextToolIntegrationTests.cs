using System.Text;

using FrontierTextTool.Services;

using ReFrontier.Tests.Mocks;

namespace ReFrontier.Tests.Integration
{
    /// <summary>
    /// Integration tests for FrontierTextTool services.
    /// Tests the complete workflow of extracting, modifying, and inserting text.
    /// </summary>
    public class TextToolIntegrationTests
    {
        private readonly InMemoryFileSystem _fileSystem;
        private readonly TestLogger _logger;

        static TextToolIntegrationTests()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public TextToolIntegrationTests()
        {
            _fileSystem = new InMemoryFileSystem();
            _logger = new TestLogger();
        }

        #region Round-Trip Tests

        [Fact]
        public void ExtractAndInsert_PreservesOriginalStrings()
        {
            // Arrange
            var extractionService = new TextExtractionService(_fileSystem, _logger);
            var insertionService = new TextInsertionService(_fileSystem, _logger);

            string[] testStrings = { "テスト文字列", "Hello World", "混合テストMixed" };
            byte[] originalData = TestDataFactory.CreateBinaryWithStrings(testStrings);

            using var ms = new MemoryStream(originalData);
            using var br = new BinaryReader(ms);

            // Act - Extract strings
            var extracted = extractionService.DumpAndHashInternal(
                "test.bin", originalData, br, 0, 0, false, false);

            // Set translations to same as original (no change)
            foreach (var entry in extracted)
            {
                entry.EString = entry.JString;
            }

            // Insert strings back
            var stringDb = new StringDatabase[extracted.Count];
            for (int i = 0; i < extracted.Count; i++)
            {
                stringDb[i] = new StringDatabase
                {
                    Offset = extracted[i].Offset,
                    EString = extracted[i].JString
                };
            }

            byte[] resultData = insertionService.UpdateBinaryStrings(stringDb, originalData, false, false);

            // Assert - Data should be larger (translations appended)
            Assert.True(resultData.Length > originalData.Length);
        }

        [Fact]
        public void ExtractAndInsert_WithTranslation_AppendsTranslatedStrings()
        {
            // Arrange
            var extractionService = new TextExtractionService(_fileSystem, _logger);
            var insertionService = new TextInsertionService(_fileSystem, _logger);

            byte[] originalData = TestDataFactory.CreateBinaryWithStrings("日本語");

            using var ms = new MemoryStream(originalData);
            using var br = new BinaryReader(ms);

            // Act - Extract
            var extracted = extractionService.DumpAndHashInternal(
                "test.bin", originalData, br, 0, 0, false, false);

            // Translate
            var stringDb = new StringDatabase[]
            {
                new() { Offset = extracted[0].Offset, EString = "English" }
            };

            byte[] resultData = insertionService.UpdateBinaryStrings(stringDb, originalData, false, false);

            // Assert - Result should contain the English translation
            string resultString = Encoding.GetEncoding("shift-jis").GetString(
                resultData, originalData.Length, 7); // "English" is 7 bytes
            Assert.Equal("English", resultString);
        }

        #endregion

        #region CSV Round-Trip Tests

        [Fact]
        public void CsvWriteAndLoad_PreservesData()
        {
            // Arrange
            var extractionService = new TextExtractionService(_fileSystem, _logger);
            var insertionService = new TextInsertionService(_fileSystem, _logger);

            byte[] originalData = TestDataFactory.CreateBinaryWithStrings("テスト1", "テスト2");

            using var ms = new MemoryStream(originalData);
            using var br = new BinaryReader(ms);

            // Act - Extract and write to CSV
            var extracted = extractionService.DumpAndHashInternal(
                "test.bin", originalData, br, 0, 0, false, false);

            extractionService.WriteCsv("/test/input.bin", extracted);

            // Read back from CSV
            var loaded = insertionService.LoadCsvToStringDatabase("input.csv");

            // Assert - LoadCsvToStringDatabase loads Offset, Hash, EString (not JString)
            Assert.Equal(extracted.Count, loaded.Length);
            Assert.Equal(extracted[0].Offset, loaded[0].Offset);
            Assert.Equal(extracted[0].Hash, loaded[0].Hash);
            Assert.Equal(extracted[1].Offset, loaded[1].Offset);
            Assert.Equal(extracted[1].Hash, loaded[1].Hash);
        }

        #endregion

        #region Merge Integration Tests

        [Fact]
        public void MergeService_TransfersTranslationsCorrectly()
        {
            // Arrange
            var mergeService = new CsvMergeService(_fileSystem, _logger);

            // Old CSV has translations
            string oldCsv = "Offset,Hash,JString,EString\n" +
                           "0,12345,Original,Translated\n";
            _fileSystem.AddFile("/test/old.csv", Encoding.GetEncoding("shift-jis").GetBytes(oldCsv));

            // New CSV with same hash but different offset (simulating file update)
            string newCsv = "Offset,Hash,JString,EString\n" +
                           "100,12345,Original,\n";
            _fileSystem.AddFile("/test/new.csv", Encoding.GetEncoding("shift-jis").GetBytes(newCsv));

            // Act
            mergeService.Merge("/test/old.csv", "/test/new.csv");

            // Assert
            string result = _fileSystem.ReadAllText("csv/old.csv", Encoding.GetEncoding("shift-jis"));
            Assert.Contains("Translated", result);
        }

        #endregion

        #region Special Character Tests

        [Fact]
        public void ExtractAndInsert_HandlesSpecialCharacters()
        {
            // Arrange
            var extractionService = new TextExtractionService(_fileSystem, _logger);
            var insertionService = new TextInsertionService(_fileSystem, _logger);

            // Test with tab and newline characters
            byte[] originalData = TestDataFactory.CreateBinaryWithStrings("Tab\tTest", "Line1\nLine2");

            using var ms = new MemoryStream(originalData);
            using var br = new BinaryReader(ms);

            // Act - Extract
            var extracted = extractionService.DumpAndHashInternal(
                "test.bin", originalData, br, 0, 0, false, false);

            // Assert - Special characters are preserved (RFC 4180 quotes them in CSV output)
            Assert.Equal("Tab\tTest", extracted[0].JString);
            Assert.Equal("Line1\nLine2", extracted[1].JString);

            // Create string database with translations using RFC 4180 quoting for special characters
            var csv = "Offset,Hash,JString,EString\n" +
                      "0,123,\"Tab\tTest\",\"New\tValue\"\n";
            _fileSystem.AddFile("/test/strings.csv", Encoding.GetEncoding("shift-jis").GetBytes(csv));

            // Load and verify
            var loaded = insertionService.LoadCsvToStringDatabase("/test/strings.csv");

            Assert.Equal("New\tValue", loaded[0].EString); // RFC 4180 handles quoted tabs
        }

        [Fact]
        public void ExtractAndInsert_HandlesCarriageReturnNewline()
        {
            // Arrange
            var extractionService = new TextExtractionService(_fileSystem, _logger);

            byte[] originalData = TestDataFactory.CreateBinaryWithStrings("Line1\r\nLine2");

            using var ms = new MemoryStream(originalData);
            using var br = new BinaryReader(ms);

            // Act - Extract
            var extracted = extractionService.DumpAndHashInternal(
                "test.bin", originalData, br, 0, 0, false, false);

            // Assert - CRLF is preserved (RFC 4180 will quote it in CSV output)
            Assert.Equal("Line1\r\nLine2", extracted[0].JString);
        }

        #endregion

        #region Mixed Japanese/English Tests

        [Fact]
        public void ExtractAndInsert_HandlesMixedContent()
        {
            // Arrange
            var extractionService = new TextExtractionService(_fileSystem, _logger);
            var insertionService = new TextInsertionService(_fileSystem, _logger);

            string mixedString = "アイテム: Item123 (説明)";
            byte[] originalData = TestDataFactory.CreateBinaryWithStrings(mixedString);

            using var ms = new MemoryStream(originalData);
            using var br = new BinaryReader(ms);

            // Act - Extract
            var extracted = extractionService.DumpAndHashInternal(
                "test.bin", originalData, br, 0, 0, false, false);

            // Assert - Mixed content preserved
            Assert.Equal(mixedString, extracted[0].JString);

            // Insert translation
            var stringDb = new StringDatabase[]
            {
                new() { Offset = 0, EString = "Item: Item123 (Description)" }
            };

            byte[] resultData = insertionService.UpdateBinaryStrings(stringDb, originalData, false, false);

            // Verify translation is in result
            int translationStart = originalData.Length;
            string resultTranslation = Encoding.GetEncoding("shift-jis").GetString(
                resultData, translationStart, 27); // "Item: Item123 (Description)" length
            Assert.Equal("Item: Item123 (Description)", resultTranslation);
        }

        #endregion
    }
}
