using System.Collections.Concurrent;

using LibReFrontier.Abstractions;

using ReFrontier.Jpk;
using ReFrontier.Services;
using ReFrontier.Tests.Mocks;

namespace ReFrontier.Tests.Integration
{
    /// <summary>
    /// Tests for race conditions in parallel processing.
    /// </summary>
    public class RaceConditionTests
    {
        /// <summary>
        /// Test that files are not processed while they're still being written.
        /// This simulates the scenario where AddNewFiles picks up files that are
        /// still being written by another thread.
        /// </summary>
        [Fact]
        public void ProcessMultipleLevels_WithNestedArchives_DoesNotHaveRaceCondition()
        {
            // Arrange
            var fileSystem = new ThreadSafeInMemoryFileSystem();
            var logger = new TestLogger();
            var codecFactory = new DefaultCodecFactory();
            var config = FileProcessingConfig.Default();
            var program = new Program(fileSystem, logger, codecFactory, config);

            // Create a simple archive that contains JKR files
            // When unpacked, the JKR files will be decompressed to .bin files
            // This simulates the real-world scenario where race conditions occur

            // Create multiple simple archives to increase chance of race condition
            for (int i = 0; i < 10; i++)
            {
                // Create a simple archive with count header
                var archiveData = CreateSimpleArchiveWithJkrFiles(i);
                fileSystem.AddFile($"/test/archive_{i}.bin", archiveData);
            }

            var files = fileSystem.GetFiles("/test", "*.bin", System.IO.SearchOption.TopDirectoryOnly);
            var args = new InputArguments
            {
                parallelism = Environment.ProcessorCount,
                recursive = true,
                createLog = false,
                verbose = false
            };

            // Act - Should not throw due to race condition
            var exception = Record.Exception(() =>
            {
                for (int run = 0; run < 5; run++) // Run multiple times to increase chance of hitting race
                {
                    program.ProcessMultipleLevels(files, args);
                }
            });

            // Assert
            Assert.Null(exception);
        }

        /// <summary>
        /// Test that the same file is not processed multiple times.
        /// </summary>
        [Fact]
        public void ProcessMultipleLevels_SameFileNotProcessedTwice()
        {
            // Arrange
            var fileSystem = new ThreadSafeInMemoryFileSystem();
            var logger = new TestLogger();
            var codecFactory = new DefaultCodecFactory();
            var config = FileProcessingConfig.Default();
            var program = new Program(fileSystem, logger, codecFactory, config);

            // Create test files
            var processedFiles = new ConcurrentBag<string>();
            for (int i = 0; i < 5; i++)
            {
                byte[] testData = new byte[100];
                fileSystem.AddFile($"/test/file_{i}.bin", testData);
            }

            var files = fileSystem.GetFiles("/test", "*.bin", System.IO.SearchOption.TopDirectoryOnly);
            var args = new InputArguments
            {
                parallelism = 4,
                recursive = false,
                createLog = false,
                verbose = true
            };

            // Act
            program.ProcessMultipleLevels(files, args);

            // Assert - Each file should only appear once in the log
            var processMessages = logger.Messages
                .Where(m => m.Contains("Processing"))
                .ToList();

            // Check for duplicates
            var distinctMessages = processMessages.Distinct().ToList();
            Assert.Equal(distinctMessages.Count, processMessages.Count);
        }

        /// <summary>
        /// Creates a simple archive structure for testing.
        /// </summary>
        private static byte[] CreateSimpleArchiveWithJkrFiles(int index)
        {
            // Simple archive format:
            // - 4 bytes: entry count
            // - For each entry: 4 bytes offset, 4 bytes size
            // - Entry data

            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            // Just create a minimal valid-looking archive header
            // The actual content doesn't matter for this test - we just need
            // files to be created and potentially accessed concurrently
            int count = 2;
            bw.Write(count); // Entry count

            int headerSize = 4 + (count * 8); // count + entries
            int entry1Size = 32;
            int entry2Size = 32;

            // Entry 1: offset and size
            bw.Write(headerSize);
            bw.Write(entry1Size);

            // Entry 2: offset and size
            bw.Write(headerSize + entry1Size);
            bw.Write(entry2Size);

            // Entry 1 data - just some bytes
            bw.Write(new byte[entry1Size]);

            // Entry 2 data - just some bytes
            bw.Write(new byte[entry2Size]);

            return ms.ToArray();
        }
    }

    /// <summary>
    /// Thread-safe version of InMemoryFileSystem for race condition testing.
    /// </summary>
    public class ThreadSafeInMemoryFileSystem : IFileSystem
    {
        private readonly ConcurrentDictionary<string, byte[]> _files = new();
        private readonly ConcurrentDictionary<string, bool> _directories = new();
        private readonly ConcurrentDictionary<string, DateTime> _lastWriteTimes = new();
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks = new();

        public void AddFile(string path, byte[] content, DateTime? lastWriteTime = null)
        {
            path = NormalizePath(path);
            _files[path] = content;
            _lastWriteTimes[path] = lastWriteTime ?? DateTime.Now;

            var dir = Path.GetDirectoryName(path);
            while (!string.IsNullOrEmpty(dir))
            {
                _directories[NormalizePath(dir)] = true;
                dir = Path.GetDirectoryName(dir);
            }
        }

