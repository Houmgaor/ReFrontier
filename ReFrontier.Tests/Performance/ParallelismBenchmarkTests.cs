using System.Diagnostics;

using LibReFrontier;

using ReFrontier.Jpk;
using ReFrontier.Services;
using ReFrontier.Tests.Mocks;

using Xunit.Abstractions;

namespace ReFrontier.Tests.Performance
{
    /// <summary>
    /// Performance benchmark tests for parallelism settings.
    /// These tests measure the impact of different parallelism levels on file processing.
    /// </summary>
    [Trait("Category", "Performance")]
    public class ParallelismBenchmarkTests
    {
        private readonly ITestOutputHelper _output;

        public ParallelismBenchmarkTests(ITestOutputHelper output)
        {
            _output = output;
        }

        /// <summary>
        /// Creates test files with realistic MOMO archive structure for benchmarking.
        /// </summary>
        private InMemoryFileSystem CreateBenchmarkFiles(int fileCount, int fileSizeBytes = 1024)
        {
            var fileSystem = new InMemoryFileSystem();
            var random = new Random(42); // Fixed seed for reproducibility

            for (int i = 0; i < fileCount; i++)
            {
                // Create a simple MOMO archive with random data
                using var ms = new MemoryStream();
                using var bw = new BinaryWriter(ms);

                // MOMO magic number
                bw.Write(FileMagic.MOMO);

                // File count (1 file in archive)
                bw.Write(1);

                // File entry: offset and size
                bw.Write(16); // offset after header
                bw.Write(fileSizeBytes);

                // Padding to align
                bw.Write(0);
                bw.Write(0);

                // File data
                byte[] randomData = new byte[fileSizeBytes];
                random.NextBytes(randomData);
                bw.Write(randomData);

                fileSystem.AddFile($"/bench/test_{i:D4}.bin", ms.ToArray());
            }

            return fileSystem;
        }

        [Theory(Skip = "Benchmark tests can fail due to TestLogger thread-safety - run manually when needed")]
        [InlineData(1)]   // Sequential baseline
        [InlineData(2)]
        [InlineData(4)]   // Old default
        [InlineData(8)]
        public void Benchmark_FileProcessing_VariousParallelism(int parallelism)
        {
            // Arrange - Create 50 test files
            const int fileCount = 50;
            const int fileSize = 1024;
            var fileSystem = CreateBenchmarkFiles(fileCount, fileSize);
            var logger = new TestLogger();
            var codecFactory = new DefaultCodecFactory();
            var config = FileProcessingConfig.Default();
            var program = new Program(fileSystem, logger, codecFactory, config);

            var files = fileSystem.GetFiles("/bench", "*.bin", SearchOption.TopDirectoryOnly);
            var args = new InputArguments
            {
                parallelism = parallelism,
                recursive = false,
                createLog = false
            };

            // Act - Measure processing time
            var stopwatch = Stopwatch.StartNew();
            program.ProcessMultipleLevels(files, args);
            stopwatch.Stop();

            // Report results
            double throughput = fileCount / stopwatch.Elapsed.TotalSeconds;
            _output.WriteLine($"Parallelism: {parallelism,2} | Time: {stopwatch.ElapsedMilliseconds,5} ms | " +
                            $"Throughput: {throughput:F2} files/sec | " +
                            $"Avg per file: {stopwatch.ElapsedMilliseconds / (double)fileCount:F2} ms");

            // Assert - Just verify it completed
            Assert.True(stopwatch.ElapsedMilliseconds > 0);
        }

