using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

using CsvHelper;

using FrontierDataTool.Structs;

using LibReFrontier;
using LibReFrontier.Abstractions;

using ReFrontier;

namespace FrontierDataTool.Services
{
    /// <summary>
    /// Service for extracting game data from binary files.
    /// </summary>
    public class DataExtractionService
    {
        private readonly IFileSystem _fileSystem;
        private readonly ILogger _logger;
        private readonly BinaryReaderService _binaryReader;
        private readonly CsvEncodingOptions _encodingOptions;

        /// <summary>
        /// Create a new DataExtractionService with default dependencies.
        /// </summary>
        public DataExtractionService()
            : this(new RealFileSystem(), new ConsoleLogger(), CsvEncodingOptions.Default)
        {
        }

        /// <summary>
        /// Create a new DataExtractionService with injectable dependencies.
        /// </summary>
        public DataExtractionService(IFileSystem fileSystem, ILogger logger)
            : this(fileSystem, logger, CsvEncodingOptions.Default)
        {
        }

        /// <summary>
        /// Create a new DataExtractionService with injectable dependencies and encoding options.
        /// </summary>
        public DataExtractionService(IFileSystem fileSystem, ILogger logger, CsvEncodingOptions encodingOptions)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _binaryReader = new BinaryReaderService();
            _encodingOptions = encodingOptions ?? CsvEncodingOptions.Default;
        }

        #region Helper Methods

        /// <summary>
        /// Read strings from a range defined by offset pointers.
        /// Seeks to startPointer, reads the actual start offset, seeks to endPointer,
        /// reads the actual end offset, then extracts all strings between them.
        /// </summary>
        /// <param name="br">Binary reader positioned in the file.</param>
        /// <param name="startPointer">Pointer to the start offset value.</param>
        /// <param name="endPointer">Pointer to the end offset value.</param>
        /// <returns>List of extracted strings.</returns>
        private List<string> ReadStringRange(BinaryReader br, int startPointer, int endPointer)
        {
            br.BaseStream.Seek(startPointer, SeekOrigin.Begin);
            int startOffset = br.ReadInt32();
            br.BaseStream.Seek(endPointer, SeekOrigin.Begin);
            int endOffset = br.ReadInt32();

            br.BaseStream.Seek(startOffset, SeekOrigin.Begin);
            var strings = new List<string>();
            while (br.BaseStream.Position < endOffset)
            {
                string name = _binaryReader.StringFromPointer(br);
                strings.Add(name);
            }
            return strings;
        }

        /// <summary>
        /// Write a list of strings to a UTF-8 text file, one per line.
        /// </summary>
        /// <param name="fileName">Output file name.</param>
        /// <param name="strings">Strings to write.</param>
        private void WriteStringsToTextFile(string fileName, IEnumerable<string> strings)
        {
            using var file = _fileSystem.CreateStreamWriter(fileName, false, Encoding.UTF8);
            foreach (string entry in strings)
                file.WriteLine("{0}", entry);
        }

        private static readonly JsonSerializerOptions s_jsonOptions = new() { WriteIndented = true };
        private static readonly Encoding s_utf8NoBom = new UTF8Encoding(false);

        /// <summary>
        /// Write a collection of records as indented JSON.
        /// </summary>
        /// <typeparam name="T">Record type.</typeparam>
        /// <param name="filePath">Output file path.</param>
        /// <param name="records">Records to serialize.</param>
        private void WriteJsonFile<T>(string filePath, IEnumerable<T> records)
        {
            string json = JsonSerializer.Serialize(records, s_jsonOptions);
            using var writer = _fileSystem.CreateStreamWriter(filePath, false, s_utf8NoBom);
            writer.Write(json);
        }

        #endregion

