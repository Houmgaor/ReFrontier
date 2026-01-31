using System;
using System.Buffers;
using System.IO;
using System.Text;

using LibReFrontier;
using LibReFrontier.Abstractions;
using LibReFrontier.Exceptions;

using ReFrontier.Jpk;

namespace ReFrontier.Services
{
    /// <summary>
    /// Service for unpacking archives and decompressing files.
    /// </summary>
    public class UnpackingService
    {
        private readonly IFileSystem _fileSystem;
        private readonly ILogger _logger;
        private readonly ICodecFactory _codecFactory;
        private readonly FileProcessingConfig _config;

        /// <summary>
        /// Create a new UnpackingService with default dependencies.
        /// </summary>
        public UnpackingService()
            : this(new RealFileSystem(), new ConsoleLogger(), new DefaultCodecFactory(), FileProcessingConfig.Default())
        {
        }

        /// <summary>
        /// Create a new UnpackingService with injectable dependencies.
        /// </summary>
        /// <param name="fileSystem">File system abstraction.</param>
        /// <param name="logger">Logger abstraction.</param>
        /// <param name="codecFactory">Codec factory for decoders.</param>
        /// <param name="config">Configuration settings.</param>
        public UnpackingService(IFileSystem fileSystem, ILogger logger, ICodecFactory codecFactory, FileProcessingConfig config)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _codecFactory = codecFactory ?? throw new ArgumentNullException(nameof(codecFactory));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Initialize a log file with container type and filename.
        /// </summary>
        /// <param name="input">Input file path.</param>
        /// <param name="containerType">Type of container (e.g., "MHA", "StageContainer").</param>
        /// <param name="createLog">Whether to actually write log content.</param>
        /// <param name="logPath">Output parameter for the log file path.</param>
        /// <returns>StreamWriter for the log file.</returns>
        private StreamWriter InitializeLogFile(string input, string containerType, bool createLog, out string logPath)
        {
            logPath = $"{input}{_config.LogSuffix}";
            var logOutput = _fileSystem.CreateStreamWriter(logPath);

            if (createLog)
            {
                logOutput.WriteLine(containerType);
                logOutput.WriteLine(Path.GetFileName(input));
            }

            return logOutput;
        }

        /// <summary>
        /// Initialize a log file with container type, filename, and count.
        /// </summary>
        /// <param name="input">Input file path.</param>
        /// <param name="containerType">Type of container (e.g., "SimpleArchive").</param>
        /// <param name="count">Entry count to write to log.</param>
        /// <param name="createLog">Whether to actually write log content.</param>
        /// <param name="logPath">Output parameter for the log file path.</param>
        /// <returns>StreamWriter for the log file.</returns>
        private StreamWriter InitializeLogFile(string input, string containerType, int count, bool createLog, out string logPath)
        {
            logPath = $"{input}{_config.LogSuffix}";
            var logOutput = _fileSystem.CreateStreamWriter(logPath);

            if (createLog)
            {
                logOutput.WriteLine(containerType);
                logOutput.WriteLine(Path.GetFileName(input));
                logOutput.WriteLine(count);
            }

            return logOutput;
        }

        /// <summary>
        /// Clean up resources after unpacking.
        /// </summary>
        /// <param name="logOutput">Log stream writer to close.</param>
        /// <param name="logPath">Path to the log file.</param>
        /// <param name="inputPath">Path to the input file (nullable).</param>
        /// <param name="createLog">Whether the log should be kept.</param>
        /// <param name="cleanUp">Whether to delete the input file.</param>
        private void CleanupAfterUnpack(StreamWriter logOutput, string logPath, string? inputPath, bool createLog, bool cleanUp)
        {
            logOutput.Close();

            if (!createLog)
                _fileSystem.DeleteFile(logPath);

            if (cleanUp && inputPath != null)
                _fileSystem.DeleteFile(inputPath);
        }

