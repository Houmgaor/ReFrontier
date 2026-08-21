using System;

using LibReFrontier;

using ReFrontier.CLI;

namespace ReFrontier.Tests.CLI
{
    /// <summary>
    /// Tests for CliSchema class.
    /// </summary>
    public class CliSchemaTests
    {
        [Fact]
        public void ExtractArguments_ByDefault_WritesMetadata()
        {
            var schema = new CliSchema();
            var command = schema.CreateRootCommand("1.0.0", "TestApp", "Test");

            // Without this a file cannot be rebuilt, and the option was easy to forget
            // until the rebuild failed.
            var cliArgs = schema.ExtractArguments(command.Parse(["mhfdat.bin"]));

            Assert.True(cliArgs.ProcessingArgs.createLog);
        }

        [Fact]
        public void ExtractArguments_WithNoMeta_SkipsMetadata()
        {
            var schema = new CliSchema();
            var command = schema.CreateRootCommand("1.0.0", "TestApp", "Test");

            var cliArgs = schema.ExtractArguments(command.Parse(["mhfdat.bin", "--noMeta"]));

            Assert.False(cliArgs.ProcessingArgs.createLog);
        }

        [Fact]
        public void ExtractArguments_WithDeprecatedSaveMeta_StillWritesMetadata()
        {
            var schema = new CliSchema();
            var command = schema.CreateRootCommand("1.0.0", "TestApp", "Test");

            // Existing scripts keep working.
            var cliArgs = schema.ExtractArguments(command.Parse(["mhfdat.bin", "--saveMeta"]));

            Assert.True(cliArgs.ProcessingArgs.createLog);
        }

        [Fact]
        public void ExtractArguments_NoMetaWinsOverDeprecatedSaveMeta()
        {
            var schema = new CliSchema();
            var command = schema.CreateRootCommand("1.0.0", "TestApp", "Test");

            var cliArgs = schema.ExtractArguments(command.Parse(["mhfdat.bin", "--saveMeta", "--noMeta"]));

            Assert.False(cliArgs.ProcessingArgs.createLog);
        }

        [Fact]
        public void ExtractArguments_WithRestore_SetsRestoreFlag()
        {
            var schema = new CliSchema();
            var command = schema.CreateRootCommand("1.0.0", "TestApp", "Test");

            var cliArgs = schema.ExtractArguments(command.Parse(["mhfdat.bin.decd.bin", "--restore"]));

            Assert.True(cliArgs.Restore);
            Assert.Null(cliArgs.CompressionLevel);
        }

        [Fact]
        public void ExtractArguments_RestoreWithLevel_DoesNotRequireCompress()
        {
            var schema = new CliSchema();
            var command = schema.CreateRootCommand("1.0.0", "TestApp", "Test");

            // --level alone is valid here: it overrides the level the recipe would use.
            var cliArgs = schema.ExtractArguments(command.Parse(["file.bin", "--restore", "--level", "100"]));

            Assert.True(cliArgs.Restore);
            Assert.Equal(100, cliArgs.CompressionLevel);
        }

        [Fact]
        public void ExtractArguments_WithoutRestore_RestoreIsFalse()
        {
            var schema = new CliSchema();
            var command = schema.CreateRootCommand("1.0.0", "TestApp", "Test");

            var cliArgs = schema.ExtractArguments(command.Parse(["test.bin", "--saveMeta"]));

            Assert.False(cliArgs.Restore);
        }

        [Fact]
        public void CreateRootCommand_ReturnsValidCommand()
        {
            var schema = new CliSchema();
            var command = schema.CreateRootCommand("1.0.0", "TestApp", "Test Description");

            Assert.NotNull(command);
            Assert.Contains("TestApp", command.Description);
        }

        [Fact]
        public void ExtractArguments_ValidArgs_ReturnsCliArguments()
        {
            var schema = new CliSchema();
            var command = schema.CreateRootCommand("1.0.0", "TestApp", "Test");

            var args = new[] { "test.bin", "--saveMeta" };
            var parseResult = command.Parse(args);
            var cliArgs = schema.ExtractArguments(parseResult);

            Assert.Equal("test.bin", cliArgs.FilePath);
            Assert.True(cliArgs.ProcessingArgs.createLog);
        }

        [Fact]
        public void ExtractArguments_WithCompress_ParsesCorrectly()
        {
            var schema = new CliSchema();
            var command = schema.CreateRootCommand("1.0.0", "TestApp", "Test");

            var args = new[] { "test.bin", "--compress", "lz", "--level", "100" };
            var parseResult = command.Parse(args);
            var cliArgs = schema.ExtractArguments(parseResult);

            Assert.Equal("test.bin", cliArgs.FilePath);
            Assert.Equal(CompressionType.LZ, cliArgs.ProcessingArgs.compression.Type);
            Assert.Equal(100, cliArgs.ProcessingArgs.compression.Level);
        }

        [Fact]
        public void ExtractArguments_CompressWithoutLevel_ThrowsException()
        {
            var schema = new CliSchema();
            var command = schema.CreateRootCommand("1.0.0", "TestApp", "Test");

            var args = new[] { "test.bin", "--compress", "lz" };
            var parseResult = command.Parse(args);

            Assert.Throws<InvalidOperationException>(() => schema.ExtractArguments(parseResult));
        }

        [Fact]
        public void ExtractArguments_NonRecursive_SetsRecursiveFalse()
        {
            var schema = new CliSchema();
            var command = schema.CreateRootCommand("1.0.0", "TestApp", "Test");

            var args = new[] { "test.bin", "--nonRecursive" };
            var parseResult = command.Parse(args);
            var cliArgs = schema.ExtractArguments(parseResult);

            Assert.False(cliArgs.ProcessingArgs.recursive);
        }

        [Fact]
        public void ExtractArguments_RecursiveByDefault()
        {
            var schema = new CliSchema();
            var command = schema.CreateRootCommand("1.0.0", "TestApp", "Test");

            var args = new[] { "test.bin" };
            var parseResult = command.Parse(args);
            var cliArgs = schema.ExtractArguments(parseResult);

            Assert.True(cliArgs.ProcessingArgs.recursive);
        }

        [Fact]
        public void ExtractArguments_ParallelismNotSpecified_DefaultsToZero()
        {
            var schema = new CliSchema();
            var command = schema.CreateRootCommand("1.0.0", "TestApp", "Test");

            var args = new[] { "test.bin" };
            var parseResult = command.Parse(args);
            var cliArgs = schema.ExtractArguments(parseResult);

            Assert.Equal(0, cliArgs.Parallelism);
        }

        [Fact]
        public void ExtractArguments_ExplicitParallelism_ParsesCorrectly()
        {
            var schema = new CliSchema();
            var command = schema.CreateRootCommand("1.0.0", "TestApp", "Test");

            var args = new[] { "test.bin", "--parallelism", "8" };
            var parseResult = command.Parse(args);
            var cliArgs = schema.ExtractArguments(parseResult);

            Assert.Equal(8, cliArgs.Parallelism);
        }

        [Fact]
        public void ExtractArguments_ParallelismZero_AllowedForAutoDetect()
        {
            var schema = new CliSchema();
            var command = schema.CreateRootCommand("1.0.0", "TestApp", "Test");

            var args = new[] { "test.bin", "--parallelism", "0" };
            var parseResult = command.Parse(args);
            var cliArgs = schema.ExtractArguments(parseResult);

            Assert.Equal(0, cliArgs.Parallelism);
        }
    }
}
