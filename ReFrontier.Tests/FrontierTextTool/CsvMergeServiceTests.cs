using System.Text;

using FrontierTextTool.Services;

using ReFrontier.Tests.Mocks;

namespace ReFrontier.Tests.TextToolTests
{
    /// <summary>
    /// Tests for CsvMergeService.
    /// </summary>
    public class CsvMergeServiceTests
    {
        private readonly InMemoryFileSystem _fileSystem;
        private readonly TestLogger _logger;
        private readonly CsvMergeService _service;

        static CsvMergeServiceTests()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public CsvMergeServiceTests()
        {
            _fileSystem = new InMemoryFileSystem();
            _logger = new TestLogger();
            _service = new CsvMergeService(_fileSystem, _logger);
        }

        #region CleanTradosText Tests

        [Fact]
        public void CleanTradosText_EmptyString_ReturnsEmpty()
        {
            string result = CsvMergeService.CleanTradosText("");
            Assert.Equal("", result);
        }

        [Fact]
        public void CleanTradosText_NoReplacements_ReturnsSameString()
        {
            string input = "Hello World";
            string result = CsvMergeService.CleanTradosText(input);
            Assert.Equal(input, result);
        }

        [Fact]
        public void CleanTradosText_ColonTilde_RemovesSpace()
        {
            string result = CsvMergeService.CleanTradosText(": ~test");
            Assert.Equal(":~test", result);
        }

        [Fact]
        public void CleanTradosText_JapanesePeriod_RemovesSpace()
        {
            string result = CsvMergeService.CleanTradosText("文章。 次の文");
            Assert.Equal("文章。次の文", result);
        }

        [Fact]
        public void CleanTradosText_JapaneseExclamation_RemovesSpace()
        {
            string result = CsvMergeService.CleanTradosText("やった！ 成功");
            Assert.Equal("やった！成功", result);
        }

        [Fact]
        public void CleanTradosText_JapaneseQuestion_RemovesSpace()
        {
            string result = CsvMergeService.CleanTradosText("なに？ 何が");
            Assert.Equal("なに？何が", result);
        }

        [Fact]
        public void CleanTradosText_JapaneseColon_RemovesSpace()
        {
            string result = CsvMergeService.CleanTradosText("項目： 説明");
            Assert.Equal("項目：説明", result);
        }

        [Fact]
        public void CleanTradosText_JapaneseDot_RemovesSpace()
        {
            string result = CsvMergeService.CleanTradosText("Ａ． Ｂ");
            Assert.Equal("Ａ．Ｂ", result);
        }

        [Fact]
        public void CleanTradosText_JapaneseQuotes_RemovesSpaces()
        {
            string result = CsvMergeService.CleanTradosText("「 内容」 後");
            Assert.Equal("「内容」後", result);
        }

        [Fact]
        public void CleanTradosText_JapaneseParentheses_RemovesSpaces()
        {
            string result = CsvMergeService.CleanTradosText("（ 内容） 後");
            Assert.Equal("（内容）後", result);
        }

        [Fact]
        public void CleanTradosText_MultipleReplacements_AppliesAll()
        {
            string input = "やった！ 成功。 次は？ 「 引用」 です";
            string expected = "やった！成功。次は？「引用」です";
            string result = CsvMergeService.CleanTradosText(input);
            Assert.Equal(expected, result);
        }

        #endregion

        #region Merge Tests

        [Fact]
        public void Merge_TransfersTranslationsByHash()
        {
            // Arrange
            // Old CSV has translations
            string oldCsv = "Offset,Hash,Original,Translation\n" +
                           "0,12345,Original1,Translated1\n" +
                           "10,67890,Original2,\n"; // No translation
            _fileSystem.AddFile("/test/old.csv", Encoding.GetEncoding("shift-jis").GetBytes(oldCsv));

            // New CSV has different offsets but same hashes
            string newCsv = "Offset,Hash,Original,Translation\n" +
                           "100,12345,Original1,\n" +
                           "200,67890,Original2,\n";
            _fileSystem.AddFile("/test/new.csv", Encoding.GetEncoding("shift-jis").GetBytes(newCsv));

            // Act
            _service.Merge("/test/old.csv", "/test/new.csv");

            // Assert
            Assert.True(_fileSystem.FileExists("csv/old.csv"));
            string result = _fileSystem.ReadAllText("csv/old.csv", Encoding.GetEncoding("shift-jis"));
            Assert.Contains("Translated1", result);
        }