        /// <summary>
        /// Extract a file entry from an archive.
        /// </summary>
        /// <param name="brInput">Binary reader for input.</param>
        /// <param name="offset">File offset in archive.</param>
        /// <param name="size">File size.</param>
        /// <param name="outputDir">Output directory.</param>
        /// <param name="fileNameBase">Base name for output file.</param>
        /// <param name="quiet">Suppress progress output.</param>
        /// <param name="logOutput">Optional log writer.</param>
        /// <param name="logMetadata">Optional additional metadata for log.</param>
        /// <returns>Tuple of (extension, headerInt).</returns>
        private (string extension, uint headerInt) ExtractFileEntry(
            BinaryReader brInput,
            int offset,
            int size,
            string outputDir,
            string fileNameBase,
            bool quiet,
            StreamWriter? logOutput = null,
            string? logMetadata = null
        )
        {
            brInput.BaseStream.Seek(offset, SeekOrigin.Begin);
            byte[] entryData = brInput.ReadBytes(size);

            string extension = ByteOperations.DetectExtension(entryData, out uint headerInt);

            if (!quiet)
                _logger.WriteLine($"Offset: 0x{offset:X8}, Size: 0x{size:X8} ({extension})");

            if (logOutput != null)
            {
                logOutput.WriteLine($"{fileNameBase}.{extension},{offset},{size}{logMetadata ?? ""},{headerInt}");
            }

            _fileSystem.WriteAllBytes($"{outputDir}/{fileNameBase}.{extension}", entryData);

            return (extension, headerInt);
        }

        /// <summary>
        /// Unpack a simple archive file container.
        /// </summary>
        /// <param name="input">Input file name to read from.</param>
        /// <param name="brInput">Binary reader to the input file.</param>
        /// <param name="magicSize">File magic size, depends on file type.</param>
        /// <param name="createLog">true is a log file should be created.</param>
        /// <param name="cleanUp">Remove the initial input file.</param>
        /// <param name="autoStage">Unpack stage container if true.</param>
        /// <param name="quiet">Suppress progress output.</param>
        /// <returns>Output folder path.</returns>
        /// <exception cref="PackingException">Thrown if the file is too small or not a valid container.</exception>
        public string UnpackSimpleArchive(
            string input, BinaryReader brInput, int magicSize, bool createLog,
            bool cleanUp, bool autoStage, bool quiet = false
        )
        {
            long fileLength = _fileSystem.GetFileLength(input);
            string outputDir = $"{input}{_config.UnpackedSuffix}";

            // Abort if too small
            if (fileLength < 16)
            {
                throw new PackingException("File is too small to be a valid archive (minimum 16 bytes required).", input);
            }

            uint count = brInput.ReadUInt32();
            uint tempCount = count;

            // Calculate complete size of extracted data to avoid extracting plausible files that aren't archives
            int completeSize = magicSize;
            for (int i = 0; i < count; i++)
            {
                brInput.BaseStream.Seek(magicSize, SeekOrigin.Current);
                if (brInput.BaseStream.Position + 4 > brInput.BaseStream.Length)
                {
                    if (!quiet)
                        _logger.WriteLine($"File terminated early ({i}/{count}) in simple container check.");
                    count = (uint)i;
                    break;
                }
                completeSize += brInput.ReadInt32();
            }

            // Very fragile check for stage container
            const int headerSize = 4;
            brInput.BaseStream.Seek(headerSize, SeekOrigin.Begin);
            int checkUnk = brInput.ReadInt32();
            long checkZero = brInput.ReadInt64();
            if (checkUnk < 9999 && checkZero == 0)
            {
                if (autoStage)
                {
                    brInput.BaseStream.Seek(0, SeekOrigin.Begin);
                    UnpackStageContainer(input, brInput, createLog, cleanUp, quiet);
                }
                else
                {
                    throw new PackingException(
                        $"Not a valid simple container, but could be stage-specific. Try: ReFrontier.exe {Path.GetFullPath(input)} --stageContainer",
                        input
                    );
                }
                return outputDir;
            }

            if (completeSize > fileLength || tempCount == 0 || tempCount > 9999)
            {
                throw new PackingException("Not a valid simple container (invalid size or entry count).", input);
            }

            if (!quiet)
                _logger.WriteLine("Trying to unpack as generic simple container.");
            brInput.BaseStream.Seek(magicSize, SeekOrigin.Begin);

            // Write to log file if desired
            _fileSystem.CreateDirectory(outputDir);
            using var logOutput = InitializeLogFile(input, "SimpleArchive", (int)count, createLog, out string logPath);

            for (int i = 0; i < count; i++)
            {
                int entryOffset = brInput.ReadInt32();
                int entrySize = brInput.ReadInt32();

                // Check bad entries
                if (
                    entrySize < headerSize ||
                    entryOffset < headerSize ||
                    entryOffset + entrySize > brInput.BaseStream.Length
                )
                {
                    if (!quiet)
                        _logger.WriteLine($"Offset: 0x{entryOffset:X8}, Size: 0x{entrySize:X8} (SKIPPED)");
                    if (createLog)
                        logOutput.WriteLine($"null,{entryOffset},{entrySize},0");
                    continue;
                }

                // Extract file entry
                ExtractFileEntry(
                    brInput,
                    entryOffset,
                    entrySize,
                    outputDir,
                    $"{i + 1:D4}_{entryOffset:X8}",
                    quiet,
                    createLog ? logOutput : null
                );

                // Move to next entry block
                brInput.BaseStream.Seek(magicSize + (i + 1) * FileFormatConstants.SimpleArchiveEntrySize, SeekOrigin.Begin);
            }
            // Clean up
            CleanupAfterUnpack(logOutput, logPath, input, createLog, cleanUp);
            return outputDir;
        }

