using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using LibReFrontier;
using LibReFrontier.Abstractions;
using LibReFrontier.Exceptions;

using ReFrontier.Jpk;

namespace ReFrontier.Services
{
    /// <summary>
    /// Service for packing archives and compressing files.
    /// </summary>
    public class PackingService
    {
        /// <summary>
        /// Suffix of the temporary file an archive is packed into before being promoted
        /// to its final name, so a failed pack cannot leave a truncated archive behind.
        /// </summary>
        private const string PackingSuffix = ".packing";

        private readonly IFileSystem _fileSystem;
        private readonly ILogger _logger;
        private readonly ICodecFactory _codecFactory;
        private readonly FileProcessingConfig _config;
        private readonly FileOperations _fileOperations;

        /// <summary>
        /// Create a new PackingService with default dependencies.
        /// </summary>
        public PackingService()
            : this(new RealFileSystem(), new ConsoleLogger(), new DefaultCodecFactory(), FileProcessingConfig.Default())
        {
        }

        /// <summary>
        /// Create a new PackingService with injectable dependencies.
        /// </summary>
        /// <param name="fileSystem">File system abstraction.</param>
        /// <param name="logger">Logger abstraction.</param>
        /// <param name="codecFactory">Codec factory for encoders.</param>
        /// <param name="config">Configuration settings.</param>
        public PackingService(IFileSystem fileSystem, ILogger logger, ICodecFactory codecFactory, FileProcessingConfig config)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _codecFactory = codecFactory ?? throw new ArgumentNullException(nameof(codecFactory));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _fileOperations = new FileOperations(fileSystem, logger);
        }

        /// <summary>
        /// Parse a string as a numeric type with validation.
        /// </summary>
        /// <typeparam name="T">The numeric type to parse (int, short, etc.).</typeparam>
        /// <param name="value">String value to parse.</param>
        /// <param name="fieldName">Name of the field being parsed (for error messages).</param>
        /// <param name="context">Additional context for the error message.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="PackingException">Thrown when the value cannot be parsed.</exception>
        private static T ParseOrThrow<T>(string value, string fieldName, string context)
            where T : struct, IParsable<T>
        {
            if (!T.TryParse(value, null, out T result))
            {
                string typeName = typeof(T).Name.ToLower();
                throw new PackingException($"Invalid {fieldName}: '{value}' is not a valid {typeName}. {context}");
            }
            return result;
        }

        /// <summary>
        /// Write a file entry with offset and size to the archive.
        /// </summary>
        /// <param name="bwOutput">Binary writer for output.</param>
        /// <param name="headerPosition">Position in header to write metadata.</param>
        /// <param name="dataOffset">Offset where file data will be written.</param>
        /// <param name="fileData">The file data to write.</param>
        /// <returns>New offset after writing file data.</returns>
        private int WriteFileEntry(BinaryWriter bwOutput, long headerPosition, int dataOffset, byte[] fileData)
        {
            bwOutput.BaseStream.Seek(headerPosition, SeekOrigin.Begin);
            bwOutput.Write(dataOffset);
            bwOutput.Write(fileData.Length);
            bwOutput.BaseStream.Seek(dataOffset, SeekOrigin.Begin);
            bwOutput.Write(fileData);
            return dataOffset + fileData.Length;
        }

        /// <summary>
        /// Write a file entry with offset, size, and unk value to the archive.
        /// </summary>
        /// <param name="bwOutput">Binary writer for output.</param>
        /// <param name="headerPosition">Position in header to write metadata.</param>
        /// <param name="dataOffset">Offset where file data will be written.</param>
        /// <param name="fileData">The file data to write.</param>
        /// <param name="unkValue">Unknown value to write after size.</param>
        /// <returns>New offset after writing file data.</returns>
        private int WriteFileEntryWithUnk(BinaryWriter bwOutput, long headerPosition, int dataOffset, byte[] fileData, int unkValue)
        {
            bwOutput.BaseStream.Seek(headerPosition, SeekOrigin.Begin);
            bwOutput.Write(dataOffset);
            bwOutput.Write(fileData.Length);
            bwOutput.Write(unkValue);
            bwOutput.BaseStream.Seek(dataOffset, SeekOrigin.Begin);
            bwOutput.Write(fileData);
            return dataOffset + fileData.Length;
        }

