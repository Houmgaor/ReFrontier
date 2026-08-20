using LibReFrontier;

using ReFrontier.CLI;
using ReFrontier.Tests.Mocks;

namespace ReFrontier.Tests.CLI
{
    /// <summary>
    /// Tests for the verb form of the CLI (<c>ReFrontier unpack file.bin</c>).
    /// <para>Each verb has to produce the same <see cref="CliArguments"/> the equivalent
    /// legacy flag produced, since everything downstream reads only that DTO.</para>
    /// </summary>
    public class VerbCommandTests
    {
        private static CliArguments Parse(params string[] args)
        {
            var schema = new CliSchema();
            var command = schema.CreateRootCommand("1.0.0", "TestApp", "Test");
            return schema.ExtractArguments(command.Parse(args));
        }

        /// <summary>
        /// Parse the same intent both ways and assert the DTOs match.
        /// </summary>
        private static void AssertSameAsLegacy(string[] legacy, string[] verb)
        {
            var fromLegacy = Parse(legacy);
            var fromVerb = Parse(verb);

            Assert.Equal(fromLegacy.FilePath, fromVerb.FilePath);
            Assert.Equal(fromLegacy.ProcessingArgs, fromVerb.ProcessingArgs);
            Assert.Equal(fromLegacy.Validate, fromVerb.Validate);
            Assert.Equal(fromLegacy.Restore, fromVerb.Restore);
            Assert.Equal(fromLegacy.DiffPath, fromVerb.DiffPath);
            Assert.Equal(fromLegacy.CompressionLevel, fromVerb.CompressionLevel);
            Assert.Equal(fromLegacy.Quiet, fromVerb.Quiet);
            Assert.Equal(fromLegacy.Verbose, fromVerb.Verbose);
        }

        [Fact]
        public void Unpack_MatchesBarePath()
        {
            AssertSameAsLegacy(["mhfdat.bin"], ["unpack", "mhfdat.bin"]);
        }

        [Fact]
        public void Decrypt_MatchesDecryptOnly()
        {
            AssertSameAsLegacy(["mhfdat.bin", "--decryptOnly"], ["decrypt", "mhfdat.bin"]);
        }

        [Fact]
        public void Pack_MatchesPackFlag()
        {
            AssertSameAsLegacy(["dir.unpacked", "--pack"], ["pack", "dir.unpacked"]);
        }

        [Fact]
        public void Restore_MatchesRestoreFlag()
        {
            AssertSameAsLegacy(["mhfdat.bin.decd.bin", "--restore"], ["restore", "mhfdat.bin.decd.bin"]);
        }

        [Fact]
        public void Validate_MatchesValidateFlag()
        {
            AssertSameAsLegacy(["mhfdat.bin", "--validate"], ["validate", "mhfdat.bin"]);
        }

        [Fact]
        public void Encrypt_MatchesEncryptFlag()
        {
            AssertSameAsLegacy(["mhfdat.bin", "--encrypt"], ["encrypt", "mhfdat.bin"]);
        }

        [Fact]
        public void Compress_MatchesCompressFlags()
        {
            AssertSameAsLegacy(
                ["mhfdat.bin", "--compress", "lz", "--level", "50"],
                ["compress", "mhfdat.bin", "--type", "lz", "--level", "50"]);
        }

        [Fact]
        public void CompressWithEncrypt_MatchesCombinedLegacyFlags()
        {
            AssertSameAsLegacy(
                ["mhfdat.bin", "--compress", "hfi", "--level", "80", "--encrypt"],
                ["compress", "mhfdat.bin", "--type", "hfi", "--level", "80", "--encrypt"]);
        }

        [Fact]
        public void Diff_MatchesDiffFlag()
        {
            AssertSameAsLegacy(
                ["first.bin", "--diff", "second.bin"],
                ["diff", "first.bin", "second.bin"]);
        }

        [Fact]
        public void UnpackModifiers_MatchLegacySpellings()
        {
            AssertSameAsLegacy(
                ["mhfdat.bin", "--nonRecursive", "--ignoreJPK", "--noDecryption", "--cleanUp", "--noMeta"],
                ["unpack", "mhfdat.bin", "--flat", "--keep-compressed", "--keep-encrypted", "--clean", "--no-meta"]);
        }

        [Fact]
        public void StageModifiers_MatchLegacySpellings()
        {
            AssertSameAsLegacy(
                ["stage.bin", "--stageContainer", "--autoStage"],
                ["unpack", "stage.bin", "--stage", "--auto-stage"]);
        }