        /// <summary>
        /// Unpack a MHA file container.
        /// </summary>
        /// <param name="input">Input file name to read from.</param>
        /// <param name="brInput">Binary reader to the input file.</param>
        /// <param name="createLog">true is a log file should be created.</param>
        /// <param name="quiet">Suppress progress output.</param>
        /// <returns>Output folder path.</returns>
        public string UnpackMHA(string input, BinaryReader brInput, bool createLog, bool quiet = false)
        {
            string outputDir = $"{input}{_config.UnpackedSuffix}";
            _fileSystem.CreateDirectory(outputDir);

            using var logOutput = InitializeLogFile(input, "MHA", createLog, out string logPath);

            // Read header
            int pointerEntryMetaBlock = brInput.ReadInt32();
            int count = brInput.ReadInt32();
            int pointerEntryNamesBlock = brInput.ReadInt32();
            brInput.ReadInt32(); // entryNamesBlockLength
            short unk1 = brInput.ReadInt16();
            short unk2 = brInput.ReadInt16();
            if (createLog)
            {
                logOutput.WriteLine(count);
                logOutput.WriteLine(unk1);
                logOutput.WriteLine(unk2);
            }

            // File Data
            for (int i = 0; i < count; i++)
            {
                // Get meta
                brInput.BaseStream.Seek(pointerEntryMetaBlock + i * FileFormatConstants.MhaEntryMetadataSize, SeekOrigin.Begin);
                int stringOffset = brInput.ReadInt32();
                int entryOffset = brInput.ReadInt32();
                int entrySize = brInput.ReadInt32();
                int pSize = brInput.ReadInt32();        // Padded size
                int fileId = brInput.ReadInt32();

                // Get name
                brInput.BaseStream.Seek(pointerEntryNamesBlock + stringOffset, SeekOrigin.Begin);
                string entryName = FileOperations.ReadNullterminatedString(brInput, Encoding.UTF8);
                if (createLog)
                    logOutput.WriteLine(entryName + "," + fileId);

                // Extract file
                brInput.BaseStream.Seek(entryOffset, SeekOrigin.Begin);
                byte[] entryData = brInput.ReadBytes(entrySize);
                _fileSystem.WriteAllBytes($"{outputDir}/{entryName}", entryData);

                if (!quiet)
                    _logger.WriteLine(
                        $"{entryName}, String Offset: 0x{stringOffset:X8}, Offset: 0x{entryOffset:X8}, Size: 0x{entrySize:X8}, pSize: 0x{pSize:X8}, File ID: 0x{fileId:X8}"
                    );
            }

            CleanupAfterUnpack(logOutput, logPath, null, createLog, false);
            return outputDir;
        }

