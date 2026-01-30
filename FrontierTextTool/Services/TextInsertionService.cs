using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

using CsvHelper;
using CsvHelper.Configuration;

using LibReFrontier;
using LibReFrontier.Abstractions;

using ReFrontier;

namespace FrontierTextTool.Services
{
    /// <summary>
    /// Service for inserting translated strings back into binary game files.
    /// </summary>
    public class TextInsertionService
    {
        private readonly IFileSystem _fileSystem;
        private readonly ILogger _logger;

        /// <summary>
        /// Create a new TextInsertionService with default dependencies.
        /// </summary>
        public TextInsertionService()
            : this(new RealFileSystem(), new ConsoleLogger())
        {
        }

        /// <summary>
        /// Create a new TextInsertionService with injectable dependencies.
        /// </summary>
        public TextInsertionService(IFileSystem fileSystem, ILogger logger)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Load a CSV file to a StringDatabase array.
        /// </summary>
        /// <param name="inputCsv">Input CSV file path.</param>
        /// <returns>Array of StringDatabase records.</returns>
        public StringDatabase[] LoadCsvToStringDatabase(string inputCsv)
        {
            var stringDatabase = new List<StringDatabase>();
            var configuration = new CsvConfiguration(CultureInfo.CreateSpecificCulture("jp-JP"))
            {
                Delimiter = "\t",
                Mode = CsvMode.Escape
            };

            using var stream = _fileSystem.OpenRead(inputCsv);
            using var reader = new StreamReader(stream, Encoding.GetEncoding("shift-jis"));
            using var csv = new CsvReader(reader, configuration);

            csv.Read();
            csv.ReadHeader();
            while (csv.Read())
            {
                var record = new StringDatabase
                {
                    Offset = csv.GetField<uint>("Offset"),
                    Hash = csv.GetField<uint>("Hash"),
                    EString = (csv.GetField("EString") ?? string.Empty)
                        .Replace("<TAB>", "\t")
                        .Replace("<CLINE>", "\r\n")
                        .Replace("<NLINE>", "\n")
                };
                stringDatabase.Add(record);
            }

            return [.. stringDatabase];
        }

        /// <summary>
        /// Update string and string indices in fileBytes.
        /// </summary>
        /// <param name="stringDatabase">The new strings used for the update, contains offsets.</param>
        /// <param name="fileBytes">The binary file to do updates to.</param>
        /// <param name="verbose">Additional verbosity.</param>
        /// <param name="trueOffsets">Use real data offsets.</param>
        /// <returns>Updated data.</returns>
        public byte[] UpdateBinaryStrings(
            StringDatabase[] stringDatabase, byte[] fileBytes, bool verbose, bool trueOffsets)
        {
            // Get info for translation array and get all offsets that need to be remapped
            var eStringsOffsets = new List<uint>();
            var eStringLengths = new List<int>();

            foreach (var obj in stringDatabase)
            {
                if (!string.IsNullOrEmpty(obj.EString))
                {
                    eStringsOffsets.Add(obj.Offset);
                    eStringLengths.Add(GetNullterminatedStringLength(obj.EString));
                }
            }

            int eStringsLength = eStringLengths.Sum();
            int eStringsCount = eStringLengths.Count;

            // Create dictionary with offset replacements
            var offsetDict = new Dictionary<int, int>();
            for (int i = 0; i < eStringsCount; i++)
            {
                offsetDict.Add(
                    (int)eStringsOffsets[i],
                    fileBytes.Length + eStringLengths.Take(i).Sum()
                );
            }

            if (verbose)
                _logger.WriteLine($"Filling array of size {eStringsLength:X8}...");

            byte[] eStringsArray = new byte[eStringsLength];
            for (int i = 0, j = 0; i < stringDatabase.Length; i++)
            {
                if (!string.IsNullOrEmpty(stringDatabase[i].EString))
                {
                    if (verbose)
                        _logger.WriteLine($"String: '{stringDatabase[i].EString}', Length: {eStringLengths[j] - 1}");

                    byte[] eStringArray = Encoding.GetEncoding("shift-jis").GetBytes(stringDatabase[i].EString!);
                    Array.Copy(eStringArray, 0, eStringsArray, eStringLengths.Take(j).Sum(), eStringLengths[j] - 1);
                    j++;
                }
            }

            // Replace offsets in binary file
            if (trueOffsets)
            {
                for (int i = 0; i < offsetDict.Count; i++)
                {
                    var element = offsetDict.ElementAt(i);
                    byte[] newPointer = BitConverter.GetBytes(element.Value);
                    for (int w = 0; w < 4; w++)
                        fileBytes[element.Key + w] = newPointer[w];
                }
            }
            else
            {
                for (int p = 0; p < fileBytes.Length; p += 4)
                {
                    if (p + 4 > fileBytes.Length)
                        continue;
                    int cur = BitConverter.ToInt32(fileBytes, p);
                    if (offsetDict.ContainsKey(cur) && p > 10000)
                    {
                        offsetDict.TryGetValue(cur, out int replacement);
                        byte[] newPointer = BitConverter.GetBytes(replacement);
                        for (int w = 0; w < 4; w++)
                            fileBytes[p + w] = newPointer[w];
                    }
                }
            }

            // Combine arrays
            byte[] updatedBytes = new byte[fileBytes.Length + eStringsLength];
            Array.Copy(fileBytes, updatedBytes, fileBytes.Length);
            Array.Copy(eStringsArray, 0, updatedBytes, fileBytes.Length, eStringsArray.Length);

            return updatedBytes;
        }

