using System;
using System.IO;
using LibReFrontier;
using LibReFrontier.Abstractions;
using ReFrontier.Jpk;

namespace ReFrontier
{
    /// <summary>
    /// Helper class to automatically detect and preprocess encrypted/compressed game files.
    /// Handles automatic decryption (ECD/EXF) and decompression (JPK) for simplified workflows.
    /// </summary>
    public class FilePreprocessor
    {
        private readonly IFileSystem _fileSystem;
        private readonly ILogger _logger;
        private readonly ICodecFactory _codecFactory;

        /// <summary>
        /// Create a new FilePreprocessor with default dependencies.
        /// </summary>
        public FilePreprocessor()
            : this(new RealFileSystem(), new ConsoleLogger(), new DefaultCodecFactory())
        {
        }

        /// <summary>
        /// Create a new FilePreprocessor with injectable dependencies.
        /// </summary>
        /// <param name="fileSystem">File system abstraction.</param>
        /// <param name="logger">Logger abstraction.</param>
        /// <param name="codecFactory">Codec factory for JPK decompression.</param>
        public FilePreprocessor(IFileSystem fileSystem, ILogger logger, ICodecFactory codecFactory)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _codecFactory = codecFactory ?? throw new ArgumentNullException(nameof(codecFactory));
        }

        /// <summary>
        /// Check if a file is encrypted (ECD or EXF format).
        /// </summary>
        /// <param name="filePath">Path to the file to check.</param>
        /// <returns>True if the file is encrypted.</returns>
        public bool IsEncrypted(string filePath)
        {
            if (!_fileSystem.FileExists(filePath))
                return false;

            byte[] buffer = _fileSystem.ReadAllBytes(filePath);
            if (buffer.Length < 4)
                return false;

            uint magic = BitConverter.ToUInt32(buffer, 0);
            return magic == 0x1A646365 || magic == 0x1A667865; // ECD or EXF
        }

        /// <summary>
        /// Check if a file is JPK compressed.
        /// </summary>
        /// <param name="filePath">Path to the file to check.</param>
        /// <returns>True if the file is JPK compressed.</returns>
        public bool IsJpkCompressed(string filePath)
        {
            if (!_fileSystem.FileExists(filePath))
                return false;

            byte[] buffer = _fileSystem.ReadAllBytes(filePath);
            if (buffer.Length < 4)
                return false;

            uint magic = BitConverter.ToUInt32(buffer, 0);
            return magic == 0x1A524B4A; // JKR
        }

        /// <summary>
        /// Decrypt a file if it's encrypted, otherwise return the original path.
        /// Creates a temporary decrypted file with .temp.decd extension.
        /// </summary>
        /// <param name="filePath">Path to the file to decrypt.</param>
        /// <param name="createMetaFile">Whether to create a .meta file for re-encryption.</param>
        /// <returns>Path to the decrypted file (or original if not encrypted).</returns>
        public string AutoDecrypt(string filePath, bool createMetaFile = true)
        {
            byte[] buffer = _fileSystem.ReadAllBytes(filePath);
            if (buffer.Length < 4)
                return filePath;

            uint magic = BitConverter.ToUInt32(buffer, 0);

            if (magic == 0x1A646365) // ECD
            {
                _logger.WriteLine($"Detected ECD encryption in {Path.GetFileName(filePath)}, decrypting...");
                return DecryptEcd(filePath, buffer, createMetaFile);
            }
            else if (magic == 0x1A667865) // EXF
            {
                _logger.WriteLine($"Detected EXF encryption in {Path.GetFileName(filePath)}, decrypting...");
                return DecryptExf(filePath, buffer);
            }

            return filePath; // Not encrypted
        }

        /// <summary>
        /// Decompress a file if it's JPK compressed, otherwise return the original path.
        /// Creates a temporary decompressed file with .temp.jpk extension.
        /// </summary>
        /// <param name="filePath">Path to the file to decompress.</param>
        /// <returns>Path to the decompressed file (or original if not compressed).</returns>
        public string AutoDecompress(string filePath)
        {
            byte[] buffer = _fileSystem.ReadAllBytes(filePath);
            if (buffer.Length < 4)
                return filePath;

            uint magic = BitConverter.ToUInt32(buffer, 0);

            if (magic == 0x1A524B4A) // JKR (JPK compressed)
            {
                _logger.WriteLine($"Detected JPK compression in {Path.GetFileName(filePath)}, decompressing...");
                return DecompressJpk(filePath, buffer);
            }

            return filePath; // Not compressed
        }

