using System.Text;

using LibReFrontier.Abstractions;

namespace ReFrontier.Tests.Mocks
{
    /// <summary>
    /// In-memory implementation of IFileSystem for testing.
    /// Uses dictionaries to simulate files and directories.
    /// </summary>
    public class InMemoryFileSystem : IFileSystem
    {
        private readonly Dictionary<string, byte[]> _files = new();
        private readonly HashSet<string> _directories = new();
        private readonly Dictionary<string, DateTime> _lastWriteTimes = new();

        /// <summary>
        /// Get all files currently in the file system.
        /// </summary>
        public IReadOnlyDictionary<string, byte[]> Files => _files;

        /// <summary>
        /// Get all directories currently in the file system.
        /// </summary>
        public IReadOnlyCollection<string> Directories => _directories;

        /// <summary>
        /// Add a file to the in-memory file system.
        /// </summary>
        /// <param name="path">File path.</param>
        /// <param name="content">File content.</param>
        /// <param name="lastWriteTime">Optional last write time.</param>
        public void AddFile(string path, byte[] content, DateTime? lastWriteTime = null)
        {
            path = NormalizePath(path);
            _files[path] = content;
            _lastWriteTimes[path] = lastWriteTime ?? DateTime.Now;

            // Ensure parent directories exist
            var dir = Path.GetDirectoryName(path);
            while (!string.IsNullOrEmpty(dir))
            {
                _directories.Add(NormalizePath(dir));
                dir = Path.GetDirectoryName(dir);
            }
        }

        /// <summary>
        /// Add a file with string content.
        /// </summary>
        /// <param name="path">File path.</param>
        /// <param name="content">File content as string.</param>
        /// <param name="lastWriteTime">Optional last write time.</param>
        public void AddFile(string path, string content, DateTime? lastWriteTime = null)
        {
            AddFile(path, Encoding.UTF8.GetBytes(content), lastWriteTime);
        }

        /// <summary>
        /// Add an empty directory.
        /// </summary>
        /// <param name="path">Directory path.</param>
        public void AddDirectory(string path)
        {
            path = NormalizePath(path);
            _directories.Add(path);
        }

        /// <inheritdoc />
        public bool FileExists(string path)
        {
            return _files.ContainsKey(NormalizePath(path));
        }

        /// <inheritdoc />
        public bool DirectoryExists(string path)
        {
            path = NormalizePath(path);
            return _directories.Contains(path) ||
                   _files.Keys.Any(f => f.StartsWith(path + "/") || f.StartsWith(path + "\\"));
        }

        /// <inheritdoc />
        public byte[] ReadAllBytes(string path)
        {
            path = NormalizePath(path);
            if (!_files.TryGetValue(path, out var content))
                throw new FileNotFoundException($"File not found: {path}");
            return content.ToArray(); // Return a copy
        }

        /// <inheritdoc />
        public void WriteAllBytes(string path, byte[] bytes)
        {
            path = NormalizePath(path);
            _files[path] = bytes.ToArray(); // Store a copy
            _lastWriteTimes[path] = DateTime.Now;

            // Ensure parent directories exist
            var dir = Path.GetDirectoryName(path);
            while (!string.IsNullOrEmpty(dir))
            {
                _directories.Add(NormalizePath(dir));
                dir = Path.GetDirectoryName(dir);
            }
        }

        /// <inheritdoc />
        public string[] ReadAllLines(string path)
        {
            var content = Encoding.UTF8.GetString(ReadAllBytes(path));
            return content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        }

        /// <inheritdoc />
        public void DeleteFile(string path)
        {
            path = NormalizePath(path);
            _files.Remove(path);
            _lastWriteTimes.Remove(path);
        }

        /// <inheritdoc />
        public void CreateDirectory(string path)
        {
            path = NormalizePath(path);
            _directories.Add(path);

            // Ensure parent directories exist
            var dir = Path.GetDirectoryName(path);
            while (!string.IsNullOrEmpty(dir))
            {
                _directories.Add(NormalizePath(dir));
                dir = Path.GetDirectoryName(dir);
            }
        }

