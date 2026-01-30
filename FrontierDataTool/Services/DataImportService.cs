using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

using CsvHelper;
using CsvHelper.Configuration;

using FrontierDataTool.Structs;
using LibReFrontier;
using LibReFrontier.Abstractions;
using ReFrontier;

namespace FrontierDataTool.Services
{
    /// <summary>
    /// Service for importing and modifying game data.
    /// </summary>
    public class DataImportService
    {
        private readonly IFileSystem _fileSystem;
        private readonly ILogger _logger;
        private readonly BinaryReaderService _binaryReader;

        // Offset pointers for mhfpac.bin
        private static readonly int _soStringSkillPt = 0xA20;
        private static readonly int _eoStringSkillPt = 0xA1C;

        private static readonly string[] ArmorClasses = ["頭", "胴", "腕", "腰", "脚"];

        /// <summary>
        /// Create a new DataImportService with default dependencies.
        /// </summary>
        public DataImportService()
            : this(new RealFileSystem(), new ConsoleLogger())
        {
        }

        /// <summary>
        /// Create a new DataImportService with injectable dependencies.
        /// </summary>
        public DataImportService(IFileSystem fileSystem, ILogger logger)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _binaryReader = new BinaryReaderService();
        }

        /// <summary>
        /// Import armor data from CSV back into mhfdat.bin.
        /// </summary>
        /// <param name="mhfdat">Path to mhfdat.bin.</param>
        /// <param name="csvPath">Path to Armor.csv.</param>
        /// <param name="mhfpac">Path to mhfpac.bin (for skill name lookup).</param>
        public void ImportArmorData(string mhfdat, string csvPath, string mhfpac)
        {
            var preprocessor = new FilePreprocessor();

            var (processedMhfdat, cleanupMhfdat) = preprocessor.AutoPreprocess(mhfdat, createMetaFile: true);
            var (processedMhfpac, cleanupMhfpac) = preprocessor.AutoPreprocess(mhfpac, createMetaFile: true);

            try
            {
                ImportArmorDataInternal(processedMhfdat, csvPath, processedMhfpac);
            }
            finally
            {
                cleanupMhfdat();
                cleanupMhfpac();
            }
        }

        /// <summary>
        /// Internal implementation of ImportArmorData that works on preprocessed files.
        /// </summary>
        public void ImportArmorDataInternal(string mhfdat, string csvPath, string mhfpac)
        {
            // Build skill name to ID lookup from mhfpac
            var skillLookup = BuildSkillLookup(mhfpac);
            _logger.WriteLine($"Loaded {skillLookup.Count} skill names for lookup.");

            // Read armor entries from CSV
            var armorEntries = LoadArmorCsv(csvPath);
            _logger.WriteLine($"Read {armorEntries.Count} armor entries from CSV.");

            // Load mhfdat.bin
            byte[] mhfdatData = _fileSystem.ReadAllBytes(mhfdat);
            using var ms = new MemoryStream(mhfdatData);
            using var br = new BinaryReader(ms);
            using var bw = new BinaryWriter(ms);

            // Process each armor class
            for (int i = 0; i < 5; i++)
            {
                br.BaseStream.Seek(DataExtractionService.DataPointersArmor[i].Key, SeekOrigin.Begin);
                int sOffset = br.ReadInt32();
                br.BaseStream.Seek(DataExtractionService.DataPointersArmor[i].Value, SeekOrigin.Begin);
                int eOffset = br.ReadInt32();

                int entryCount = (eOffset - sOffset) / BinaryReaderService.ARMOR_ENTRY_SIZE;

                var classEntries = armorEntries.Where(e => e.EquipClass == ArmorClasses[i]).ToList();

                if (classEntries.Count != entryCount)
                {
                    _logger.Error($"Warning: CSV has {classEntries.Count} entries for {ArmorClasses[i]}, but mhfdat expects {entryCount}. Skipping this class.");
                    continue;
                }

                _logger.WriteLine($"Writing {entryCount} {ArmorClasses[i]} entries starting at 0x{sOffset:X8}");

                for (int j = 0; j < entryCount; j++)
                {
                    int entryOffset = sOffset + (j * BinaryReaderService.ARMOR_ENTRY_SIZE);
                    bw.BaseStream.Seek(entryOffset, SeekOrigin.Begin);
                    _binaryReader.WriteArmorEntry(bw, classEntries[j], skillLookup);
                }
            }

            _fileSystem.CreateDirectory("output");
            string outputPath = Path.Combine("output", "mhfdat.bin");
            _fileSystem.WriteAllBytes(outputPath, mhfdatData);
            _logger.WriteLine($"Wrote modified data to {outputPath}");
        }

