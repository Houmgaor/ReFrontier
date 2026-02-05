using System.Text;

using FrontierTextTool.Services;

using FrontierTextProgram = FrontierTextTool.Program;

namespace ReFrontier.Tests.TextToolTests
{
    /// <summary>
    /// Tests for FrontierTextTool.Program class.
    /// </summary>
    public class FrontierTextToolProgramTests
    {
        static FrontierTextToolProgramTests()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        #region Constructor Tests

        [Fact]
        public void Program_DefaultConstructor_CreatesValidInstance()
        {
            // Act
            var program = new FrontierTextProgram();

            // Assert
            Assert.NotNull(program);
        }

        [Fact]
        public void Program_InjectedServices_CreatesValidInstance()
        {
            // Arrange
            var extractionService = new TextExtractionService();
            var insertionService = new TextInsertionService();
            var mergeService = new CsvMergeService();

            // Act
            var program = new FrontierTextProgram(extractionService, insertionService, mergeService);

            // Assert
            Assert.NotNull(program);
        }

        [Fact]
        public void Program_NullExtractionService_ThrowsArgumentNullException()
        {
            // Arrange
            var insertionService = new TextInsertionService();
            var mergeService = new CsvMergeService();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new FrontierTextProgram(null!, insertionService, mergeService));
        }

        [Fact]
        public void Program_NullInsertionService_ThrowsArgumentNullException()
        {
            // Arrange
            var extractionService = new TextExtractionService();
            var mergeService = new CsvMergeService();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new FrontierTextProgram(extractionService, null!, mergeService));
        }

        [Fact]
        public void Program_NullMergeService_ThrowsArgumentNullException()
        {
            // Arrange
            var extractionService = new TextExtractionService();
            var insertionService = new TextInsertionService();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new FrontierTextProgram(extractionService, insertionService, null!));
        }

        #endregion

        #region Static Helper Method Tests

        [Fact]
        public void GetNullterminatedStringLength_DelegatesToTextInsertionService()
        {
            // Act
            int result = FrontierTextProgram.GetNullterminatedStringLength("Hello");

            // Assert
            Assert.Equal(6, result); // 5 + 1 null
        }

        [Fact]
        public void GetNullterminatedStringLength_JapaneseText_ReturnsCorrectLength()
        {
            // Act - Japanese text (2 bytes per char in Shift-JIS)
            int result = FrontierTextProgram.GetNullterminatedStringLength("あいう");

            // Assert
            Assert.Equal(7, result); // 6 + 1 null
        }

        [Fact]
        public void CleanTradosText_DelegatesToCsvMergeService()
        {
            // Act
            string result = FrontierTextProgram.CleanTradosText("文章。 次の文");

            // Assert
            Assert.Equal("文章。次の文", result);
        }

        [Fact]
        public void CleanTradosText_RemovesMultipleSpaces()
        {
            // Act
            string result = FrontierTextProgram.CleanTradosText("やった！ 成功。 次は？ 「 引用」 です");

            // Assert
            Assert.Equal("やった！成功。次は？「引用」です", result);
        }

        #endregion
    }
}
