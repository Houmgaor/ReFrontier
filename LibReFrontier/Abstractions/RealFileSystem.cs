using System;
using System.IO;
using System.Text;

namespace LibReFrontier.Abstractions
{
    /// <summary>
    /// Default implementation of IFileSystem that wraps System.IO.
    /// </summary>
    public class RealFileSystem : IFileSystem
    {
        /// <inheritdoc />
        public bool FileExists(string path) => File.Exists(path);

        /// <inheritdoc />
        public bool DirectoryExists(string path) => Directory.Exists(path);

        /// <inheritdoc />
        public byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);

        /// <inheritdoc />
        public void WriteAllBytes(string path, byte[] bytes) => File.WriteAllBytes(path, bytes);

        /// <inheritdoc />
        public string[] ReadAllLines(string path) => File.ReadAllLines(path);

        /// <inheritdoc />
        public void DeleteFile(string path) => File.Delete(path);

        /// <inheritdoc />
        public void CreateDirectory(string path) => Directory.CreateDirectory(path);

        /// <inheritdoc />
        public string[] GetFiles(string path, string searchPattern, SearchOption searchOption)
            => Directory.GetFiles(path, searchPattern, searchOption);

        /// <inheritdoc />
        public DateTime GetLastWriteTime(string path) => File.GetLastWriteTime(path);

        /// <inheritdoc />
        public FileAttributes GetAttributes(string path) => File.GetAttributes(path);

        /// <inheritdoc />
        public long GetFileLength(string path) => new FileInfo(path).Length;

        /// <inheritdoc />
        public Stream OpenRead(string path) => File.OpenRead(path);

        /// <inheritdoc />
        public Stream OpenWrite(string path) => File.Open(path, FileMode.Create);

        /// <inheritdoc />
        public Stream Create(string path) => File.Create(path);

        /// <inheritdoc />
        public void Copy(string sourceFileName, string destFileName)
            => File.Copy(sourceFileName, destFileName, overwrite: true);

        /// <inheritdoc />
        public StreamWriter CreateStreamWriter(string path) => new StreamWriter(path);

        /// <inheritdoc />
        public StreamWriter CreateStreamWriter(string path, bool append, Encoding encoding)
            => new StreamWriter(path, append, encoding);
    }
}