        /// <summary>
        /// Unpack, decompress, a JPK file.
        /// </summary>
        /// <param name="input">Input file path.</param>
        /// <param name="quiet">Suppress progress output.</param>
        /// <returns>Output file path.</returns>
        /// <exception cref="PackingException">Thrown if the JKR header is invalid or compression type is unsupported.</exception>
        /// <exception cref="ReFrontierException">Thrown if decompression fails.</exception>
        public string UnpackJPK(string input, bool quiet = false)
        {
            byte[] buffer = _fileSystem.ReadAllBytes(input);
            using MemoryStream ms = new(buffer);
            using BinaryReader br = new(ms);

            // Check for JKR header
            uint magic = br.ReadUInt32();
            if (magic != FileMagic.JKR)
            {
                throw new PackingException(
                    $"Invalid JKR header: expected 0x{FileMagic.JKR:X8}, got 0x{magic:X8}.",
                    input
                );
            }

            ms.Seek(0x2, SeekOrigin.Current);
            int type = br.ReadUInt16();
            var compressionTypes = Enum.GetValues<CompressionType>();
            if (type < 0 || type >= compressionTypes.Length)
            {
                throw new PackingException(
                    $"Invalid compression type {type}. Valid range is 0-{compressionTypes.Length - 1}.",
                    input
                );
            }
            var compressionType = compressionTypes[type];
            if (!quiet)
                _logger.WriteLine($"JPK {compressionType} (type {type})");
            IJPKDecode decoder;
            try
            {
                decoder = _codecFactory.CreateDecoder(compressionType);
            }
            catch (ReFrontierException ex)
            {
                throw ex.WithFilePath(input);
            }

            // Decompress file
            int startOffset = br.ReadInt32();
            int outSize = br.ReadInt32();

            // Use ArrayPool to reduce GC pressure for large decompression buffers
            byte[] outBuffer = ArrayPool<byte>.Shared.Rent(outSize);
            try
            {
                ms.Seek(startOffset, SeekOrigin.Begin);
                try
                {
                    decoder.ProcessOnDecode(ms, outBuffer, outSize);
                }
                catch (ReFrontierException ex)
                {
                    throw ex.WithFilePath(input);
                }

                // Get extension (only needs to read first few bytes)
                string extension = ByteOperations.DetectExtension(outBuffer, out _);

                string output = $"{input}.{extension}";
                _fileSystem.DeleteFile(input);

                // Write only the actual data size, not the full rented buffer
                using (var outStream = _fileSystem.OpenWrite(output))
                {
                    outStream.Write(outBuffer, 0, outSize);
                }

                return output;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(outBuffer);
            }
        }

