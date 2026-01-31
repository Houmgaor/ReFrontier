using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using System.Text;

using LibReFrontier.Abstractions;

namespace LibReFrontier
{
    /// <summary>
    /// Operation relative to file reading and creation.
    /// </summary>
    public class FileOperations
    {
        private static readonly IFileSystem DefaultFileSystem = new RealFileSystem();
        private static readonly ILogger DefaultLogger = new ConsoleLogger();

        private readonly IFileSystem _fileSystem;
        private readonly ILogger _logger;

        /// <summary>
        /// Create a new FileOperations instance with default dependencies.
        /// </summary>
        public FileOperations() : this(DefaultFileSystem, DefaultLogger)
        {
        }

        /// <summary>
        /// Create a new FileOperations instance with injectable dependencies.
        /// </summary>
        /// <param name="fileSystem">File system abstraction.</param>
        /// <param name="logger">Logger abstraction.</param>
        public FileOperations(IFileSystem fileSystem, ILogger logger)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Read bytes from position until meeting null (0x00) or end-of-file.
        /// </summary>
        /// <param name="brInput">Reader to read from, we stop at the first null termination.</param>
        /// <param name="encoding">Encoding to use.</param>
        /// <returns>The decoded string found.</returns>
        public static string ReadNullterminatedString(BinaryReader brInput, Encoding encoding)
        {
            ArgumentNullException.ThrowIfNull(brInput);
            ArgumentNullException.ThrowIfNull(encoding);

            var charByteList = new List<byte>();
            string str;
            if (brInput.BaseStream.Position == brInput.BaseStream.Length)
            {
                byte[] charByteArray = [.. charByteList];
                str = encoding.GetString(charByteArray);
                return str;
            }
            byte b = brInput.ReadByte();
            while ((b != 0x00) && (brInput.BaseStream.Position != brInput.BaseStream.Length))
            {
                charByteList.Add(b);
                b = brInput.ReadByte();
            }
            byte[] char_bytes = [.. charByteList];
            str = encoding.GetString(char_bytes);
            return str;
        }

        /// <summary>
        /// Find files corresponding to multiple filters using injected file system.
        /// </summary>
        /// <param name="path">Directory to search into.</param>
        /// <param name="searchPatterns">Patterns to search for.</param>
        /// <param name="searchOption">Options</param>
        /// <returns>All files found.</returns>
        public string[] GetFilesInstance(
            string path,
            string[] searchPatterns,
            SearchOption searchOption = SearchOption.TopDirectoryOnly
        )
        {
            return searchPatterns.AsParallel()
                    .SelectMany(searchPattern =>
                    _fileSystem.GetFiles(path, searchPattern, searchOption)
                    )
                    .ToArray();
        }

        /// <summary>
        /// Get the update entry format for MHFUP_00.DAT.
        /// </summary>
        /// <param name="fileName">File that was updated</param>
        /// <returns>Modified data in custom format for MHFUP_00.DAT</returns>
        public string GetUpdateEntryInstance(string fileName)
        {
            ArgumentNullException.ThrowIfNull(fileName);

            var result = ComputeUpdateEntry(fileName, _fileSystem);
            _logger.WriteLine($"{result.crc32:X8},{result.dateHex1},{result.dateHex2},{fileName.Replace("output", "dat")},{result.fileSize},0");
            return $"{result.crc32:X8},{result.dateHex1},{result.dateHex2},{fileName},{result.fileSize},0";
        }

        /// <summary>
        /// Compute update entry data without side effects (pure function).
        /// </summary>
        /// <param name="fileName">File path.</param>
        /// <param name="fileSystem">File system to use.</param>
        /// <returns>Tuple containing computed values.</returns>
        public static (uint crc32, string dateHex1, string dateHex2, int fileSize) ComputeUpdateEntry(
            string fileName, IFileSystem fileSystem)
        {
            ArgumentNullException.ThrowIfNull(fileSystem);

            DateTime date = fileSystem.GetLastWriteTime(fileName);
            string dateHex2 = date.Subtract(new DateTime(1601, 1, 1)).Ticks.ToString("X16")[..8];
            string dateHex1 = date.Subtract(new DateTime(1601, 1, 1)).Ticks.ToString("X16")[8..];
            byte[] repackData = fileSystem.ReadAllBytes(fileName);
            uint crc32 = Crc32.HashToUInt32(repackData);
            return (crc32, dateHex1, dateHex2, repackData.Length);
        }
    }
}
