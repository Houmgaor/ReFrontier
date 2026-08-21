using FrontierTextTool.CLI;

using ReFrontier.Tests.Mocks;

namespace ReFrontier.Tests.CLI
{
    /// <summary>
    /// Tests for the verb form of FrontierTextTool
    /// (<c>FrontierTextTool dump mhfdat.bin</c>).
    /// <para>Each verb has to produce the same <see cref="CliArguments"/> the equivalent
    /// legacy flag produced, since everything downstream reads only that DTO.</para>
    /// </summary>
    public class TextToolVerbCommandTests
    {
        private static CliArguments Parse(params string[] args)
        {
            var schema = new CliSchema();
            var command = schema.CreateRootCommand("1.0.0");
            return schema.ExtractArguments(command.Parse(args));
        }

        private static System.Exception? ParseError(params string[] args)
        {
            return Record.Exception(() => Parse(args));
        }

        /// <summary>
        /// Parse the same intent both ways and assert the DTOs match.
        /// </summary>
        private static void AssertSameAsLegacy(string[] legacy, string[] verb)
        {
            var fromLegacy = Parse(legacy);
            var fromVerb = Parse(verb);

            Assert.Equal(fromLegacy.Action, fromVerb.Action);
            Assert.Equal(fromLegacy.InputPath, fromVerb.InputPath);
            Assert.Equal(fromLegacy.CsvPath, fromVerb.CsvPath);
            Assert.Equal(fromLegacy.StartIndex, fromVerb.StartIndex);
            Assert.Equal(fromLegacy.EndIndex, fromVerb.EndIndex);
            Assert.Equal(fromLegacy.TrueOffsets, fromVerb.TrueOffsets);
            Assert.Equal(fromLegacy.NullStrings, fromVerb.NullStrings);
            Assert.Equal(fromLegacy.Verbose, fromVerb.Verbose);
            Assert.Equal(fromLegacy.Close, fromVerb.Close);
            Assert.Equal(fromLegacy.ShiftJis, fromVerb.ShiftJis);
        }

        [Fact]
        public void Dump_MatchesFulldump()
        {
            AssertSameAsLegacy(["mhfdat.bin", "--fulldump"], ["dump", "mhfdat.bin"]);
        }

        [Fact]
        public void DumpRange_MatchesLegacyIndices()
        {
            AssertSameAsLegacy(
                ["mhfdat.bin", "--dump", "--startIndex", "3040", "--endIndex", "3328506"],
                ["dump", "mhfdat.bin", "--start-index", "3040", "--end-index", "3328506"]);
        }

        [Fact]
        public void DumpModifiers_MatchLegacySpellings()
        {
            AssertSameAsLegacy(
                ["mhfdat.bin", "--fulldump", "--trueOffsets", "--nullStrings"],
                ["dump", "mhfdat.bin", "--true-offsets", "--null-strings"]);
        }

        [Fact]
        public void Insert_MatchesLegacyInsert()
        {
            AssertSameAsLegacy(
                ["mhfdat.bin", "--insert", "--csv", "mhfdat.csv"],
                ["insert", "mhfdat.bin", "mhfdat.csv"]);
        }

        [Fact]
        public void Merge_MatchesLegacyMerge()
        {
            AssertSameAsLegacy(
                ["old.csv", "--merge", "--csv", "new.csv"],
                ["merge", "old.csv", "new.csv"]);
        }

        [Fact]
        public void CleanTrados_MatchesLegacyFlag()
        {
            AssertSameAsLegacy(["file.csv", "--cleanTrados"], ["clean-trados", "file.csv"]);
        }

        [Fact]
        public void InsertCat_MatchesLegacyFlag()
        {
            AssertSameAsLegacy(
                ["catfile.txt", "--insertCAT", "--csv", "target.csv"],
                ["insert-cat", "catfile.txt", "target.csv"]);
        }