        /// <summary>
        /// Standard packing of an input directory.
        ///
        /// It needs a log file to work.
        /// </summary>
        /// <param name="inputDir">Input directory path.</param>
        /// <exception cref="FileNotFoundException">Thrown if the log file does not exist.</exception>
        /// <exception cref="PackingException">Thrown if the container type is unknown or packing fails.</exception>
        public void ProcessPackInput(string inputDir)
        {
            ProcessPackInput(inputDir, null);
        }

        /// <summary>
        /// Standard packing of an input directory, into a chosen directory.
        ///
        /// <para>Restoring a nested container writes it back beside its siblings rather
        /// than into the output directory, so that the parent container can then be packed
        /// from a complete directory.</para>
        /// </summary>
        /// <param name="inputDir">Input directory path.</param>
        /// <param name="outputDirectory">Directory to write the packed container into,
        /// or null to use the configured output directory.</param>
        /// <returns>Path of the packed container.</returns>
        /// <exception cref="FileNotFoundException">Thrown if the log file does not exist.</exception>
        /// <exception cref="PackingException">Thrown if the container type is unknown or packing fails.</exception>
        public string ProcessPackInput(string inputDir, string? outputDirectory)
        {
            string logFile = Path.Join(
                inputDir,
                $"{inputDir[(inputDir.LastIndexOf('/') + 1)..]}{_config.LogSuffix}"
            );
            if (!_fileSystem.FileExists(logFile))
            {
                string tempLog = logFile;
                logFile = inputDir[..inputDir.LastIndexOf('.')] + _config.LogSuffix;
                if (!_fileSystem.FileExists(logFile))
                    throw new FileNotFoundException(
                        $"Neither log files {tempLog} nor {logFile} exist."
                    );
            }
            string[] logContent = _fileSystem.ReadAllLines(logFile);

            if (logContent.Length < 2)
            {
                throw new PackingException(
                    $"Log file {logFile} is too short to describe a container. " +
                    "Extract the original file again with --saveMeta to regenerate it.",
                    inputDir
                );
            }

            string containerType = logContent[0];
            string targetDirectory = outputDirectory ?? _config.OutputDirectory;
            _fileSystem.CreateDirectory(targetDirectory);
            string outputPath = $"{targetDirectory}/{logContent[1]}";

            // Pack into a temporary file and only promote it once packing has fully
            // succeeded. Writing straight to the output path would leave a truncated
            // archive behind on failure, which reads as a result rather than a failure.
            string workingPath = $"{outputPath}{PackingSuffix}";
            try
            {
                switch (containerType)
                {
                    case "SimpleArchive":
                        PackSimpleArchive(logContent, inputDir, workingPath);
                        break;
                    case "MOMO":
                        PackMomoArchive(logContent, inputDir, workingPath);
                        break;
                    case "MHA":
                        PackMHA(logContent, inputDir, workingPath);
                        break;
                    case "StageContainer":
                        PackStageContainer(logContent, inputDir, workingPath);
                        break;
                    default:
                        throw new PackingException("Unknown container type: " + containerType, inputDir);
                }

                _fileSystem.Copy(workingPath, outputPath);
            }
            finally
            {
                if (_fileSystem.FileExists(workingPath))
                    _fileSystem.DeleteFile(workingPath);
            }

            if (containerType != "StageContainer")
                _fileOperations.GetUpdateEntryInstance(outputPath);
            _logger.WriteSeparator();
            return outputPath;
        }

