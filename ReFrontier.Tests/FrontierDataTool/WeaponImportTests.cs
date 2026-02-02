#nullable enable

using System.Text;

using FrontierDataTool.Services;
using FrontierDataTool.Structs;

using ReFrontier.Tests.Mocks;

namespace ReFrontier.Tests.DataToolTests
{
    /// <summary>
    /// Tests for weapon and quest import functionality.
    /// </summary>
    public class WeaponImportTests
    {
        private readonly BinaryReaderService _binaryReader;
        private readonly InMemoryFileSystem _fileSystem;
        private readonly TestLogger _logger;
        private readonly DataImportService _importService;

        static WeaponImportTests()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public WeaponImportTests()
        {
            _binaryReader = new BinaryReaderService();
            _fileSystem = new InMemoryFileSystem();
            _logger = new TestLogger();
            _importService = new DataImportService(_fileSystem, _logger);
        }

        #region LookupElementId Tests

        [Theory]
        [InlineData("なし", 0)]
        [InlineData("火", 1)]
        [InlineData("水", 2)]
        [InlineData("雷", 3)]
        [InlineData("龍", 4)]
        [InlineData("氷", 5)]
        [InlineData("炎", 6)]
        [InlineData("Unknown", 0)]
        [InlineData(null, 0)]
        [InlineData("", 0)]
        public void LookupElementId_ReturnsCorrectValue(string? name, byte expected)
        {
            byte result = BinaryReaderService.LookupElementId(name);
            Assert.Equal(expected, result);
        }

        #endregion

        #region LookupAilmentId Tests

        [Theory]
        [InlineData("なし", 0)]
        [InlineData("毒", 1)]
        [InlineData("麻痺", 2)]
        [InlineData("睡眠", 3)]
        [InlineData("爆破", 4)]
        [InlineData("Unknown", 0)]
        [InlineData(null, 0)]
        [InlineData("", 0)]
        public void LookupAilmentId_ReturnsCorrectValue(string? name, byte expected)
        {
            byte result = BinaryReaderService.LookupAilmentId(name);
            Assert.Equal(expected, result);
        }

        #endregion

        #region LookupWeaponClassId Tests

        [Theory]
        [InlineData("大剣", 0)]
        [InlineData("ヘビィボウガン", 1)]
        [InlineData("ハンマー", 2)]
        [InlineData("ランス", 3)]
        [InlineData("片手剣", 4)]
        [InlineData("ライトボウガン", 5)]
        [InlineData("双剣", 6)]
        [InlineData("太刀", 7)]
        [InlineData("狩猟笛", 8)]
        [InlineData("ガンランス", 9)]
        [InlineData("弓", 10)]
        [InlineData("穿龍棍", 11)]
        [InlineData("スラッシュアックスＦ", 12)]
        [InlineData("マグネットスパイク", 13)]
        [InlineData("Unknown", 0)]
        [InlineData(null, 0)]
        [InlineData("", 0)]
        public void LookupWeaponClassId_ReturnsCorrectValue(string? name, byte expected)
        {
            byte result = BinaryReaderService.LookupWeaponClassId(name);
            Assert.Equal(expected, result);
        }

        #endregion

        #region LookupQuestType Tests

        [Theory]
        [InlineData("None", 0)]
        [InlineData("Hunt", 0x00000001)]
        [InlineData("Capture", 0x00000101)]
        [InlineData("Slay", 0x00000201)]
        [InlineData("Delivery", 0x00000002)]
        [InlineData("GuildFlag", 0x00001002)]
        [InlineData("Damaging", 0x00008004)]
        [InlineData("00000001", 0x00000001)]
        [InlineData("00000101", 0x00000101)]
        [InlineData("00008004", 0x00008004)]  // Hex format from export (8 digits with leading zeros)
        [InlineData("Unknown", 0)]
        [InlineData(null, 0)]
        [InlineData("", 0)]
        public void LookupQuestType_ReturnsCorrectValue(string? typeName, int expected)
        {
            int result = BinaryReaderService.LookupQuestType(typeName);
            Assert.Equal(expected, result);
        }

        #endregion

        #region WriteMeleeWeaponEntry Tests

        [Fact]
        public void WriteMeleeWeaponEntry_WritesCorrectSize()
        {
            var entry = new MeleeWeaponEntry
            {
                ModelId = 100,
                Rarity = 5,
                ClassId = "大剣",
                ZennyCost = 5000,
                RawDamage = 1000,
                ElementId = "火",
                EleDamage = 300,
                AilmentId = "毒",
                AilDamage = 200
            };

            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            _binaryReader.WriteMeleeWeaponEntry(bw, entry);

            Assert.Equal(BinaryReaderService.MELEE_WEAPON_ENTRY_SIZE, ms.Length);
        }

