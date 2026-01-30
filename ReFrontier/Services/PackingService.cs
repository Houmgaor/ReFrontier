using System;
using System.Collections.Generic;
using System.IO;

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
        /// Standard packing of an input directory.
        ///
        /// It needs a log file to work.
        /// </summary>
        /// <param name="inputDir">Input directory path.</param>
        /// <exception cref="FileNotFoundException">The log file does not exist.</exception>
        /// <exception cref="InvalidOperationException">The packing format does not exist.</exception>
        public void ProcessPackInput(string inputDir)
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

            switch (logContent[0])
            {
                case "SimpleArchive":
                    PackSimpleArchive(logContent, inputDir);
                    break;
                case "MHA":
                    PackMHA(logContent, inputDir);
                    break;
                case "StageContainer":
                    PackStageContainer(logContent, inputDir);
                    break;
                default:
                    throw new PackingException("Unknown container type: " + logContent[0], inputDir);
            }
            _logger.WriteSeparator();
        }

        /// <summary>
        /// Simple archive packing.
        /// </summary>
        /// <param name="logContent">Content of the log file.</param>
        /// <param name="input">Input directory to pack.</param>
        private void PackSimpleArchive(string[] logContent, string input)
        {
            string fileName = logContent[1];
            int count = int.Parse(logContent[2]);
            _logger.WriteLine($"Simple archive with {count} entries.");

            // Entries
            List<string> listFileNames = [];

            for (int i = 3; i < logContent.Length; i++)
            {
                string[] columns = logContent[i].Split(',');
                listFileNames.Add(columns[0]);
            }

            _fileSystem.CreateDirectory(_config.OutputDirectory);
            fileName = $"{_config.OutputDirectory}/{fileName}";
            using (var stream = _fileSystem.OpenWrite(fileName))
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
                    bwOutput.BaseStream.Seek(0x04 + i * 0x08, SeekOrigin.Begin);
                    bwOutput.Write(offset);
                    bwOutput.Write(fileData.Length);
                    bwOutput.BaseStream.Seek(offset, SeekOrigin.Begin);
                    bwOutput.Write(fileData);
                    offset += fileData.Length;
                }
            }
            _fileOperations.GetUpdateEntryInstance(fileName);
        }

        /// <summary>
        /// MHA packing.
        ///
        /// This doesn't do file data padding for now, but seems the game works just fine without it.
        /// </summary>
        /// <param name="logContent">Content of the log file.</param>
        /// <param name="input">Input directory to pack.</param>
        private void PackMHA(string[] logContent, string input)
        {
            string fileName = logContent[1];
            int count = int.Parse(logContent[2]);
            short unk1 = short.Parse(logContent[3]);
            short unk2 = short.Parse(logContent[4]);
            _logger.WriteLine($"MHA with {count} entries (unk1: {unk1}, unk2: {unk2}).");

            // Entries
            List<string> listFileNames = [];
            List<int> listFileIds = [];

            for (int i = 0; i < count; i++)
            {
                string[] columns = logContent[i + 5].Split(',');  // 5 = Account for meta data entries before
                listFileNames.Add(columns[0]);
                listFileIds.Add(int.Parse(columns[1]));
            }

            // Set up memory streams for segments
            MemoryStream entryMetaBlock = new();
            MemoryStream entryNamesBlock = new();

            _fileSystem.CreateDirectory(_config.OutputDirectory);
            fileName = $"{_config.OutputDirectory}/{fileName}";
            using var stream = _fileSystem.OpenWrite(fileName);
            using BinaryWriter bwOutput = new(stream);
            // Header
            bwOutput.Write(23160941);    // MHA magic
            bwOutput.Write(0);           // pointerEntryMetaBlock
            bwOutput.Write(count);
            bwOutput.Write(0);           // pointerEntryNamesBlock
            bwOutput.Write(0);           // entryNamesBlockLength
            bwOutput.Write(unk1);
            bwOutput.Write(unk2);

            int pointerEntryNamesBlock = 0x18;   // 0x18 = Header length
            int stringOffset = 0;
            for (int i = 0; i < count; i++)
            {
                _logger.WriteLine($"{input}/{listFileNames[i]}");
                byte[] fileData = _fileSystem.ReadAllBytes($"{input}/{listFileNames[i]}");
                bwOutput.Write(fileData);

                entryMetaBlock.Write(BitConverter.GetBytes(stringOffset), 0, 4);
                entryMetaBlock.Write(BitConverter.GetBytes(pointerEntryNamesBlock), 0, 4);
                entryMetaBlock.Write(BitConverter.GetBytes(fileData.Length), 0, 4);
                entryMetaBlock.Write(BitConverter.GetBytes(fileData.Length), 0, 4); // write psize if necessary
                entryMetaBlock.Write(BitConverter.GetBytes(listFileIds[i]), 0, 4);

                System.Text.UTF8Encoding enc = new();
                byte[] arrayFileName = enc.GetBytes(listFileNames[i]);
                entryNamesBlock.Write(arrayFileName, 0, arrayFileName.Length);
                entryNamesBlock.WriteByte(0);
                stringOffset += arrayFileName.Length + 1;

                pointerEntryNamesBlock += fileData.Length; // update with psize if necessary
            }

            bwOutput.Write(entryNamesBlock.ToArray());
            bwOutput.Write(entryMetaBlock.ToArray());

            // Update offsets
            bwOutput.Seek(4, SeekOrigin.Begin);
            bwOutput.Write((int)(pointerEntryNamesBlock + entryNamesBlock.Length));
            bwOutput.Write(count);
            bwOutput.Write(pointerEntryNamesBlock);
            bwOutput.Write((int)entryNamesBlock.Length);
        }

        /// <summary>
        /// Stage container packing.
        /// </summary>
        /// <param name="logContent">Content of the log file.</param>
        /// <param name="input">Input directory to pack.</param>
        private void PackStageContainer(string[] logContent, string input)
        {
            string fileName = logContent[1];

            // Entries
            List<string> listFileNames = [];

            // For first three segments
            for (int i = 2; i < 5; i++)
            {
                string[] columns = logContent[i].Split(',');
                listFileNames.Add(columns[0]);
            }

            // For rest of files
            int restCount = int.Parse(logContent[5].Split(',')[0]);
            int restUnkHeader = int.Parse(logContent[5].Split(',')[1]);

            for (int i = 6; i < 6 + restCount; i++)
            {
                string[] columns = logContent[i].Split(',');
                listFileNames.Add(columns[0]);
            }

            _logger.WriteLine($"Stage Container with {listFileNames.Count} entries.");

            _fileSystem.CreateDirectory(_config.OutputDirectory);
            fileName = $"{_config.OutputDirectory}/{fileName}";
            using var stream = _fileSystem.OpenWrite(fileName);
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
                    bwOutput.Write(offset);
                    bwOutput.Write(fileData.Length);
                    bwOutput.BaseStream.Seek(offset, SeekOrigin.Begin);
                    bwOutput.Write(fileData);
                    offset += fileData.Length;
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
                    bwOutput.Write(offset);
                    bwOutput.Write(fileData.Length);
                    bwOutput.Write(int.Parse(logContent[6 + i - 3].Split(',')[3]));
                    bwOutput.BaseStream.Seek(offset, SeekOrigin.Begin);
                    bwOutput.Write(fileData);
                    offset += fileData.Length;
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
        public void JPKEncode(Compression compression, string inPath, string otPath)
        {
            _fileSystem.CreateDirectory(_config.OutputDirectory);

            byte[] buffer = _fileSystem.ReadAllBytes(inPath);
            int insize = buffer.Length;
            if (_fileSystem.FileExists(otPath))
                _fileSystem.DeleteFile(otPath);
            _logger.WriteLine(
                $"Starting file compression, type {compression.type}, level {compression.level} to {otPath}"
            );
            using var fsot = _fileSystem.Create(otPath);
            using BinaryWriter br = new(fsot);
            // JKR header
            br.Write((uint)0x1A524B4A);
            br.Write((ushort)0x108);
            br.Write((ushort)compression.type);
            br.Write((uint)0x10);
            br.Write(insize);

            IJPKEncode encoder;
            try
            {
                encoder = _codecFactory.CreateEncoder(compression.type);
            }
            catch (InvalidOperationException)
            {
                fsot.Close();
                _fileSystem.DeleteFile(otPath);
                throw;
            }

            DateTime start, finnish;
            start = DateTime.Now;
            encoder.ProcessOnEncode(buffer, fsot, compression.level * 100);
            finnish = DateTime.Now;
            _logger.PrintWithSeparator(
                $"File compressed using {compression.type} compression level {compression.level}: " +
                $"{fsot.Length} bytes ({1 - (decimal)fsot.Length / insize:P} saved) in {finnish - start:%m\\:ss\\.ff}",
                false
            );
        }
    }
}
