using System.IO;

namespace LibReFrontier.Abstractions
{
    /// <summary>
    /// Abstracts file system operations for testability.
    /// </summary>
    public interface IFileSystem
    {
        /// <summary>
        /// Check if a file exists.
        /// </summary>
        /// <param name="path">File path.</param>
        /// <returns>true if the file exists.</returns>
        bool FileExists(string path);

        /// <summary>
        /// Check if a directory exists.
        /// </summary>
        /// <param name="path">Directory path.</param>
        /// <returns>true if the directory exists.</returns>
        bool DirectoryExists(string path);

        /// <summary>
        /// Read all bytes from a file.
        /// </summary>
        /// <param name="path">File path.</param>
        /// <returns>File contents as byte array.</returns>
        byte[] ReadAllBytes(string path);

        /// <summary>
        /// Write all bytes to a file.
        /// </summary>
        /// <param name="path">File path.</param>
        /// <param name="bytes">Bytes to write.</param>
        void WriteAllBytes(string path, byte[] bytes);

        /// <summary>
        /// Read all lines from a file.
        /// </summary>
        /// <param name="path">File path.</param>
        /// <returns>Array of lines.</returns>
        string[] ReadAllLines(string path);

        /// <summary>
        /// Delete a file.
        /// </summary>
        /// <param name="path">File path.</param>
        void DeleteFile(string path);

        /// <summary>
        /// Create a directory if it doesn't exist.
        /// </summary>
        /// <param name="path">Directory path.</param>
        void CreateDirectory(string path);

        /// <summary>
        /// Get files in a directory matching a pattern.
        /// </summary>
        /// <param name="path">Directory path.</param>
        /// <param name="searchPattern">Search pattern (e.g., "*.bin").</param>
        /// <param name="searchOption">Search option for subdirectories.</param>
        /// <returns>Array of file paths.</returns>
        string[] GetFiles(string path, string searchPattern, SearchOption searchOption);

        /// <summary>
        /// Get the last write time of a file.
        /// </summary>
        /// <param name="path">File path.</param>
        /// <returns>Last write time.</returns>
        System.DateTime GetLastWriteTime(string path);

        /// <summary>
        /// Get file attributes.
        /// </summary>
        /// <param name="path">File or directory path.</param>
        /// <returns>File attributes.</returns>
        FileAttributes GetAttributes(string path);

        /// <summary>
        /// Get file info (size, etc.).
        /// </summary>
        /// <param name="path">File path.</param>
        /// <returns>File length in bytes.</returns>
        long GetFileLength(string path);

        /// <summary>
        /// Open a file for reading.
        /// </summary>
        /// <param name="path">File path.</param>
        /// <returns>Stream for reading.</returns>
        Stream OpenRead(string path);

        /// <summary>
        /// Open a file for writing (creates or overwrites).
        /// </summary>
        /// <param name="path">File path.</param>
        /// <returns>Stream for writing.</returns>
        Stream OpenWrite(string path);

        /// <summary>
        /// Create a new file for writing.
        /// </summary>
        /// <param name="path">File path.</param>
        /// <returns>Stream for writing.</returns>
        Stream Create(string path);

        /// <summary>
        /// Copy a file.
        /// </summary>
        /// <param name="sourceFileName">Source file path.</param>
        /// <param name="destFileName">Destination file path.</param>
        void Copy(string sourceFileName, string destFileName);

        /// <summary>
        /// Create a StreamWriter for a file.
        /// </summary>
        /// <param name="path">File path.</param>
        /// <returns>StreamWriter instance.</returns>
        StreamWriter CreateStreamWriter(string path);

        /// <summary>
        /// Create a StreamWriter for a file with specified encoding.
        /// </summary>
        /// <param name="path">File path.</param>
        /// <param name="append">Whether to append to existing file.</param>
        /// <param name="encoding">Text encoding.</param>
        /// <returns>StreamWriter instance.</returns>
        StreamWriter CreateStreamWriter(string path, bool append, System.Text.Encoding encoding);
    }
}
