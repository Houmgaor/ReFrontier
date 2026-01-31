using System;

using LibReFrontier;

using ReFrontier.CLI;

using Xunit;

namespace ReFrontier.Tests.CLI
{
    /// <summary>
    /// Tests for CliSchema class.
    /// </summary>
    public class CliSchemaTests
    {
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

            var args = new[] { "test.bin", "--log" };
            var parseResult = command.Parse(args);
            var cliArgs = schema.ExtractArguments(parseResult);

            Assert.Equal("test.bin", cliArgs.FilePath);
            Assert.True(cliArgs.ProcessingArgs.createLog);
            Assert.False(cliArgs.CloseAfterCompletion);
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
