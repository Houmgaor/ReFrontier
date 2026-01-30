using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

using CsvHelper;
using CsvHelper.Configuration;

using LibReFrontier.Abstractions;

namespace FrontierTextTool.Services
{
    /// <summary>
    /// Service for merging CSV files and handling CAT translation files.
    /// </summary>
    public class CsvMergeService
    {
        private readonly IFileSystem _fileSystem;
        private readonly ILogger _logger;

        /// <summary>
        /// Create a new CsvMergeService with default dependencies.
        /// </summary>
        public CsvMergeService()
            : this(new RealFileSystem(), new ConsoleLogger())
        {
        }

        /// <summary>
        /// Create a new CsvMergeService with injectable dependencies.
        /// </summary>
        public CsvMergeService(IFileSystem fileSystem, ILogger logger)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Merge old and updated CSVs by matching CRC32 hashes.
        /// </summary>
        /// <param name="oldCsv">CSV to merge to (contains translations).</param>
        /// <param name="newCsv">New CSV with updated data structure.</param>
        public void Merge(string oldCsv, string newCsv)
        {
            var csvConf = new CsvConfiguration(CultureInfo.CreateSpecificCulture("jp-JP"))
            {
                Delimiter = "\t",
                Mode = CsvMode.Escape
            };

            // Read old CSV
            var stringDbOld = new List<StringDatabase>();
            using (var stream = _fileSystem.OpenRead(oldCsv))
            using (var reader = new StreamReader(stream, Encoding.GetEncoding("shift-jis")))
            using (var csv = new CsvReader(reader, csvConf))
            {
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

            // Read new CSV
            var stringDbNew = new List<StringDatabase>();
            using (var stream = _fileSystem.OpenRead(newCsv))
            using (var reader = new StreamReader(stream, Encoding.GetEncoding("shift-jis")))
            using (var csv = new CsvReader(reader, csvConf))
            {
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

            // Copy EStrings to new db by matching hash
            for (int i = 0; i < stringDbOld.Count; i++)
            {
                _logger.Write($"\rUpdating entry {i + 1}/{stringDbOld.Count}");
                if (stringDbOld[i].EString != "")
                {
                    var matchedNewObjs = stringDbNew.Where(x => x.Hash.Equals(stringDbOld[i].Hash));
                    foreach (var obj in matchedNewObjs)
                        obj.EString = stringDbOld[i].EString;
                }
            }
            _logger.WriteLine("");

            // Write merged output
            _fileSystem.CreateDirectory("csv");
            string fileName = "csv/" + Path.GetFileName(oldCsv);

            if (_fileSystem.FileExists(fileName))
                _fileSystem.DeleteFile(fileName);

            using (var txtOutput = _fileSystem.CreateStreamWriter(fileName, false, Encoding.GetEncoding("shift-jis")))
            using (var csvOutput = new CsvWriter(txtOutput, csvConf))
            {
                csvOutput.WriteHeader<StringDatabase>();
                csvOutput.WriteRecords(stringDbNew);
            }

            _fileSystem.DeleteFile(newCsv);
        }

        /// <summary>
        /// Insert CAT translation file into a CSV file.
        /// </summary>
        /// <param name="catFile">CAT file with translations (line-by-line).</param>
        /// <param name="csvFile">Target CSV file.</param>
        public void InsertCatFile(string catFile, string csvFile)
        {
            _logger.WriteLine($"Processing {catFile}...");

            // Clean CAT file
            CleanTrados(catFile);
            string[] catStrings = _fileSystem.ReadAllLines(catFile);

            // Read existing CSV
            var stringDb = new List<StringDatabase>();
            var configuration = new CsvConfiguration(CultureInfo.CreateSpecificCulture("jp-JP"))
            {
                Delimiter = "\t",
                Mode = CsvMode.Escape
            };

            using (var stream = _fileSystem.OpenRead(csvFile))
            using (var reader = new StreamReader(stream, Encoding.GetEncoding("shift-jis")))
            using (var csv = new CsvReader(reader, configuration))
            {
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

            // Copy CAT strings to new db
            for (int i = 0; i < stringDb.Count; i++)
            {
                _logger.Write($"\rUpdating entry {i + 1}/{stringDb.Count}");
                if (stringDb[i].JString != catStrings[i])
                    stringDb[i].EString = catStrings[i];
                else if (stringDb[i].JString == catStrings[i] && stringDb[i].EString != "")
                    stringDb[i].EString = "";
            }
            _logger.WriteLine("");

            // Write output
            _fileSystem.CreateDirectory("csv");
            string fileName = "csv/" + Path.GetFileName(csvFile);

            if (_fileSystem.FileExists(fileName))
                _fileSystem.DeleteFile(fileName);

            using var txtOutput = _fileSystem.CreateStreamWriter(fileName, false, Encoding.GetEncoding("shift-jis"));
            txtOutput.WriteLine("Offset\tHash\tJString\tEString");
            foreach (var obj in stringDb)
                txtOutput.WriteLine($"{obj.Offset}\t{obj.Hash}\t{obj.JString}\t{obj.EString}");

            // Backup CAT file
            _fileSystem.CreateDirectory("backup");
            string backupPath = $"backup/{Path.GetFileNameWithoutExtension(catFile)}_{DateTime.Now:yyyyMMdd_HHmm}.txt";
            _fileSystem.Copy(catFile, backupPath);
            _fileSystem.DeleteFile(catFile);
        }

        /// <summary>
        /// Clean pollution caused by Trados or other CAT software.
        /// </summary>
        /// <param name="file">Input file path.</param>
        public void CleanTrados(string file)
        {
            using var stream = _fileSystem.OpenRead(file);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            string text = reader.ReadToEnd();

            text = CleanTradosText(text);

            using var writer = _fileSystem.CreateStreamWriter(file, false, Encoding.UTF8);
            writer.Write(text);

            _logger.WriteLine("Cleaned up");
        }

        /// <summary>
        /// Clean pollution caused by Trados or other CAT from text.
        /// Removes extra spaces after Japanese punctuation.
        /// </summary>
        /// <param name="text">Input text to clean.</param>
        /// <returns>Cleaned text.</returns>
        public static string CleanTradosText(string text)
        {
            return text
                .Replace(": ~", ":~")
                .Replace("。 ", "。")
                .Replace("！ ", "！")
                .Replace("？ ", "？")
                .Replace("： ", "：")
                .Replace("． ", "．")
                .Replace("」 ", "」")
                .Replace("「 ", "「")
                .Replace("） ", "）")
                .Replace("（ ", "（");
        }
    }
}
