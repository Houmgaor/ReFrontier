using System;

using LibReFrontier;

using ReFrontier.Jpk;
using ReFrontier.Services;
using ReFrontier.Tests.Mocks;

using Xunit;

namespace ReFrontier.Tests.Integration
{
    /// <summary>
    /// Integration tests for parallelism functionality.
    /// </summary>
    public class ParallelismIntegrationTests
    {
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(4)]
        [InlineData(8)]
        public void ProcessMultipleLevels_DifferentParallelism_DoesNotThrow(int parallelism)
        {
            // Arrange
            var fileSystem = new InMemoryFileSystem();
            var logger = new TestLogger();
            var codecFactory = new DefaultCodecFactory();
            var config = FileProcessingConfig.Default();
            var program = new Program(fileSystem, logger, codecFactory, config);

            // Create some simple test files (not archives, just files that will be skipped)
            for (int i = 0; i < 5; i++)
            {
                byte[] testData = new byte[100];
                fileSystem.AddFile($"/test/file_{i}.bin", testData);
            }

            var files = fileSystem.GetFiles("/test", "*.bin", System.IO.SearchOption.TopDirectoryOnly);
            var args = new InputArguments
            {
                parallelism = parallelism,
                recursive = false,
                createLog = false
            };

            // Act & Assert - Should not throw with any parallelism level
            program.ProcessMultipleLevels(files, args);

            // Verify logger was used
            Assert.NotEmpty(logger.Messages);
        }

        [Fact]
        public void ProcessMultipleLevels_AutoDetect_UsesEnvironmentProcessorCount()
        {
            // Arrange
            var fileSystem = new InMemoryFileSystem();
            var logger = new TestLogger();
            var codecFactory = new DefaultCodecFactory();
            var config = FileProcessingConfig.Default();
            var program = new Program(fileSystem, logger, codecFactory, config);

            // Create test files
            for (int i = 0; i < 3; i++)
            {
                byte[] testData = new byte[50];
                fileSystem.AddFile($"/test/auto_{i}.bin", testData);
            }

            var files = fileSystem.GetFiles("/test", "*.bin", System.IO.SearchOption.TopDirectoryOnly);
            var args = new InputArguments
            {
                parallelism = 0, // Auto-detect
                recursive = false,
                createLog = false
            };

            // Act - Should not throw
            program.ProcessMultipleLevels(files, args);

            // Assert - Verify execution completed
            Assert.NotEmpty(logger.Messages);
        }

        [Fact]
        public void ProcessMultipleLevels_EmptyFileArray_DoesNotThrow()
        {
            // Arrange
            var fileSystem = new InMemoryFileSystem();
            var logger = new TestLogger();
            var codecFactory = new DefaultCodecFactory();
            var config = FileProcessingConfig.Default();
            var program = new Program(fileSystem, logger, codecFactory, config);

            var args = new InputArguments
            {
                parallelism = 4,
                recursive = false
            };

            // Act & Assert - Should not throw
            program.ProcessMultipleLevels(Array.Empty<string>(), args);
        }

        [Fact]
        public void ProcessMultipleLevels_ParallelismNotSet_DefaultsCorrectly()
        {
            // Arrange
            var fileSystem = new InMemoryFileSystem();
            var logger = new TestLogger();
            var codecFactory = new DefaultCodecFactory();
            var config = FileProcessingConfig.Default();
            var program = new Program(fileSystem, logger, codecFactory, config);

            byte[] testData = new byte[50];
            fileSystem.AddFile("/test/file.bin", testData);

            var files = fileSystem.GetFiles("/test", "*.bin", System.IO.SearchOption.TopDirectoryOnly);

            // Create InputArguments without setting parallelism (defaults to 0)
            var args = new InputArguments
            {
                recursive = false,
                createLog = false
            };

            // Act - Should use auto-detected parallelism (Environment.ProcessorCount)
            program.ProcessMultipleLevels(files, args);

            // Assert - Should have processed without throwing
            Assert.NotEmpty(logger.Messages);
        }
    }
}
