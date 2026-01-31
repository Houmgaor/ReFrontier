using LibReFrontier;

using ReFrontier.CLI;
using ReFrontier.Jpk;
using ReFrontier.Orchestration;
using ReFrontier.Services;
using ReFrontier.Tests.Mocks;

namespace ReFrontier.Tests.Orchestration
{
    /// <summary>
    /// Tests for ApplicationOrchestrator class.
    /// </summary>
    public class ApplicationOrchestratorTests
    {
        private readonly InMemoryFileSystem _fileSystem;
        private readonly TestLogger _logger;
        private readonly ApplicationOrchestrator _orchestrator;

        public ApplicationOrchestratorTests()
        {
            _fileSystem = new InMemoryFileSystem();
            _logger = new TestLogger();
            _orchestrator = new ApplicationOrchestrator(
                _fileSystem,
                _logger,
                new DefaultCodecFactory(),
                FileProcessingConfig.Default(),
                "TestApp",
                "1.0.0",
                "Test Description"
            );
        }

        [Fact]
        public void Execute_FileDoesNotExist_ReturnsError()
        {
            var args = new CliArguments
            {
                FilePath = "nonexistent.bin",
                ProcessingArgs = new InputArguments()
            };

            var result = _orchestrator.Execute(args);

            Assert.Equal(1, result);
            Assert.Contains("does not exist", _logger.Output);
        }

        [Fact]
        public void Execute_CompressDirectory_ReturnsError()
        {
            _fileSystem.CreateDirectory("testdir");

            var args = new CliArguments
            {
                FilePath = "testdir",
                ProcessingArgs = new InputArguments
                {
                    compression = new Compression(CompressionType.LZ, 100)
                }
            };

            var result = _orchestrator.Execute(args);

            Assert.Equal(1, result);
            Assert.Contains("Cannot compress a directory", _logger.Output);
        }

        [Fact]
        public void Execute_EncryptDirectory_ReturnsError()
        {
            _fileSystem.CreateDirectory("testdir");

            var args = new CliArguments
            {
                FilePath = "testdir",
                ProcessingArgs = new InputArguments
                {
                    encrypt = true
                }
            };

            var result = _orchestrator.Execute(args);

            Assert.Equal(1, result);
            Assert.Contains("Cannot encrypt a directory", _logger.Output);
        }

        [Fact]
        public void Execute_RepackFile_ReturnsError()
        {
            _fileSystem.WriteAllBytes("test.bin", new byte[] { 1, 2, 3 });

            var args = new CliArguments
            {
                FilePath = "test.bin",
                ProcessingArgs = new InputArguments
                {
                    repack = true
                }
            };

            var result = _orchestrator.Execute(args);

            Assert.Equal(1, result);
            Assert.Contains("single file cannot be used while in repacking mode", _logger.Output);
        }

        [Fact]
        public void Execute_ValidFile_ReturnsSuccess()
        {
            // Create a simple file with non-zero content
            var data = new byte[16];
            data[0] = 1; data[1] = 2; data[2] = 3; data[3] = 4; // Non-matching magic
            _fileSystem.WriteAllBytes("test.bin", data);

            var args = new CliArguments
            {
                FilePath = "test.bin",
                ProcessingArgs = new InputArguments()
            };

            var result = _orchestrator.Execute(args);

            Assert.Equal(0, result);
            Assert.Contains("Done", _logger.Output);
        }
    }
}