        [Fact]
        public void VerbAcceptsHiddenLegacyOptionSpelling()
        {
            // A script can adopt the verb without renaming every option on the same line.
            var kebab = Parse("unpack", "mhfdat.bin", "--flat", "--keep-compressed");
            var camel = Parse("unpack", "mhfdat.bin", "--nonRecursive", "--ignoreJPK");

            Assert.Equal(kebab.ProcessingArgs, camel.ProcessingArgs);
            Assert.False(camel.ProcessingArgs.recursive);
            Assert.True(camel.ProcessingArgs.ignoreJPK);
        }

        [Fact]
        public void Unpack_WritesMetadataByDefault()
        {
            Assert.True(Parse("unpack", "mhfdat.bin").ProcessingArgs.createLog);
        }

        [Fact]
        public void CompressWithoutLevel_UsesDefaultLevel()
        {
            // The legacy form rejects a missing --level; the verb has a sensible default.
            var args = Parse("compress", "mhfdat.bin", "--type", "hfi");

            Assert.Equal(CliSchema.DefaultCompressionLevel, args.ProcessingArgs.compression.Level);
            Assert.Equal(CompressionType.HFI, args.ProcessingArgs.compression.Type);
        }

        [Fact]
        public void CompressWithoutType_IsRejected()
        {
            var schema = new CliSchema();
            var command = schema.CreateRootCommand("1.0.0", "TestApp", "Test");

            var parseResult = command.Parse(["compress", "mhfdat.bin"]);

            Assert.NotEmpty(parseResult.Errors);
        }

        [Fact]
        public void RestoreWithoutLevel_LeavesRecipeLevelAlone()
        {
            Assert.Null(Parse("restore", "file.bin").CompressionLevel);
        }

        [Fact]
        public void RestoreWithLevel_OverridesRecipeLevel()
        {
            Assert.Equal(100, Parse("restore", "file.bin", "--level", "100").CompressionLevel);
        }

        [Fact]
        public void GlobalOptions_WorkAfterTheVerb()
        {
            var args = Parse("unpack", "mhfdat.bin", "--verbose", "--quiet");

            Assert.True(args.Verbose);
            Assert.True(args.Quiet);
            Assert.Equal("mhfdat.bin", args.FilePath);
        }

        [Fact]
        public void GlobalOptions_WorkBeforeTheVerb()
        {
            var args = Parse("--verbose", "--quiet", "unpack", "mhfdat.bin");

            Assert.True(args.Verbose);
            Assert.True(args.Quiet);
            Assert.Equal("mhfdat.bin", args.FilePath);
        }

        [Fact]
        public void Parallelism_IsSharedWithVerbs()
        {
            Assert.Equal(8, Parse("unpack", "dir/", "--parallelism", "8").Parallelism);
        }

        [Fact]
        public void BarePathWithoutVerb_StillParses()
        {
            // The most common invocation must keep working now that subcommands exist.
            var schema = new CliSchema();
            var command = schema.CreateRootCommand("1.0.0", "TestApp", "Test");

            var parseResult = command.Parse(["mhfdat.bin"]);

            Assert.Empty(parseResult.Errors);
            Assert.Equal("mhfdat.bin", schema.ExtractArguments(parseResult).FilePath);
        }

        [Fact]
        public void NoArguments_ReportsMissingInput()
        {
            var schema = new CliSchema();
            var command = schema.CreateRootCommand("1.0.0", "TestApp", "Test");

            var exception = Assert.Throws<InvalidOperationException>(
                () => schema.ExtractArguments(command.Parse(Array.Empty<string>())));

            Assert.Contains("No input file or directory specified", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void VerbPathCollision_IsReportedWhenPathExists()
        {
            var fileSystem = new InMemoryFileSystem();
            fileSystem.AddDirectory("pack");

            var message = CliSchema.DescribeVerbPathCollision(["pack"], fileSystem);

            Assert.NotNull(message);
            Assert.Contains("the command wins", message, StringComparison.Ordinal);
        }

        [Fact]
        public void VerbPathCollision_IsSilentWhenNoSuchPath()
        {
            Assert.Null(CliSchema.DescribeVerbPathCollision(["pack"], new InMemoryFileSystem()));
        }

        [Fact]
        public void VerbPathCollision_IsSilentForOrdinaryPaths()
        {
            var fileSystem = new InMemoryFileSystem();
            fileSystem.AddFile("mhfdat.bin", [0x00]);

            Assert.Null(CliSchema.DescribeVerbPathCollision(["mhfdat.bin"], fileSystem));
        }

        [Fact]
        public void VerbPathCollision_IsSilentWhenTheVerbHasAnArgument()
        {
            // 'pack dir/' is unambiguous even with a ./pack directory present.
            var fileSystem = new InMemoryFileSystem();
            fileSystem.AddDirectory("pack");

            Assert.Null(CliSchema.DescribeVerbPathCollision(["pack", "dir/"], fileSystem));
        }
    }
}
