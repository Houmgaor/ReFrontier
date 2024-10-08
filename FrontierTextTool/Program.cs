using CsvHelper;
using ReFrontier;
using LibReFrontier;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace FrontierTextTool
{
    class Program
    {
        static bool verbose = false;
        static bool autoClose = false;

        //[STAThread]
        /// <summary>
        /// Main CLI for text edition.
        /// </summary>
        /// <param name="args">Arguments passed</param>
        static void Main(string[] args)
        {
            if (args.Length < 2) {
                Console.WriteLine("Too few arguments.");
                return;
            }

            if (args.Any("-verbose".Contains)) verbose = true;
            if (args.Any("-close)".Contains)) autoClose = true;

            switch (args[0]) {
                case "dump":
                    DumpAndHash(args[1], Convert.ToInt32(args[2]), Convert.ToInt32(args[3]));
                    break;
                case "insert":
                    InsertStrings(args[1], args[2]);
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
                break;
            }

            if (!autoClose) {
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
        static void UpdateList(string updEntry)
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
        static void InsertCatFile(string catFile, string csvFile)
        {
            Console.WriteLine($"Processing {catFile}...");
            CleanTrados(catFile);
            string[] catStrings = File.ReadAllLines(catFile, Encoding.UTF8);

            var stringDb = new List<StringDatabase>();
            using (var reader = new StreamReader(csvFile, Encoding.GetEncoding("shift-jis")))
            {
                using var csv = new CsvReader(reader);
                csv.Configuration.Delimiter = "\t";
                csv.Configuration.IgnoreQuotes = true;
                csv.Configuration.MissingFieldFound = null;
                csv.Read();
                csv.ReadHeader();
                while (csv.Read())
                {
                    var record = new StringDatabase
                    {
                        Offset = csv.GetField<uint>("Offset"),
                        Hash = csv.GetField<uint>("Hash"),
                        EString = csv.GetField("eString"),
                        JString = csv.GetField("jString")
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

            // Using this approach because csvHelper would always escape some strings which might mess up in-game when copy-pasting where required
            string fileName = "csv/" + Path.GetFileName(csvFile);
            
            if (File.Exists(fileName)) File.Delete(fileName);
            StreamWriter txtOutput = new(fileName, true, Encoding.GetEncoding("shift-jis"));
            txtOutput.WriteLine("Offset\tHash\tjString\teString");
            foreach (var obj in stringDb)
                txtOutput.WriteLine($"{obj.Offset}\t{obj.Hash}\t{obj.JString}\t{obj.EString}");
            txtOutput.Close();

            if (!Directory.Exists("backup")) Directory.CreateDirectory("backup");
            File.Move(
                catFile, 
                $"backup/{Path.GetFileNameWithoutExtension(catFile)}_{DateTime.Now:yyyyMMdd_HHmm}.txt"
            );
        }


        /// <summary>
        /// Clean pollution caused by Trados or other CAT
        /// </summary>
        /// <param name="file">Input file path.</param>
        static void CleanTrados(string file)
        {
            string text = File.ReadAllText(file, Encoding.UTF8);
            text = text.Replace(": ~", ":~");
            text = text.Replace("。 ", "。");
            text = text.Replace("！ ", "！");
            text = text.Replace("？ ", "？");
            text = text.Replace("： ", "：");
            text = text.Replace("． ", "．");
            text = text.Replace("． ", "．");
            text = text.Replace("」 ", "」");
            text = text.Replace("「 ", "「");
            text = text.Replace("） ", "）");
            text = text.Replace("（ ", "（");
            File.WriteAllText(file, text, Encoding.UTF8);
            Console.WriteLine("Cleaned up");
        }

        /// <summary>
        /// Load a CSV file to a string database
        /// </summary>
        /// <param name="inputCsv">Input CSV file path.</param>
        /// <returns>StringDatabase object.</returns>
        static StringDatabase[] LoadCsvToStringDatabase(string inputCsv)
        {
            var stringDatabase = new List<StringDatabase>();
            using (var reader = new StreamReader(inputCsv, Encoding.GetEncoding("shift-jis")))
            {
                using var csv = new CsvReader(reader);
                csv.Configuration.Delimiter = "\t";
                csv.Configuration.IgnoreQuotes = true;
                csv.Configuration.MissingFieldFound = null;
                csv.Read();
                csv.ReadHeader();
                while (csv.Read())
                {
                    var record = new StringDatabase
                    {
                        Offset = csv.GetField<uint>("Offset"),
                        Hash = csv.GetField<uint>("Hash"),
                        EString = csv.GetField("eString").
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
        /// <returns></returns>
        static byte[] UpdateBinaryStrings(StringDatabase[] stringDatabase, byte[] fileBytes)
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
        /// <param name="inputCsv">CSV with the updated transaltions.</param>
        static void InsertStrings(string inputFile, string inputCsv)
        {
            Console.WriteLine($"Processing {inputFile}...");
            byte[] inputBytes = File.ReadAllBytes(inputFile);

            // Read csv
            var stringDatabase = LoadCsvToStringDatabase(inputCsv);

            // Update the array
            var updatedBytes = UpdateBinaryStrings(stringDatabase, inputBytes);

            // Output file
            Directory.CreateDirectory("output");
            string outputFile = $"output/{Path.GetFileName(inputFile)}";
            File.WriteAllBytes(outputFile, updatedBytes);

            // Pack with jpk type 0 and encrypt file with ecd
            Pack.JPKEncode(0, outputFile, outputFile, 15);
            byte[] buffer = File.ReadAllBytes(outputFile);
            byte[] bufferMeta = File.ReadAllBytes($"{outputFile}.meta");
            buffer = Crypto.EncEcd(buffer, bufferMeta);
            File.WriteAllBytes(outputFile, buffer);

            // Update list
            string updEntry = Helpers.GetUpdateEntry(outputFile);
            UpdateList(updEntry);
        }

        /// <summary>
        /// Dump data to a file.
        /// 
        /// dump mhfpac.bin 4416 1278872
        /// dump mhfdat.bin 3072 3328538
        /// </summary>
        /// <param name="input">File to read</param>
        /// <param name="startOffset">Initial offset to read file from.</param>
        /// <param name="endOffset">End offset where stopping file reading</param>
        static void DumpAndHash(string input, int startOffset, int endOffset)
        {
            byte[] buffer = File.ReadAllBytes(input);
            MemoryStream msInput = new(buffer);
            BinaryReader brInput = new(msInput);

            Console.WriteLine(
                $"Strings at: 0x{startOffset:X8} - 0x{endOffset:X8}. Size 0x{endOffset - startOffset:X8}"
            );

            string fileName = Path.GetFileNameWithoutExtension(input);
            if (File.Exists($"{fileName}.csv")) File.Delete($"{fileName}.csv");
            StreamWriter txtOutput = new($"{fileName}.csv", true, Encoding.GetEncoding("shift-jis"));
            txtOutput.WriteLine("Offset\tHash\tjString\teString");

            brInput.BaseStream.Seek(startOffset, SeekOrigin.Begin);
            while (brInput.BaseStream.Position < endOffset)
            {
                long off = brInput.BaseStream.Position;
                string str = Helpers.ReadNullterminatedString(
                    brInput, 
                    Encoding.GetEncoding("shift-jis")
                ).
                Replace("\t", "<TAB>"). // Replace tab
                Replace("\r\n", "<CLINE>"). // Replace carriage return
                Replace("\n", "<NLINE>"); // Replace new line
                txtOutput.WriteLine(
                    $"{off}\t{Helpers.GetCrc32(Encoding.GetEncoding("shift-jis").GetBytes(str))}\t{str}\t"
                );
            }
            txtOutput.Close();
        }

        /// <summary>
        /// Merge old and updated CSVs
        /// </summary>
        /// <param name="oldCsv">CSV to merge to</param>
        /// <param name="newCsv">New CSV with updated data</param>
        static void Merge(string oldCsv, string newCsv)
        {
            // Read csv
            var stringDbOld = new List<StringDatabase>();
            using (var reader = new StreamReader(oldCsv, Encoding.GetEncoding("shift-jis")))
            {
                using var csv = new CsvReader(reader);
                csv.Configuration.Delimiter = "\t";
                csv.Configuration.IgnoreQuotes = true;
                csv.Configuration.MissingFieldFound = null;
                csv.Read();
                csv.ReadHeader();
                while (csv.Read())
                {
                    var record = new StringDatabase
                    {
                        Hash = csv.GetField<uint>("Hash"),
                        EString = csv.GetField("eString")
                    };
                    stringDbOld.Add(record);
                }
            }

            var stringDbNew = new List<StringDatabase>();
            using (var reader = new StreamReader(newCsv, Encoding.GetEncoding("shift-jis")))
            {
                using var csv = new CsvReader(reader);
                csv.Configuration.Delimiter = "\t";
                csv.Configuration.IgnoreQuotes = true;
                csv.Configuration.MissingFieldFound = null;
                csv.Read();
                csv.ReadHeader();
                while (csv.Read())
                {
                    var record = new StringDatabase
                    {
                        Offset = csv.GetField<uint>("Offset"),
                        Hash = csv.GetField<uint>("Hash"),
                        EString = csv.GetField("eString"),
                        JString = csv.GetField("jString")
                    };
                    stringDbNew.Add(record);
                }
            }

            // Copy eStrings to new db
            for (int i = 0; i < stringDbOld.Count; i++)
            {
                Console.Write($"\rUpdating entry {i+1}/{stringDbOld.Count}");
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

            // Using this approach because csvHelper would always escape
            // some strings which might mess up in-game when copy-pasting where required
            string fileName = "csv/" + Path.GetFileName(oldCsv);
            if (File.Exists(fileName)) File.Delete(fileName);
            StreamWriter txtOutput = new(fileName, true, Encoding.GetEncoding("shift-jis"));
            txtOutput.WriteLine("Offset\tHash\tjString\teString");
            foreach (var obj in stringDbNew)
                txtOutput.WriteLine($"{obj.Offset}\t{obj.Hash}\t{obj.JString}\t{obj.EString}");
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