        /// <summary>
        /// Check that every entry the log names is present before any output is written.
        ///
        /// <para>Extraction unpacks nested entries by default, which renames them
        /// (<c>entry.jkr</c> becomes <c>entry.jkr.bin</c>) and leaves the log pointing at
        /// the original names. Packing then failed part way through the archive, so this
        /// reports the whole picture up front instead.</para>
        /// </summary>
        /// <param name="entryNames">Entry names taken from the log.</param>
        /// <param name="expectedCount">How many entries the packer will write.</param>
        /// <param name="input">Input directory being packed.</param>
        /// <param name="containerType">Container type, for the error message.</param>
        /// <exception cref="PackingException">Thrown if the log is truncated or entries are missing.</exception>
        private void EnsureEntriesArePackable(
            IReadOnlyList<string> entryNames, int expectedCount, string input, string containerType)
        {
            if (expectedCount > entryNames.Count)
            {
                throw new PackingException(
                    $"Cannot pack {containerType} {input}: the log declares {expectedCount} entries " +
                    $"but lists only {entryNames.Count}. The log file is truncated or corrupt.",
                    input
                );
            }

            var missing = new List<string>();
            for (int i = 0; i < expectedCount; i++)
            {
                string name = entryNames[i];
                if (name == "null" || string.IsNullOrEmpty(name))
                    continue;
                if (_fileSystem.FileExists($"{input}/{name}"))
                    continue;

                string? detail = DescribeMissingEntry(input, name);
                missing.Add(detail == null ? $"  {name}" : $"  {name} - {detail}");
            }

            if (missing.Count == 0)
                return;

            throw new PackingException(
                $"Cannot pack {containerType} {input}: {missing.Count} of {expectedCount} entries " +
                $"named by the log are missing.\n" +
                string.Join("\n", missing) +
                "\n\nUnpacking is recursive by default, which replaces nested entries with their " +
                "unpacked form and leaves the log naming the originals. Rebuild each entry under the " +
                "name the log uses, or extract the container again with --nonRecursive so its entries " +
                "stay packed.",
                input
            );
        }

        /// <summary>
        /// Explain what became of an entry the log names but that is no longer on disk.
        /// </summary>
        /// <param name="input">Input directory being packed.</param>
        /// <param name="name">Entry name as it appears in the log.</param>
        /// <returns>A short explanation, or null if nothing recognisable was found.</returns>
        private string? DescribeMissingEntry(string input, string name)
        {
            // A recipe means extraction decrypted or decompressed this entry in place,
            // and records exactly what it became.
            string recipePath = $"{input}/{name}{ExtractionRecipe.FileSuffix}";
            if (_fileSystem.FileExists(recipePath))
            {
                var recipe = ExtractionRecipe.Deserialize(_fileSystem.ReadAllBytes(recipePath));
                if (recipe != null && !string.IsNullOrEmpty(recipe.ExtractedFile))
                    return $"unpacked to {recipe.ExtractedFile}, rebuild it with --restore";
            }

            if (_fileSystem.DirectoryExists($"{input}/{name}{_config.UnpackedSuffix}"))
                return $"unpacked into {name}{_config.UnpackedSuffix}/, repack that directory first";

            string[] candidates = _fileSystem.GetFiles(input, $"{name}.*", SearchOption.TopDirectoryOnly);
            if (candidates.Length > 0)
                return $"found {Path.GetFileName(candidates[0])} instead";

            return null;
        }

        /// <summary>
        /// Simple archive packing.
        /// </summary>
        /// <param name="logContent">Content of the log file.</param>
        /// <param name="input">Input directory to pack.</param>
        /// <param name="outputPath">Path to write the packed archive to.</param>
        private void PackSimpleArchive(string[] logContent, string input, string outputPath)
        {
            int count = ParseOrThrow<int>(logContent[2], "entry count", "Check the log file format.");
            _logger.WriteLine($"Simple archive with {count} entries.");

            // Entries
            List<string> listFileNames = [];

            for (int i = 3; i < logContent.Length; i++)
            {
                string[] columns = logContent[i].Split(',');
                listFileNames.Add(columns[0]);
            }

            EnsureEntriesArePackable(listFileNames, count, input, "simple archive");

            using (var stream = _fileSystem.OpenWrite(outputPath))
            using (BinaryWriter bwOutput = new(stream))
            {
                bwOutput.Write(count);
                int offset = 0x04 + count * 0x08;
                for (int i = 0; i < count; i++)
                {
                    _logger.WriteLine($"{input}/{listFileNames[i]}");
                    byte[] fileData = [];
                    if (listFileNames[i] != "null")
                    {
                        fileData = _fileSystem.ReadAllBytes($"{input}/{listFileNames[i]}");
                    }
                    offset = WriteFileEntry(bwOutput, 0x04 + i * 0x08, offset, fileData);
                }
            }
        }

