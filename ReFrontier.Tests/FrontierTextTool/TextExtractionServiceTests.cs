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
            Assert.Equal("Hello", result[0].Original);
            Assert.Equal("World", result[1].Original);
            Assert.Equal("Test", result[2].Original);
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
        public void DumpAndHashInternal_PreservesTabInString()
        {
            // Arrange - tabs are preserved as-is, RFC 4180 CSV will quote them
            byte[] data = TestDataFactory.CreateBinaryWithStrings("Hello\tWorld");

            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);

            // Act
            var result = _service.DumpAndHashInternal("test.bin", data, br, 0, 0, false, false);

            // Assert
            Assert.Single(result);
            Assert.Equal("Hello\tWorld", result[0].Original);
        }

        [Fact]
        public void DumpAndHashInternal_PreservesNewlineInString()
        {
            // Arrange - newlines are preserved as-is, RFC 4180 CSV will quote them
            byte[] data = TestDataFactory.CreateBinaryWithStrings("Line1\nLine2");

            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);

            // Act
            var result = _service.DumpAndHashInternal("test.bin", data, br, 0, 0, false, false);

            // Assert
            Assert.Single(result);
            Assert.Equal("Line1\nLine2", result[0].Original);
        }

        [Fact]
        public void DumpAndHashInternal_PreservesCarriageReturnNewlineInString()
        {
            // Arrange - CRLF is preserved as-is, RFC 4180 CSV will quote them
            byte[] data = TestDataFactory.CreateBinaryWithStrings("Line1\r\nLine2");

            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);

            // Act
            var result = _service.DumpAndHashInternal("test.bin", data, br, 0, 0, false, false);

            // Assert
            Assert.Single(result);
            Assert.Equal("Line1\r\nLine2", result[0].Original);
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
            Assert.Equal("Second", result[0].Original);
            Assert.Equal("Third", result[1].Original);
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
            Assert.Equal("Hello", result[0].Original);
            Assert.Equal("World", result[1].Original);
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
            Assert.Equal("こんにちは", result[0].Original);
            Assert.Equal("世界", result[1].Original);
        }

        #endregion

        #region WriteCsv Tests

        [Fact]
        public void WriteCsv_CreatesValidCsvFile()
        {
            // Arrange
            var stringsDb = new List<StringDatabase>
            {
                new() { Offset = 0, Hash = 123, Original = "Test1", Translation = "Trans1" },
                new() { Offset = 10, Hash = 456, Original = "Test2", Translation = "" }
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
                new() { Offset = 0, Hash = 123, Original = "New", Translation = "" }
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

        [Fact]
        public void Constructor_WithEncodingOptions_CreatesValidInstance()
        {
            var encodingOptions = LibReFrontier.CsvEncodingOptions.ShiftJis;
            var service = new TextExtractionService(_fileSystem, _logger, encodingOptions);
            Assert.NotNull(service);
        }

        #endregion

        #region Additional DumpAndHashInternal Tests

        [Fact]
        public void DumpAndHashInternal_PreservesBackslashInString()
        {
            // Arrange - backslash is preserved as-is in RFC 4180 CSV
            byte[] data = TestDataFactory.CreateBinaryWithStrings("Path\\File");

            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);

            // Act
            var result = _service.DumpAndHashInternal("test.bin", data, br, 0, 0, false, false);

            // Assert
            Assert.Single(result);
            Assert.Equal("Path\\File", result[0].Original);
        }

        [Fact]
        public void DumpAndHashInternal_WithTrueOffsets_SkipsInvalidPointerTooSmall()
        {
            // Arrange - pointer value < 10 should be skipped
            byte[] data = new byte[50];
            // Write a pointer with value 5 (less than 10)
            BitConverter.GetBytes(5).CopyTo(data, 0);
            // Write a valid pointer
            BitConverter.GetBytes(20).CopyTo(data, 4);
            // Write string at offset 20
            Encoding.GetEncoding("shift-jis").GetBytes("Valid").CopyTo(data, 20);

            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);

            // Act - read first 8 bytes (2 pointers)
            var result = _service.DumpAndHashInternal("test.bin", data, br, 0, 8, true, false);

            // Assert - only one string extracted (the valid pointer)
            Assert.Single(result);
            Assert.Equal("Valid", result[0].Original);
        }

        [Fact]
        public void DumpAndHashInternal_WithTrueOffsets_SkipsPointerBeyondFileLength()
        {
            // Arrange - pointer beyond file length should be skipped
            byte[] data = new byte[50];
            // Write a pointer with value 100 (beyond file length)
            BitConverter.GetBytes(100).CopyTo(data, 0);
            // Write a valid pointer
            BitConverter.GetBytes(20).CopyTo(data, 4);
            // Write string at offset 20
            Encoding.GetEncoding("shift-jis").GetBytes("Valid").CopyTo(data, 20);

            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);

            // Act
            var result = _service.DumpAndHashInternal("test.bin", data, br, 0, 8, true, false);

            // Assert - only valid pointer extracted
            Assert.Single(result);
            Assert.Equal("Valid", result[0].Original);
        }

        [Fact]
        public void DumpAndHashInternal_WithCheckNullPredecessor_SkipsStringWithoutNullBefore()
        {
            // Arrange - create data where string doesn't have null predecessor
            byte[] data = new byte[50];
            // Write pointer to offset 20
            BitConverter.GetBytes(20).CopyTo(data, 0);
            // Write non-null byte before string position (at offset 18)
            data[18] = 0x41; // 'A'
            data[19] = 0x00; // This should be non-null for skip
            // Actually the check is: if (byte at strPos-2 == 0 || byte at strPos-1 != 0) continue
            // So we need: strPos-2 != 0 AND strPos-1 == 0
            // To pass: strPos-2 must be != 0, strPos-1 must be == 0
            data[18] = 0x00; // strPos-2 = 0, should fail first condition and skip
            data[19] = 0x00; // strPos-1 = 0, second condition check
            // Write string at offset 20
            Encoding.GetEncoding("shift-jis").GetBytes("Test").CopyTo(data, 20);

            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);

            // Act - with checkNullPredecessor = true
            var result = _service.DumpAndHashInternal("test.bin", data, br, 0, 4, true, true);

            // Assert - string should be skipped because predecessor check fails
            Assert.Empty(result);
        }

        [Fact]
        public void DumpAndHashInternal_WithCheckNullPredecessor_IncludesValidString()
        {
            // Arrange - string with proper null predecessor
            byte[] data = new byte[50];
            // Write pointer to offset 22
            BitConverter.GetBytes(22).CopyTo(data, 0);
            // Set up proper predecessor: strPos-2 != 0, strPos-1 == 0
            data[20] = 0x41; // strPos-2 != 0 (passes first check)
            data[21] = 0x00; // strPos-1 == 0 (passes second check)
            // Write string at offset 22
            Encoding.GetEncoding("shift-jis").GetBytes("Valid").CopyTo(data, 22);

            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);

            // Act
            var result = _service.DumpAndHashInternal("test.bin", data, br, 0, 4, true, true);

            // Assert
            Assert.Single(result);
            Assert.Equal("Valid", result[0].Original);
        }

        [Fact]
        public void DumpAndHashInternal_ExtractsMultipleStringsWithMixedLengths()
        {
            // Arrange - strings with mixed lengths
            byte[] data = TestDataFactory.CreateBinaryWithStrings("Short", "LongerString", "Med");

            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);

            // Act
            var result = _service.DumpAndHashInternal("test.bin", data, br, 0, 0, false, false);

            // Assert
            Assert.Equal(3, result.Count);
            Assert.Equal("Short", result[0].Original);
            Assert.Equal("LongerString", result[1].Original);
            Assert.Equal("Med", result[2].Original);
        }

        [Fact]
        public void DumpAndHashInternal_LogsOffsetRange()
        {
            // Arrange
            byte[] data = TestDataFactory.CreateBinaryWithStrings("Test");

            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);

            // Act
            _service.DumpAndHashInternal("test.bin", data, br, 0, 0, false, false);

            // Assert
            Assert.True(_logger.ContainsMessage("Strings at:"));
        }

        [Fact]
        public void DumpAndHashInternal_EndOffsetZero_UsesStreamLength()
        {
            // Arrange
            byte[] data = TestDataFactory.CreateBinaryWithStrings("Test");

            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);

            // Act - endOffset = 0 should use stream length
            var result = _service.DumpAndHashInternal("test.bin", data, br, 0, 0, false, false);

            // Assert
            Assert.Single(result);
            Assert.Equal("Test", result[0].Original);
        }

        #endregion

        #region WriteCsv Encoding Tests

        [Fact]
        public void WriteCsv_WithShiftJisEncoding_CreatesFile()
        {
            // Arrange
            var encodingOptions = LibReFrontier.CsvEncodingOptions.ShiftJis;
            var service = new TextExtractionService(_fileSystem, _logger, encodingOptions);

            var stringsDb = new List<StringDatabase>
            {
                new() { Offset = 0, Hash = 123, Original = "日本語", Translation = "" }
            };

            // Act
            service.WriteCsv("/test/input.bin", stringsDb);

            // Assert
            Assert.True(_fileSystem.FileExists("input.csv"));
        }

        #endregion
    }
}