        /// <summary>
        /// Load armor entries from a CSV file.
        /// </summary>
        public List<ArmorDataEntry> LoadArmorCsv(string csvPath)
        {
            using var textReader = new StreamReader(
                _fileSystem.OpenRead(csvPath),
                Encoding.GetEncoding("shift-jis")
            );
            var configuration = new CsvConfiguration(CultureInfo.CreateSpecificCulture("jp-JP"))
            {
                Delimiter = "\t",
            };
            using var csvReader = new CsvReader(textReader, configuration);
            return csvReader.GetRecords<ArmorDataEntry>().ToList();
        }

        /// <summary>
        /// Build a dictionary mapping skill names to their IDs.
        /// </summary>
        public Dictionary<string, byte> BuildSkillLookup(string mhfpac)
        {
            var skillLookup = new Dictionary<string, byte>();

            using var ms = new MemoryStream(_fileSystem.ReadAllBytes(mhfpac));
            using var br = new BinaryReader(ms);

            br.BaseStream.Seek(_soStringSkillPt, SeekOrigin.Begin);
            int sOffset = br.ReadInt32();
            br.BaseStream.Seek(_eoStringSkillPt, SeekOrigin.Begin);
            int eOffset = br.ReadInt32();

            br.BaseStream.Seek(sOffset, SeekOrigin.Begin);
            byte id = 0;
            while (br.BaseStream.Position < eOffset)
            {
                string name = _binaryReader.StringFromPointer(br);
                if (!skillLookup.ContainsKey(name))
                {
                    skillLookup[name] = id;
                }
                id++;
            }

            return skillLookup;
        }

        /// <summary>
        /// Add all-items shop to file, change item prices, change armor prices.
        /// </summary>
        /// <param name="file">Input file path, usually mhfdat.bin.</param>
        public void ModShop(string file)
        {
            var preprocessor = new FilePreprocessor();

            var (processedFile, cleanup) = preprocessor.AutoPreprocess(file, createMetaFile: true);

            try
            {
                ModShopInternal(processedFile);
            }
            finally
            {
                cleanup();
            }
        }