        public bool FileExists(string path) => _files.ContainsKey(NormalizePath(path));

        public bool DirectoryExists(string path)
        {
            path = NormalizePath(path);
            return _directories.ContainsKey(path) ||
                   _files.Keys.Any(f => f.StartsWith(path + "/"));
        }

        public byte[] ReadAllBytes(string path)
        {
            path = NormalizePath(path);
            if (!_files.TryGetValue(path, out var content))
                throw new FileNotFoundException($"File not found: {path}");
            return content.ToArray();
        }

        public void WriteAllBytes(string path, byte[] bytes)
        {
            path = NormalizePath(path);
            _files[path] = bytes.ToArray();
            _lastWriteTimes[path] = DateTime.Now;

            var dir = Path.GetDirectoryName(path);
            while (!string.IsNullOrEmpty(dir))
            {
                _directories[NormalizePath(dir)] = true;
                dir = Path.GetDirectoryName(dir);
            }
        }

        public string[] ReadAllLines(string path)
        {
            var content = System.Text.Encoding.UTF8.GetString(ReadAllBytes(path));
            return content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        }

        public void DeleteFile(string path)
        {
            path = NormalizePath(path);
            _files.TryRemove(path, out _);
            _lastWriteTimes.TryRemove(path, out _);
        }

        public void CreateDirectory(string path)
        {
            path = NormalizePath(path);
            _directories[path] = true;

            var dir = Path.GetDirectoryName(path);
            while (!string.IsNullOrEmpty(dir))
            {
                _directories[NormalizePath(dir)] = true;
                dir = Path.GetDirectoryName(dir);
            }
        }

        public string[] GetFiles(string path, string searchPattern, SearchOption searchOption)
        {
            path = NormalizePath(path);
            var pattern = searchPattern.Replace("*", ".*").Replace("?", ".");
            var regex = new System.Text.RegularExpressions.Regex($"^{pattern}$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            return _files.Keys
                .Where(f =>
                {
                    var normalized = NormalizePath(f);
                    if (!normalized.StartsWith(path + "/") && normalized != path)
                        return false;

                    var relativePath = normalized.Substring(path.Length).TrimStart('/');
                    var fileName = Path.GetFileName(normalized);

                    if (searchOption == SearchOption.TopDirectoryOnly && relativePath.Contains('/'))
                        return false;

                    return regex.IsMatch(fileName);
                })
                .ToArray();
        }

        public DateTime GetLastWriteTime(string path)
        {
            path = NormalizePath(path);
            if (_lastWriteTimes.TryGetValue(path, out var time))
                return time;
            throw new FileNotFoundException($"File not found: {path}");
        }

        public FileAttributes GetAttributes(string path)
        {
            path = NormalizePath(path);
            if (_directories.ContainsKey(path))
                return FileAttributes.Directory;
            if (_files.ContainsKey(path))
                return FileAttributes.Normal;
            throw new FileNotFoundException($"Path not found: {path}");
        }

        public long GetFileLength(string path)
        {
            path = NormalizePath(path);
            if (!_files.TryGetValue(path, out var content))
                throw new FileNotFoundException($"File not found: {path}");
            return content.Length;
        }

        public Stream OpenRead(string path)
        {
            return new MemoryStream(ReadAllBytes(path), writable: false);
        }

        public Stream OpenWrite(string path)
        {
            path = NormalizePath(path);
            return new ThreadSafeWriteStream(this, path);
        }

        public Stream Create(string path) => OpenWrite(path);

        public void Copy(string sourceFileName, string destFileName)
        {
            var content = ReadAllBytes(sourceFileName);
            WriteAllBytes(destFileName, content);
        }

        public StreamWriter CreateStreamWriter(string path)
        {
            return new StreamWriter(OpenWrite(path));
        }

        public StreamWriter CreateStreamWriter(string path, bool append, System.Text.Encoding encoding)
        {
            if (append && FileExists(path))
            {
                var existing = ReadAllBytes(path);
                var stream = new ThreadSafeWriteStream(this, NormalizePath(path), existing);
                return new StreamWriter(stream, encoding);
            }
            return new StreamWriter(OpenWrite(path), encoding);
        }

        private static string NormalizePath(string path) => path.Replace('\\', '/').TrimEnd('/');

        private class ThreadSafeWriteStream : MemoryStream
        {
            private readonly ThreadSafeInMemoryFileSystem _fileSystem;
            private readonly string _path;

            public ThreadSafeWriteStream(ThreadSafeInMemoryFileSystem fileSystem, string path)
            {
                _fileSystem = fileSystem;
                _path = path;
            }

            public ThreadSafeWriteStream(ThreadSafeInMemoryFileSystem fileSystem, string path, byte[] initialContent)
            {
                _fileSystem = fileSystem;
                _path = path;
                Write(initialContent, 0, initialContent.Length);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _fileSystem._files[_path] = ToArray();
                    _fileSystem._lastWriteTimes[_path] = DateTime.Now;

                    var dir = Path.GetDirectoryName(_path);
                    while (!string.IsNullOrEmpty(dir))
                    {
                        _fileSystem._directories[NormalizePath(dir)] = true;
                        dir = Path.GetDirectoryName(dir);
                    }
                }
                base.Dispose(disposing);
            }

            private static string NormalizePath(string path) => path.Replace('\\', '/').TrimEnd('/');
        }
    }
}