        /// <summary>
        /// Unpack a stage file container.
        /// </summary>
        /// <param name="input">Input file name to read from.</param>
        /// <param name="brInput">Binary reader to the input file.</param>
        /// <param name="createLog">true is a log file should be created.</param>
        /// <param name="cleanUp">Remove the initial input file.</param>
        /// <param name="quiet">Suppress progress output.</param>
        /// <returns>Output folder path.</returns>
        public string UnpackStageContainer(string input, BinaryReader brInput, bool createLog, bool cleanUp, bool quiet = false)
        {
            if (!quiet)
                _logger.WriteLine("Trying to unpack as stage-specific container.");

            string outputDir = $"{input}{_config.UnpackedSuffix}";
            _fileSystem.CreateDirectory(outputDir);

            using var logOutput = InitializeLogFile(input, "StageContainer", createLog, out string logPath);

            // First three segments
            for (int i = 0; i < 3; i++)
            {
                int offset = brInput.ReadInt32();
                int size = brInput.ReadInt32();

                if (size == 0)
                {
                    if (!quiet)
                        _logger.WriteLine(
                            $"Offset: 0x{offset:X8}, Size: 0x{size:X8} (SKIPPED)"
                        );
                    if (createLog)
                        logOutput.WriteLine($"null,{offset},{size},0");
                    continue;
                }

                // Extract file entry
                ExtractFileEntry(
                    brInput,
                    offset,
                    size,
                    outputDir,
                    $"{i + 1:D4}_{offset:X8}",
                    quiet,
                    createLog ? logOutput : null
                );

                // Move to next entry block
                brInput.BaseStream.Seek((i + 1) * 0x08, SeekOrigin.Begin);
            }

            // Rest
            int restCount = brInput.ReadInt32();
            int unkHeader = brInput.ReadInt32();
            if (createLog)
                logOutput.WriteLine(restCount + "," + unkHeader);
            for (int i = 3; i < restCount + 3; i++)
            {
                int offset = brInput.ReadInt32();
                int size = brInput.ReadInt32();
                int unk = brInput.ReadInt32();

                if (size == 0)
                {
                    if (!quiet)
                        _logger.WriteLine(
                            $"Offset: 0x{offset:X8}, Size: 0x{size:X8}, Unk: 0x{unk:X8} (SKIPPED)"
                        );
                    if (createLog)
                        logOutput.WriteLine($"null,{offset},{size},{unk},0");
                    continue;
                }

                // Extract file entry
                brInput.BaseStream.Seek(offset, SeekOrigin.Begin);
                byte[] data = brInput.ReadBytes(size);

                string extension = ByteOperations.DetectExtension(data, out uint headerInt);

                // Print info with unk value
                if (!quiet)
                    _logger.WriteLine($"Offset: 0x{offset:X8}, Size: 0x{size:X8}, Unk: 0x{unk:X8} ({extension})");
                if (createLog)
                    logOutput.WriteLine($"{i + 1:D4}_{offset:X8}.{extension},{offset},{size},{unk},{headerInt}");

                // Extract file
                _fileSystem.WriteAllBytes($"{outputDir}/{i + 1:D4}_{offset:X8}.{extension}", data);

                // Move to next entry block
                brInput.BaseStream.Seek(FileFormatConstants.StageContainerHeaderSize + FileFormatConstants.StageContainerRestHeaderSize + (i - 3 + 1) * FileFormatConstants.StageContainerRestEntrySize, SeekOrigin.Begin);
            }

            // Clean up
            CleanupAfterUnpack(logOutput, logPath, input, createLog, cleanUp);
            return outputDir;
        }

        /// <summary>
        /// Write output to txt file.
        /// </summary>
        /// <param name="input">Input ftxt file, usually has MHF header.</param>
        /// <param name="brInput">Binary reader to the file.</param>
        /// <returns>Output file path.</returns>
        public string PrintFTXT(string input, BinaryReader brInput)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            string outputPath = $"{input}{_config.TextSuffix}";
            if (_fileSystem.FileExists(outputPath))
                _fileSystem.DeleteFile(outputPath);
            using var txtOutput = _fileSystem.CreateStreamWriter(outputPath, true, Encoding.GetEncoding("shift-jis"));

            // Read header
            brInput.BaseStream.Seek(10, SeekOrigin.Current);
            int stringCount = brInput.ReadInt16();
            brInput.ReadInt32(); // textBlockSize

            for (int i = 0; i < stringCount; i++)
            {
                string str = FileOperations.ReadNullterminatedString(brInput, Encoding.GetEncoding("shift-jis"));
                txtOutput.WriteLine(str.Replace("\n", "<NEWLINE>"));
            }

            txtOutput.Close();
            return outputPath;
        }
    }
}