        /// <summary>
        /// Automatically decrypt and decompress a file if needed.
        /// Returns the path to the processed file and a cleanup action.
        /// </summary>
        /// <param name="filePath">Path to the file to process.</param>
        /// <param name="createMetaFile">Whether to create a .meta file for re-encryption.</param>
        /// <returns>Tuple of (processedFilePath, cleanupAction).</returns>
        public (string processedPath, Action cleanup) AutoPreprocess(string filePath, bool createMetaFile = true)
        {
            string tempFiles = "";
            string currentPath = filePath;

            // First decrypt if needed
            string decryptedPath = AutoDecrypt(currentPath, createMetaFile);
            if (decryptedPath != currentPath)
            {
                tempFiles = decryptedPath;
                currentPath = decryptedPath;
            }

            // Then decompress if needed
            string decompressedPath = AutoDecompress(currentPath);
            if (decompressedPath != currentPath)
            {
                if (!string.IsNullOrEmpty(tempFiles))
                    tempFiles += ";";
                tempFiles += decompressedPath;
            }

            // Create cleanup action
            Action cleanup = () =>
            {
                if (!string.IsNullOrEmpty(tempFiles))
                {
                    foreach (string temp in tempFiles.Split(';'))
                    {
                        if (_fileSystem.FileExists(temp) && temp != filePath)
                        {
                            _fileSystem.DeleteFile(temp);
                        }
                    }
                }
            };

            return (decompressedPath, cleanup);
        }

        /// <summary>
        /// Decrypt an ECD file.
        /// </summary>
        private string DecryptEcd(string filePath, byte[] buffer, bool createMetaFile)
        {
            Crypto.DecodeEcd(buffer);
            const int headerLength = 0x10;

            byte[] ecdHeader = new byte[headerLength];
            Array.Copy(buffer, 0, ecdHeader, 0, headerLength);
            byte[] bufferStripped = new byte[buffer.Length - headerLength];
            Array.Copy(buffer, headerLength, bufferStripped, 0, buffer.Length - headerLength);

            string outputFile = filePath + ".temp.decd";
            _fileSystem.WriteAllBytes(outputFile, bufferStripped);

            if (createMetaFile)
            {
                string metaFile = $"{filePath}.meta";
                _fileSystem.WriteAllBytes(metaFile, ecdHeader);
            }

            return outputFile;
        }

        /// <summary>
        /// Decrypt an EXF file.
        /// </summary>
        private string DecryptExf(string filePath, byte[] buffer)
        {
            Crypto.DecodeExf(buffer);
            const int headerLength = 0x10;
            byte[] bufferStripped = new byte[buffer.Length - headerLength];
            Array.Copy(buffer, headerLength, bufferStripped, 0, buffer.Length - headerLength);

            string outputFile = filePath + ".temp.dexf";
            _fileSystem.WriteAllBytes(outputFile, bufferStripped);

            return outputFile;
        }

        /// <summary>
        /// Decompress a JPK file.
        /// </summary>
        private string DecompressJpk(string filePath, byte[] buffer)
        {
            using var msInput = new MemoryStream(buffer);
            using var brInput = new BinaryReader(msInput);

            // Read JPK header
            brInput.ReadUInt32(); // magic
            brInput.ReadUInt16(); // skip 2 bytes
            int type = brInput.ReadUInt16();
            
            var compressionTypes = Enum.GetValues<CompressionType>();
            if (type < 0 || type >= compressionTypes.Length)
            {
                throw new InvalidOperationException(
                    $"Invalid compression type {type}. Valid range is 0-{compressionTypes.Length - 1}."
                );
            }
            var compressionType = compressionTypes[type];

            int startOffset = brInput.ReadInt32();
            int decompressedSize = brInput.ReadInt32();

            // Decompress using codec
            IJPKDecode decoder = _codecFactory.CreateDecoder(compressionType);
            byte[] decompressedData = new byte[decompressedSize];
            
            msInput.Seek(startOffset, SeekOrigin.Begin);
            decoder.ProcessOnDecode(msInput, decompressedData);

            string outputFile = filePath + ".temp.jpk";
            _fileSystem.WriteAllBytes(outputFile, decompressedData);

            return outputFile;
        }
    }
}