        /// <summary>
        /// Dump all data from the game files.
        /// </summary>
        /// <param name="suffix">Output file suffix.</param>
        /// <param name="mhfpac">Path to mhfpac.bin.</param>
        /// <param name="mhfdat">Path to mhfdat.bin.</param>
        /// <param name="mhfinf">Path to mhfinf.bin.</param>
        public void DumpData(string suffix, string mhfpac, string mhfdat, string mhfinf)
        {
            var preprocessor = new FilePreprocessor();

            var (processedMhfpac, cleanupMhfpac) = preprocessor.AutoPreprocess(mhfpac, createMetaFile: true);
            var (processedMhfdat, cleanupMhfdat) = preprocessor.AutoPreprocess(mhfdat, createMetaFile: true);
            var (processedMhfinf, cleanupMhfinf) = preprocessor.AutoPreprocess(mhfinf, createMetaFile: true);

            try
            {
                var skillId = DumpSkillSystem(processedMhfpac, suffix);
                DumpSkillData(processedMhfpac, suffix);
                DumpItemData(processedMhfdat, suffix);
                DumpEquipmentData(processedMhfdat, suffix, skillId);
                DumpWeaponData(processedMhfdat);
                DumpQuestData(processedMhfinf);
            }
            finally
            {
                cleanupMhfpac();
                cleanupMhfdat();
                cleanupMhfinf();
            }
        }

        /// <summary>
        /// Dump skill system data and return skill lookup.
        /// </summary>
        public List<KeyValuePair<int, string>> DumpSkillSystem(string mhfpac, string suffix)
        {
            _logger.WriteLine("Dumping skill tree names.");

            using var msInput = new MemoryStream(_fileSystem.ReadAllBytes(mhfpac));
            using var brInput = new BinaryReader(msInput);

            var skillNames = ReadStringRange(brInput,
                MhfDataOffsets.MhfPac.Skills.TreeNameStart,
                MhfDataOffsets.MhfPac.Skills.TreeNameEnd);

            var skillId = new List<KeyValuePair<int, string>>();
            for (int i = 0; i < skillNames.Count; i++)
            {
                skillId.Add(new KeyValuePair<int, string>(i, skillNames[i]));
            }

            WriteStringsToTextFile($"mhsx_SkillSys_{suffix}.txt", skillNames);
            return skillId;
        }

        /// <summary>
        /// Dump active skills, descriptions, and Z skills.
        /// </summary>
        public void DumpSkillData(string mhfpac, string suffix)
        {
            using var msInput = new MemoryStream(_fileSystem.ReadAllBytes(mhfpac));
            using var brInput = new BinaryReader(msInput);

            // Active skills
            _logger.WriteLine("Dumping active skill names.");
            var activeSkills = ReadStringRange(brInput,
                MhfDataOffsets.MhfPac.Skills.ActiveNameStart,
                MhfDataOffsets.MhfPac.Skills.ActiveNameEnd);
            WriteStringsToTextFile($"mhsx_SkillActivate_{suffix}.txt", activeSkills);

            // Skill descriptions
            _logger.WriteLine("Dumping active skill descriptions.");
            var skillDescs = ReadStringRange(brInput,
                MhfDataOffsets.MhfPac.Skills.DescriptionStart,
                MhfDataOffsets.MhfPac.Skills.DescriptionEnd);
            WriteStringsToTextFile($"mhsx_SkillDesc_{suffix}.txt", skillDescs);

            // Z skills
            _logger.WriteLine("Dumping Z skill names.");
            var zSkills = ReadStringRange(brInput,
                MhfDataOffsets.MhfPac.Skills.ZSkillNameStart,
                MhfDataOffsets.MhfPac.Skills.ZSkillNameEnd);
            WriteStringsToTextFile($"mhsx_SkillZ_{suffix}.txt", zSkills);
        }

        /// <summary>
        /// Dump item names and descriptions.
        /// </summary>
        public void DumpItemData(string mhfdat, string suffix)
        {
            using var msInput = new MemoryStream(_fileSystem.ReadAllBytes(mhfdat));
            using var brInput = new BinaryReader(msInput);

            _logger.WriteLine("Dumping item names.");
            var items = ReadStringRange(brInput,
                MhfDataOffsets.MhfDat.Items.StringStart,
                MhfDataOffsets.MhfDat.Items.StringEnd);
            WriteStringsToTextFile($"mhsx_Items_{suffix}.txt", items);

            _logger.WriteLine("Dumping item descriptions.");
            var itemDescs = ReadStringRange(brInput,
                MhfDataOffsets.MhfDat.Items.DescriptionStart,
                MhfDataOffsets.MhfDat.Items.DescriptionEnd);
            WriteStringsToTextFile($"Items_Desc_{suffix}.txt", itemDescs);
        }