        /// <summary>
        /// Internal implementation of ModShop that works on preprocessed files.
        /// </summary>
        public void ModShopInternal(string file)
        {
            int count;

            using (var msInput = new MemoryStream(_fileSystem.ReadAllBytes(file)))
            using (var brInput = new BinaryReader(msInput))
            using (var outputStream = _fileSystem.OpenWrite(file))
            using (var brOutput = new BinaryWriter(outputStream))
            {
                // Patch item prices
                brInput.BaseStream.Seek(0xFC, SeekOrigin.Begin);
                int sOffset = brInput.ReadInt32();
                brInput.BaseStream.Seek(0xA70, SeekOrigin.Begin);
                int eOffset = brInput.ReadInt32();

                count = (eOffset - sOffset) / BinaryReaderService.ITEM_ENTRY_SIZE;
                _logger.WriteLine($"Patching prices for {count} items starting at 0x{sOffset:X8}");

                for (int i = 0; i < count; i++)
                {
                    brOutput.BaseStream.Seek(sOffset + (i * BinaryReaderService.ITEM_ENTRY_SIZE) + 12, SeekOrigin.Begin);
                    brInput.BaseStream.Seek(sOffset + (i * BinaryReaderService.ITEM_ENTRY_SIZE) + 12, SeekOrigin.Begin);
                    int buyPrice = brInput.ReadInt32() / 50;
                    brOutput.Write(buyPrice);

                    brOutput.BaseStream.Seek(sOffset + (i * BinaryReaderService.ITEM_ENTRY_SIZE) + 16, SeekOrigin.Begin);
                    brInput.BaseStream.Seek(sOffset + (i * BinaryReaderService.ITEM_ENTRY_SIZE) + 16, SeekOrigin.Begin);
                    int sellPrice = brInput.ReadInt32() * 5;
                    brOutput.Write(sellPrice);
                }

                // Patch equip prices
                for (int i = 0; i < 5; i++)
                {
                    brInput.BaseStream.Seek(DataExtractionService.DataPointersArmor[i].Key, SeekOrigin.Begin);
                    sOffset = brInput.ReadInt32();
                    brInput.BaseStream.Seek(DataExtractionService.DataPointersArmor[i].Value, SeekOrigin.Begin);
                    eOffset = brInput.ReadInt32();

                    count = (eOffset - sOffset) / BinaryReaderService.ARMOR_ENTRY_SIZE;
                    _logger.WriteLine($"Patching prices for {count} armor pieces starting at 0x{sOffset:X8}");

                    for (int j = 0; j < count; j++)
                    {
                        brOutput.BaseStream.Seek(sOffset + (j * BinaryReaderService.ARMOR_ENTRY_SIZE) + 12, SeekOrigin.Begin);
                        brOutput.Write(50);
                    }
                }
            }

            // Generate shop array
            count = 16700;
            byte[] shopArray = new byte[(count * BinaryReaderService.SHOP_ENTRY_SIZE) + 5 * 32];

            for (int i = 0; i < count; i++)
            {
                byte[] id = BitConverter.GetBytes((short)(i + 1));
                byte[] item = new byte[8];
                Array.Copy(id, item, 2);
                Array.Copy(item, 0, shopArray, i * BinaryReaderService.SHOP_ENTRY_SIZE, 8);
            }

            // Append modshop data to file
            byte[] inputArray = _fileSystem.ReadAllBytes(file);
            byte[] outputArray = new byte[inputArray.Length + shopArray.Length];
            Array.Copy(inputArray, outputArray, inputArray.Length);
            Array.Copy(shopArray, 0, outputArray, inputArray.Length, shopArray.Length);

            // Find and modify item shop data pointer
            byte[] needle = [0x0F, 01, 01, 00, 00, 00, 00, 00, 03, 01, 01, 00, 00, 00, 00, 00];
            int offsetData = ByteOperations.GetOffsetOfArray(outputArray, needle);

            if (offsetData != -1)
            {
                _logger.WriteLine($"Found shop inventory to modify at 0x{offsetData:X8}.");
                byte[] offsetArray = BitConverter.GetBytes(offsetData);
                offsetArray.Reverse();
                int offsetPointer = ByteOperations.GetOffsetOfArray(outputArray, offsetArray);

                if (offsetPointer != -1)
                {
                    _logger.WriteLine($"Found shop pointer at 0x{offsetPointer:X8}.");
                    byte[] patchedPointer = BitConverter.GetBytes(inputArray.Length);
                    patchedPointer.Reverse();
                    Array.Copy(patchedPointer, 0, outputArray, offsetPointer, patchedPointer.Length);
                }
                else
                {
                    _logger.WriteLine("Could not find shop pointer, please check manually and correct code.");
                }
            }
            else
            {
                _logger.WriteLine("Could not find shop needle, please check manually and correct code.");
            }

            // Find and modify Hunter Pearl Skill unlocks
            needle = [01, 00, 01, 00, 00, 00, 00, 00, 0x25, 00, 0x25, 00, 0x25, 00, 0x25, 00, 0x25, 00, 0x25, 00, 0x25, 00];
            offsetData = ByteOperations.GetOffsetOfArray(outputArray, needle);

            if (offsetData != -1)
            {
                _logger.WriteLine($"Found hunter pearl skill data to modify at 0x{offsetData:X8}.");
                byte[] pearlPatch = [02, 00, 02, 00, 02, 00, 02, 00, 02, 00, 02, 00, 02, 00];
                for (int i = 0; i < 108; i++)
                    Array.Copy(pearlPatch, 0, outputArray, offsetData + (i * BinaryReaderService.PEARL_ENTRY_SIZE) + 8, pearlPatch.Length);
            }
            else
            {
                _logger.WriteLine("Could not find pearl skill needle, please check manually and correct code.");
            }

            _fileSystem.WriteAllBytes(file, outputArray);
        }
    }
}