        /// <summary>
        /// MOMO archive packing.
        ///
        /// <para>A MOMO archive is a 4-byte magic, the entry count, then a table of
        /// offset and size pairs. Entry data is aligned to
        /// <see cref="FileFormatConstants.MomoEntryAlignment"/> and the file is padded with
        /// zeros to the same boundary, which reproduces the game's archives exactly.</para>
        /// </summary>
        /// <param name="logContent">Content of the log file.</param>
        /// <param name="input">Input directory to pack.</param>
        /// <param name="outputPath">Path to write the packed archive to.</param>
        private void PackMomoArchive(string[] logContent, string input, string outputPath)
        {
            int count = ParseOrThrow<int>(logContent[2], "entry count", "Check the MOMO log file format.");
            _logger.WriteLine($"MOMO archive with {count} entries.");

            List<string> listFileNames = [];
            for (int i = 3; i < logContent.Length; i++)
            {
                string[] columns = logContent[i].Split(',');
                listFileNames.Add(columns[0]);
            }

            EnsureEntriesArePackable(listFileNames, count, input, "MOMO archive");

            using (var stream = _fileSystem.OpenWrite(outputPath))
            using (BinaryWriter bwOutput = new(stream))
            {
                bwOutput.Write(FileMagic.MOMO);
                bwOutput.Write(count);

                int offset = AlignUp(
                    FileFormatConstants.MomoHeaderSize + count * FileFormatConstants.SimpleArchiveEntrySize,
                    FileFormatConstants.MomoEntryAlignment
                );
                for (int i = 0; i < count; i++)
                {
                    _logger.WriteLine($"{input}/{listFileNames[i]}");
                    byte[] fileData = [];
                    if (listFileNames[i] != "null")
                    {
                        fileData = _fileSystem.ReadAllBytes($"{input}/{listFileNames[i]}");
                    }
                    int entryHeader = FileFormatConstants.MomoHeaderSize
                        + i * FileFormatConstants.SimpleArchiveEntrySize;
                    int entryEnd = WriteFileEntry(bwOutput, entryHeader, offset, fileData);
                    offset = AlignUp(entryEnd, FileFormatConstants.MomoEntryAlignment);
                }

                // Pad out to the alignment boundary, as the game's own archives are.
                if (bwOutput.BaseStream.Length < offset)
                    bwOutput.BaseStream.SetLength(offset);
            }
        }

        /// <summary>
        /// Round a value up to the next multiple of an alignment.
        /// </summary>
        /// <param name="value">Value to round.</param>
        /// <param name="alignment">Alignment boundary, a power of two.</param>
        /// <returns>The rounded value.</returns>
        private static int AlignUp(int value, int alignment)
        {
            return (value + alignment - 1) & ~(alignment - 1);
        }