        [Fact(Skip = "Benchmark tests can fail due to TestLogger thread-safety - run manually when needed")]
        public void Benchmark_CompareAllParallelismLevels()
        {
            // Arrange
            const int fileCount = 100;
            const int fileSize = 2048;
            int[] parallelismLevels = { 1, 2, 4, 8, Environment.ProcessorCount };
            var results = new (int Parallelism, long TimeMs, double Throughput)[parallelismLevels.Length];

            _output.WriteLine($"Running benchmark with {fileCount} files of {fileSize} bytes each");
            _output.WriteLine($"System has {Environment.ProcessorCount} processor cores");
            _output.WriteLine("");

            // Run benchmarks for each parallelism level
            for (int i = 0; i < parallelismLevels.Length; i++)
            {
                int parallelism = parallelismLevels[i];

                // Create fresh file system for each run
                var fileSystem = CreateBenchmarkFiles(fileCount, fileSize);
                var logger = new TestLogger();
                var codecFactory = new DefaultCodecFactory();
                var config = FileProcessingConfig.Default();
                var program = new Program(fileSystem, logger, codecFactory, config);

                var files = fileSystem.GetFiles("/bench", "*.bin", SearchOption.TopDirectoryOnly);
                var args = new InputArguments
                {
                    parallelism = parallelism,
                    recursive = false,
                    createLog = false
                };

                // Measure
                var stopwatch = Stopwatch.StartNew();
                program.ProcessMultipleLevels(files, args);
                stopwatch.Stop();

                double throughput = fileCount / stopwatch.Elapsed.TotalSeconds;
                results[i] = (parallelism, stopwatch.ElapsedMilliseconds, throughput);
            }

            // Calculate speedup ratios vs sequential baseline
            long baselineTime = results[0].TimeMs;

            _output.WriteLine("Benchmark Results:");
            _output.WriteLine("═════════════════════════════════════════════════════════════════");
            _output.WriteLine("Parallelism │   Time (ms) │ Files/sec │ Speedup vs Sequential");
            _output.WriteLine("────────────┼─────────────┼───────────┼──────────────────────");

            foreach (var (parallelism, timeMs, throughput) in results)
            {
                double speedup = (double)baselineTime / timeMs;
                string parallelStr = parallelism == Environment.ProcessorCount
                    ? $"{parallelism} (auto)"
                    : parallelism.ToString();
                _output.WriteLine($"{parallelStr,11} │ {timeMs,11:N0} │ {throughput,9:F2} │ {speedup,20:F2}x");
            }
            _output.WriteLine("═════════════════════════════════════════════════════════════════");

            // Assert - Parallel should generally be faster than sequential (with some tolerance)
            // We allow for some variability in test environment
            Assert.True(results[0].TimeMs > 0, "Benchmark should have measurable time");
        }

        [Fact(Skip = "Benchmark tests can fail due to TestLogger thread-safety - run manually when needed")]
        public void Benchmark_AutoDetectVsManual()
        {
            // Arrange
            const int fileCount = 50;
            const int fileSize = 1024;

            // Test auto-detect (0) vs explicit processor count
            int[] parallelismLevels = { 0, Environment.ProcessorCount };

            _output.WriteLine($"Comparing auto-detect (0) vs explicit ({Environment.ProcessorCount})");
            _output.WriteLine("");

            foreach (int parallelism in parallelismLevels)
            {
                var fileSystem = CreateBenchmarkFiles(fileCount, fileSize);
                var logger = new TestLogger();
                var codecFactory = new DefaultCodecFactory();
                var config = FileProcessingConfig.Default();
                var program = new Program(fileSystem, logger, codecFactory, config);

                var files = fileSystem.GetFiles("/bench", "*.bin", SearchOption.TopDirectoryOnly);
                var args = new InputArguments
                {
                    parallelism = parallelism,
                    recursive = false,
                    createLog = false
                };

                var stopwatch = Stopwatch.StartNew();
                program.ProcessMultipleLevels(files, args);
                stopwatch.Stop();

                double throughput = fileCount / stopwatch.Elapsed.TotalSeconds;
                string parallelStr = parallelism == 0 ? "0 (auto)" : parallelism.ToString();
                _output.WriteLine($"Parallelism: {parallelStr,10} | Time: {stopwatch.ElapsedMilliseconds,5} ms | " +
                                $"Throughput: {throughput:F2} files/sec");
            }

            // Assert - Just verify completion
            Assert.True(true);
        }
    }
}
