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
        [InlineData("Kill", 0x00000201)]
        [InlineData("Delivery", 0x00000002)]
        [InlineData("GuildFlag", 0x00001002)]
        [InlineData("Damging", 0x00008004)]
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
                Unk3 = 1,
                Unk4 = 2,
                Unk5 = 100,
                Unk6 = 200,
                Unk7 = 300,
                Unk8 = 1000,
                Unk9 = 2000,
                Unk10 = 50,
                Unk11 = 60,
                Unk12 = 3,
                Unk13 = 4,
                Unk14 = 5,
                Unk15 = 6,
                Unk16 = 3000,
                Unk17 = 4000
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
                Unk2_1 = 1,
                EqType = "10",
                Unk2_3 = 2,
                ZennyCost = 8000,
                RawDamage = 320,
                Defense = 10,
                RecoilMaybe = 2,
                Slots = 2,
                Affinity = 10,
                SortOrderMaybe = 5,
                Unk6_1 = 3,
                ElementId = "氷",
                EleDamage = 180,
                Unk6_4 = 4
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
            sb.AppendLine("Name\tModelId\tModelIdData\tRarity\tClassId\tZennyCost\tSharpnessId\tRawDamage\tDefense\tAffinity\tElementId\tEleDamage\tAilmentId\tAilDamage\tSlots\tUnk3\tUnk4\tUnk5\tUnk6\tUnk7\tUnk8\tUnk9\tUnk10\tUnk11\tUnk12\tUnk13\tUnk14\tUnk15\tUnk16\tUnk17");

            for (int i = 0; i < count; i++)
            {
                sb.AppendLine($"TestMelee{i}\t{i}\twe{i:D3}\t5\t大剣\t1000\t100\t1500\t10\t5\t火\t300\t毒\t200\t2\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0");
            }

            return sb.ToString();
        }

        private static string CreateRangedCsv(int count)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Name\tModelId\tModelIdData\tRarity\tMaxSlotsMaybe\tClassId\tUnk2_1\tEqType\tUnk2_3\tUnk3_1\tUnk3_2\tUnk3_3\tUnk3_4\tUnk4_1\tUnk4_2\tUnk4_3\tUnk4_4\tUnk5_1\tUnk5_2\tUnk5_3\tUnk5_4\tZennyCost\tRawDamage\tDefense\tRecoilMaybe\tSlots\tAffinity\tSortOrderMaybe\tUnk6_1\tElementId\tEleDamage\tUnk6_4\tUnk7_1\tUnk7_2\tUnk7_3\tUnk7_4\tUnk8_1\tUnk8_2\tUnk8_3\tUnk8_4\tUnk9_1\tUnk9_2\tUnk9_3\tUnk9_4\tUnk10_1\tUnk10_2\tUnk10_3\tUnk10_4\tUnk11_1\tUnk11_2\tUnk11_3\tUnk11_4\tUnk12_1\tUnk12_2\tUnk12_3\tUnk12_4");

            for (int i = 0; i < count; i++)
            {
                sb.AppendLine($"TestRanged{i}\t{i + 1000}\twf{i:D3}\t6\t3\tヘビィボウガン\t0\t5\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t2000\t300\t5\t2\t2\t10\t1\t0\t水\t200\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0");
            }

            return sb.ToString();
        }

        private static string CreateQuestCsv(int count)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Title\tTextMain\tTextSubA\tTextSubB\tUnk1\tUnk2\tUnk3\tUnk4\tLevel\tUnk5\tCourseType\tUnk7\tUnk8\tUnk9\tUnk10\tUnk11\tFee\tZennyMain\tZennyKo\tZennySubA\tZennySubB\tTime\tUnk12\tUnk13\tUnk14\tUnk15\tUnk16\tUnk17\tUnk18\tUnk19\tUnk20\tMainGoalType\tMainGoalTarget\tMainGoalCount\tSubAGoalType\tSubAGoalTarget\tSubAGoalCount\tSubBGoalType\tSubBGoalTarget\tSubBGoalCount\tMainGRP\tSubAGRP\tSubBGRP");

            for (int i = 0; i < count; i++)
            {
                sb.AppendLine($"TestQuest{i}\tMainText{i}\tSubA{i}\tSubB{i}\t0\t0\t0\t0\t5\t0\t6\t0\t0\t0\t0\t0\t500\t1000\t500\t200\t200\t3000\t0\t0\t0\t0\t0\t0\t0\t0\t0\tHunt\t{i + 1}\t1\tDelivery\t100\t5\tNone\t0\t0\t100\t50\t50");
            }

            return sb.ToString();
        }

        #endregion
    }
}
