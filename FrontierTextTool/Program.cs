using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

using CsvHelper;
using CsvHelper.Configuration;

using LibReFrontier;
using ReFrontier;

namespace FrontierTextTool
{
    /// <summary>
    /// Utility program for text data edition.
    /// </summary>
    internal class Program
    {
        /// <summary>
        /// Main CLI for text edition.
        /// </summary>
        /// <param name="args">Arguments passed</param>
        private static void Main(string[] args)
        {
            var parsedArgs = ArgumentsParser.ParseArguments(args);
            var keyArgs = parsedArgs.Keys;

            if (keyArgs.Contains("--help"))
            {
                var assembly = Assembly.GetExecutingAssembly();
                var fileVersionAttribute = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>().Version;
                ArgumentsParser.Print(
                    $"FrontierTextTool v{fileVersionAttribute} - extract and edit text data.\n" +
                    "=========================\n" +
                    "General commands\n" +
                    "================\n" +
                    "dump <file> <startIndex> <endIndex> [--trueOffsets] [--nullStrings]: dump data from file to CSV.\n" +
                    "fulldump <file> [--trueOffsets] [--nullStrings]: dump all data from file.\n" +
                    "insert <output file> <input CSV> [--verbose] [--trueOffsets]: add data from CSV to file.\n" +
                    "merge <old CSV> <new CSV>: merge two CSV files\n" +
                    "cleanTrados <file>: clean-up ill-encoded characters in file.\n" +
                    "insertCAT <file> <csvFile>: insert CAT file to CSV file.\n" +
                    "Options\n" +
                    "===============\n" +
                    "--trueOffsets: correct the value of string offsets. It is recommended to use it with --nullStrings.\n" +
                    "--nullStrings: check if strings are valid before outputing them.\n" +
                    "--verbose: more verbosity.\n" +
                    "--close: close the terminal after command.\n" +
                    "--help: display this message.",
                    false
                );
                return;
            }

            if (parsedArgs.Count < 2)
            {
                throw new ArgumentException($"Too few arguments: {parsedArgs.Count}. Need at least 2 arguments.");
            }

            bool verbose = keyArgs.Contains("--verbose") || keyArgs.Contains("-verbose");
            bool autoClose = keyArgs.Contains("--close") || keyArgs.Contains("-close");
            bool trueOffsets = keyArgs.Contains("--trueOffsets") || keyArgs.Contains("-trueoffsets");
            bool nullStrings = keyArgs.Contains("--nullStrings") || keyArgs.Contains("-nullstrings");

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);


            switch (args[0])
            {
                case "fulldump":
                    DumpAndHash(args[1], 0, 0, trueOffsets, nullStrings);
                    break;
                case "dump":
                    DumpAndHash(
                        args[1],
                        Convert.ToInt32(args[2]),
                        Convert.ToInt32(args[3]),
                        trueOffsets,
                        nullStrings
                    );
                    break;
                case "insert":
                    InsertStrings(args[1], args[2], verbose, trueOffsets);
                    break;
                case "merge":
                    Merge(args[1], args[2]);
                    break;
                case "cleanTrados":
                    CleanTrados(args[1]);
                    break;
                case "insertCAT":
                    InsertCatFile(args[1], args[2]);
                    break;
                default:
                    throw new ArgumentException($"{args[0]} is not a valid argument.");
            }

            if (!autoClose)
            {
                Console.WriteLine("Done");
                Console.Read();
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
            /// CRC32 hash
            /// </summary>
            public uint Hash { get; set; }
            /// <summary>
            /// Japanese version of the string
            /// </summary>
            public string JString { get; set; }
            /// <summary>
            /// English translation.
            /// </summary>
            public string EString { get; set; }
        }