        [Fact]
        public void Merge_DeletesNewCsvAfterMerge()
        {
            // Arrange
            string oldCsv = "Offset,Hash,Original,Translation\n0,123,Test,Trans\n";
            string newCsv = "Offset,Hash,Original,Translation\n0,123,Test,\n";
            _fileSystem.AddFile("/test/old.csv", Encoding.GetEncoding("shift-jis").GetBytes(oldCsv));
            _fileSystem.AddFile("/test/new.csv", Encoding.GetEncoding("shift-jis").GetBytes(newCsv));

            // Act
            _service.Merge("/test/old.csv", "/test/new.csv");

            // Assert
            Assert.False(_fileSystem.FileExists("/test/new.csv"));
        }

        [Fact]
        public void Merge_LogsProgress()
        {
            // Arrange
            string oldCsv = "Offset,Hash,Original,Translation\n0,123,Test,\n";
            string newCsv = "Offset,Hash,Original,Translation\n0,123,Test,\n";
            _fileSystem.AddFile("/test/old.csv", Encoding.GetEncoding("shift-jis").GetBytes(oldCsv));
            _fileSystem.AddFile("/test/new.csv", Encoding.GetEncoding("shift-jis").GetBytes(newCsv));

            // Act
            _service.Merge("/test/old.csv", "/test/new.csv");

            // Assert
            Assert.True(_logger.ContainsMessage("Updating entry"));
        }

        #endregion

        #region InsertCatFile Tests

        [Fact]
        public void InsertCatFile_UpdatesTranslations()
        {
            // Arrange
            // CAT file with translations (line by line)
            string catContent = "Translation1\nTranslation2\n";
            _fileSystem.AddFile("/test/cat.txt", Encoding.UTF8.GetBytes(catContent));

            // CSV file
            string csvContent = "Offset,Hash,Original,Translation\n" +
                               "0,123,Original1,\n" +
                               "10,456,Original2,\n";
            _fileSystem.AddFile("/test/strings.csv", Encoding.GetEncoding("shift-jis").GetBytes(csvContent));

            // Act
            _service.InsertCatFile("/test/cat.txt", "/test/strings.csv");

            // Assert
            Assert.True(_fileSystem.FileExists("csv/strings.csv"));
            string result = _fileSystem.ReadAllText("csv/strings.csv", Encoding.GetEncoding("shift-jis"));
            Assert.Contains("Translation1", result);
            Assert.Contains("Translation2", result);
        }

        [Fact]
        public void InsertCatFile_BackupsOriginalCatFile()
        {
            // Arrange
            string catContent = "Translation\n";
            _fileSystem.AddFile("/test/cat.txt", Encoding.UTF8.GetBytes(catContent));

            string csvContent = "Offset,Hash,Original,Translation\n0,123,Original,\n";
            _fileSystem.AddFile("/test/strings.csv", Encoding.GetEncoding("shift-jis").GetBytes(csvContent));

            // Act
            _service.InsertCatFile("/test/cat.txt", "/test/strings.csv");

            // Assert - CAT file should be moved to backup
            Assert.False(_fileSystem.FileExists("/test/cat.txt"));
            Assert.True(_fileSystem.DirectoryExists("backup"));
        }