        [Fact]
        public void WriteMeleeWeaponEntry_RoundTrip()
        {
            var originalEntry = new MeleeWeaponEntry
            {
                ModelId = 500,
                Rarity = 7,
                ClassId = "太刀",
                ZennyCost = 10000,
                SharpnessId = 123,
                RawDamage = 1500,
                Defense = 20,
                Affinity = 15,
                ElementId = "雷",
                EleDamage = 350,
                AilmentId = "麻痺",
                AilDamage = 250,
                Slots = 2,
                WeaponAttribute = 1,
                ParticleEffect = 2,
                UpgradePath = 100,
                DrawnModelId = 200,
                EqType = 300,
                Length = 1000,
                WeaponType = 2000,
                VisualEffects = 50,
                Unk11 = 60,
                Unk12 = 3,
                Unk13 = 4,
                Unk14 = 5,
                ZeroF = 6,
                Unk16 = 3000,
                ZenithSkill = 4000
            };

            // Write
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            _binaryReader.WriteMeleeWeaponEntry(bw, originalEntry);

            // Read back
            ms.Position = 0;
            using var br = new BinaryReader(ms);
            var readEntry = _binaryReader.ReadMeleeWeaponEntry(br);

            // Assert key fields match
            Assert.Equal(originalEntry.ModelId, readEntry.ModelId);
            Assert.Equal(originalEntry.Rarity, readEntry.Rarity);
            Assert.Equal(originalEntry.ClassId, readEntry.ClassId);
            Assert.Equal(originalEntry.ZennyCost, readEntry.ZennyCost);
            Assert.Equal(originalEntry.SharpnessId, readEntry.SharpnessId);
            Assert.Equal(originalEntry.RawDamage, readEntry.RawDamage);
            Assert.Equal(originalEntry.Defense, readEntry.Defense);
            Assert.Equal(originalEntry.Affinity, readEntry.Affinity);
            Assert.Equal(originalEntry.ElementId, readEntry.ElementId);
            Assert.Equal(originalEntry.EleDamage, readEntry.EleDamage);
            Assert.Equal(originalEntry.AilmentId, readEntry.AilmentId);
            Assert.Equal(originalEntry.AilDamage, readEntry.AilDamage);
            Assert.Equal(originalEntry.Slots, readEntry.Slots);
        }

        #endregion

        #region WriteRangedWeaponEntry Tests

        [Fact]
        public void WriteRangedWeaponEntry_WritesCorrectSize()
        {
            var entry = new RangedWeaponEntry
            {
                ModelId = 1500,
                Rarity = 6,
                ClassId = "ヘビィボウガン",
                ZennyCost = 8000,
                RawDamage = 300,
                ElementId = "水",
                EleDamage = 200,
                EqType = "5"
            };

            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            _binaryReader.WriteRangedWeaponEntry(bw, entry);

            Assert.Equal(BinaryReaderService.RANGED_WEAPON_ENTRY_SIZE, ms.Length);
        }

        [Fact]
        public void WriteRangedWeaponEntry_RoundTrip()
        {
            var originalEntry = new RangedWeaponEntry
            {
                ModelId = 1500,
                Rarity = 6,
                MaxSlotsMaybe = 3,
                ClassId = "ライトボウガン",
                Unk05 = 1,
                EqType = "10",
                Unk07 = 2,
                ZennyCost = 8000,
                RawDamage = 320,
                Defense = 10,
                RecoilMaybe = 2,
                Slots = 2,
                Affinity = 10,
                SortOrderMaybe = 5,
                WeaponAttribute = 3,
                ElementId = "氷",
                EleDamage = 180,
                Unk23 = 4
            };

            // Write
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            _binaryReader.WriteRangedWeaponEntry(bw, originalEntry);

            // Read back
            ms.Position = 0;
            using var br = new BinaryReader(ms);
            var readEntry = _binaryReader.ReadRangedWeaponEntry(br);

            // Assert key fields match
            Assert.Equal(originalEntry.ModelId, readEntry.ModelId);
            Assert.Equal(originalEntry.Rarity, readEntry.Rarity);
            Assert.Equal(originalEntry.MaxSlotsMaybe, readEntry.MaxSlotsMaybe);
            Assert.Equal(originalEntry.ClassId, readEntry.ClassId);
            Assert.Equal(originalEntry.EqType, readEntry.EqType);
            Assert.Equal(originalEntry.ZennyCost, readEntry.ZennyCost);
            Assert.Equal(originalEntry.RawDamage, readEntry.RawDamage);
            Assert.Equal(originalEntry.Defense, readEntry.Defense);
            Assert.Equal(originalEntry.Slots, readEntry.Slots);
            Assert.Equal(originalEntry.Affinity, readEntry.Affinity);
            Assert.Equal(originalEntry.ElementId, readEntry.ElementId);
            Assert.Equal(originalEntry.EleDamage, readEntry.EleDamage);
        }

        #endregion

        #region LoadMeleeCsv Tests

        [Fact]
        public void LoadMeleeCsv_ParsesEntries()
        {
            // Arrange
            string csv = CreateMeleeCsv(3);
            _fileSystem.AddFile("/test/Melee.csv", Encoding.GetEncoding("shift-jis").GetBytes(csv));

            // Act
            var entries = _importService.LoadMeleeCsv("/test/Melee.csv");

            // Assert
            Assert.Equal(3, entries.Count);
            Assert.Equal("TestMelee0", entries[0].Name);
            Assert.Equal("大剣", entries[0].ClassId);
            Assert.Equal(1000, entries[0].ZennyCost);
            Assert.Equal(1500, entries[0].RawDamage);
            Assert.Equal("火", entries[0].ElementId);
            Assert.Equal(300, entries[0].EleDamage);
        }