        /// <summary>
        /// Update MHFUP_00.DAT.
        /// </summary>
        /// <param name="updEntry">Updated elements in specific format.</param>
        private static void UpdateList(string updEntry)
        {
            string file = updEntry.Split(',')[3].Split('/')[1];
            string[] lines = File.ReadAllLines("src/MHFUP_00.DAT");
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains(file))
                    lines[i] = updEntry.Replace("output", "dat");
            }
            File.WriteAllLines("src/MHFUP_00.DAT", lines);
        }

        /// <summary>
        /// Insert CAT file to csv
        /// </summary>
        /// <param name="catFile">CAT file to insert</param>
        /// <param name="csvFile">Destination file</param>
        private static void InsertCatFile(string catFile, string csvFile)
        {
            Console.WriteLine($"Processing {catFile}...");
            CleanTrados(catFile);
            string[] catStrings = File.ReadAllLines(catFile, Encoding.UTF8);

            var stringDb = new List<StringDatabase>();
            var configuration = new CsvConfiguration(CultureInfo.CreateSpecificCulture("jp-JP"))
            {
                Delimiter = "\t",
                MissingFieldFound = null,
                IgnoreQuotes = true,
            };
            using (var reader = new StreamReader(csvFile, Encoding.GetEncoding("shift-jis")))
            {
                using var csv = new CsvReader(reader, configuration);
                csv.Read();
                csv.ReadHeader();
                while (csv.Read())
                {
                    var record = new StringDatabase
                    {
                        Offset = csv.GetField<uint>("Offset"),
                        Hash = csv.GetField<uint>("Hash"),
                        EString = csv.GetField("EString"),
                        JString = csv.GetField("JString")
                    };
                    stringDb.Add(record);
                }
            }

            // Copy catStrings to new db
            for (int i = 0; i < stringDb.Count; i++)
            {
                Console.Write($"\rUpdating entry {i + 1}/{stringDb.Count}");
                if (stringDb[i].JString != catStrings[i])
                    stringDb[i].EString = catStrings[i];
                // Allow for deletions
                else if (stringDb[i].JString == catStrings[i] && stringDb[i].EString != "")
                    stringDb[i].EString = "";
            }
            Console.WriteLine();

            // Using this approach because csvHelper would always escape some strings
            // which might mess up in-game when copy-pasting were required
            string fileName = "csv/" + Path.GetFileName(csvFile);

            if (File.Exists(fileName))
                File.Delete(fileName);
            StreamWriter txtOutput = new(fileName, true, Encoding.GetEncoding("shift-jis"));
            txtOutput.WriteLine("Offset\tHash\tJString\tEString");
            foreach (var obj in stringDb)
                txtOutput.WriteLine($"{obj.Offset}\t{obj.Hash}\t{obj.JString}\t{obj.EString}");
            txtOutput.Close();

            if (!Directory.Exists("backup"))
                Directory.CreateDirectory("backup");
            File.Move(
                catFile,
                $"backup/{Path.GetFileNameWithoutExtension(catFile)}_{DateTime.Now:yyyyMMdd_HHmm}.txt"
            );
        }


        /// <summary>
        /// Clean pollution caused by Trados or other CAT
        /// </summary>
        /// <param name="file">Input file path.</param>
        private static void CleanTrados(string file)
        {
            string text = File.ReadAllText(file, Encoding.UTF8);
            text = text
            .Replace(": ~", ":~")
            .Replace("。 ", "。")
            .Replace("！ ", "！")
            .Replace("？ ", "？")
            .Replace("： ", "：")
            .Replace("． ", "．")
            .Replace("． ", "．")
            .Replace("」 ", "」")
            .Replace("「 ", "「")
            .Replace("） ", "）")
            .Replace("（ ", "（");
            File.WriteAllText(file, text, Encoding.UTF8);
            Console.WriteLine("Cleaned up");
        }

        /// <summary>
        /// Load a CSV file to a string database
        /// </summary>
        /// <param name="inputCsv">Input CSV file path.</param>
        /// <returns>StringDatabase object.</returns>
        private static StringDatabase[] LoadCsvToStringDatabase(string inputCsv)
        {
            var stringDatabase = new List<StringDatabase>();
            var configuration = new CsvConfiguration(CultureInfo.CreateSpecificCulture("jp-JP"))
            {
                Delimiter = "\t",
                MissingFieldFound = null,
                IgnoreQuotes = true,
            };
            using (var reader = new StreamReader(inputCsv, Encoding.GetEncoding("shift-jis")))
            {
                using var csv = new CsvReader(reader, configuration);
                csv.Read();
                csv.ReadHeader();
                while (csv.Read())
                {
                    var record = new StringDatabase
                    {
                        Offset = csv.GetField<uint>("Offset"),
                        Hash = csv.GetField<uint>("Hash"),
                        EString = csv.GetField("EString").
                        Replace("<TAB>", "\t"). // Replace tab
                        Replace("<CLINE>", "\r\n"). // Replace carriage return
                        Replace("<NLINE>", "\n") // Replace new line
                    };
                    stringDatabase.Add(record);
                }
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
        /// <returns>Updated data</returns>
        private static byte[] UpdateBinaryStrings(
            StringDatabase[] stringDatabase, byte[] fileBytes, bool verbose, bool trueOffsets
        )
        {
            // Get info for translation array and get all offsets that need to be remapped
            List<uint> eStringsOffsets = [];
            List<int> eStringLengths = [];
            foreach (var obj in stringDatabase)
            {
                if (obj.EString != "")
                {
                    eStringsOffsets.Add(obj.Offset);
                    eStringLengths.Add(GetNullterminatedStringLength(obj.EString));
                }
            }
            int eStringsLength = eStringLengths.Sum();
            int eStringsCount = eStringLengths.Count;

            // Create dictionary with offset replacements
            Dictionary<int, int> offsetDict = [];
            for (int i = 0; i < eStringsCount; i++)
                // Key: previous offset, new value: fileLength + sum of all new offsets
                offsetDict.Add(
                    (int)eStringsOffsets[i],
                    fileBytes.Length + eStringLengths.Take(i).Sum()
                );

            if (verbose)
                Console.WriteLine($"Filling array of size {eStringsLength:X8}...");
            byte[] eStringsArray = new byte[eStringsLength];
            for (int i = 0, j = 0; i < stringDatabase.Length; i++)
            {
                if (stringDatabase[i].EString != "")
                {
                    // Write string to string array
                    if (verbose)
                        Console.WriteLine($"String: '{stringDatabase[i].EString}', Length: {eStringLengths[j] - 1}");
                    byte[] eStringArray = Encoding.GetEncoding("shift-jis").GetBytes(stringDatabase[i].EString);
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
        /// Append translations and update pointers.
        /// 
        /// The output generated in output will be compressed and encoded.
        /// </summary>
        /// <param name="inputFile">File to insert the translation to, such as mhfdat.bin</param>
        /// <param name="inputCsv">CSV with the updated translations.</param>
        /// <param name="verbose">Additional verbosity.</param>
        /// <param name="trueOffsets">Use real string offsets.</param>
        private static void InsertStrings(string inputFile, string inputCsv, bool verbose, bool trueOffsets)
        {
            Console.WriteLine($"Processing {inputFile}...");
            byte[] inputBytes = File.ReadAllBytes(inputFile);

            // Read csv
            var stringDatabase = LoadCsvToStringDatabase(inputCsv);

            // Update the array
            var updatedBytes = UpdateBinaryStrings(stringDatabase, inputBytes, verbose, trueOffsets);

            // Output file
            Directory.CreateDirectory("output");
            string outputFile = $"output/{Path.GetFileName(inputFile)}";
            File.WriteAllBytes(outputFile, updatedBytes);

            // Pack with jpk type 0 and encrypt file with ecd
            Compression compression = new()
            {
                type = CompressionType.RW,
                level = 15
            };
            Pack.JPKEncode(compression, outputFile, outputFile);
            byte[] buffer = File.ReadAllBytes(outputFile);
            if (File.Exists($"{outputFile}.meta"))
            {
                throw new FileNotFoundException(
                    $"META file {outputFile}.meta does not exist, did you use '--log' during decrypting to generate it?"
                );
            }
            byte[] bufferMeta = File.ReadAllBytes($"{outputFile}.meta");
            buffer = Crypto.EncodeEcd(buffer, bufferMeta);
            Console.WriteLine($"Writing file to {outputFile}.");
            File.WriteAllBytes(outputFile, buffer);

            // Update list
            string updEntry = FileOperations.GetUpdateEntry(outputFile);
            UpdateList(updEntry);
        }

        /// <summary>
        /// Dump data to a file.
        /// </summary>
        /// <param name="input">File to read</param>
        /// <param name="startOffset">Initial offset to read file from.</param>
        /// <param name="endOffset">End offset where stopping file reading.</param>
        /// <param name="trueOffsets">Use real string offsets.</param>
        /// <param name="checkNullPredecessor">When using <cref>trueOffsets</cref>, check if the previous strings starts by a null pointer.</param>
        private static void DumpAndHash(
            string input, int startOffset, int endOffset, bool trueOffsets, bool checkNullPredecessor
        )
        {
            byte[] buffer = File.ReadAllBytes(input);
            MemoryStream msInput = new(buffer);
            BinaryReader brInput = new(msInput);

            if (endOffset == 0)
                endOffset = (int)brInput.BaseStream.Length;

            Console.WriteLine(
                $"Strings at: 0x{startOffset:X8} - 0x{endOffset:X8}. Size 0x{endOffset - startOffset:X8}"
            );

            brInput.BaseStream.Seek(startOffset, SeekOrigin.Begin);
            List<StringDatabase> stringsDatabase = [];
            while (brInput.BaseStream.Position + 4 <= endOffset)
            {
                long offset = brInput.BaseStream.Position;
                long tmpPos = brInput.BaseStream.Position;

                // Follow string pointer
                if (trueOffsets)
                {
                    // String pointer, the position of a string
                    uint strPos = brInput.ReadUInt32();
                    if (strPos < 10 || strPos > brInput.BaseStream.Length)
                        continue;
                    tmpPos = brInput.BaseStream.Position;
                    // Check if previous string is valid, otherwise continue
                    if (checkNullPredecessor)
                    {
                        // Go to string position, check if previous string is null terminated
                        brInput.BaseStream.Seek(strPos - 2, SeekOrigin.Begin);
                        if (brInput.ReadByte() == 0 || brInput.ReadByte() != 0)
                        {
                            // Not valid, go back to previous position
                            brInput.BaseStream.Seek(tmpPos, SeekOrigin.Begin);
                            continue;
                        }
                    }
                    // Go to string
                    brInput.BaseStream.Seek(strPos, SeekOrigin.Begin);
                }

                string str = FileOperations.ReadNullterminatedString(brInput, Encoding.GetEncoding("shift-jis")).
                    Replace("\t", "<TAB>"). // Replace tab
                    Replace("\r\n", "<CLINE>"). // Replace carriage return
                    Replace("\n", "<NLINE>"); // Replace new line

                stringsDatabase.Add(
                    new StringDatabase()
                    {
                        Offset = (uint)offset,
                        Hash = Crypto.GetCrc32(Encoding.GetEncoding("shift-jis").GetBytes(str)),
                        JString = str
                    }
                );

                if (trueOffsets)
                {
                    // Go back to previous position
                    brInput.BaseStream.Seek(tmpPos, SeekOrigin.Begin);
                }
                if (str == "")
                    continue;
            }

            // Write to file
            string fileName = Path.GetFileNameWithoutExtension(input);
            if (File.Exists($"{fileName}.csv"))
                File.Delete($"{fileName}.csv");
            using StreamWriter txtOutput = new($"{fileName}.csv", true, Encoding.GetEncoding("shift-jis"));
            var configuration = new CsvConfiguration(CultureInfo.CreateSpecificCulture("jp-JP"))
            {
                Delimiter = "\t",
            };
            using var csvOutput = new CsvWriter(txtOutput, configuration);

            csvOutput.WriteHeader<StringDatabase>();
            csvOutput.NextRecord();

            csvOutput.WriteRecords(stringsDatabase);
        }

        /// <summary>
        /// Merge old and updated CSVs
        /// </summary>
        /// <param name="oldCsv">CSV to merge to</param>
        /// <param name="newCsv">New CSV with updated data</param>
        private static void Merge(string oldCsv, string newCsv)
        {
            var csvConf = new CsvConfiguration(CultureInfo.CreateSpecificCulture("jp-JP"))
            {
                Delimiter = "\t",
                MissingFieldFound = null,
                IgnoreQuotes = true,
            };
            // Read csv
            var stringDbOld = new List<StringDatabase>();
            using (var reader = new StreamReader(oldCsv, Encoding.GetEncoding("shift-jis")))
            {
                using var csv = new CsvReader(reader, csvConf);
                csv.Read();
                csv.ReadHeader();
                while (csv.Read())
                {
                    var record = new StringDatabase
                    {
                        Hash = csv.GetField<uint>("Hash"),
                        EString = csv.GetField("EString")
                    };
                    stringDbOld.Add(record);
                }
            }

            var stringDbNew = new List<StringDatabase>();
            using (var reader = new StreamReader(newCsv, Encoding.GetEncoding("shift-jis")))
            {
                using var csv = new CsvReader(reader, csvConf);
                csv.Read();
                csv.ReadHeader();
                while (csv.Read())
                {
                    var record = new StringDatabase
                    {
                        Offset = csv.GetField<uint>("Offset"),
                        Hash = csv.GetField<uint>("Hash"),
                        EString = csv.GetField("EString"),
                        JString = csv.GetField("JString")
                    };
                    stringDbNew.Add(record);
                }
            }

            // Copy eStrings to new db
            for (int i = 0; i < stringDbOld.Count; i++)
            {
                Console.Write($"\rUpdating entry {i + 1}/{stringDbOld.Count}");
                if (stringDbOld[i].EString != "")
                {
                    var matchedNewObjs = stringDbNew.Where(x => x.Hash.Equals(stringDbOld[i].Hash));
                    if (matchedNewObjs.Any())
                    {
                        foreach (var obj in matchedNewObjs)
                            obj.EString = stringDbOld[i].EString;
                    }
                }
            }
            Console.WriteLine();

            string fileName = "csv/" + Path.GetFileName(oldCsv);
            if (File.Exists(fileName))
                File.Delete(fileName);
            StreamWriter txtOutput = new(fileName, true, Encoding.GetEncoding("shift-jis"));
            // Note from v1.1.0: CsvHelpers may escape too many characters
            using (var csvOutput = new CsvWriter(txtOutput, csvConf))
            {
                csvOutput.WriteHeader<StringDatabase>();
                csvOutput.WriteRecords(stringDbNew);
            }
            txtOutput.Close();
            File.Delete(newCsv);
        }

        /// <summary>
        /// Get byte length of string (avoids issues with special spacing characters)
        /// </summary>
        /// <param name="input">Input string to get length</param>
        /// <returns>Length of string in SHIFT-JIS</returns>
        public static int GetNullterminatedStringLength(string input)
        {
            return Encoding.GetEncoding("shift-jis").GetBytes(input).Length + 1;
        }
    }
}
