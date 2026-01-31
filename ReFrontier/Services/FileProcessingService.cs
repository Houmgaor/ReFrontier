using System;
using System.IO;

using LibReFrontier;
using LibReFrontier.Abstractions;
using LibReFrontier.Exceptions;

namespace ReFrontier.Services
{
    /// <summary>
    /// Service for file encryption and decryption operations.
    /// </summary>
    public class FileProcessingService
    {
        private readonly IFileSystem _fileSystem;
        private readonly ILogger _logger;
        private readonly FileProcessingConfig _config;
        private readonly FileOperations _fileOperations;

        /// <summary>
        /// Create a new FileProcessingService with default dependencies.
        /// </summary>
        public FileProcessingService()
            : this(new RealFileSystem(), new ConsoleLogger(), FileProcessingConfig.Default())
        {
        }

        /// <summary>
        /// Create a new FileProcessingService with injectable dependencies.
        /// </summary>
        /// <param name="fileSystem">File system abstraction.</param>
        /// <param name="logger">Logger abstraction.</param>
        /// <param name="config">Configuration settings.</param>
        public FileProcessingService(IFileSystem fileSystem, ILogger logger, FileProcessingConfig config)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _fileOperations = new FileOperations(fileSystem, logger);
        }

        /// <summary>
        /// Encrypt a single file to a new file.
        ///
        /// If inputFile is "mhfdat.bin.decd",
        /// metaFile should be "mhfdat.bin.meta" and
        /// the output file will be "mhfdat.bin".
        /// </summary>
        /// <param name="inputFile">Input file to encrypt.</param>
        /// <param name="metaFile">Data to use for encryption.</param>
        /// <param name="cleanUp">Remove both inputFile and metaFile.</param>
        /// <returns>Encrypted file path.</returns>
        /// <exception cref="FileNotFoundException">Thrown if the meta file does not exist.</exception>
        public string EncryptEcdFile(string inputFile, string metaFile, bool cleanUp)
        {
            byte[] buffer = _fileSystem.ReadAllBytes(inputFile);
            // From mhfdat.bin.decd to mhdat.bin
            string encryptedFilePath = Path.Join(
                Path.GetDirectoryName(inputFile),
                Path.GetFileNameWithoutExtension(inputFile)
            );
            if (!_fileSystem.FileExists(metaFile))
            {
                throw new FileNotFoundException(
                    $"META file {metaFile} does not exist, " +
                    $"cannot encrypt {inputFile}. " +
                    "Make sure to decrypt the initial file with the -log option, " +
                    "and to place the generated meta file in the same folder as the file " +
                    "to encrypt."
                );
            }
            byte[] bufferMeta = _fileSystem.ReadAllBytes(metaFile);
            try
            {
                buffer = Crypto.EncodeEcd(buffer, bufferMeta);
            }
            catch (ReFrontierException ex)
            {
                throw ex.WithFilePath(inputFile);
            }
            _fileSystem.WriteAllBytes(encryptedFilePath, buffer);
            _logger.PrintWithSeparator($"File encrypted to {encryptedFilePath}.", false);
            _fileOperations.GetUpdateEntryInstance(inputFile);
            if (cleanUp)
            {
                _fileSystem.DeleteFile(inputFile);
                _fileSystem.DeleteFile(metaFile);
            }
            return encryptedFilePath;
        }

        /// <summary>
        /// Decrypt an ECD encoded file to a new file.
        /// </summary>
        /// <param name="inputFile">Input file path.</param>
        /// <param name="createLog">True if we should create a log file with the header.</param>
        /// <param name="cleanUp">true if the original file should be deleted.</param>
        /// <param name="rewriteOldFile">Should we overwrite inputFile.</param>
        /// <returns>Path to the decrypted file, in the form inputFile.decd</returns>
        public string DecryptEcdFile(string inputFile, bool createLog, bool cleanUp, bool rewriteOldFile)
        {
            byte[] buffer = _fileSystem.ReadAllBytes(inputFile);
            try
            {
                Crypto.DecodeEcd(buffer);
            }
            catch (ReFrontierException ex)
            {
                throw ex.WithFilePath(inputFile);
            }

            byte[] ecdHeader = new byte[FileFormatConstants.EncryptionHeaderLength];
            Array.Copy(buffer, 0, ecdHeader, 0, FileFormatConstants.EncryptionHeaderLength);
            byte[] bufferStripped = new byte[buffer.Length - FileFormatConstants.EncryptionHeaderLength];
            Array.Copy(buffer, FileFormatConstants.EncryptionHeaderLength, bufferStripped, 0, buffer.Length - FileFormatConstants.EncryptionHeaderLength);

            string outputFile = inputFile + _config.DecryptedSuffix;
            _fileSystem.WriteAllBytes(outputFile, bufferStripped);
            _logger.Write($"File decrypted to {outputFile}");
            if (createLog)
            {
                string metaFile = $"{inputFile}{_config.MetaSuffix}";
                _fileSystem.WriteAllBytes(metaFile, ecdHeader);
                _logger.Write($", log file at {metaFile}");
            }
            _logger.Write(".\n");
            if (rewriteOldFile)
            {
                _fileSystem.WriteAllBytes(inputFile, bufferStripped);
                _logger.WriteLine(
                    $"Rewriting original file {inputFile}. " +
                    "This behavior is deprecated and will be removed in 2.0.0. " +
                    "Use --noFileRewrite to remove this warning."
                );
            }
            else if (cleanUp)
                _fileSystem.DeleteFile(inputFile);

            return outputFile;
        }

        /// <summary>
        /// Decrypt an Exf file.
        /// </summary>
        /// <param name="inputFile">Input file path.</param>
        /// <param name="cleanUp">Should the original file be removed.</param>
        /// <returns>Output file at {inputFile}.dexf</returns>
        public string DecryptExfFile(string inputFile, bool cleanUp)
        {
            byte[] buffer = _fileSystem.ReadAllBytes(inputFile);
            try
            {
                Crypto.DecodeExf(buffer);
            }
            catch (ReFrontierException ex)
            {
                throw ex.WithFilePath(inputFile);
            }
            byte[] bufferStripped = new byte[buffer.Length - FileFormatConstants.EncryptionHeaderLength];
            Array.Copy(buffer, FileFormatConstants.EncryptionHeaderLength, bufferStripped, 0, buffer.Length - FileFormatConstants.EncryptionHeaderLength);
            string outputFile = inputFile + _config.DecryptedExfSuffix;
            _fileSystem.WriteAllBytes(outputFile, bufferStripped);
            if (cleanUp)
                _fileSystem.DeleteFile(inputFile);
            _logger.WriteLine($"File decrypted to {outputFile}.");
            return outputFile;
        }
    }
}
