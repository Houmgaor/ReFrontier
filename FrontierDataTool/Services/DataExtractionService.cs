using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

using CsvHelper;
using CsvHelper.Configuration;

using FrontierDataTool.Structs;

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

        // Offset pointers for mhfdat.bin
        private static readonly int _soStringHead = 0x64, _soStringBody = 0x68, _soStringArm = 0x6C, _soStringWaist = 0x70, _soStringLeg = 0x74;
        private static readonly int _eoStringHead = 0x60, _eoStringBody = 0x64, _eoStringArm = 0x68, _eoStringWaist = 0x6C, _eoStringLeg = 0x70;
        private static readonly int _soStringRanged = 0x84, _soStringMelee = 0x88;
        private static readonly int _soStringItem = 0x100, _soStringItemDesc = 0x12C;
        private static readonly int _eoStringItem = 0xFC, _eoStringItemDesc = 0x100;
        private static readonly int _soHead = 0x50, _soBody = 0x54, _soArm = 0x58, _soWaist = 0x5C, _soLeg = 0x60;
        private static readonly int _eoHead = 0xE8, _eoBody = 0x50, _eoArm = 0x54, _eoWaist = 0x58, _eoLeg = 0x5C;
        private static readonly int _soRanged = 0x80, _soMelee = 0x7C;
        private static readonly int _eoRanged = 0x7C, _eoMelee = 0x90;

        // Offset pointers for mhfpac.bin
        private static readonly int _soStringSkillPt = 0xA20, _soStringSkillActivate = 0xA1C, _soStringZSkill = 0xFBC, _soStringSkillDesc = 0xb8;
        private static readonly int _eoStringSkillPt = 0xA1C, _eoStringSkillActivate = 0xBC0, _eoStringZSkill = 0xFB0, _eoStringSkillDesc = 0xc0;

        /// <summary>
        /// Pointers for armor data sections.
        /// </summary>
        public static readonly List<KeyValuePair<int, int>> DataPointersArmor =
        [
            new KeyValuePair<int, int>(_soHead, _eoHead),
            new KeyValuePair<int, int>(_soBody, _eoBody),
            new KeyValuePair<int, int>(_soArm, _eoArm),
            new KeyValuePair<int, int>(_soWaist, _eoWaist),
            new KeyValuePair<int, int>(_soLeg, _eoLeg)
        ];

        private static readonly string[] ArmorClassIds = ["頭", "胴", "腕", "腰", "脚"];

        /// <summary>
        /// Create a new DataExtractionService with default dependencies.
        /// </summary>
        public DataExtractionService()
            : this(new RealFileSystem(), new ConsoleLogger())
        {
        }

        /// <summary>
        /// Create a new DataExtractionService with injectable dependencies.
        /// </summary>
        public DataExtractionService(IFileSystem fileSystem, ILogger logger)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _binaryReader = new BinaryReaderService();
        }

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

            brInput.BaseStream.Seek(_soStringSkillPt, SeekOrigin.Begin);
            int sOffset = brInput.ReadInt32();
            brInput.BaseStream.Seek(_eoStringSkillPt, SeekOrigin.Begin);
            int eOffset = brInput.ReadInt32();

            brInput.BaseStream.Seek(sOffset, SeekOrigin.Begin);
            var skillId = new List<KeyValuePair<int, string>>();
            int id = 0;
            while (brInput.BaseStream.Position < eOffset)
            {
                string name = _binaryReader.StringFromPointer(brInput);
                skillId.Add(new KeyValuePair<int, string>(id, name));
                id++;
            }

            string textName = $"mhsx_SkillSys_{suffix}.txt";
            using (var file = _fileSystem.CreateStreamWriter(textName, false, Encoding.UTF8))
            {
                foreach (var entry in skillId)
                    file.WriteLine("{0}", entry.Value);
            }

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
            brInput.BaseStream.Seek(_soStringSkillActivate, SeekOrigin.Begin);
            int sOffset = brInput.ReadInt32();
            brInput.BaseStream.Seek(_eoStringSkillActivate, SeekOrigin.Begin);
            int eOffset = brInput.ReadInt32();

            brInput.BaseStream.Seek(sOffset, SeekOrigin.Begin);
            var activeSkill = new List<string>();
            while (brInput.BaseStream.Position < eOffset)
            {
                string name = _binaryReader.StringFromPointer(brInput);
                activeSkill.Add(name);
            }

            string textName = $"mhsx_SkillActivate_{suffix}.txt";
            using (var file = _fileSystem.CreateStreamWriter(textName, false, Encoding.UTF8))
            {
                foreach (string entry in activeSkill)
                    file.WriteLine("{0}", entry);
            }

            // Skill descriptions
            _logger.WriteLine("Dumping active skill descriptions.");
            brInput.BaseStream.Seek(_soStringSkillDesc, SeekOrigin.Begin);
            sOffset = brInput.ReadInt32();
            brInput.BaseStream.Seek(_eoStringSkillDesc, SeekOrigin.Begin);
            eOffset = brInput.ReadInt32();

            brInput.BaseStream.Seek(sOffset, SeekOrigin.Begin);
            var skillDesc = new List<string>();
            while (brInput.BaseStream.Position < eOffset)
            {
                string name = _binaryReader.StringFromPointer(brInput);
                skillDesc.Add(name);
            }

            textName = $"mhsx_SkillDesc_{suffix}.txt";
            using (var file = _fileSystem.CreateStreamWriter(textName, false, Encoding.UTF8))
            {
                foreach (string entry in skillDesc)
                    file.WriteLine("{0}", entry);
            }

            // Z skills
            _logger.WriteLine("Dumping Z skill names.");
            brInput.BaseStream.Seek(_soStringZSkill, SeekOrigin.Begin);
            sOffset = brInput.ReadInt32();
            brInput.BaseStream.Seek(_eoStringZSkill, SeekOrigin.Begin);
            eOffset = brInput.ReadInt32();

            brInput.BaseStream.Seek(sOffset, SeekOrigin.Begin);
            var zSkill = new List<string>();
            while (brInput.BaseStream.Position < eOffset)
            {
                string name = _binaryReader.StringFromPointer(brInput);
                zSkill.Add(name);
            }

            textName = $"mhsx_SkillZ_{suffix}.txt";
            using (var file = _fileSystem.CreateStreamWriter(textName, false, Encoding.UTF8))
            {
                foreach (string entry in zSkill)
                    file.WriteLine("{0}", entry);
            }
        }

        /// <summary>
        /// Dump item names and descriptions.
        /// </summary>
        public void DumpItemData(string mhfdat, string suffix)
        {
            _logger.WriteLine("Dumping item names.");

            using var msInput = new MemoryStream(_fileSystem.ReadAllBytes(mhfdat));
            using var brInput = new BinaryReader(msInput);

            brInput.BaseStream.Seek(_soStringItem, SeekOrigin.Begin);
            int sOffset = brInput.ReadInt32();
            brInput.BaseStream.Seek(_eoStringItem, SeekOrigin.Begin);
            int eOffset = brInput.ReadInt32();

            brInput.BaseStream.Seek(sOffset, SeekOrigin.Begin);
            var items = new List<string>();
            while (brInput.BaseStream.Position < eOffset)
            {
                string name = _binaryReader.StringFromPointer(brInput);
                items.Add(name);
            }

            string textName = $"mhsx_Items_{suffix}.txt";
            using (var file = _fileSystem.CreateStreamWriter(textName, false, Encoding.UTF8))
            {
                foreach (string entry in items)
                    file.WriteLine("{0}", entry);
            }

            _logger.WriteLine("Dumping item descriptions.");
            brInput.BaseStream.Seek(_soStringItemDesc, SeekOrigin.Begin);
            sOffset = brInput.ReadInt32();
            brInput.BaseStream.Seek(_eoStringItemDesc, SeekOrigin.Begin);
            eOffset = brInput.ReadInt32();

            brInput.BaseStream.Seek(sOffset, SeekOrigin.Begin);
            var itemsDesc = new List<string>();
            while (brInput.BaseStream.Position < eOffset)
            {
                string name = _binaryReader.StringFromPointer(brInput);
                itemsDesc.Add(name);
            }

            textName = $"Items_Desc_{suffix}.txt";
            using (var file = _fileSystem.CreateStreamWriter(textName, false, Encoding.UTF8))
            {
                foreach (string entry in itemsDesc)
                    file.WriteLine("{0}", entry);
            }
        }

        /// <summary>
        /// Dump equipment (armor) data.
        /// </summary>
        public void DumpEquipmentData(string mhfdat, string suffix, List<KeyValuePair<int, string>> skillId)
        {
            var stringPointersArmor = new List<KeyValuePair<int, int>>
            {
                new(_soStringHead, _eoStringHead),
                new(_soStringBody, _eoStringBody),
                new(_soStringArm, _eoStringArm),
                new(_soStringWaist, _eoStringWaist),
                new(_soStringLeg, _eoStringLeg)
            };

            int totalCount = 0;
            int sOffset, eOffset;

            using var msInput = new MemoryStream(_fileSystem.ReadAllBytes(mhfdat));
            using var brInput = new BinaryReader(msInput);

            for (int i = 0; i < 5; i++)
            {
                brInput.BaseStream.Seek(DataPointersArmor[i].Key, SeekOrigin.Begin);
                sOffset = brInput.ReadInt32();
                brInput.BaseStream.Seek(DataPointersArmor[i].Value, SeekOrigin.Begin);
                eOffset = brInput.ReadInt32();

                int entryCount = (eOffset - sOffset) / BinaryReaderService.ARMOR_ENTRY_SIZE;
                totalCount += entryCount;
            }
            _logger.WriteLine($"Total armor count: {totalCount}");

            var armorEntries = new ArmorDataEntry[totalCount];
            int currentCount = 0;

            for (int i = 0; i < 5; i++)
            {
                brInput.BaseStream.Seek(DataPointersArmor[i].Key, SeekOrigin.Begin);
                sOffset = brInput.ReadInt32();
                brInput.BaseStream.Seek(DataPointersArmor[i].Value, SeekOrigin.Begin);
                eOffset = brInput.ReadInt32();

                int entryCount = (eOffset - sOffset) / BinaryReaderService.ARMOR_ENTRY_SIZE;
                brInput.BaseStream.Seek(sOffset, SeekOrigin.Begin);
                _logger.WriteLine($"{ArmorClassIds[i]} count: {entryCount}");

                for (int j = 0; j < entryCount; j++)
                {
                    armorEntries[j + currentCount] = _binaryReader.ReadArmorEntry(brInput, skillId, ArmorClassIds[i]);
                }

                // Get strings
                brInput.BaseStream.Seek(stringPointersArmor[i].Key, SeekOrigin.Begin);
                sOffset = brInput.ReadInt32();

                brInput.BaseStream.Seek(sOffset, SeekOrigin.Begin);
                for (int j = 0; j < entryCount - 1; j++)
                {
                    string name = _binaryReader.StringFromPointer(brInput);
                    armorEntries[j + currentCount].Name = name;
                }
                currentCount += entryCount;
            }

            // Write armor CSV
            using (var textWriter = _fileSystem.CreateStreamWriter("Armor.csv", false, Encoding.GetEncoding("shift-jis")))
            {
                var configuration = new CsvConfiguration(CultureInfo.CreateSpecificCulture("jp-JP"))
                {
                    Delimiter = "\t",
                };
                var writer = new CsvWriter(textWriter, configuration);
                writer.WriteRecords(armorEntries);
            }

            // Write armor txt
            string textName = $"mhsx_Armor_{suffix}.txt";
            using var file = _fileSystem.CreateStreamWriter(textName, false, Encoding.UTF8);
            foreach (var entry in armorEntries)
                file.WriteLine("{0}", entry.Name);
        }

        /// <summary>
        /// Dump weapon data (melee and ranged).
        /// </summary>
        public void DumpWeaponData(string mhfdat)
        {
            using var msInput = new MemoryStream(_fileSystem.ReadAllBytes(mhfdat));
            using var brInput = new BinaryReader(msInput);

            // Melee weapons
            brInput.BaseStream.Seek(_soMelee, SeekOrigin.Begin);
            int sOffset = brInput.ReadInt32();
            brInput.BaseStream.Seek(_eoMelee, SeekOrigin.Begin);
            int eOffset = brInput.ReadInt32();

            int entryCountMelee = (eOffset - sOffset) / BinaryReaderService.MELEE_WEAPON_ENTRY_SIZE;
            brInput.BaseStream.Seek(sOffset, SeekOrigin.Begin);
            _logger.WriteLine($"Melee count: {entryCountMelee}");

            var meleeEntries = new MeleeWeaponEntry[entryCountMelee];
            for (int i = 0; i < entryCountMelee; i++)
            {
                meleeEntries[i] = _binaryReader.ReadMeleeWeaponEntry(brInput);
            }

            // Get strings
            brInput.BaseStream.Seek(_soStringMelee, SeekOrigin.Begin);
            sOffset = brInput.ReadInt32();

            brInput.BaseStream.Seek(sOffset, SeekOrigin.Begin);
            for (int j = 0; j < entryCountMelee - 1; j++)
            {
                string name = _binaryReader.StringFromPointer(brInput);
                meleeEntries[j].Name = name;
            }

            // Write CSV
            using (var textWriter = _fileSystem.CreateStreamWriter("Melee.csv", false, Encoding.GetEncoding("shift-jis")))
            {
                var configuration = new CsvConfiguration(CultureInfo.CreateSpecificCulture("jp-JP"))
                {
                    Delimiter = "\t",
                };
                var writer = new CsvWriter(textWriter, configuration);
                writer.WriteRecords(meleeEntries);
            }

            // Ranged weapons
            brInput.BaseStream.Seek(_soRanged, SeekOrigin.Begin);
            sOffset = brInput.ReadInt32();
            brInput.BaseStream.Seek(_eoRanged, SeekOrigin.Begin);
            eOffset = brInput.ReadInt32();

            int entryCountRanged = (eOffset - sOffset) / BinaryReaderService.RANGED_WEAPON_ENTRY_SIZE;
            brInput.BaseStream.Seek(sOffset, SeekOrigin.Begin);
            _logger.WriteLine($"Ranged count: {entryCountRanged}");

            var rangedEntries = new RangedWeaponEntry[entryCountRanged];
            for (int i = 0; i < entryCountRanged; i++)
            {
                rangedEntries[i] = _binaryReader.ReadRangedWeaponEntry(brInput);
            }

            // Get strings
            brInput.BaseStream.Seek(_soStringRanged, SeekOrigin.Begin);
            sOffset = brInput.ReadInt32();

            brInput.BaseStream.Seek(sOffset, SeekOrigin.Begin);
            for (int j = 0; j < entryCountRanged - 1; j++)
            {
                string name = _binaryReader.StringFromPointer(brInput);
                rangedEntries[j].Name = name;
            }

            // Write CSV
            using (var textWriter = _fileSystem.CreateStreamWriter("Ranged.csv", false, Encoding.GetEncoding("shift-jis")))
            {
                var configuration = new CsvConfiguration(CultureInfo.CreateSpecificCulture("jp-JP"))
                {
                    Delimiter = "\t",
                };
                var writer = new CsvWriter(textWriter, configuration);
                writer.WriteRecords(rangedEntries);
            }
        }

        /// <summary>
        /// Dump quest data.
        /// </summary>
        public void DumpQuestData(string mhfinf)
        {
            var offsetInfQuestData = new List<KeyValuePair<int, int>>
            {
                new(0x6bd60, 95),
                new(0x74100, 62),
                new(0x797e0, 99),
                new(0x821a0, 98),
                new(0x8aa00, 99),
                new(0x933c0, 99),
                new(0x9bd80, 99),
                new(0xa4740, 99),
                new(0xad100, 99),
                new(0xb5b40, 36),
                new(0xb8e60, 96),
                new(0xc1400, 91),
                new(0x161220, 20),
            };

            using var msInput = new MemoryStream(_fileSystem.ReadAllBytes(mhfinf));
            using var brInput = new BinaryReader(msInput);

            int totalCount = 0;
            foreach (var offset in offsetInfQuestData)
                totalCount += offset.Value;

            var quests = new QuestData[totalCount];
            int currentCount = 0;

            foreach (var offset in offsetInfQuestData)
            {
                brInput.BaseStream.Seek(offset.Key, SeekOrigin.Begin);
                for (int i = 0; i < offset.Value; i++)
                {
                    quests[currentCount + i] = _binaryReader.ReadQuestEntry(brInput);
                    _logger.WriteLine(brInput.BaseStream.Position.ToString("X8"));
                }
                currentCount += offset.Value;
            }

            // Write CSV
            using var textWriter = _fileSystem.CreateStreamWriter("InfQuests.csv", false, Encoding.GetEncoding("shift-jis"));
            var configuration = new CsvConfiguration(CultureInfo.CreateSpecificCulture("jp-JP"))
            {
                Delimiter = "\t",
            };
            var writer = new CsvWriter(textWriter, configuration);
            writer.WriteRecords(quests);
        }
    }
}