        /// <inheritdoc />
        public string[] GetFiles(string path, string searchPattern, SearchOption searchOption)
        {
            path = NormalizePath(path);
            var pattern = searchPattern.Replace("*", ".*").Replace("?", ".");
            var regex = new System.Text.RegularExpressions.Regex($"^{pattern}$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            return _files.Keys
                .Where(f =>
                {
                    var normalized = NormalizePath(f);
                    if (!normalized.StartsWith(path + "/") && normalized != path)
                        return false;

                    var relativePath = normalized.Substring(path.Length).TrimStart('/');
                    var fileName = Path.GetFileName(normalized);

                    if (searchOption == SearchOption.TopDirectoryOnly)
                    {
                        // Only files directly in this directory
                        if (relativePath.Contains('/'))
                            return false;
                    }

                    return regex.IsMatch(fileName);
                })
                .ToArray();
        }

        /// <inheritdoc />
        public DateTime GetLastWriteTime(string path)
        {
            path = NormalizePath(path);
            if (_lastWriteTimes.TryGetValue(path, out var time))
                return time;
            throw new FileNotFoundException($"File not found: {path}");
        }

        /// <inheritdoc />
        public FileAttributes GetAttributes(string path)
        {
            path = NormalizePath(path);
            if (_directories.Contains(path))
                return FileAttributes.Directory;
            if (_files.ContainsKey(path))
                return FileAttributes.Normal;
            throw new FileNotFoundException($"Path not found: {path}");
        }

        /// <inheritdoc />
        public long GetFileLength(string path)
        {
            path = NormalizePath(path);
            if (!_files.TryGetValue(path, out var content))
                throw new FileNotFoundException($"File not found: {path}");
            return content.Length;
        }

        /// <inheritdoc />
        public Stream OpenRead(string path)
        {
            return new MemoryStream(ReadAllBytes(path), writable: false);
        }

        /// <inheritdoc />
        public Stream OpenWrite(string path)
        {
            path = NormalizePath(path);
            var stream = new InMemoryWriteStream(this, path);
            return stream;
        }

        /// <inheritdoc />
        public Stream Create(string path)
        {
            return OpenWrite(path);
        }

        /// <inheritdoc />
        public void Copy(string sourceFileName, string destFileName)
        {
            var content = ReadAllBytes(sourceFileName);
            WriteAllBytes(destFileName, content);
        }

        /// <inheritdoc />
        public StreamWriter CreateStreamWriter(string path)
        {
            return new StreamWriter(OpenWrite(path));
        }

        /// <inheritdoc />
        public StreamWriter CreateStreamWriter(string path, bool append, Encoding encoding)
        {
            if (append && FileExists(path))
            {
                var existing = ReadAllBytes(path);
                var stream = new InMemoryWriteStream(this, NormalizePath(path), existing);
                return new StreamWriter(stream, encoding);
            }
            return new StreamWriter(OpenWrite(path), encoding);
        }

        /// <summary>
        /// Create a StreamReader for a file with specified encoding.
        /// </summary>
        /// <param name="path">File path.</param>
        /// <param name="encoding">Text encoding.</param>
        /// <returns>StreamReader instance.</returns>
        public StreamReader CreateStreamReader(string path, Encoding encoding)
        {
            return new StreamReader(OpenRead(path), encoding);
        }

        /// <summary>
        /// Read all text from a file with specified encoding.
        /// </summary>
        /// <param name="path">File path.</param>
        /// <param name="encoding">Text encoding.</param>
        /// <returns>File contents as string.</returns>
        public string ReadAllText(string path, Encoding encoding)
        {
            using var reader = CreateStreamReader(path, encoding);
            return reader.ReadToEnd();
        }

        /// <summary>
        /// Write all text to a file with specified encoding.
        /// </summary>
        /// <param name="path">File path.</param>
        /// <param name="content">Text content.</param>
        /// <param name="encoding">Text encoding.</param>
        public void WriteAllText(string path, string content, Encoding encoding)
        {
            WriteAllBytes(path, encoding.GetBytes(content));
        }

        private static string NormalizePath(string path)
        {
            return path.Replace('\\', '/').TrimEnd('/');
        }

        /// <summary>
        /// Internal stream that writes back to the in-memory file system on close/dispose.
        /// </summary>
        private class InMemoryWriteStream : MemoryStream
        {
            private readonly InMemoryFileSystem _fileSystem;
            private readonly string _path;

            public InMemoryWriteStream(InMemoryFileSystem fileSystem, string path)
            {
                _fileSystem = fileSystem;
                _path = path;
            }

            public InMemoryWriteStream(InMemoryFileSystem fileSystem, string path, byte[] initialContent)
                : base()
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

                    // Ensure parent directories exist
                    var dir = Path.GetDirectoryName(_path);
                    while (!string.IsNullOrEmpty(dir))
                    {
                        _fileSystem._directories.Add(NormalizePath(dir));
                        dir = Path.GetDirectoryName(dir);
                    }
                }
                base.Dispose(disposing);
            }
        }
    }
}
