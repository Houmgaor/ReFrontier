using System;
using System.Collections.Generic;
using System.IO;

using CsvHelper;

using LibReFrontier;
using LibReFrontier.Abstractions;

using ReFrontier;

namespace FrontierTextTool.Services
{
    /// <summary>
    /// Service for extracting text strings from binary game files.
    /// </summary>
    public class TextExtractionService
    {
        private readonly IFileSystem _fileSystem;
        private readonly ILogger _logger;
        private readonly CsvEncodingOptions _encodingOptions;

        /// <summary>
        /// Create a new TextExtractionService with default dependencies.
        /// </summary>
        public TextExtractionService()
            : this(new RealFileSystem(), new ConsoleLogger(), CsvEncodingOptions.Default)
        {
        }

        /// <summary>
        /// Create a new TextExtractionService with injectable dependencies.
        /// </summary>
        public TextExtractionService(IFileSystem fileSystem, ILogger logger)
            : this(fileSystem, logger, CsvEncodingOptions.Default)
        {
        }

        /// <summary>
        /// Create a new TextExtractionService with injectable dependencies and encoding options.
        /// </summary>
        public TextExtractionService(IFileSystem fileSystem, ILogger logger, CsvEncodingOptions encodingOptions)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _encodingOptions = encodingOptions ?? CsvEncodingOptions.Default;
        }

        /// <summary>
        /// Dump strings from a binary file with CRC32 hashes.
        /// </summary>
        /// <param name="input">Input file path.</param>
        /// <param name="startOffset">Start offset to read from (0 for beginning).</param>
        /// <param name="endOffset">End offset (0 for end of file).</param>
        /// <param name="trueOffsets">Use string pointer offsets instead of sequential reading.</param>
        /// <param name="checkNullPredecessor">When using trueOffsets, validate strings have null predecessor.</param>
        public void DumpAndHash(string input, int startOffset, int endOffset, bool trueOffsets, bool checkNullPredecessor)
        {
            var preprocessor = new FilePreprocessor();

            var (processedFile, cleanup) = preprocessor.AutoPreprocess(input, createMetaFile: true);

            try
            {
                byte[] buffer = _fileSystem.ReadAllBytes(processedFile);
                using var msInput = new MemoryStream(buffer);
                using var brInput = new BinaryReader(msInput);

                var stringsDatabase = DumpAndHashInternal(input, buffer, brInput, startOffset, endOffset, trueOffsets, checkNullPredecessor);

                WriteCsv(input, stringsDatabase);
            }
            finally
            {
                cleanup();
            }
        }

        /// <summary>
        /// Internal implementation of DumpAndHash that works on preprocessed data.
        /// </summary>
        public List<StringDatabase> DumpAndHashInternal(
            string originalInput, byte[] buffer, BinaryReader brInput,
            int startOffset, int endOffset, bool trueOffsets, bool checkNullPredecessor)
        {
            if (endOffset == 0)
                endOffset = (int)brInput.BaseStream.Length;

            _logger.WriteLine(
                $"Strings at: 0x{startOffset:X8} - 0x{endOffset:X8}. Size 0x{endOffset - startOffset:X8}"
            );

            brInput.BaseStream.Seek(startOffset, SeekOrigin.Begin);
            var stringsDatabase = new List<StringDatabase>();

            while (brInput.BaseStream.Position + 4 <= endOffset)
            {
                long offset = brInput.BaseStream.Position;
                long tmpPos = brInput.BaseStream.Position;

                if (trueOffsets)
                {
                    uint strPos = brInput.ReadUInt32();
                    if (strPos < 10 || strPos > brInput.BaseStream.Length)
                        continue;
                    tmpPos = brInput.BaseStream.Position;

                    if (checkNullPredecessor)
                    {
                        brInput.BaseStream.Seek(strPos - 2, SeekOrigin.Begin);
                        if (brInput.ReadByte() == 0 || brInput.ReadByte() != 0)
                        {
                            brInput.BaseStream.Seek(tmpPos, SeekOrigin.Begin);
                            continue;
                        }
                    }
                    brInput.BaseStream.Seek(strPos, SeekOrigin.Begin);
                }

                string str = FileOperations.ReadNullterminatedString(brInput, TextFileConfiguration.ShiftJisEncoding);

                stringsDatabase.Add(new StringDatabase
                {
                    Offset = (uint)offset,
                    Hash = Crypto.GetCrc32(TextFileConfiguration.ShiftJisEncoding.GetBytes(str)),
                    JString = str
                });

                if (trueOffsets)
                {
                    brInput.BaseStream.Seek(tmpPos, SeekOrigin.Begin);
                }

                if (string.IsNullOrEmpty(str))
                    continue;
            }

            return stringsDatabase;
        }

        /// <summary>
        /// Write a string database to a CSV file.
        /// </summary>
        /// <param name="originalInput">Original input file name (used for output naming).</param>
        /// <param name="stringsDatabase">List of strings to write.</param>
        public void WriteCsv(string originalInput, List<StringDatabase> stringsDatabase)
        {
            string fileName = Path.GetFileNameWithoutExtension(originalInput);
            string csvPath = $"{fileName}.csv";

            if (_fileSystem.FileExists(csvPath))
                _fileSystem.DeleteFile(csvPath);

            using var txtOutput = _fileSystem.CreateStreamWriter(csvPath, false, _encodingOptions.GetOutputEncoding());
            using var csvOutput = new CsvWriter(txtOutput, TextFileConfiguration.CreateJapaneseCsvConfig());

            csvOutput.WriteHeader<StringDatabase>();
            csvOutput.NextRecord();
            csvOutput.WriteRecords(stringsDatabase);
        }
    }

    /// <summary>
    /// Define how a string should be saved to the database.
    /// </summary>
    public class StringDatabase
    {
        /// <summary>
        /// String offset from the beginning of the file.
        /// </summary>
        public uint Offset { get; set; }

        /// <summary>
        /// CRC32 hash.
        /// </summary>
        public uint Hash { get; set; }

        /// <summary>
        /// Japanese version of the string.
        /// </summary>
        public string? JString { get; set; }

        /// <summary>
        /// English translation.
        /// </summary>
        public string? EString { get; set; }
    }
}