        /// <summary>
        /// Dump equipment (armor) data.
        /// </summary>
        public void DumpEquipmentData(string mhfdat, string suffix, List<KeyValuePair<int, string>> skillId)
        {
            var dataPointers = MhfDataOffsets.MhfDat.Armor.DataPointers;
            var stringPointers = MhfDataOffsets.MhfDat.Armor.StringPointers;
            var slotNames = MhfDataOffsets.MhfDat.Armor.SlotNames;

            using var msInput = new MemoryStream(_fileSystem.ReadAllBytes(mhfdat));
            using var brInput = new BinaryReader(msInput);

            // Count total entries
            int totalCount = 0;
            for (int i = 0; i < dataPointers.Count; i++)
            {
                brInput.BaseStream.Seek(dataPointers[i].Start, SeekOrigin.Begin);
                int sOffset = brInput.ReadInt32();
                brInput.BaseStream.Seek(dataPointers[i].End, SeekOrigin.Begin);
                int eOffset = brInput.ReadInt32();
                totalCount += (eOffset - sOffset) / BinaryReaderService.ARMOR_ENTRY_SIZE;
            }
            _logger.WriteLine($"Total armor count: {totalCount}");

            var armorEntries = new ArmorDataEntry[totalCount];
            int currentCount = 0;

            // Read entries for each armor slot
            for (int i = 0; i < dataPointers.Count; i++)
            {
                brInput.BaseStream.Seek(dataPointers[i].Start, SeekOrigin.Begin);
                int sOffset = brInput.ReadInt32();
                brInput.BaseStream.Seek(dataPointers[i].End, SeekOrigin.Begin);
                int eOffset = brInput.ReadInt32();

                int entryCount = (eOffset - sOffset) / BinaryReaderService.ARMOR_ENTRY_SIZE;
                brInput.BaseStream.Seek(sOffset, SeekOrigin.Begin);
                _logger.WriteLine($"{slotNames[i]} count: {entryCount}");

                for (int j = 0; j < entryCount; j++)
                {
                    armorEntries[j + currentCount] = _binaryReader.ReadArmorEntry(brInput, skillId, slotNames[i]);
                }

                // Get strings for this slot
                brInput.BaseStream.Seek(stringPointers[i].Start, SeekOrigin.Begin);
                sOffset = brInput.ReadInt32();
                brInput.BaseStream.Seek(sOffset, SeekOrigin.Begin);

                for (int j = 0; j < entryCount - 1; j++)
                {
                    armorEntries[j + currentCount].Name = _binaryReader.StringFromPointer(brInput);
                }
                currentCount += entryCount;
            }

            // Write armor data
            if (_encodingOptions.Format == OutputFormat.Json)
            {
                WriteJsonFile("Armor.json", armorEntries);
            }
            else
            {
                using var textWriter = _fileSystem.CreateStreamWriter("Armor.csv", false, _encodingOptions.GetOutputEncoding());
                var writer = new CsvWriter(textWriter, TextFileConfiguration.CreateJapaneseCsvConfig());
                writer.WriteRecords(armorEntries);
            }

            // Write armor names txt
            var armorNames = new List<string>();
            foreach (var entry in armorEntries)
                armorNames.Add(entry.Name ?? "");
            WriteStringsToTextFile($"mhsx_Armor_{suffix}.txt", armorNames);
        }

