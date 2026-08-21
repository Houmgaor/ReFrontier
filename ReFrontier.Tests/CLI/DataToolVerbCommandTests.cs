using FrontierDataTool.CLI;

namespace ReFrontier.Tests.CLI
{
    /// <summary>
    /// Tests for the verb form of FrontierDataTool
    /// (<c>FrontierDataTool import Armor.csv --mhfdat mhfdat.bin</c>).
    /// <para>Each verb has to produce the same <see cref="CliArguments"/> the equivalent
    /// legacy flag produced, since everything downstream reads only that DTO.</para>
    /// </summary>
    public class DataToolVerbCommandTests
    {
        private static CliArguments Parse(params string[] args)
        {
            var schema = new CliSchema();
            var command = schema.CreateRootCommand();
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
            Assert.Equal(fromLegacy.Suffix, fromVerb.Suffix);
            Assert.Equal(fromLegacy.MhfPac, fromVerb.MhfPac);
            Assert.Equal(fromLegacy.MhfDat, fromVerb.MhfDat);
            Assert.Equal(fromLegacy.MhfInf, fromVerb.MhfInf);
            Assert.Equal(fromLegacy.Rengoku, fromVerb.Rengoku);
            Assert.Equal(fromLegacy.CsvPath, fromVerb.CsvPath);
            Assert.Equal(fromLegacy.Close, fromVerb.Close);
            Assert.Equal(fromLegacy.Cp932, fromVerb.Cp932);
            Assert.Equal(fromLegacy.Json, fromVerb.Json);
        }

        [Fact]
        public void Dump_MatchesLegacyDump()
        {
            AssertSameAsLegacy(
                ["--dump", "--suffix", "demo", "--mhfpac", "mhfpac.bin", "--mhfdat", "mhfdat.bin", "--mhfinf", "mhfinf.bin"],
                ["dump", "--suffix", "demo", "--mhfpac", "mhfpac.bin", "--mhfdat", "mhfdat.bin", "--mhfinf", "mhfinf.bin"]);
        }

        [Fact]
        public void RengokuDump_MatchesLegacyDump()
        {
            AssertSameAsLegacy(
                ["--dump", "--rengoku", "rengoku_data.bin"],
                ["dump", "--rengoku", "rengoku_data.bin"]);
        }

        [Fact]
        public void ModShop_MatchesLegacyModshop()
        {
            AssertSameAsLegacy(
                ["--modshop", "--mhfdat", "mhfdat.bin"],
                ["modshop", "mhfdat.bin"]);
        }

        [Fact]
        public void ModShop_AcceptsHiddenLegacyOptionSpelling()
        {
            // A script can adopt the verb without moving the file off --mhfdat.
            Assert.Equal("mhfdat.bin", Parse("modshop", "--mhfdat", "mhfdat.bin").MhfDat);
        }

        [Fact]
        public void Import_MatchesLegacyImport()
        {
            AssertSameAsLegacy(
                ["--import", "--csv", "Armor.csv", "--mhfdat", "mhfdat.bin", "--mhfpac", "mhfpac.bin"],
                ["import", "Armor.csv", "--mhfdat", "mhfdat.bin", "--mhfpac", "mhfpac.bin"]);
        }

        [Fact]
        public void Import_AcceptsHiddenLegacyCsvOption()
        {
            Assert.Equal("Melee.csv", Parse("import", "--csv", "Melee.csv", "--mhfdat", "mhfdat.bin").CsvPath);
        }

        [Fact]
        public void GlobalOptions_WorkAfterTheVerb()
        {
            var args = Parse("dump", "--rengoku", "rengoku_data.bin", "--close", "--shift-jis", "--json");

            Assert.True(args.Close);
            Assert.True(args.Cp932);
            Assert.True(args.Json);
        }

        [Fact]
        public void GlobalOptions_WorkBeforeTheVerb()
        {
            var args = Parse("--json", "import", "Armor.csv");

            Assert.True(args.Json);
            Assert.Equal("Armor.csv", args.CsvPath);
        }

        [Fact]
        public void Cp932_AcceptsTheOldShiftJisSpelling()
        {
            // The flag was renamed because it named a narrower encoding than it selected.
            Assert.True(Parse("import", "Armor.csv", "--cp932").Cp932);
            Assert.True(Parse("import", "Armor.csv", "--shift-jis").Cp932);
            Assert.False(Parse("import", "Armor.csv").Cp932);
        }

        [Fact]
        public void NoCommand_ReportsMissingCommand()
        {
            var exception = ParseError("--mhfdat", "mhfdat.bin");

            Assert.IsType<System.InvalidOperationException>(exception);
            Assert.Contains("No command given", exception!.Message, System.StringComparison.Ordinal);
        }

        [Fact]
        public void TwoLegacyCommands_AreRejected()
        {
            var exception = ParseError("--dump", "--modshop", "--mhfdat", "mhfdat.bin");

            Assert.IsType<System.InvalidOperationException>(exception);
            Assert.Contains("Only one command", exception!.Message, System.StringComparison.Ordinal);
        }
    }
}