        /// <summary>
        /// Insert translations and update pointers in a binary file.
        /// </summary>
        /// <param name="inputFile">File to insert the translation to.</param>
        /// <param name="inputCsv">CSV with the updated translations.</param>
        /// <param name="verbose">Additional verbosity.</param>
        /// <param name="trueOffsets">Use real string offsets.</param>
        public void InsertStrings(string inputFile, string inputCsv, bool verbose, bool trueOffsets)
        {
            _logger.WriteLine($"Processing {inputFile}...");

            var preprocessor = new FilePreprocessor();
            var (processedFile, cleanup) = preprocessor.AutoPreprocess(inputFile, createMetaFile: true);

            try
            {
                byte[] inputBytes = _fileSystem.ReadAllBytes(processedFile);

                // Read CSV
                var stringDatabase = LoadCsvToStringDatabase(inputCsv);

                // Update the array
                var updatedBytes = UpdateBinaryStrings(stringDatabase, inputBytes, verbose, trueOffsets);

                // Output file
                _fileSystem.CreateDirectory("output");
                string outputFile = $"output/{Path.GetFileName(inputFile)}";
                _fileSystem.WriteAllBytes(outputFile, updatedBytes);

                // Pack with JPK type 0 and encrypt file with ECD
                var compression = new Compression(CompressionType.RW, 15);
                var pack = new Pack();
                pack.JPKEncode(compression, outputFile, outputFile);
                byte[] buffer = _fileSystem.ReadAllBytes(outputFile);

                // Use the meta file from the original input file
                string metaFile = $"{inputFile}.meta";
                if (!_fileSystem.FileExists(metaFile))
                {
                    throw new FileNotFoundException(
                        $"META file {metaFile} does not exist. " +
                        "Make sure the original file is encrypted with ECD format, " +
                        "or that you previously decrypted it with ReFrontier using the --log option."
                    );
                }
                byte[] bufferMeta = _fileSystem.ReadAllBytes(metaFile);
                buffer = Crypto.EncodeEcd(buffer, bufferMeta);
                _logger.WriteLine($"Writing file to {outputFile}.");
                _fileSystem.WriteAllBytes(outputFile, buffer);

                // Update list
                string updEntry = FileOperations.GetUpdateEntry(outputFile);
                UpdateList(updEntry);
            }
            finally
            {
                cleanup();
            }
        }

        /// <summary>
        /// Update MHFUP_00.DAT with new entry.
        /// </summary>
        /// <param name="updEntry">Updated elements in specific format.</param>
        private void UpdateList(string updEntry)
        {
            string file = updEntry.Split(',')[3].Split('/')[1];
            string listPath = "src/MHFUP_00.DAT";

            if (!_fileSystem.FileExists(listPath))
                return;

            string[] lines = _fileSystem.ReadAllLines(listPath);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains(file))
                    lines[i] = updEntry.Replace("output", "dat");
            }

            using var writer = _fileSystem.CreateStreamWriter(listPath, false, Encoding.UTF8);
            foreach (var line in lines)
                writer.WriteLine(line);
        }

        /// <summary>
        /// Get byte length of string including null terminator.
        /// </summary>
        /// <param name="input">Input string to get length.</param>
        /// <returns>Length of string in SHIFT-JIS + 1 for null terminator.</returns>
        public static int GetNullterminatedStringLength(string input)
        {
            return Encoding.GetEncoding("shift-jis").GetBytes(input).Length + 1;
        }
    }
}