        [Fact]
        public void InsertCatFile_ClearsTranslationWhenSameAsOriginal()
        {
            // Arrange
            // CAT file where translation is same as original
            string catContent = "Original1\n";
            _fileSystem.AddFile("/test/cat.txt", Encoding.UTF8.GetBytes(catContent));

            // CSV with existing translation
            string csvContent = "Offset,Hash,Original,Translation\n0,123,Original1,ExistingTrans\n";
            _fileSystem.AddFile("/test/strings.csv", Encoding.GetEncoding("shift-jis").GetBytes(csvContent));

            // Act
            _service.InsertCatFile("/test/cat.txt", "/test/strings.csv");

            // Assert
            string result = _fileSystem.ReadAllText("csv/strings.csv", Encoding.GetEncoding("shift-jis"));
            // Translation should be cleared (empty) because CAT = Original
            Assert.DoesNotContain("ExistingTrans", result);
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullFileSystem_ThrowsArgumentNullException()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
                new CsvMergeService(null!, _logger));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
                new CsvMergeService(_fileSystem, null!));
        }

        [Fact]
        public void DefaultConstructor_CreatesValidInstance()
        {
            var service = new CsvMergeService();
            Assert.NotNull(service);
        }

        [Fact]
        public void Constructor_WithEncodingOptions_CreatesValidInstance()
        {
            var encodingOptions = LibReFrontier.CsvEncodingOptions.ShiftJis;
            var service = new CsvMergeService(_fileSystem, _logger, encodingOptions);
            Assert.NotNull(service);
        }

        #endregion

        #region CleanTrados File Tests

        [Fact]
        public void CleanTrados_ModifiesFileInPlace()
        {
            // Arrange
            string content = "やった！ 成功";
            _fileSystem.AddFile("/test/file.txt", Encoding.UTF8.GetBytes(content));

            // Act
            _service.CleanTrados("/test/file.txt");

            // Assert
            string result = _fileSystem.ReadAllText("/test/file.txt", Encoding.UTF8);
            Assert.Equal("やった！成功", result);
        }

        [Fact]
        public void CleanTrados_LogsCleanedUp()
        {
            // Arrange
            string content = "Test";
            _fileSystem.AddFile("/test/file.txt", Encoding.UTF8.GetBytes(content));

            // Act
            _service.CleanTrados("/test/file.txt");

            // Assert
            Assert.True(_logger.ContainsMessage("Cleaned up"));
        }

        #endregion

        #region Merge Encoding Tests

        [Fact]
        public void Merge_HandlesUtf8BomEncoding()
        {
            // Arrange - UTF-8 with BOM
            string oldCsv = "Offset,Hash,Original,Translation\n0,123,Test,Trans\n";
            byte[] utf8Bom = new byte[] { 0xEF, 0xBB, 0xBF };
            byte[] content = Encoding.UTF8.GetBytes(oldCsv);
            byte[] withBom = new byte[utf8Bom.Length + content.Length];
            utf8Bom.CopyTo(withBom, 0);
            content.CopyTo(withBom, utf8Bom.Length);
            _fileSystem.AddFile("/test/old.csv", withBom);

            string newCsv = "Offset,Hash,Original,Translation\n0,123,Test,\n";
            _fileSystem.AddFile("/test/new.csv", Encoding.GetEncoding("shift-jis").GetBytes(newCsv));

            // Act
            _service.Merge("/test/old.csv", "/test/new.csv");

            // Assert
            Assert.True(_fileSystem.FileExists("csv/old.csv"));
        }

        [Fact]
        public void Merge_DeletesExistingOutputFile()
        {
            // Arrange
            _fileSystem.AddFile("csv/old.csv", "old content");

            string oldCsv = "Offset,Hash,Original,Translation\n0,123,Test,Trans\n";
            string newCsv = "Offset,Hash,Original,Translation\n0,123,Test,\n";
            _fileSystem.AddFile("/test/old.csv", Encoding.GetEncoding("shift-jis").GetBytes(oldCsv));
            _fileSystem.AddFile("/test/new.csv", Encoding.GetEncoding("shift-jis").GetBytes(newCsv));

            // Act
            _service.Merge("/test/old.csv", "/test/new.csv");

            // Assert
            Assert.True(_fileSystem.FileExists("csv/old.csv"));
            string result = _fileSystem.ReadAllText("csv/old.csv", Encoding.GetEncoding("shift-jis"));
            Assert.Contains("Trans", result);
        }

        [Fact]
        public void Merge_MatchesMultipleEntriesWithSameHash()
        {
            // Arrange - multiple entries with same hash should all get the translation
            string oldCsv = "Offset,Hash,Original,Translation\n0,12345,Text,Translated\n";
            _fileSystem.AddFile("/test/old.csv", Encoding.GetEncoding("shift-jis").GetBytes(oldCsv));

            string newCsv = "Offset,Hash,Original,Translation\n" +
                           "100,12345,Text,\n" +
                           "200,12345,Text,\n";
            _fileSystem.AddFile("/test/new.csv", Encoding.GetEncoding("shift-jis").GetBytes(newCsv));

            // Act
            _service.Merge("/test/old.csv", "/test/new.csv");

            // Assert - both entries should have the translation
            string result = _fileSystem.ReadAllText("csv/old.csv", Encoding.GetEncoding("shift-jis"));
            // Count occurrences of "Translated"
            int count = result.Split(new[] { "Translated" }, StringSplitOptions.None).Length - 1;
            Assert.Equal(2, count);
        }

        #endregion

        #region InsertCatFile Additional Tests

        [Fact]
        public void InsertCatFile_LogsProcessing()
        {
            // Arrange
            string catContent = "Translation\n";
            _fileSystem.AddFile("/test/cat.txt", Encoding.UTF8.GetBytes(catContent));

            string csvContent = "Offset,Hash,Original,Translation\n0,123,Original,\n";
            _fileSystem.AddFile("/test/strings.csv", Encoding.GetEncoding("shift-jis").GetBytes(csvContent));

            // Act
            _service.InsertCatFile("/test/cat.txt", "/test/strings.csv");

            // Assert
            Assert.True(_logger.ContainsMessage("Processing"));
        }

        [Fact]
        public void InsertCatFile_DeletesExistingOutputFile()
        {
            // Arrange
            _fileSystem.AddFile("csv/strings.csv", "old content");

            string catContent = "NewTrans\n";
            _fileSystem.AddFile("/test/cat.txt", Encoding.UTF8.GetBytes(catContent));

            string csvContent = "Offset,Hash,Original,Translation\n0,123,Original,\n";
            _fileSystem.AddFile("/test/strings.csv", Encoding.GetEncoding("shift-jis").GetBytes(csvContent));

            // Act
            _service.InsertCatFile("/test/cat.txt", "/test/strings.csv");

            // Assert
            string result = _fileSystem.ReadAllText("csv/strings.csv", Encoding.GetEncoding("shift-jis"));
            Assert.Contains("NewTrans", result);
        }

        [Fact]
        public void InsertCatFile_HandlesUtf8BomInCsv()
        {
            // Arrange
            string catContent = "Translation\n";
            _fileSystem.AddFile("/test/cat.txt", Encoding.UTF8.GetBytes(catContent));

            string csvContent = "Offset,Hash,Original,Translation\n0,123,Original,\n";
            byte[] utf8Bom = new byte[] { 0xEF, 0xBB, 0xBF };
            byte[] content = Encoding.UTF8.GetBytes(csvContent);
            byte[] withBom = new byte[utf8Bom.Length + content.Length];
            utf8Bom.CopyTo(withBom, 0);
            content.CopyTo(withBom, utf8Bom.Length);
            _fileSystem.AddFile("/test/strings.csv", withBom);

            // Act
            _service.InsertCatFile("/test/cat.txt", "/test/strings.csv");

            // Assert
            Assert.True(_fileSystem.FileExists("csv/strings.csv"));
        }

        [Fact]
        public void InsertCatFile_KeepsExistingTranslationWhenCatDiffers()
        {
            // Arrange
            string catContent = "DifferentText\n";
            _fileSystem.AddFile("/test/cat.txt", Encoding.UTF8.GetBytes(catContent));

            // CSV where Original != CAT text, so translation should be updated
            string csvContent = "Offset,Hash,Original,Translation\n0,123,Original,\n";
            _fileSystem.AddFile("/test/strings.csv", Encoding.GetEncoding("shift-jis").GetBytes(csvContent));

            // Act
            _service.InsertCatFile("/test/cat.txt", "/test/strings.csv");

            // Assert
            string result = _fileSystem.ReadAllText("csv/strings.csv", Encoding.GetEncoding("shift-jis"));
            Assert.Contains("DifferentText", result);
        }

        #endregion

        #region InsertCatFile Additional Edge Cases

        [Fact]
        public void InsertCatFile_WhenOriginalMatchesCatAndTranslationIsEmpty_LeavesEmpty()
        {
            // Arrange - CAT matches Original, but Translation is already empty
            // This tests the else-if branch where we don't modify
            string catContent = "Original1\n";
            _fileSystem.AddFile("/test/cat.txt", Encoding.UTF8.GetBytes(catContent));

            // CSV where Original == CAT and Translation is already empty
            string csvContent = "Offset,Hash,Original,Translation\n0,123,Original1,\n";
            _fileSystem.AddFile("/test/strings.csv", Encoding.GetEncoding("shift-jis").GetBytes(csvContent));

            // Act
            _service.InsertCatFile("/test/cat.txt", "/test/strings.csv");

            // Assert - Translation should still be empty (no change made)
            string result = _fileSystem.ReadAllText("csv/strings.csv", Encoding.GetEncoding("shift-jis"));
            // Count commas - if translation is empty, last field should be empty
            Assert.Contains("Original1,", result); // Original followed by comma
        }

        [Fact]
        public void InsertCatFile_WithMultipleEntries_UpdatesCorrectly()
        {
            // Arrange
            string catContent = "TranslatedLine1\nOriginal2\nTranslatedLine3\n";
            _fileSystem.AddFile("/test/cat.txt", Encoding.UTF8.GetBytes(catContent));

            string csvContent = "Offset,Hash,Original,Translation\n" +
                               "0,123,Original1,\n" +
                               "10,456,Original2,ExistingTrans\n" +
                               "20,789,Original3,\n";
            _fileSystem.AddFile("/test/strings.csv", Encoding.GetEncoding("shift-jis").GetBytes(csvContent));

            // Act
            _service.InsertCatFile("/test/cat.txt", "/test/strings.csv");

            // Assert
            string result = _fileSystem.ReadAllText("csv/strings.csv", Encoding.GetEncoding("shift-jis"));
            // First entry: Original1 != TranslatedLine1, so Translation = TranslatedLine1
            Assert.Contains("TranslatedLine1", result);
            // Second entry: Original2 == Original2 AND has existing translation, so clear it
            Assert.DoesNotContain("ExistingTrans", result);
            // Third entry: Original3 != TranslatedLine3, so Translation = TranslatedLine3
            Assert.Contains("TranslatedLine3", result);
        }

        #endregion

        #region Merge Edge Cases

        [Fact]
        public void Merge_WithNoTranslations_StillCreatesOutput()
        {
            // Arrange - Old CSV with no translations
            string oldCsv = "Offset,Hash,Original,Translation\n" +
                           "0,12345,Text1,\n" +
                           "10,67890,Text2,\n";
            _fileSystem.AddFile("/test/old.csv", Encoding.GetEncoding("shift-jis").GetBytes(oldCsv));

            string newCsv = "Offset,Hash,Original,Translation\n" +
                           "100,12345,Text1,\n" +
                           "200,67890,Text2,\n";
            _fileSystem.AddFile("/test/new.csv", Encoding.GetEncoding("shift-jis").GetBytes(newCsv));

            // Act
            _service.Merge("/test/old.csv", "/test/new.csv");

            // Assert
            Assert.True(_fileSystem.FileExists("csv/old.csv"));
        }

        [Fact]
        public void Merge_WithNoMatchingHashes_KeepsNewStructure()
        {
            // Arrange - Different hashes between old and new
            string oldCsv = "Offset,Hash,Original,Translation\n0,111,OldText,OldTrans\n";
            _fileSystem.AddFile("/test/old.csv", Encoding.GetEncoding("shift-jis").GetBytes(oldCsv));

            string newCsv = "Offset,Hash,Original,Translation\n100,999,NewText,\n";
            _fileSystem.AddFile("/test/new.csv", Encoding.GetEncoding("shift-jis").GetBytes(newCsv));

            // Act
            _service.Merge("/test/old.csv", "/test/new.csv");

            // Assert - New structure preserved, no translation transferred
            string result = _fileSystem.ReadAllText("csv/old.csv", Encoding.GetEncoding("shift-jis"));
            Assert.Contains("NewText", result);
            Assert.DoesNotContain("OldText", result);
            Assert.DoesNotContain("OldTrans", result);
        }

        #endregion
    }
}
