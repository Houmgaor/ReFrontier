using System.Text;

using FrontierDataTool.Services;

using ReFrontier.Tests.Mocks;

namespace ReFrontier.Tests.DataToolTests
{
    /// <summary>
    /// Tests for DataImportService.
    /// </summary>
    public class DataImportServiceTests
    {
        private readonly InMemoryFileSystem _fileSystem;
        private readonly TestLogger _logger;
        private readonly DataImportService _service;

        static DataImportServiceTests()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public DataImportServiceTests()
        {
            _fileSystem = new InMemoryFileSystem();
            _logger = new TestLogger();
            _service = new DataImportService(_fileSystem, _logger);
        }

        #region BuildSkillLookup Tests

        [Fact]
        public void BuildSkillLookup_ParsesSkillNames()
        {
            // Arrange
            byte[] mhfpac = TestDataFactory.CreateMinimalMhfpac(new[] { "攻撃", "防御", "回避" });
            _fileSystem.AddFile("/test/mhfpac.bin", mhfpac);

            // Act
            var lookup = _service.BuildSkillLookup("/test/mhfpac.bin");

            // Assert
            Assert.Equal(3, lookup.Count);
            Assert.Equal(0, lookup["攻撃"]);
            Assert.Equal(1, lookup["防御"]);
            Assert.Equal(2, lookup["回避"]);
        }

        [Fact]
        public void BuildSkillLookup_HandlesDuplicateNames()
        {
            // Arrange - file with duplicate skill names
            byte[] mhfpac = TestDataFactory.CreateMinimalMhfpac(new[] { "攻撃", "攻撃", "防御" });
            _fileSystem.AddFile("/test/mhfpac.bin", mhfpac);

            // Act
            var lookup = _service.BuildSkillLookup("/test/mhfpac.bin");

            // Assert - first occurrence wins
            Assert.Equal(2, lookup.Count);
            Assert.Equal(0, lookup["攻撃"]); // First one
        }

        #endregion

        #region LoadArmorCsv Tests

        [Fact]
        public void LoadArmorCsv_ParsesEntries()
        {
            // Arrange
            string csv = TestDataFactory.CreateArmorCsv(2);
            _fileSystem.AddFile("/test/Armor.csv", Encoding.GetEncoding("shift-jis").GetBytes(csv));

            // Act
            var entries = _service.LoadArmorCsv("/test/Armor.csv");

            // Assert
            Assert.Equal(10, entries.Count); // 5 classes × 2 entries each

            // Check first entry
            Assert.Equal("頭", entries[0].EquipClass);
            Assert.Equal("TestArmor0", entries[0].Name);
            Assert.True(entries[0].IsMaleEquip);
            Assert.True(entries[0].IsFemaleEquip);
            Assert.Equal(100, entries[0].ZennyCost);
        }