        [Fact]
        public void VerbAcceptsHiddenLegacyOptionSpelling()
        {
            // A script can adopt the verb without renaming every option on the same line.
            var positional = Parse("insert", "mhfdat.bin", "mhfdat.csv", "--true-offsets");
            var legacy = Parse("insert", "mhfdat.bin", "--csv", "mhfdat.csv", "--trueOffsets");

            Assert.Equal(positional.CsvPath, legacy.CsvPath);
            Assert.Equal(positional.TrueOffsets, legacy.TrueOffsets);
            Assert.True(legacy.TrueOffsets);
        }

        [Fact]
        public void DumpWithoutRange_DumpsWholeFile()
        {
            var args = Parse("dump", "mhfdat.bin");

            Assert.Equal(TextToolAction.Dump, args.Action);
            Assert.Equal(0, args.StartIndex);
            Assert.Equal(0, args.EndIndex);
        }

        [Fact]
        public void GlobalOptions_WorkAfterTheVerb()
        {
            var args = Parse("dump", "mhfdat.bin", "--verbose", "--close", "--shift-jis");

            Assert.True(args.Verbose);
            Assert.True(args.Close);
            Assert.True(args.ShiftJis);
        }

        [Fact]
        public void GlobalOptions_WorkBeforeTheVerb()
        {
            var args = Parse("--close", "--shift-jis", "dump", "mhfdat.bin");

            Assert.True(args.Close);
            Assert.True(args.ShiftJis);
            Assert.Equal("mhfdat.bin", args.InputPath);
        }

        [Theory]
        [InlineData("insert")]
        [InlineData("merge")]
        [InlineData("insert-cat")]
        public void VerbNeedingCsv_IsRejectedWithoutOne(string verb)
        {
            var exception = ParseError(verb, "input.bin");

            Assert.IsType<System.InvalidOperationException>(exception);
            Assert.Contains("needs a CSV file", exception!.Message, System.StringComparison.Ordinal);
        }

        [Fact]
        public void LegacyInsertWithoutCsv_IsRejected()
        {
            var exception = ParseError("mhfdat.bin", "--insert");

            Assert.IsType<System.InvalidOperationException>(exception);
            Assert.Contains("needs a CSV file", exception!.Message, System.StringComparison.Ordinal);
        }

        [Fact]
        public void NoCommand_ReportsMissingCommand()
        {
            var exception = ParseError("mhfdat.bin");

            Assert.IsType<System.InvalidOperationException>(exception);
            Assert.Contains("No command given", exception!.Message, System.StringComparison.Ordinal);
        }

        [Fact]
        public void TwoLegacyCommands_AreRejected()
        {
            var exception = ParseError("mhfdat.bin", "--fulldump", "--cleanTrados");

            Assert.IsType<System.InvalidOperationException>(exception);
            Assert.Contains("Only one command", exception!.Message, System.StringComparison.Ordinal);
        }

        [Fact]
        public void LegacyCommandWithoutFile_ReportsMissingInput()
        {
            var exception = ParseError("--fulldump");

            Assert.IsType<System.InvalidOperationException>(exception);
            Assert.Contains("No input file specified", exception!.Message, System.StringComparison.Ordinal);
        }

        [Fact]
        public void VerbPathCollision_IsReportedWhenPathExists()
        {
            var fileSystem = new InMemoryFileSystem();
            fileSystem.AddFile("merge", [0x00]);

            var message = CliSchema.DescribeVerbPathCollision(["merge"], fileSystem);

            Assert.NotNull(message);
            Assert.Contains("the command wins", message, System.StringComparison.Ordinal);
        }

        [Fact]
        public void VerbPathCollision_IsSilentForOrdinaryPaths()
        {
            var fileSystem = new InMemoryFileSystem();
            fileSystem.AddFile("mhfdat.bin", [0x00]);

            Assert.Null(CliSchema.DescribeVerbPathCollision(["mhfdat.bin"], fileSystem));
        }
    }
}