        /// <summary>
        /// MHA packing.
        ///
        /// This doesn't do file data padding for now, but seems the game works just fine without it.
        /// </summary>
        /// <param name="logContent">Content of the log file.</param>
        /// <param name="input">Input directory to pack.</param>
        /// <param name="outputPath">Path to write the packed archive to.</param>
        private void PackMHA(string[] logContent, string input, string outputPath)
        {
            int count = ParseOrThrow<int>(logContent[2], "entry count", "Check the MHA log file format.");
            short unk1 = ParseOrThrow<short>(logContent[3], "unk1", "Check the MHA log file format.");
            short unk2 = ParseOrThrow<short>(logContent[4], "unk2", "Check the MHA log file format.");
            _logger.WriteLine($"MHA with {count} entries (unk1: {unk1}, unk2: {unk2}).");

            // Entries
            List<string> listFileNames = [];
            List<int> listFileIds = [];
            List<int> listPaddedSizes = [];

            for (int i = 0; i < count; i++)
            {
                string[] columns = logContent[i + 5].Split(',');  // 5 = Account for meta data entries before
                listFileNames.Add(columns[0]);
                listFileIds.Add(ParseOrThrow<int>(columns[1], "file ID", $"Entry {i + 1} in MHA log file."));

                // Third column added later; logs written by older versions do not have it.
                listPaddedSizes.Add(
                    columns.Length > 2
                        ? ParseOrThrow<int>(columns[2], "padded size", $"Entry {i + 1} in MHA log file.")
                        : 0
                );
            }

            EnsureEntriesArePackable(listFileNames, count, input, "MHA archive");

            // Set up memory streams for segments
            MemoryStream entryMetaBlock = new();
            MemoryStream entryNamesBlock = new();

            using (var stream = _fileSystem.OpenWrite(outputPath))
            using (BinaryWriter bwOutput = new(stream))
            {
                // Header
                bwOutput.Write(FileMagic.MHA);
                bwOutput.Write(0);           // pointerEntryMetaBlock
                bwOutput.Write(count);
                bwOutput.Write(0);           // pointerEntryNamesBlock
                bwOutput.Write(0);           // entryNamesBlockLength
                bwOutput.Write(unk1);
                bwOutput.Write(unk2);

                int entryOffset = FileFormatConstants.MhaHeaderSize;
                int stringOffset = 0;
                for (int i = 0; i < count; i++)
                {
                    _logger.WriteLine($"{input}/{listFileNames[i]}");
                    byte[] fileData = _fileSystem.ReadAllBytes($"{input}/{listFileNames[i]}");

                    // Keep the original padding when the entry still fits inside it, so an
                    // untouched archive is rebuilt byte for byte. Entries that grew, and
                    // logs from before the padded size was recorded, pad to the next
                    // boundary past the data.
                    int paddedSize = listPaddedSizes[i] > fileData.Length
                        ? listPaddedSizes[i]
                        : PadEntrySize(fileData.Length);

                    bwOutput.BaseStream.Seek(entryOffset, SeekOrigin.Begin);
                    bwOutput.Write(fileData);

                    entryMetaBlock.Write(BitConverter.GetBytes(stringOffset), 0, 4);
                    entryMetaBlock.Write(BitConverter.GetBytes(entryOffset), 0, 4);
                    entryMetaBlock.Write(BitConverter.GetBytes(fileData.Length), 0, 4);
                    entryMetaBlock.Write(BitConverter.GetBytes(paddedSize), 0, 4);
                    entryMetaBlock.Write(BitConverter.GetBytes(listFileIds[i]), 0, 4);

                    byte[] arrayFileName = Encoding.UTF8.GetBytes(listFileNames[i]);
                    entryNamesBlock.Write(arrayFileName, 0, arrayFileName.Length);
                    entryNamesBlock.WriteByte(0);
                    stringOffset += arrayFileName.Length + 1;

                    entryOffset += paddedSize;
                }

                // Names then metadata follow the padded entry data, and end the file.
                // Seeking past the last entry's padding leaves it zero filled.
                bwOutput.BaseStream.Seek(entryOffset, SeekOrigin.Begin);
                bwOutput.Write(entryNamesBlock.ToArray());
                bwOutput.Write(entryMetaBlock.ToArray());

                // Update offsets
                bwOutput.Seek(4, SeekOrigin.Begin);
                bwOutput.Write(entryOffset + (int)entryNamesBlock.Length);
                bwOutput.Write(count);
                bwOutput.Write(entryOffset);
                bwOutput.Write((int)entryNamesBlock.Length);
            }
        }

        /// <summary>
        /// Padded size of an MHA entry: the next alignment boundary strictly past its data.
        ///
        /// <para>Every entry in the game's archives carries at least one padding byte, so an
        /// entry whose size already lands on the boundary still gains a full block.</para>
        /// </summary>
        /// <param name="size">Entry size in bytes.</param>
        /// <returns>The padded size.</returns>
        private static int PadEntrySize(int size)
        {
            return size - (size % FileFormatConstants.MhaEntryAlignment)
                + FileFormatConstants.MhaEntryAlignment;
        }