        [Fact]
        public void LoadArmorCsv_ParsesAllArmorClasses()
        {
            // Arrange
            string csv = TestDataFactory.CreateArmorCsv(1);
            _fileSystem.AddFile("/test/Armor.csv", Encoding.GetEncoding("shift-jis").GetBytes(csv));

            // Act
            var entries = _service.LoadArmorCsv("/test/Armor.csv");

            // Assert - should have one entry per class
            Assert.Contains(entries, e => e.EquipClass == "頭");
            Assert.Contains(entries, e => e.EquipClass == "胴");
            Assert.Contains(entries, e => e.EquipClass == "腕");
            Assert.Contains(entries, e => e.EquipClass == "腰");
            Assert.Contains(entries, e => e.EquipClass == "脚");
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullFileSystem_ThrowsArgumentNullException()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
                new DataImportService(null!, _logger));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
                new DataImportService(_fileSystem, null!));
        }

        [Fact]
        public void DefaultConstructor_CreatesValidInstance()
        {
            var service = new DataImportService();
            Assert.NotNull(service);
        }

        #endregion

        #region ImportArmorDataInternal Tests

        [Fact]
        public void ImportArmorDataInternal_WritesOutputFile()
        {
            // Arrange
            byte[] mhfpac = TestDataFactory.CreateMinimalMhfpac(new[] { "攻撃", "防御" });
            byte[] mhfdat = CreateMhfdatWithArmorData(2);
            string csv = TestDataFactory.CreateArmorCsv(2);

            _fileSystem.AddFile("/test/mhfpac.bin", mhfpac);
            _fileSystem.AddFile("/test/mhfdat.bin", mhfdat);
            _fileSystem.AddFile("/test/Armor.csv", Encoding.GetEncoding("shift-jis").GetBytes(csv));

            // Act
            _service.ImportArmorDataInternal("/test/mhfdat.bin", "/test/Armor.csv", "/test/mhfpac.bin");

            // Assert
            Assert.True(_fileSystem.FileExists("output/mhfdat.bin"));
            Assert.True(_logger.ContainsMessage("Loaded"));
            Assert.True(_logger.ContainsMessage("armor entries from CSV"));
        }

        [Fact]
        public void ImportArmorDataInternal_LogsSkippedClassOnMismatch()
        {
            // Arrange
            byte[] mhfpac = TestDataFactory.CreateMinimalMhfpac(new[] { "攻撃" });
            byte[] mhfdat = CreateMhfdatWithArmorData(3); // 3 entries per class
            string csv = TestDataFactory.CreateArmorCsv(2); // Only 2 entries per class

            _fileSystem.AddFile("/test/mhfpac.bin", mhfpac);
            _fileSystem.AddFile("/test/mhfdat.bin", mhfdat);
            _fileSystem.AddFile("/test/Armor.csv", Encoding.GetEncoding("shift-jis").GetBytes(csv));

            // Act
            _service.ImportArmorDataInternal("/test/mhfdat.bin", "/test/Armor.csv", "/test/mhfpac.bin");

            // Assert
            Assert.True(_logger.ContainsMessage("Skipping this class"));
        }

        #endregion

        #region ImportMeleeDataInternal Tests

        [Fact]
        public void ImportMeleeDataInternal_WritesOutputFile()
        {
            // Arrange
            byte[] mhfdat = CreateMhfdatWithWeaponData(3, 0);
            string csv = CreateMeleeCsv(3);

            _fileSystem.AddFile("/test/mhfdat.bin", mhfdat);
            _fileSystem.AddFile("/test/Melee.csv", Encoding.GetEncoding("shift-jis").GetBytes(csv));

            // Act
            _service.ImportMeleeDataInternal("/test/mhfdat.bin", "/test/Melee.csv");

            // Assert
            Assert.True(_fileSystem.FileExists("output/mhfdat.bin"));
            Assert.True(_logger.ContainsMessage("melee weapon entries"));
        }

        [Fact]
        public void ImportMeleeDataInternal_AbortsOnCountMismatch()
        {
            // Arrange
            byte[] mhfdat = CreateMhfdatWithWeaponData(5, 0); // 5 melee entries
            string csv = CreateMeleeCsv(3); // Only 3 CSV entries

            _fileSystem.AddFile("/test/mhfdat.bin", mhfdat);
            _fileSystem.AddFile("/test/Melee.csv", Encoding.GetEncoding("shift-jis").GetBytes(csv));

            // Act
            _service.ImportMeleeDataInternal("/test/mhfdat.bin", "/test/Melee.csv");

            // Assert - should abort without writing output
            Assert.True(_logger.ContainsMessage("Aborting"));
            Assert.False(_fileSystem.FileExists("output/mhfdat.bin"));
        }

        #endregion

        #region ImportRangedDataInternal Tests

        [Fact]
        public void ImportRangedDataInternal_AbortsOnCountMismatch()
        {
            // Arrange - create file where ranged count = 5
            byte[] mhfdat = CreateMhfdatForRangedImport(5);
            string csv = CreateRangedCsv(2); // CSV only has 2 entries

            _fileSystem.AddFile("/test/mhfdat.bin", mhfdat);
            _fileSystem.AddFile("/test/Ranged.csv", Encoding.GetEncoding("shift-jis").GetBytes(csv));

            // Act
            _service.ImportRangedDataInternal("/test/mhfdat.bin", "/test/Ranged.csv");

            // Assert
            Assert.True(_logger.ContainsMessage("Aborting"));
        }

        [Fact]
        public void LoadRangedCsv_ParsesEntries()
        {
            // Arrange
            string csv = CreateRangedCsv(3);
            _fileSystem.AddFile("/test/Ranged.csv", Encoding.GetEncoding("shift-jis").GetBytes(csv));

            // Act
            var entries = _service.LoadRangedCsv("/test/Ranged.csv");

            // Assert
            Assert.Equal(3, entries.Count);
            Assert.Equal("TestRanged0", entries[0].Name);
            Assert.Equal("ヘビィボウガン", entries[0].ClassId);
        }

        #endregion

        #region ImportQuestDataInternal Tests

        [Fact]
        public void ImportQuestDataInternal_AbortsOnCountMismatch()
        {
            // Arrange
            byte[] mhfinf = new byte[0x200000];
            string csv = CreateQuestCsv(5); // Much less than expected

            _fileSystem.AddFile("/test/mhfinf.bin", mhfinf);
            _fileSystem.AddFile("/test/InfQuests.csv", Encoding.GetEncoding("shift-jis").GetBytes(csv));

            // Act
            _service.ImportQuestDataInternal("/test/mhfinf.bin", "/test/InfQuests.csv");

            // Assert
            Assert.True(_logger.ContainsMessage("Aborting"));
        }

        [Fact]
        public void ImportQuestDataInternal_LogsReadOnlyNote()
        {
            // Arrange - need to create exactly the right number of quest entries
            int totalCount = FrontierDataTool.MhfDataOffsets.MhfInf.TotalQuestCount;
            byte[] mhfinf = new byte[0x200000];
            string csv = CreateQuestCsv(totalCount);

            _fileSystem.AddFile("/test/mhfinf.bin", mhfinf);
            _fileSystem.AddFile("/test/InfQuests.csv", Encoding.GetEncoding("shift-jis").GetBytes(csv));

            // Act
            _service.ImportQuestDataInternal("/test/mhfinf.bin", "/test/InfQuests.csv");

            // Assert
            Assert.True(_fileSystem.FileExists("output/mhfinf.bin"));
            Assert.True(_logger.ContainsMessage("read-only"));
        }

        #endregion

        #region ModShopInternal Tests

        [Fact]
        public void ModShopInternal_PatchesItemPrices()
        {
            // Arrange
            byte[] mhfdat = CreateMhfdatWithShopData();
            _fileSystem.AddFile("/test/mhfdat.bin", mhfdat);

            // Act
            _service.ModShopInternal("/test/mhfdat.bin");

            // Assert
            Assert.True(_logger.ContainsMessage("Patching prices"));
        }

        [Fact]
        public void ModShopInternal_LogsNeedleNotFoundGracefully()
        {
            // Arrange - create file without shop needle
            byte[] mhfdat = CreateMhfdatWithShopData(includeShopNeedle: false);
            _fileSystem.AddFile("/test/mhfdat.bin", mhfdat);

            // Act
            _service.ModShopInternal("/test/mhfdat.bin");

            // Assert - should log that needle wasn't found
            Assert.True(_logger.ContainsMessage("Could not find shop needle"));
        }

        #endregion

        #region LoadCsv UTF-8 Encoding Tests

        [Fact]
        public void LoadArmorCsv_DetectsUtf8Encoding()
        {
            // Arrange - create CSV with UTF-8 BOM
            string csv = TestDataFactory.CreateArmorCsv(1);
            byte[] bom = new byte[] { 0xEF, 0xBB, 0xBF };
            byte[] csvBytes = Encoding.UTF8.GetBytes(csv);
            byte[] withBom = new byte[bom.Length + csvBytes.Length];
            Array.Copy(bom, withBom, bom.Length);
            Array.Copy(csvBytes, 0, withBom, bom.Length, csvBytes.Length);

            _fileSystem.AddFile("/test/Armor.csv", withBom);

            // Act
            var entries = _service.LoadArmorCsv("/test/Armor.csv");

            // Assert
            Assert.NotEmpty(entries);
        }

        [Fact]
        public void LoadMeleeCsv_DetectsUtf8Encoding()
        {
            // Arrange
            string csv = CreateMeleeCsv(2);
            byte[] bom = new byte[] { 0xEF, 0xBB, 0xBF };
            byte[] csvBytes = Encoding.UTF8.GetBytes(csv);
            byte[] withBom = new byte[bom.Length + csvBytes.Length];
            Array.Copy(bom, withBom, bom.Length);
            Array.Copy(csvBytes, 0, withBom, bom.Length, csvBytes.Length);

            _fileSystem.AddFile("/test/Melee.csv", withBom);

            // Act
            var entries = _service.LoadMeleeCsv("/test/Melee.csv");

            // Assert
            Assert.Equal(2, entries.Count);
        }

        [Fact]
        public void LoadRangedCsv_DetectsUtf8Encoding()
        {
            // Arrange
            string csv = CreateRangedCsv(2);
            byte[] bom = new byte[] { 0xEF, 0xBB, 0xBF };
            byte[] csvBytes = Encoding.UTF8.GetBytes(csv);
            byte[] withBom = new byte[bom.Length + csvBytes.Length];
            Array.Copy(bom, withBom, bom.Length);
            Array.Copy(csvBytes, 0, withBom, bom.Length, csvBytes.Length);

            _fileSystem.AddFile("/test/Ranged.csv", withBom);

            // Act
            var entries = _service.LoadRangedCsv("/test/Ranged.csv");

            // Assert
            Assert.Equal(2, entries.Count);
        }

        [Fact]
        public void LoadQuestCsv_DetectsUtf8Encoding()
        {
            // Arrange
            string csv = CreateQuestCsv(2);
            byte[] bom = new byte[] { 0xEF, 0xBB, 0xBF };
            byte[] csvBytes = Encoding.UTF8.GetBytes(csv);
            byte[] withBom = new byte[bom.Length + csvBytes.Length];
            Array.Copy(bom, withBom, bom.Length);
            Array.Copy(csvBytes, 0, withBom, bom.Length, csvBytes.Length);

            _fileSystem.AddFile("/test/Quests.csv", withBom);

            // Act
            var entries = _service.LoadQuestCsv("/test/Quests.csv");

            // Assert
            Assert.Equal(2, entries.Count);
        }

        #endregion

        #region Helper Methods

        private static byte[] CreateMhfdatWithArmorData(int entriesPerSlot)
        {
            const int ARMOR_ENTRY_SIZE = 0x48;
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            // Create header area
            bw.Write(new byte[0x200]);

            // Calculate offsets
            int dataStart = 0x200;
            int[] slotStarts = new int[5];
            int currentOffset = dataStart;

            for (int i = 0; i < 5; i++)
            {
                slotStarts[i] = currentOffset;
                currentOffset += entriesPerSlot * ARMOR_ENTRY_SIZE;
            }
            int dataEnd = currentOffset;

            // Write offset pointers
            ms.Seek(0x50, SeekOrigin.Begin);
            for (int i = 0; i < 5; i++)
            {
                bw.Write(slotStarts[i]);
            }

            ms.Seek(0xE8, SeekOrigin.Begin);
            bw.Write(slotStarts[1]); // Head end = Body start

            // Write armor data
            ms.Seek(dataStart, SeekOrigin.Begin);
            for (int slot = 0; slot < 5; slot++)
            {
                for (int i = 0; i < entriesPerSlot; i++)
                {
                    WriteMinimalArmorEntry(bw);
                }
            }

            return ms.ToArray();
        }

        private static byte[] CreateMhfdatForRangedImport(int rangedCount)
        {
            const int RANGED_ENTRY_SIZE = 0x3C;

            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            bw.Write(new byte[0x200]);

            // Set ranged data offsets
            // RangedStart at 0x80 points to start offset pointer
            // RangedEnd at 0x7C points to end offset pointer
            int rangedStart = 0x200;
            int rangedEnd = rangedStart + rangedCount * RANGED_ENTRY_SIZE;

            ms.Seek(0x80, SeekOrigin.Begin);
            bw.Write(rangedStart);

            // Note: RangedEnd is at 0x7C which would normally hold the melee start
            // For this test, we set it directly
            ms.Seek(0x7C, SeekOrigin.Begin);
            bw.Write(rangedEnd);

            // Write ranged weapon data
            ms.Seek(rangedStart, SeekOrigin.Begin);
            for (int i = 0; i < rangedCount; i++)
            {
                WriteMinimalRangedEntry(bw);
            }

            return ms.ToArray();
        }

        private static byte[] CreateMhfdatWithWeaponData(int meleeCount, int rangedCount)
        {
            const int MELEE_ENTRY_SIZE = 0x34;
            const int RANGED_ENTRY_SIZE = 0x3C;

            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            bw.Write(new byte[0x200]);

            int meleeStart = 0x200;
            int meleeEnd = meleeStart + meleeCount * MELEE_ENTRY_SIZE;
            int rangedStart = meleeEnd;
            int rangedEnd = rangedStart + rangedCount * RANGED_ENTRY_SIZE;

            // Write melee offsets
            ms.Seek(0x7C, SeekOrigin.Begin);
            bw.Write(meleeStart);
            ms.Seek(0x90, SeekOrigin.Begin);
            bw.Write(meleeEnd);

            // Write ranged offsets
            ms.Seek(0x80, SeekOrigin.Begin);
            bw.Write(rangedStart);
            // RangedEnd at 0x7C overlaps with MeleeStart, handle separately

            // Write weapon data
            ms.Seek(meleeStart, SeekOrigin.Begin);
            for (int i = 0; i < meleeCount; i++)
            {
                WriteMinimalMeleeEntry(bw);
            }
            for (int i = 0; i < rangedCount; i++)
            {
                WriteMinimalRangedEntry(bw);
            }

            return ms.ToArray();
        }

        private static byte[] CreateMhfdatWithShopData(bool includeShopNeedle = true)
        {
            const int ITEM_ENTRY_SIZE = 0x24;
            const int ARMOR_ENTRY_SIZE = 0x48;

            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            bw.Write(new byte[0x1000]);

            // Item data section
            int itemStart = 0x1000;
            int itemEnd = itemStart + 10 * ITEM_ENTRY_SIZE;

            ms.Seek(0xFC, SeekOrigin.Begin);
            bw.Write(itemStart);
            ms.Seek(0xA70, SeekOrigin.Begin);
            bw.Write(itemEnd);

            // Armor data section - use existing helper structure
            int armorStart = itemEnd;
            ms.Seek(0x50, SeekOrigin.Begin);
            for (int i = 0; i < 5; i++)
            {
                bw.Write(armorStart + i * 2 * ARMOR_ENTRY_SIZE);
            }
            ms.Seek(0xE8, SeekOrigin.Begin);
            bw.Write(armorStart + 2 * ARMOR_ENTRY_SIZE);

            // Write item data with prices
            ms.Seek(itemStart, SeekOrigin.Begin);
            for (int i = 0; i < 10; i++)
            {
                bw.Write(new byte[12]); // First 12 bytes
                bw.Write(5000);         // Buy price at offset 12
                bw.Write(100);          // Sell price at offset 16
                bw.Write(new byte[ITEM_ENTRY_SIZE - 20]);
            }

            // Write minimal armor data
            for (int slot = 0; slot < 5; slot++)
            {
                for (int i = 0; i < 2; i++)
                {
                    WriteMinimalArmorEntry(bw);
                }
            }

            // Optionally add shop needle for ModShop to find
            if (includeShopNeedle)
            {
                byte[] needle = { 0x0F, 0x01, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x01, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00 };
                long needleOffset = ms.Position;
                bw.Write(needle);

                // Write pointer to needle (big-endian)
                byte[] pointerBytes = BitConverter.GetBytes((int)needleOffset);
                Array.Reverse(pointerBytes);
                bw.Write(pointerBytes);
            }

            return ms.ToArray();
        }

        private static void WriteMinimalArmorEntry(BinaryWriter bw)
        {
            bw.Write((short)1);         // ModelIdMale
            bw.Write((short)1);         // ModelIdFemale
            bw.Write((byte)0x03);       // Bitfield
            bw.Write((byte)5);          // Rarity
            bw.Write((byte)7);          // MaxLevel
            bw.Write(new byte[5]);
            bw.Write(100);              // ZennyCost
            bw.Write((short)0);
            bw.Write((short)50);        // BaseDefense
            bw.Write(new byte[5]);      // Resistances
            bw.Write((short)0);
            bw.Write((byte)1);
            bw.Write((byte)3);
            bw.Write(new byte[9]);
            bw.Write((short)0);
            for (int i = 0; i < 5; i++)
            {
                bw.Write((byte)0);
                bw.Write((sbyte)0);
            }
            bw.Write(0);
            bw.Write(0);
            bw.Write(new byte[4]);
            bw.Write(0);
            bw.Write((short)0);
            bw.Write((short)0);
        }

        private static void WriteMinimalMeleeEntry(BinaryWriter bw)
        {
            bw.Write((short)1);
            bw.Write((byte)5);
            bw.Write((byte)0);          // ClassIdx
            bw.Write(1000);
            bw.Write((short)0);
            bw.Write((short)1000);
            bw.Write((short)0);
            bw.Write((sbyte)0);
            bw.Write((byte)0);
            bw.Write((byte)0);
            bw.Write((byte)0);
            bw.Write((byte)0);
            bw.Write((byte)2);
            bw.Write(new byte[3]);
            bw.Write(new byte[6]);
            bw.Write(0);
            bw.Write(0);
            bw.Write(new byte[4]);
            bw.Write(new byte[4]);
            bw.Write(0);
            bw.Write(0);
        }

        private static void WriteMinimalRangedEntry(BinaryWriter bw)
        {
            bw.Write((short)1000);
            bw.Write((byte)5);
            bw.Write((byte)0);
            bw.Write((byte)1);          // ClassIdx (bowgun)
            bw.Write((byte)0);
            bw.Write((byte)0);
            bw.Write((byte)0);
            bw.Write(new byte[12]);
            bw.Write(2000);
            bw.Write((short)300);
            bw.Write((short)0);
            bw.Write((byte)0);
            bw.Write((byte)2);
            bw.Write((sbyte)0);
            bw.Write((byte)0);
            bw.Write((byte)0);
            bw.Write((byte)0);
            bw.Write((byte)0);
            bw.Write((byte)0);
            bw.Write(new byte[20]);
        }

        private static string CreateMeleeCsv(int count)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Name,ModelId,ModelIdData,Rarity,ClassId,ZennyCost,SharpnessId,RawDamage,Defense,Affinity,ElementId,EleDamage,AilmentId,AilDamage,Slots,Unk3,Unk4,Unk5,Unk6,Unk7,Unk8,Unk9,Unk10,Unk11,Unk12,Unk13,Unk14,Unk15,Unk16,Unk17");

            for (int i = 0; i < count; i++)
            {
                sb.AppendLine($"TestMelee{i},{i},we{i:D3},5,大剣,1000,100,1500,10,5,火,300,毒,200,2,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0");
            }

            return sb.ToString();
        }

        private static string CreateRangedCsv(int count)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Name,ModelId,ModelIdData,Rarity,MaxSlotsMaybe,ClassId,Unk2_1,EqType,Unk2_3,Unk3_1,Unk3_2,Unk3_3,Unk3_4,Unk4_1,Unk4_2,Unk4_3,Unk4_4,Unk5_1,Unk5_2,Unk5_3,Unk5_4,ZennyCost,RawDamage,Defense,RecoilMaybe,Slots,Affinity,SortOrderMaybe,Unk6_1,ElementId,EleDamage,Unk6_4,Unk7_1,Unk7_2,Unk7_3,Unk7_4,Unk8_1,Unk8_2,Unk8_3,Unk8_4,Unk9_1,Unk9_2,Unk9_3,Unk9_4,Unk10_1,Unk10_2,Unk10_3,Unk10_4,Unk11_1,Unk11_2,Unk11_3,Unk11_4,Unk12_1,Unk12_2,Unk12_3,Unk12_4");

            for (int i = 0; i < count; i++)
            {
                sb.AppendLine($"TestRanged{i},{i + 1000},wf{i:D3},6,3,ヘビィボウガン,0,5,0,0,0,0,0,0,0,0,0,0,0,0,0,2000,300,5,2,2,10,1,0,水,200,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0");
            }

            return sb.ToString();
        }

        private static string CreateQuestCsv(int count)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Title,TextMain,TextSubA,TextSubB,Unk1,Unk2,Unk3,Unk4,Level,Unk5,CourseType,Unk7,Unk8,Unk9,Unk10,Unk11,Fee,ZennyMain,ZennyKo,ZennySubA,ZennySubB,Time,Unk12,Unk13,Unk14,Unk15,Unk16,Unk17,Unk18,Unk19,Unk20,MainGoalType,MainGoalTarget,MainGoalCount,SubAGoalType,SubAGoalTarget,SubAGoalCount,SubBGoalType,SubBGoalTarget,SubBGoalCount,MainGRP,SubAGRP,SubBGRP");

            for (int i = 0; i < count; i++)
            {
                sb.AppendLine($"TestQuest{i},MainText{i},SubA{i},SubB{i},0,0,0,0,5,0,6,0,0,0,0,0,500,1000,500,200,200,3000,0,0,0,0,0,0,0,0,0,Hunt,{i + 1},1,Delivery,100,5,None,0,0,100,50,50");
            }

            return sb.ToString();
        }

        #endregion
    }
}