        /// <summary>
        /// Dump weapon data (melee and ranged).
        /// </summary>
        public void DumpWeaponData(string mhfdat)
        {
            using var msInput = new MemoryStream(_fileSystem.ReadAllBytes(mhfdat));
            using var brInput = new BinaryReader(msInput);

            // Melee weapons
            brInput.BaseStream.Seek(MhfDataOffsets.MhfDat.Weapons.MeleeStart, SeekOrigin.Begin);
            int sOffset = brInput.ReadInt32();
            brInput.BaseStream.Seek(MhfDataOffsets.MhfDat.Weapons.MeleeEnd, SeekOrigin.Begin);
            int eOffset = brInput.ReadInt32();

            int entryCountMelee = (eOffset - sOffset) / BinaryReaderService.MELEE_WEAPON_ENTRY_SIZE;
            brInput.BaseStream.Seek(sOffset, SeekOrigin.Begin);
            _logger.WriteLine($"Melee count: {entryCountMelee}");

            var meleeEntries = new MeleeWeaponEntry[entryCountMelee];
            for (int i = 0; i < entryCountMelee; i++)
            {
                meleeEntries[i] = _binaryReader.ReadMeleeWeaponEntry(brInput);
            }

            // Get melee weapon strings
            brInput.BaseStream.Seek(MhfDataOffsets.MhfDat.Weapons.MeleeStringStart, SeekOrigin.Begin);
            sOffset = brInput.ReadInt32();
            brInput.BaseStream.Seek(sOffset, SeekOrigin.Begin);
            for (int j = 0; j < entryCountMelee - 1; j++)
            {
                meleeEntries[j].Name = _binaryReader.StringFromPointer(brInput);
            }

            // Write melee data
            if (_encodingOptions.Format == OutputFormat.Json)
            {
                WriteJsonFile("Melee.json", meleeEntries);
            }
            else
            {
                using var textWriter = _fileSystem.CreateStreamWriter("Melee.csv", false, _encodingOptions.GetOutputEncoding());
                var writer = new CsvWriter(textWriter, TextFileConfiguration.CreateJapaneseCsvConfig());
                writer.WriteRecords(meleeEntries);
            }

            // Ranged weapons
            brInput.BaseStream.Seek(MhfDataOffsets.MhfDat.Weapons.RangedStart, SeekOrigin.Begin);
            sOffset = brInput.ReadInt32();
            brInput.BaseStream.Seek(MhfDataOffsets.MhfDat.Weapons.RangedEnd, SeekOrigin.Begin);
            eOffset = brInput.ReadInt32();

            int entryCountRanged = (eOffset - sOffset) / BinaryReaderService.RANGED_WEAPON_ENTRY_SIZE;
            brInput.BaseStream.Seek(sOffset, SeekOrigin.Begin);
            _logger.WriteLine($"Ranged count: {entryCountRanged}");

            var rangedEntries = new RangedWeaponEntry[entryCountRanged];
            for (int i = 0; i < entryCountRanged; i++)
            {
                rangedEntries[i] = _binaryReader.ReadRangedWeaponEntry(brInput);
            }

            // Get ranged weapon strings
            brInput.BaseStream.Seek(MhfDataOffsets.MhfDat.Weapons.RangedStringStart, SeekOrigin.Begin);
            sOffset = brInput.ReadInt32();
            brInput.BaseStream.Seek(sOffset, SeekOrigin.Begin);
            for (int j = 0; j < entryCountRanged - 1; j++)
            {
                rangedEntries[j].Name = _binaryReader.StringFromPointer(brInput);
            }

            // Write ranged data
            if (_encodingOptions.Format == OutputFormat.Json)
            {
                WriteJsonFile("Ranged.json", rangedEntries);
            }
            else
            {
                using var textWriter = _fileSystem.CreateStreamWriter("Ranged.csv", false, _encodingOptions.GetOutputEncoding());
                var writer = new CsvWriter(textWriter, TextFileConfiguration.CreateJapaneseCsvConfig());
                writer.WriteRecords(rangedEntries);
            }
        }

        /// <summary>
        /// Dump quest data.
        /// </summary>
        public void DumpQuestData(string mhfinf)
        {
            var questSections = MhfDataOffsets.MhfInf.QuestSections;

            using var msInput = new MemoryStream(_fileSystem.ReadAllBytes(mhfinf));
            using var brInput = new BinaryReader(msInput);

            var quests = new QuestData[MhfDataOffsets.MhfInf.TotalQuestCount];
            int currentCount = 0;

            foreach (var section in questSections)
            {
                brInput.BaseStream.Seek(section.Offset, SeekOrigin.Begin);
                for (int i = 0; i < section.Count; i++)
                {
                    quests[currentCount + i] = _binaryReader.ReadQuestEntry(brInput);
                    _logger.WriteLine(brInput.BaseStream.Position.ToString("X8"));
                }
                currentCount += section.Count;
            }

            // Write output
            if (_encodingOptions.Format == OutputFormat.Json)
            {
                WriteJsonFile("InfQuests.json", quests);
            }
            else
            {
                using var textWriter = _fileSystem.CreateStreamWriter("InfQuests.csv", false, _encodingOptions.GetOutputEncoding());
                var writer = new CsvWriter(textWriter, TextFileConfiguration.CreateJapaneseCsvConfig());
                writer.WriteRecords(quests);
            }
        }
    }
}