        /// <summary>
        /// Stage container packing.
        /// </summary>
        /// <param name="logContent">Content of the log file.</param>
        /// <param name="input">Input directory to pack.</param>
        /// <param name="outputPath">Path to write the packed container to.</param>
        private void PackStageContainer(string[] logContent, string input, string outputPath)
        {
            // Entries
            List<string> listFileNames = [];

            // For first three segments
            for (int i = 2; i < 5; i++)
            {
                string[] columns = logContent[i].Split(',');
                listFileNames.Add(columns[0]);
            }

            // For rest of files
            string[] restMetadata = logContent[5].Split(',');
            int restCount = ParseOrThrow<int>(restMetadata[0], "rest count", "Check the StageContainer log file format.");
            int restUnkHeader = ParseOrThrow<int>(restMetadata[1], "rest header", "Check the StageContainer log file format.");

            for (int i = 6; i < 6 + restCount; i++)
            {
                string[] columns = logContent[i].Split(',');
                listFileNames.Add(columns[0]);
            }

            _logger.WriteLine($"Stage Container with {listFileNames.Count} entries.");

            EnsureEntriesArePackable(listFileNames, listFileNames.Count, input, "stage container");

            using var stream = _fileSystem.OpenWrite(outputPath);
            using BinaryWriter bwOutput = new(stream);
            // Write temp dir
            // + 8 = rest count and unk header int
            // the directory in the requested test file is padded to 16 bytes
            // not sure if necessary and if this applies to all
            byte[] tempDir = new byte[(3 * 8 + restCount * 0x0C + 8 + 15) & ~15];
            bwOutput.Write(tempDir);

            int offset = tempDir.Length;

            // For first three segments
            for (int i = 0; i < 3; i++)
            {
                byte[] fileData = [];
                bwOutput.BaseStream.Seek(i * 0x08, SeekOrigin.Begin);

                if (listFileNames[i] != "null")
                {
                    _logger.WriteLine($"{input}/{listFileNames[i]}");
                    fileData = _fileSystem.ReadAllBytes($"{input}/{listFileNames[i]}");
                    offset = WriteFileEntry(bwOutput, bwOutput.BaseStream.Position, offset, fileData);
                }
                else
                {
                    _logger.WriteLine("Writing null entry");
                    bwOutput.Write((long)0);
                }
            }

            // For rest
            bwOutput.BaseStream.Seek(3 * 0x08, SeekOrigin.Begin);
            bwOutput.Write(restCount);
            bwOutput.Write(restUnkHeader);

            for (int i = 3; i < restCount + 3; i++)
            {
                byte[] fileData = [];
                bwOutput.BaseStream.Seek(3 * 8 + (i - 3) * 0x0C + 8, SeekOrigin.Begin); // + 8 = rest count and unk header int

                if (listFileNames[i] != "null")
                {
                    _logger.WriteLine($"{input}/{listFileNames[i]}");
                    fileData = _fileSystem.ReadAllBytes($"{input}/{listFileNames[i]}");
                    int unkValue = ParseOrThrow<int>(logContent[6 + i - 3].Split(',')[3], "unk value", $"Entry {i + 1} in StageContainer log file.");
                    offset = WriteFileEntryWithUnk(bwOutput, bwOutput.BaseStream.Position, offset, fileData, unkValue);
                }
                else
                {
                    _logger.WriteLine("Writing null entry");
                    bwOutput.Write((long)0);
                    bwOutput.Write(0);
                }
            }
        }

        /// <summary>
        /// Compress a JPK file to a JKR type.
        /// </summary>
        /// <param name="compression">Compression to use.</param>
        /// <param name="inPath">Input file path.</param>
        /// <param name="otPath">Output file path.</param>
        /// <exception cref="ReFrontierException">Thrown if compression fails or codec is unavailable.</exception>
        public void JPKEncode(Compression compression, string inPath, string otPath)
        {
            _fileSystem.CreateDirectory(_config.OutputDirectory);

            byte[] buffer = _fileSystem.ReadAllBytes(inPath);
            int insize = buffer.Length;
            if (_fileSystem.FileExists(otPath))
                _fileSystem.DeleteFile(otPath);
            _logger.WriteLine(
                $"Starting file compression, type {compression.Type}, level {compression.Level} to {otPath}"
            );
            using var fsot = _fileSystem.Create(otPath);
            using BinaryWriter br = new(fsot);
            // JKR header
            br.Write(FileMagic.JKR);
            br.Write((ushort)0x108);
            br.Write((ushort)compression.Type);
            br.Write((uint)0x10);
            br.Write(insize);

            IJPKEncode encoder;
            try
            {
                encoder = _codecFactory.CreateEncoder(compression.Type);
            }
            catch (ReFrontierException ex)
            {
                fsot.Close();
                _fileSystem.DeleteFile(otPath);
                throw ex.WithFilePath(inPath);
            }

            DateTime start, finnish;
            start = DateTime.Now;
            try
            {
                encoder.ProcessOnEncode(buffer, fsot, compression.Level * 100);
            }
            catch (ReFrontierException ex)
            {
                throw ex.WithFilePath(inPath);
            }
            finnish = DateTime.Now;
            _logger.PrintWithSeparator(
                $"File compressed using {compression.Type} compression level {compression.Level}: " +
                $"{fsot.Length} bytes ({1 - (decimal)fsot.Length / insize:P} saved) in {finnish - start:%m\\:ss\\.ff}",
                false
            );
        }