        #endregion

        #region LoadRangedCsv Tests

        [Fact]
        public void LoadRangedCsv_ParsesEntries()
        {
            // Arrange
            string csv = CreateRangedCsv(2);
            _fileSystem.AddFile("/test/Ranged.csv", Encoding.GetEncoding("shift-jis").GetBytes(csv));

            // Act
            var entries = _importService.LoadRangedCsv("/test/Ranged.csv");

            // Assert
            Assert.Equal(2, entries.Count);
            Assert.Equal("TestRanged0", entries[0].Name);
            Assert.Equal("ヘビィボウガン", entries[0].ClassId);
            Assert.Equal(2000, entries[0].ZennyCost);
            Assert.Equal(300, entries[0].RawDamage);
        }

        #endregion

        #region LoadQuestCsv Tests

        [Fact]
        public void LoadQuestCsv_ParsesEntries()
        {
            // Arrange
            string csv = CreateQuestCsv(2);
            _fileSystem.AddFile("/test/InfQuests.csv", Encoding.GetEncoding("shift-jis").GetBytes(csv));

            // Act
            var entries = _importService.LoadQuestCsv("/test/InfQuests.csv");

            // Assert
            Assert.Equal(2, entries.Count);
            Assert.Equal("TestQuest0", entries[0].Title);
            Assert.Equal(5, entries[0].Level);
            Assert.Equal(500, entries[0].Fee);
            Assert.Equal(1000, entries[0].ZennyMain);
            Assert.Equal("Hunt", entries[0].MainGoalType);
        }

        #endregion

        #region Helper Methods

        private static string CreateMeleeCsv(int count)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Name,ModelId,ModelIdData,Rarity,ClassId,ZennyCost,SharpnessId,RawDamage,Defense,Affinity,ElementId,EleDamage,AilmentId,AilDamage,Slots,WeaponAttribute,ParticleEffect,UpgradePath,DrawnModelId,EqType,Length,WeaponType,VisualEffects,Unk11,Unk12,Unk13,Unk14,ZeroF,Unk16,ZenithSkill");

            for (int i = 0; i < count; i++)
            {
                sb.AppendLine($"TestMelee{i},{i},we{i:D3},5,大剣,1000,100,1500,10,5,火,300,毒,200,2,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0");
            }

            return sb.ToString();
        }

        private static string CreateRangedCsv(int count)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Name,ModelId,ModelIdData,Rarity,MaxSlotsMaybe,ClassId,Unk05,EqType,Unk07,Unk08_1,Unk08_2,Unk08_3,Unk08_4,WeaponType1,WeaponType2,WeaponType3,WeaponType4,Unk10_1,Unk10_2,Unk10_3,Unk10_4,ZennyCost,RawDamage,Defense,RecoilMaybe,Slots,Affinity,SortOrderMaybe,WeaponAttribute,ElementId,EleDamage,Unk23,Unk24_1,Unk24_2,Unk24_3,Unk24_4,Bullet1,Bullet2,Bullet3,Bullet4,Unk2C_1,Unk2C_2,Unk2C_3,Unk2C_4,Unk30_1,Unk30_2,Unk30_3,Unk30_4,Unk34_1,Unk34_2,Unk34_3,Unk34_4,Unk38_1,Unk38_2,Unk38_3,Unk38_4");

            for (int i = 0; i < count; i++)
            {
                sb.AppendLine($"TestRanged{i},{i + 1000},wf{i:D3},6,3,ヘビィボウガン,0,5,0,0,0,0,0,0,0,0,0,0,0,0,0,2000,300,5,2,2,10,1,0,水,200,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0");
            }

            return sb.ToString();
        }

        private static string CreateQuestCsv(int count)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Title,TextMain,TextSubA,TextSubB,Unk1,Unk2,Unk3,Unk4,Level,Unk5,CourseType,Unk7,Unk8,Unk9,Unk10,MaxPlayers,Fee,ZennyMain,ZennyKo,ZennySubA,ZennySubB,Time,MapId,QuestStringPtr,QuestRestrictions,QuestId,MainGoalType,MainGoalTarget,MainGoalCount,SubAGoalType,SubAGoalTarget,SubAGoalCount,SubBGoalType,SubBGoalTarget,SubBGoalCount,MainGRP,SubAGRP,SubBGRP");

            for (int i = 0; i < count; i++)
            {
                sb.AppendLine($"TestQuest{i},MainText{i},SubA{i},SubB{i},0,0,0,0,5,0,6,0,0,0,0,4,500,1000,500,200,200,3000,1,0,0,{i + 1},Hunt,{i + 1},1,Delivery,100,5,None,0,0,100,50,50");
            }

            return sb.ToString();
        }

        #endregion
    }
}
