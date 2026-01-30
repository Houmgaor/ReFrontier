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
            string oldCsv = "Offset\tHash\tJString\tEString\n" +
                           "0\t12345\tOriginal1\tTranslated1\n" +
                           "10\t67890\tOriginal2\t\n"; // No translation
            _fileSystem.AddFile("/test/old.csv", Encoding.GetEncoding("shift-jis").GetBytes(oldCsv));

            // New CSV has different offsets but same hashes
            string newCsv = "Offset\tHash\tJString\tEString\n" +
                           "100\t12345\tOriginal1\t\n" +
                           "200\t67890\tOriginal2\t\n";
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
            string oldCsv = "Offset\tHash\tJString\tEString\n0\t123\tTest\tTrans\n";
            string newCsv = "Offset\tHash\tJString\tEString\n0\t123\tTest\t\n";
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
            string oldCsv = "Offset\tHash\tJString\tEString\n0\t123\tTest\t\n";
            string newCsv = "Offset\tHash\tJString\tEString\n0\t123\tTest\t\n";
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
            string csvContent = "Offset\tHash\tJString\tEString\n" +
                               "0\t123\tOriginal1\t\n" +
                               "10\t456\tOriginal2\t\n";
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

            string csvContent = "Offset\tHash\tJString\tEString\n0\t123\tOriginal\t\n";
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
            string csvContent = "Offset\tHash\tJString\tEString\n0\t123\tOriginal1\tExistingTrans\n";
            _fileSystem.AddFile("/test/strings.csv", Encoding.GetEncoding("shift-jis").GetBytes(csvContent));

            // Act
            _service.InsertCatFile("/test/cat.txt", "/test/strings.csv");

            // Assert
            string result = _fileSystem.ReadAllText("csv/strings.csv", Encoding.GetEncoding("shift-jis"));
            // EString should be cleared (empty) because CAT = JString
            Assert.DoesNotContain("ExistingTrans", result);
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullFileSystem_ThrowsArgumentNullException()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
                new CsvMergeService(null, _logger));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
                new CsvMergeService(_fileSystem, null));
        }

        [Fact]
        public void DefaultConstructor_CreatesValidInstance()
        {
            var service = new CsvMergeService();
            Assert.NotNull(service);
        }

        #endregion
    }
}