        /// <summary>
        /// Pack an FTXT text file from extracted .txt format.
        ///
        /// The input file should be a .txt file with one string per line.
        /// Newlines within strings are represented as &lt;NEWLINE&gt; markers.
        /// </summary>
        /// <param name="inputFile">Input .txt file path.</param>
        /// <param name="metaFile">Meta file containing the original 16-byte header.</param>
        /// <param name="cleanUp">Remove input and meta files after packing.</param>
        /// <param name="verbose">Show per-file processing messages.</param>
        /// <returns>Output file path.</returns>
        /// <exception cref="FileNotFoundException">Thrown if the meta file does not exist.</exception>
        /// <exception cref="PackingException">Thrown if packing fails.</exception>
        public string PackFTXT(string inputFile, string metaFile, bool cleanUp, bool verbose = false)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Encoding shiftJis = Encoding.GetEncoding("shift-jis");

            if (!_fileSystem.FileExists(metaFile))
            {
                throw new FileNotFoundException(
                    $"META file {metaFile} does not exist, " +
                    $"cannot pack {inputFile}. " +
                    "Make sure to extract the original file with the --saveMeta option, " +
                    "and to place the generated meta file in the same folder as the file " +
                    "to pack."
                );
            }

            // Read meta (original header)
            byte[] meta = _fileSystem.ReadAllBytes(metaFile);
            if (meta.Length < FileFormatConstants.FtxtHeaderLength)
            {
                throw new PackingException(
                    $"META file {metaFile} is too small: expected {FileFormatConstants.FtxtHeaderLength} bytes, got {meta.Length}.",
                    inputFile
                );
            }

            // Read strings from text file
            string[] lines = _fileSystem.ReadAllLines(inputFile);
            List<byte[]> encodedStrings = new();

            foreach (string line in lines)
            {
                // Unescape special characters back to actual values
                string processed = line
                    .Replace("\\r\\n", "\r\n")
                    .Replace("\\n", "\n")
                    .Replace("\\t", "\t")
                    .Replace("\\\\", "\\");
                byte[] encoded = shiftJis.GetBytes(processed);
                encodedStrings.Add(encoded);
            }

            // Calculate text block size (sum of encoded lengths + null terminators)
            int textBlockSize = 0;
            foreach (byte[] encoded in encodedStrings)
            {
                textBlockSize += encoded.Length + 1; // +1 for null terminator
            }

            // Build output: 16-byte header + string data
            // From file.ftxt.txt to file.ftxt
            string outputFile = Path.Join(
                Path.GetDirectoryName(inputFile),
                Path.GetFileNameWithoutExtension(inputFile)
            );

            using var stream = _fileSystem.OpenWrite(outputFile);
            using BinaryWriter bw = new(stream);

            // Write header: copy first 10 bytes from meta, then update count and size
            bw.Write(meta, 0, 10);
            bw.Write((short)encodedStrings.Count);
            bw.Write(textBlockSize);

            // Write strings with null terminators
            foreach (byte[] encoded in encodedStrings)
            {
                bw.Write(encoded);
                bw.Write((byte)0); // null terminator
            }

            if (verbose)
                _logger.PrintWithSeparator($"FTXT packed to {outputFile} ({encodedStrings.Count} strings).", false);

            if (cleanUp)
            {
                _fileSystem.DeleteFile(inputFile);
                _fileSystem.DeleteFile(metaFile);
            }

            return outputFile;
        }
    }
}
