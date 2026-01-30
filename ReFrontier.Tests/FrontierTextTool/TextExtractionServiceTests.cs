using System.Text;

using FrontierTextTool.Services;

using ReFrontier.Tests.Mocks;

namespace ReFrontier.Tests.TextToolTests
{
    /// <summary>
    /// Tests for TextExtractionService.
    /// </summary>
    public class TextExtractionServiceTests
    {
        private readonly InMemoryFileSystem _fileSystem;
        private readonly TestLogger _logger;
        private readonly TextExtractionService _service;

        static TextExtractionServiceTests()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public TextExtractionServiceTests()
        {
            _fileSystem = new InMemoryFileSystem();
            _logger = new TestLogger();
            _service = new TextExtractionService(_fileSystem, _logger);
        }

        #region DumpAndHashInternal Tests

        [Fact]
        public void DumpAndHashInternal_ExtractsSequentialStrings()
        {
            // Arrange
            byte[] data = TestDataFactory.CreateBinaryWithStrings("Hello", "World", "Test");

            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);

            // Act
            var result = _service.DumpAndHashInternal("test.bin", data, br, 0, 0, false, false);

            // Assert
            Assert.Equal(3, result.Count);
            Assert.Equal("Hello", result[0].JString);
            Assert.Equal("World", result[1].JString);
            Assert.Equal("Test", result[2].JString);
        }

        [Fact]
        public void DumpAndHashInternal_CalculatesCrc32Hash()
        {
            // Arrange
            byte[] data = TestDataFactory.CreateBinaryWithStrings("Test");

            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);

            // Act
            var result = _service.DumpAndHashInternal("test.bin", data, br, 0, 0, false, false);

            // Assert
            Assert.Single(result);
            uint expectedHash = LibReFrontier.Crypto.GetCrc32(Encoding.GetEncoding("shift-jis").GetBytes("Test"));
            Assert.Equal(expectedHash, result[0].Hash);
        }

        [Fact]
        public void DumpAndHashInternal_ReplacesTabWithMarker()
        {
            // Arrange
            byte[] data = TestDataFactory.CreateBinaryWithStrings("Hello\tWorld");

            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);

            // Act
            var result = _service.DumpAndHashInternal("test.bin", data, br, 0, 0, false, false);

            // Assert
            Assert.Single(result);
            Assert.Equal("Hello<TAB>World", result[0].JString);
        }

        [Fact]
        public void DumpAndHashInternal_ReplacesNewlineWithMarker()
        {
            // Arrange
            byte[] data = TestDataFactory.CreateBinaryWithStrings("Line1\nLine2");

            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);

            // Act
            var result = _service.DumpAndHashInternal("test.bin", data, br, 0, 0, false, false);

            // Assert
            Assert.Single(result);
            Assert.Equal("Line1<NLINE>Line2", result[0].JString);
        }

        [Fact]
        public void DumpAndHashInternal_ReplacesCarriageReturnNewlineWithMarker()
        {
            // Arrange
            byte[] data = TestDataFactory.CreateBinaryWithStrings("Line1\r\nLine2");

            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);

            // Act
            var result = _service.DumpAndHashInternal("test.bin", data, br, 0, 0, false, false);

            // Assert
            Assert.Single(result);
            Assert.Equal("Line1<CLINE>Line2", result[0].JString);
        }

        [Fact]
        public void DumpAndHashInternal_RespectsOffsetRange()
        {
            // Arrange
            string[] strings = { "First", "Second", "Third", "Fourth" };
            byte[] data = TestDataFactory.CreateBinaryWithStrings(strings);

            // Calculate offset of "Second"
            int secondStart = 6; // "First" (5) + null (1)
            int thirdEnd = secondStart + 7 + 6; // "Second" (6) + null (1) + "Third" (5) + null (1)

            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);

            // Act - extract only "Second" and "Third"
            var result = _service.DumpAndHashInternal("test.bin", data, br, secondStart, thirdEnd, false, false);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal("Second", result[0].JString);
            Assert.Equal("Third", result[1].JString);
        }

        [Fact]
        public void DumpAndHashInternal_WithTrueOffsets_FollowsPointers()
        {
            // Arrange
            // CreateBinaryWithStringPointers creates:
            // - Pointer table at offset 0 (8 bytes for 2 pointers)
            // - Padding to offset 16
            // - String data at offset 16+
            byte[] data = TestDataFactory.CreateBinaryWithStringPointers("Hello", "World");

            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);

            // Act - endOffset=8 covers both pointers (2 pointers × 4 bytes)
            // The pointers point to strings at offset 16+ which pass the strPos >= 10 check
            var result = _service.DumpAndHashInternal("test.bin", data, br, 0, 8, true, false);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal("Hello", result[0].JString);
            Assert.Equal("World", result[1].JString);
        }

        [Fact]
        public void DumpAndHashInternal_HandlesJapaneseText()
        {
            // Arrange
            byte[] data = TestDataFactory.CreateBinaryWithStrings("こんにちは", "世界");

            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);

            // Act
            var result = _service.DumpAndHashInternal("test.bin", data, br, 0, 0, false, false);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal("こんにちは", result[0].JString);
            Assert.Equal("世界", result[1].JString);
        }

        #endregion

        #region WriteCsv Tests

        [Fact]
        public void WriteCsv_CreatesValidCsvFile()
        {
            // Arrange
            var stringsDb = new List<StringDatabase>
            {
                new() { Offset = 0, Hash = 123, JString = "Test1", EString = "Trans1" },
                new() { Offset = 10, Hash = 456, JString = "Test2", EString = "" }
            };

            // Act
            _service.WriteCsv("/test/input.bin", stringsDb);

            // Assert
            Assert.True(_fileSystem.FileExists("input.csv"));
        }

        [Fact]
        public void WriteCsv_DeletesExistingFile()
        {
            // Arrange
            _fileSystem.AddFile("existing.csv", "old content");
            var stringsDb = new List<StringDatabase>
            {
                new() { Offset = 0, Hash = 123, JString = "New", EString = "" }
            };

            // Act
            _service.WriteCsv("/test/existing.bin", stringsDb);

            // Assert - file should be recreated
            Assert.True(_fileSystem.FileExists("existing.csv"));
            string content = _fileSystem.ReadAllText("existing.csv", Encoding.GetEncoding("shift-jis"));
            Assert.Contains("New", content);
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullFileSystem_ThrowsArgumentNullException()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
                new TextExtractionService(null!, _logger));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
                new TextExtractionService(_fileSystem, null!));
        }

        [Fact]
        public void DefaultConstructor_CreatesValidInstance()
        {
            var service = new TextExtractionService();
            Assert.NotNull(service);
        }

        #endregion
    }
}
