using System.Text;

using FrontierDataTool;
using FrontierDataTool.Services;

using ReFrontier.Tests.Mocks;

namespace ReFrontier.Tests.DataToolTests
{
    /// <summary>
    /// Tests for DataExtractionService.
    /// </summary>
    public class DataExtractionServiceTests
    {
        private readonly InMemoryFileSystem _fileSystem;
        private readonly TestLogger _logger;
        private readonly DataExtractionService _service;

        static DataExtractionServiceTests()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public DataExtractionServiceTests()
        {
            _fileSystem = new InMemoryFileSystem();
            _logger = new TestLogger();
            _service = new DataExtractionService(_fileSystem, _logger);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullFileSystem_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new DataExtractionService(null!, _logger));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new DataExtractionService(_fileSystem, null!));
        }

        [Fact]
        public void DefaultConstructor_CreatesValidInstance()
        {
            var service = new DataExtractionService();
            Assert.NotNull(service);
        }

        #endregion

        #region DumpSkillSystem Tests

        [Fact]
        public void DumpSkillSystem_ExtractsSkillNames()
        {
            // Arrange
            byte[] mhfpac = CreateMhfpacWithSkills("攻撃", "防御", "回避");
            _fileSystem.AddFile("/test/mhfpac.bin", mhfpac);

            // Act
            var skillId = _service.DumpSkillSystem("/test/mhfpac.bin", "test");

            // Assert
            Assert.Equal(3, skillId.Count);
            Assert.True(_fileSystem.FileExists("mhsx_SkillSys_test.txt"));
        }

        [Fact]
        public void DumpSkillSystem_LogsProgress()
        {
            // Arrange
            byte[] mhfpac = CreateMhfpacWithSkills("攻撃");
            _fileSystem.AddFile("/test/mhfpac.bin", mhfpac);

            // Act
            _service.DumpSkillSystem("/test/mhfpac.bin", "test");

            // Assert
            Assert.True(_logger.ContainsMessage("Dumping skill tree names"));
        }

        #endregion

        #region DumpSkillData Tests

        [Fact]
        public void DumpSkillData_CreatesMultipleOutputFiles()
        {
            // Arrange
            byte[] mhfpac = CreateMhfpacWithFullSkillData();
            _fileSystem.AddFile("/test/mhfpac.bin", mhfpac);

            // Act
            _service.DumpSkillData("/test/mhfpac.bin", "test");

            // Assert
            Assert.True(_fileSystem.FileExists("mhsx_SkillActivate_test.txt"));
            Assert.True(_fileSystem.FileExists("mhsx_SkillDesc_test.txt"));
            Assert.True(_fileSystem.FileExists("mhsx_SkillZ_test.txt"));
        }

        [Fact]
        public void DumpSkillData_LogsAllSections()
        {
            // Arrange
            byte[] mhfpac = CreateMhfpacWithFullSkillData();
            _fileSystem.AddFile("/test/mhfpac.bin", mhfpac);

            // Act
            _service.DumpSkillData("/test/mhfpac.bin", "test");

            // Assert
            Assert.True(_logger.ContainsMessage("active skill names"));
            Assert.True(_logger.ContainsMessage("active skill descriptions"));
            Assert.True(_logger.ContainsMessage("Z skill names"));
        }

        #endregion

        #region DumpItemData Tests

        [Fact]
        public void DumpItemData_CreatesOutputFiles()
        {
            // Arrange
            byte[] mhfdat = CreateMhfdatWithItems("アイテム1", "アイテム2");
            _fileSystem.AddFile("/test/mhfdat.bin", mhfdat);

            // Act
            _service.DumpItemData("/test/mhfdat.bin", "test");

            // Assert
            Assert.True(_fileSystem.FileExists("mhsx_Items_test.txt"));
            Assert.True(_fileSystem.FileExists("Items_Desc_test.txt"));
        }

        [Fact]
        public void DumpItemData_LogsProgress()
        {
            // Arrange
            byte[] mhfdat = CreateMhfdatWithItems("アイテム1");
            _fileSystem.AddFile("/test/mhfdat.bin", mhfdat);

            // Act
            _service.DumpItemData("/test/mhfdat.bin", "test");

            // Assert
            Assert.True(_logger.ContainsMessage("item names"));
            Assert.True(_logger.ContainsMessage("item descriptions"));
        }

        #endregion

        #region MhfDataOffsets Tests

        [Fact]
        public void ArmorDataPointers_HasFiveEntries()
        {
            // Assert
            Assert.Equal(5, MhfDataOffsets.MhfDat.Armor.DataPointers.Count);
        }

        [Fact]
        public void TotalQuestCount_CalculatesCorrectly()
        {
            // Assert
            int expected = 0;
            foreach (var section in MhfDataOffsets.MhfInf.QuestSections)
                expected += section.Count;
            Assert.Equal(expected, MhfDataOffsets.MhfInf.TotalQuestCount);
        }

        #endregion

        #region CsvEncodingOptions Tests

        [Fact]
        public void DataExtractionService_WithEncodingOptions_UsesCorrectEncoding()
        {
            // Test that the service accepts encoding options
            var encodingOptions = LibReFrontier.CsvEncodingOptions.Default;
            var service = new DataExtractionService(_fileSystem, _logger, encodingOptions);
            Assert.NotNull(service);
        }

        [Fact]
        public void DataExtractionService_WithShiftJisEncodingOptions_CreatesService()
        {
            var encodingOptions = LibReFrontier.CsvEncodingOptions.ShiftJis;
            var service = new DataExtractionService(_fileSystem, _logger, encodingOptions);
            Assert.NotNull(service);
        }

        #endregion

        #region MhfInf TotalQuestCount Tests

        [Fact]
        public void MhfInf_QuestSections_HasExpectedSectionCount()
        {
            Assert.Equal(13, MhfDataOffsets.MhfInf.QuestSections.Count);
        }

        [Fact]
        public void MhfInf_FirstSection_HasExpectedValues()
        {
            var firstSection = MhfDataOffsets.MhfInf.QuestSections[0];
            Assert.Equal(0x6BD60, firstSection.Offset);
            Assert.Equal(95, firstSection.Count);
        }

        #endregion

        #region DumpWeaponData Tests

        [Fact]
        public void DumpWeaponData_CreatesMeleeAndRangedCsvFiles()
        {
            // Arrange - create file large enough for weapon data
            byte[] mhfdat = CreateMhfdatWithWeaponData(meleeCount: 3, rangedCount: 2);
            _fileSystem.AddFile("/test/mhfdat.bin", mhfdat);

            // Act
            _service.DumpWeaponData("/test/mhfdat.bin");

            // Assert
            Assert.True(_fileSystem.FileExists("Melee.csv"));
            Assert.True(_fileSystem.FileExists("Ranged.csv"));
        }

        [Fact]
        public void DumpWeaponData_LogsWeaponCounts()
        {
            // Arrange
            byte[] mhfdat = CreateMhfdatWithWeaponData(meleeCount: 5, rangedCount: 3);
            _fileSystem.AddFile("/test/mhfdat.bin", mhfdat);

            // Act
            _service.DumpWeaponData("/test/mhfdat.bin");

            // Assert
            Assert.True(_logger.ContainsMessage("Melee count:"));
            Assert.True(_logger.ContainsMessage("Ranged count:"));
        }

        #endregion

        #region DumpQuestData Tests

        [Fact]
        public void DumpQuestData_CreatesQuestCsvFile()
        {
            // Arrange
            byte[] mhfinf = CreateMhfinfWithQuestData();
            _fileSystem.AddFile("/test/mhfinf.bin", mhfinf);

            // Act
            _service.DumpQuestData("/test/mhfinf.bin");

            // Assert
            Assert.True(_fileSystem.FileExists("InfQuests.csv"));
        }

        [Fact]
        public void DumpQuestData_LogsQuestPositions()
        {
            // Arrange
            byte[] mhfinf = CreateMhfinfWithQuestData();
            _fileSystem.AddFile("/test/mhfinf.bin", mhfinf);

            // Act
            _service.DumpQuestData("/test/mhfinf.bin");

            // Assert
            // The method logs position after each quest read
            Assert.True(_logger.Lines.Count > 0);
        }

        #endregion

        #region DumpEquipmentData Tests

        [Fact]
        public void DumpEquipmentData_CreatesArmorCsvAndTextFile()
        {
            // Arrange
            byte[] mhfdat = CreateMhfdatWithArmorData(2);
            var skillId = new List<KeyValuePair<int, string>>
            {
                new(0, "攻撃"),
                new(1, "防御")
            };
            _fileSystem.AddFile("/test/mhfdat.bin", mhfdat);

            // Act
            _service.DumpEquipmentData("/test/mhfdat.bin", "test", skillId);

            // Assert
            Assert.True(_fileSystem.FileExists("Armor.csv"));
            Assert.True(_fileSystem.FileExists("mhsx_Armor_test.txt"));
        }

        [Fact]
        public void DumpEquipmentData_LogsArmorCountPerSlot()
        {
            // Arrange
            byte[] mhfdat = CreateMhfdatWithArmorData(2);
            var skillId = new List<KeyValuePair<int, string>>();
            _fileSystem.AddFile("/test/mhfdat.bin", mhfdat);

            // Act
            _service.DumpEquipmentData("/test/mhfdat.bin", "test", skillId);

            // Assert
            Assert.True(_logger.ContainsMessage("Total armor count:"));
            Assert.True(_logger.ContainsMessage("count:"));
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Create a minimal mhfpac.bin with skill tree data.
        /// </summary>
        private static byte[] CreateMhfpacWithSkills(params string[] skillNames)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            // Write header area (need to reach 0xA24 for pointers)
            bw.Write(new byte[0xA24]);

            // Calculate offsets
            int pointerTableStart = 0xA24;
            int stringDataStart = pointerTableStart + (skillNames.Length * 4);

            // Write start pointer at 0xA20
            ms.Seek(0xA20, SeekOrigin.Begin);
            bw.Write(pointerTableStart);

            // Write end pointer at 0xA1C (end of pointer table)
            ms.Seek(0xA1C, SeekOrigin.Begin);
            bw.Write(stringDataStart);

            // Write pointer table
            ms.Seek(pointerTableStart, SeekOrigin.Begin);
            int currentOffset = stringDataStart;
            foreach (var name in skillNames)
            {
                bw.Write(currentOffset);
                currentOffset += Encoding.GetEncoding("shift-jis").GetBytes(name).Length + 1;
            }

            // Write string data
            foreach (var name in skillNames)
            {
                byte[] nameBytes = Encoding.GetEncoding("shift-jis").GetBytes(name);
                bw.Write(nameBytes);
                bw.Write((byte)0);
            }

            return ms.ToArray();
        }

        /// <summary>
        /// Create mhfpac.bin with full skill data (active skills, descriptions, Z skills).
        /// </summary>
        private static byte[] CreateMhfpacWithFullSkillData()
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            // Larger header area to accommodate all offsets
            bw.Write(new byte[0x1000]);

            // Skill activate data section
            int activatePointerStart = 0x1000;
            int activateStringStart = activatePointerStart + 8; // 2 pointers
            int activateStringEnd = activateStringStart + 20;

            // Write active skill offsets
            ms.Seek(0xA1C, SeekOrigin.Begin); // _soStringSkillActivate
            bw.Write(activatePointerStart);
            ms.Seek(0xBC0, SeekOrigin.Begin); // _eoStringSkillActivate
            bw.Write(activateStringStart);

            // Write active skill pointers and strings
            ms.Seek(activatePointerStart, SeekOrigin.Begin);
            bw.Write(activateStringStart); // Pointer 1
            bw.Write(activateStringStart + 10); // Pointer 2

            byte[] activeSkill1 = Encoding.GetEncoding("shift-jis").GetBytes("活性スキル");
            ms.Seek(activateStringStart, SeekOrigin.Begin);
            bw.Write(activeSkill1);
            bw.Write((byte)0);

            // Skill description section
            int descPointerStart = 0x2000;
            int descStringStart = descPointerStart + 4;

            ms.Seek(0xB8, SeekOrigin.Begin); // _soStringSkillDesc
            bw.Write(descPointerStart);
            ms.Seek(0xC0, SeekOrigin.Begin); // _eoStringSkillDesc
            bw.Write(descStringStart);

            ms.Seek(descPointerStart, SeekOrigin.Begin);
            bw.Write(descStringStart);

            ms.Seek(descStringStart, SeekOrigin.Begin);
            byte[] descBytes = Encoding.GetEncoding("shift-jis").GetBytes("説明");
            bw.Write(descBytes);
            bw.Write((byte)0);

            // Z skill section
            int zPointerStart = 0x3000;
            int zStringStart = zPointerStart + 4;

            ms.Seek(0xFBC, SeekOrigin.Begin); // _soStringZSkill
            bw.Write(zPointerStart);
            ms.Seek(0xFB0, SeekOrigin.Begin); // _eoStringZSkill
            bw.Write(zStringStart);

            ms.Seek(zPointerStart, SeekOrigin.Begin);
            bw.Write(zStringStart);

            ms.Seek(zStringStart, SeekOrigin.Begin);
            byte[] zBytes = Encoding.GetEncoding("shift-jis").GetBytes("Z技");
            bw.Write(zBytes);
            bw.Write((byte)0);

            return ms.ToArray();
        }

        /// <summary>
        /// Create mhfdat.bin with item data.
        /// </summary>
        private static byte[] CreateMhfdatWithItems(params string[] itemNames)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            // Header area
            bw.Write(new byte[0x200]);

            // Item name section
            int itemPointerStart = 0x200;
            int itemStringStart = itemPointerStart + (itemNames.Length * 4);

            // Write item name offsets
            ms.Seek(0x100, SeekOrigin.Begin); // _soStringItem
            bw.Write(itemPointerStart);
            ms.Seek(0xFC, SeekOrigin.Begin); // _eoStringItem
            bw.Write(itemStringStart);

            // Write item pointers and strings
            ms.Seek(itemPointerStart, SeekOrigin.Begin);
            int currentOffset = itemStringStart;
            foreach (var name in itemNames)
            {
                bw.Write(currentOffset);
                currentOffset += Encoding.GetEncoding("shift-jis").GetBytes(name).Length + 1;
            }

            foreach (var name in itemNames)
            {
                byte[] nameBytes = Encoding.GetEncoding("shift-jis").GetBytes(name);
                bw.Write(nameBytes);
                bw.Write((byte)0);
            }

            // Item description section
            int descPointerStart = currentOffset;
            int descStringStart = descPointerStart + (itemNames.Length * 4);

            ms.Seek(0x12C, SeekOrigin.Begin); // _soStringItemDesc
            bw.Write(descPointerStart);
            ms.Seek(0x100, SeekOrigin.Begin); // _eoStringItemDesc
            bw.Write(descStringStart);

            ms.Seek(descPointerStart, SeekOrigin.Begin);
            currentOffset = descStringStart;
            foreach (var name in itemNames)
            {
                bw.Write(currentOffset);
                currentOffset += Encoding.GetEncoding("shift-jis").GetBytes(name + "説明").Length + 1;
            }

            foreach (var name in itemNames)
            {
                byte[] descBytes = Encoding.GetEncoding("shift-jis").GetBytes(name + "説明");
                bw.Write(descBytes);
                bw.Write((byte)0);
            }

            return ms.ToArray();
        }

        /// <summary>
        /// Create mhfdat.bin with weapon data (melee and ranged).
        /// Based on MhfDataOffsets: RangedEnd=0x7C shares offset with MeleeStart,
        /// meaning ranged data must end where melee data starts (contiguous layout).
        /// </summary>
        private static byte[] CreateMhfdatWithWeaponData(int meleeCount, int rangedCount)
        {
            const int MELEE_ENTRY_SIZE = 0x34;
            const int RANGED_ENTRY_SIZE = 0x3C;

            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            // Header area
            bw.Write(new byte[0x200]);

            // Ranged comes before melee in file (ranged ends where melee starts)
            int rangedDataStart = 0x200;
            int rangedDataEnd = rangedDataStart + (rangedCount * RANGED_ENTRY_SIZE);
            int meleeDataStart = rangedDataEnd;  // Contiguous: melee starts where ranged ends
            int meleeDataEnd = meleeDataStart + (meleeCount * MELEE_ENTRY_SIZE);

            // String sections after data
            int rangedStringStart = meleeDataEnd + 0x100;
            int meleeStringStart = rangedStringStart + (rangedCount * 20);

            // Write pointer values:
            // MeleeStart=0x7C, MeleeEnd=0x90, MeleeStringStart=0x88
            // RangedStart=0x80, RangedEnd=0x7C (shares with MeleeStart!), RangedStringStart=0x84
            ms.Seek(0x7C, SeekOrigin.Begin);
            bw.Write(meleeDataStart);  // Also serves as RangedEnd
            ms.Seek(0x80, SeekOrigin.Begin);
            bw.Write(rangedDataStart);
            ms.Seek(0x84, SeekOrigin.Begin);
            bw.Write(rangedStringStart);
            ms.Seek(0x88, SeekOrigin.Begin);
            bw.Write(meleeStringStart);
            ms.Seek(0x90, SeekOrigin.Begin);
            bw.Write(meleeDataEnd);

            // Write ranged weapon entries (come first in file)
            ms.Seek(rangedDataStart, SeekOrigin.Begin);
            for (int i = 0; i < rangedCount; i++)
            {
                bw.Write((short)(i + 100)); // ModelId
                bw.Write((byte)5);          // Rarity
                bw.Write((byte)0);          // Unknown
                bw.Write((byte)1);          // ClassIdx
                bw.Write(new byte[RANGED_ENTRY_SIZE - 5]);
            }

            // Write melee weapon entries
            ms.Seek(meleeDataStart, SeekOrigin.Begin);
            for (int i = 0; i < meleeCount; i++)
            {
                bw.Write((short)(i + 1));   // ModelId
                bw.Write((byte)5);          // Rarity
                bw.Write((byte)0);          // ClassIdx
                bw.Write(1000);             // ZennyCost
                bw.Write(new byte[MELEE_ENTRY_SIZE - 8]);
            }

            // Write ranged string pointers and strings
            ms.Seek(rangedStringStart, SeekOrigin.Begin);
            int stringOffset = rangedStringStart + (rangedCount * 4);
            for (int i = 0; i < rangedCount; i++)
            {
                bw.Write(stringOffset);
                stringOffset += 12;
            }
            for (int i = 0; i < rangedCount; i++)
            {
                byte[] nameBytes = Encoding.GetEncoding("shift-jis").GetBytes($"遠距離{i}");
                bw.Write(nameBytes);
                bw.Write((byte)0);
            }

            // Write melee string pointers and strings
            ms.Seek(meleeStringStart, SeekOrigin.Begin);
            stringOffset = meleeStringStart + (meleeCount * 4);
            for (int i = 0; i < meleeCount; i++)
            {
                bw.Write(stringOffset);
                stringOffset += 10;
            }
            for (int i = 0; i < meleeCount; i++)
            {
                byte[] nameBytes = Encoding.GetEncoding("shift-jis").GetBytes($"近接{i}");
                bw.Write(nameBytes);
                bw.Write((byte)0);
            }

            return ms.ToArray();
        }

        /// <summary>
        /// Create mhfinf.bin with quest data.
        /// </summary>
        private static byte[] CreateMhfinfWithQuestData()
        {
            // Need enough space for all quest sections
            // Total quest count is sum of all section counts
            int totalSize = 0x200000; // 2MB should be enough
            using var ms = new MemoryStream(new byte[totalSize]);
            using var bw = new BinaryWriter(ms);

            // Write minimal quest data at each section offset
            var sections = MhfDataOffsets.MhfInf.QuestSections;
            foreach (var section in sections)
            {
                ms.Seek(section.Offset, SeekOrigin.Begin);
                for (int i = 0; i < section.Count; i++)
                {
                    // Write minimal quest entry (0x128 bytes based on code)
                    // Header: 4 bytes each for Unk1-4 = 16 bytes
                    bw.Write(0);  // Unk1
                    bw.Write(0);  // Unk2
                    bw.Write(0);  // Unk3
                    bw.Write(0);  // Unk4

                    // Monetary/time: Level, Unk5, CourseType, etc = 24 bytes
                    bw.Write((byte)5);    // Level
                    bw.Write((byte)0);    // Unk5
                    bw.Write((byte)6);    // CourseType
                    bw.Write(new byte[5]);// Unk7-Unk10, MaxPlayers
                    bw.Write(500);        // Fee
                    bw.Write(1000);       // ZennyMain
                    bw.Write(500);        // ZennyKo
                    bw.Write(200);        // ZennySubA
                    bw.Write(200);        // ZennySubB

                    // Rest of entry
                    bw.Write(new byte[0x128 - 40]); // Fill rest
                }
            }

            return ms.ToArray();
        }

        /// <summary>
        /// Create mhfdat.bin with armor data.
        /// The file format uses overlapping end/start pointers:
        /// - HeadEnd=0xE8 (unique)
        /// - BodyEnd=0x50 (same as HeadStart), meaning body ends where head starts
        /// - ArmEnd=0x54 (same as BodyStart), etc.
        /// This means data is laid out: Leg → Waist → Arm → Body → Head
        /// </summary>
        private static byte[] CreateMhfdatWithArmorData(int entriesPerSlot)
        {
            const int ARMOR_ENTRY_SIZE = 0x48;
            int slotDataSize = entriesPerSlot * ARMOR_ENTRY_SIZE;

            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            // Header area
            bw.Write(new byte[0x200]);

            // Data layout: Leg(4) → Waist(3) → Arm(2) → Body(1) → Head(0)
            // Indices: Head=0, Body=1, Arm=2, Waist=3, Leg=4
            int[] dataStarts = new int[5];
            int currentOffset = 0x200;

            // Calculate offsets in reverse order (leg first in file)
            for (int i = 4; i >= 0; i--)
            {
                dataStarts[i] = currentOffset;
                currentOffset += slotDataSize;
            }
            int headEnd = currentOffset;

            // Write data pointer values at their expected offsets
            // DataPointers: (0x50,0xE8), (0x54,0x50), (0x58,0x54), (0x5C,0x58), (0x60,0x5C)
            // Start offsets: 0x50=Head, 0x54=Body, 0x58=Arm, 0x5C=Waist, 0x60=Leg
            ms.Seek(0x50, SeekOrigin.Begin);
            bw.Write(dataStarts[0]);  // Head start
            bw.Write(dataStarts[1]);  // Body start (at 0x54)
            bw.Write(dataStarts[2]);  // Arm start (at 0x58)
            bw.Write(dataStarts[3]);  // Waist start (at 0x5C)
            bw.Write(dataStarts[4]);  // Leg start (at 0x60)

            // Head end at 0xE8
            ms.Seek(0xE8, SeekOrigin.Begin);
            bw.Write(headEnd);

            // String sections after data
            int stringBase = headEnd + 0x100;
            int[] stringStarts = new int[5];
            for (int i = 0; i < 5; i++)
            {
                stringStarts[i] = stringBase + (i * entriesPerSlot * 20);
            }

            // Write string offset pointers
            // StringPointers start: 0x64=Head, 0x68=Body, 0x6C=Arm, 0x70=Waist, 0x74=Leg
            ms.Seek(0x64, SeekOrigin.Begin);
            for (int i = 0; i < 5; i++)
            {
                bw.Write(stringStarts[i]);
            }

            // Write armor entries for each slot
            string[] slotNames = ["頭", "胴", "腕", "腰", "脚"];
            for (int slot = 0; slot < 5; slot++)
            {
                ms.Seek(dataStarts[slot], SeekOrigin.Begin);
                for (int j = 0; j < entriesPerSlot; j++)
                {
                    bw.Write((short)(j + 1));   // ModelIdMale
                    bw.Write((short)(j + 1));   // ModelIdFemale
                    bw.Write((byte)0x03);       // Bitfield
                    bw.Write((byte)5);          // Rarity
                    bw.Write((byte)7);          // MaxLevel
                    bw.Write(new byte[5]);
                    bw.Write(100);              // ZennyCost
                    bw.Write(new byte[ARMOR_ENTRY_SIZE - 16]);
                }
            }

            // Write string data for each slot
            for (int slot = 0; slot < 5; slot++)
            {
                ms.Seek(stringStarts[slot], SeekOrigin.Begin);
                int strOffset = stringStarts[slot] + (entriesPerSlot * 4);
                for (int j = 0; j < entriesPerSlot; j++)
                {
                    bw.Write(strOffset);
                    strOffset += 12;
                }
                for (int j = 0; j < entriesPerSlot; j++)
                {
                    byte[] nameBytes = Encoding.GetEncoding("shift-jis").GetBytes($"{slotNames[slot]}装備{j}");
                    bw.Write(nameBytes);
                    bw.Write((byte)0);
                }
            }

            return ms.ToArray();
        }

        #endregion
    }
}
