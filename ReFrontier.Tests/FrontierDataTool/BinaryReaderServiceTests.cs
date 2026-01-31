#nullable enable

using System.Text;

using FrontierDataTool.Services;
using FrontierDataTool.Structs;

namespace ReFrontier.Tests.DataToolTests
{
    /// <summary>
    /// Tests for BinaryReaderService.
    /// </summary>
    public class BinaryReaderServiceTests
    {
        private readonly BinaryReaderService _service;

        static BinaryReaderServiceTests()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public BinaryReaderServiceTests()
        {
            _service = new BinaryReaderService();
        }

        #region ReadArmorEntry Tests

        [Fact]
        public void ReadArmorEntry_ParsesBasicFields()
        {
            // Arrange
            var skillLookup = new List<KeyValuePair<int, string>>
            {
                new(0, "攻撃"),
                new(1, "防御"),
            };

            byte[] data = CreateArmorEntryBytes(
                modelIdMale: 100,
                modelIdFemale: 101,
                bitfield: 0x0F, // Male, Female, Blade, Gunner
                rarity: 5,
                zennyCost: 5000,
                baseDefense: 150
            );

            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);

            // Act
            var entry = _service.ReadArmorEntry(br, skillLookup, "頭");

            // Assert
            Assert.Equal("頭", entry.EquipClass);
            Assert.Equal(100, entry.ModelIdMale);
            Assert.Equal(101, entry.ModelIdFemale);
            Assert.True(entry.IsMaleEquip);
            Assert.True(entry.IsFemaleEquip);
            Assert.True(entry.IsBladeEquip);
            Assert.True(entry.IsGunnerEquip);
            Assert.Equal(5, entry.Rarity);
            Assert.Equal(5000, entry.ZennyCost);
            Assert.Equal(150, entry.BaseDefense);
        }

        [Fact]
        public void ReadArmorEntry_ParsesBitfieldCorrectly()
        {
            // Arrange
            var skillLookup = new List<KeyValuePair<int, string>> { new(0, "") };

            // Bitfield: 0b10101010 = Bit1, Bit3, Bit5, Bit7 set
            byte[] data = CreateArmorEntryBytes(bitfield: 0xAA);

            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);

            // Act
            var entry = _service.ReadArmorEntry(br, skillLookup, "胴");

            // Assert
            Assert.False(entry.IsMaleEquip);    // Bit 0
            Assert.True(entry.IsFemaleEquip);   // Bit 1
            Assert.False(entry.IsBladeEquip);   // Bit 2
            Assert.True(entry.IsGunnerEquip);   // Bit 3
            Assert.False(entry.Bool1);          // Bit 4
            Assert.True(entry.IsSPEquip);       // Bit 5
            Assert.False(entry.Bool3);          // Bit 6
            Assert.True(entry.Bool4);           // Bit 7
        }

        [Fact]
        public void ReadArmorEntry_HandlesSkillLookup()
        {
            // Arrange
            var skillLookup = new List<KeyValuePair<int, string>>
            {
                new(0, "攻撃"),
                new(1, "防御"),
                new(2, "回避"),
            };

            byte[] data = CreateArmorEntryBytes(skillIndices: new byte[] { 0, 1, 2, 0, 1 });

            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);

            // Act
            var entry = _service.ReadArmorEntry(br, skillLookup, "腕");

            // Assert
            Assert.Equal("攻撃", entry.SkillId1);
            Assert.Equal("防御", entry.SkillId2);
            Assert.Equal("回避", entry.SkillId3);
            Assert.Equal("攻撃", entry.SkillId4);
            Assert.Equal("防御", entry.SkillId5);
        }

        [Fact]
        public void ReadArmorEntry_HandlesOutOfRangeSkillIndex()
        {
            // Arrange
            var skillLookup = new List<KeyValuePair<int, string>>
            {
                new(0, "攻撃"),
            };

            // Skill index 5 is out of range
            byte[] data = CreateArmorEntryBytes(skillIndices: new byte[] { 5, 0, 0, 0, 0 });

            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);

            // Act
            var entry = _service.ReadArmorEntry(br, skillLookup, "腰");

            // Assert
            Assert.Equal("", entry.SkillId1); // Out of range returns empty
            Assert.Equal("攻撃", entry.SkillId2);
        }

        #endregion

        #region ReadMeleeWeaponEntry Tests

        [Fact]
        public void ReadMeleeWeaponEntry_ParsesBasicFields()
        {
            // Arrange
            byte[] data = CreateMeleeWeaponEntryBytes(
                modelId: 500,
                rarity: 7,
                classIdx: 0, // 大剣
                zennyCost: 10000,
                rawDamage: 1500,
                affinity: 15
            );

            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);

            // Act
            var entry = _service.ReadMeleeWeaponEntry(br);

            // Assert
            Assert.Equal(500, entry.ModelId);
            Assert.Equal("we500", entry.ModelIdData);
            Assert.Equal(7, entry.Rarity);
            Assert.Equal("大剣", entry.ClassId);
            Assert.Equal(10000, entry.ZennyCost);
            Assert.Equal(1500, entry.RawDamage);
            Assert.Equal(15, entry.Affinity);
        }

        [Fact]
        public void ReadMeleeWeaponEntry_ParsesElement()
        {
            // Arrange
            byte[] data = CreateMeleeWeaponEntryBytes(
                elementIdx: 1, // 火
                eleDamage: 30  // Will be multiplied by 10 = 300
            );

            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);

            // Act
            var entry = _service.ReadMeleeWeaponEntry(br);

            // Assert
            Assert.Equal("火", entry.ElementId);
            Assert.Equal(300, entry.EleDamage);
        }

        [Fact]
        public void ReadMeleeWeaponEntry_ParsesAilment()
        {
            // Arrange
            byte[] data = CreateMeleeWeaponEntryBytes(
                ailmentIdx: 2, // 麻痺
                ailDamage: 25  // Will be multiplied by 10 = 250
            );

            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);

            // Act
            var entry = _service.ReadMeleeWeaponEntry(br);

            // Assert
            Assert.Equal("麻痺", entry.AilmentId);
            Assert.Equal(250, entry.AilDamage);
        }

        #endregion

        #region ReadRangedWeaponEntry Tests

        [Fact]
        public void ReadRangedWeaponEntry_ParsesBasicFields()
        {
            // Arrange
            byte[] data = CreateRangedWeaponEntryBytes(
                modelId: 1500,
                rarity: 6,
                classIdx: 1, // ヘビィボウガン
                zennyCost: 8000
            );

            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);

            // Act
            var entry = _service.ReadRangedWeaponEntry(br);

            // Assert
            Assert.Equal(1500, entry.ModelId);
            Assert.Equal("wf500", entry.ModelIdData);
            Assert.Equal(6, entry.Rarity);
            Assert.Equal("ヘビィボウガン", entry.ClassId);
            Assert.Equal(8000, entry.ZennyCost);
        }

        #endregion

        #region GetModelIdData Tests

        [Theory]
        [InlineData(0, "we000")]
        [InlineData(999, "we999")]
        [InlineData(1000, "wf000")]
        [InlineData(1999, "wf999")]
        [InlineData(2000, "wg000")]
        [InlineData(3000, "wh000")]
        [InlineData(4000, "wi000")]
        [InlineData(6000, "wk000")]
        [InlineData(7000, "wl000")]
        [InlineData(8000, "wm000")]
        [InlineData(9000, "wg000")]
        [InlineData(10000, "Unmapped")]
        public void GetModelIdData_ReturnsCorrectPrefix(int id, string expected)
        {
            string result = BinaryReaderService.GetModelIdData(id);
            Assert.Equal(expected, result);
        }

        #endregion

        #region ReconstructArmorBitfield Tests

        [Fact]
        public void ReconstructArmorBitfield_AllFalse_ReturnsZero()
        {
            var entry = new ArmorDataEntry();
            byte result = BinaryReaderService.ReconstructArmorBitfield(entry);
            Assert.Equal(0, result);
        }

        [Fact]
        public void ReconstructArmorBitfield_AllTrue_ReturnsFF()
        {
            var entry = new ArmorDataEntry
            {
                IsMaleEquip = true,
                IsFemaleEquip = true,
                IsBladeEquip = true,
                IsGunnerEquip = true,
                Bool1 = true,
                IsSPEquip = true,
                Bool3 = true,
                Bool4 = true
            };
            byte result = BinaryReaderService.ReconstructArmorBitfield(entry);
            Assert.Equal(0xFF, result);
        }

        [Fact]
        public void ReconstructArmorBitfield_SpecificBits()
        {
            var entry = new ArmorDataEntry
            {
                IsMaleEquip = true,    // Bit 0
                IsBladeEquip = true,   // Bit 2
                IsSPEquip = true       // Bit 5
            };
            byte result = BinaryReaderService.ReconstructArmorBitfield(entry);
            Assert.Equal(0b00100101, result); // 0x25
        }

        #endregion

        #region LookupSkillId Tests

        [Fact]
        public void LookupSkillId_FoundSkill_ReturnsId()
        {
            var lookup = new Dictionary<string, byte>
            {
                { "攻撃", 1 },
                { "防御", 2 }
            };

            byte result = BinaryReaderService.LookupSkillId("攻撃", lookup);
            Assert.Equal(1, result);
        }

        [Fact]
        public void LookupSkillId_NotFound_ReturnsZero()
        {
            var lookup = new Dictionary<string, byte> { { "攻撃", 1 } };

            byte result = BinaryReaderService.LookupSkillId("Unknown", lookup);
            Assert.Equal(0, result);
        }

        [Fact]
        public void LookupSkillId_NullOrEmpty_ReturnsZero()
        {
            var lookup = new Dictionary<string, byte> { { "攻撃", 1 } };

            Assert.Equal(0, BinaryReaderService.LookupSkillId(null, lookup));
            Assert.Equal(0, BinaryReaderService.LookupSkillId("", lookup));
        }

        #endregion

        #region WriteArmorEntry Tests

        [Fact]
        public void WriteArmorEntry_WritesCorrectSize()
        {
            var entry = new ArmorDataEntry
            {
                EquipClass = "頭",
                ModelIdMale = 1,
                ModelIdFemale = 2,
                Rarity = 5,
                ZennyCost = 1000
            };
            var skillLookup = new Dictionary<string, byte>();

            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            _service.WriteArmorEntry(bw, entry, skillLookup);

            Assert.Equal(BinaryReaderService.ARMOR_ENTRY_SIZE, ms.Length);
        }

        [Fact]
        public void WriteArmorEntry_RoundTrip()
        {
            var skillLookup = new List<KeyValuePair<int, string>>
            {
                new(0, "攻撃"),
                new(1, "防御"),
            };
            var skillDict = new Dictionary<string, byte>
            {
                { "攻撃", 0 },
                { "防御", 1 }
            };

            var originalEntry = new ArmorDataEntry
            {
                EquipClass = "頭",
                ModelIdMale = 100,
                ModelIdFemale = 101,
                IsMaleEquip = true,
                IsFemaleEquip = true,
                Rarity = 7,
                MaxLevel = 10,
                ZennyCost = 5000,
                BaseDefense = 200,
                FireRes = 5,
                WaterRes = -3,
                SkillId1 = "攻撃",
                SkillPts1 = 10,
                SkillId2 = "防御",
                SkillPts2 = -5
            };

            // Write
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            _service.WriteArmorEntry(bw, originalEntry, skillDict);

            // Read back
            ms.Position = 0;
            using var br = new BinaryReader(ms);
            var readEntry = _service.ReadArmorEntry(br, skillLookup, "頭");

            // Assert key fields match
            Assert.Equal(originalEntry.ModelIdMale, readEntry.ModelIdMale);
            Assert.Equal(originalEntry.ModelIdFemale, readEntry.ModelIdFemale);
            Assert.Equal(originalEntry.IsMaleEquip, readEntry.IsMaleEquip);
            Assert.Equal(originalEntry.Rarity, readEntry.Rarity);
            Assert.Equal(originalEntry.ZennyCost, readEntry.ZennyCost);
            Assert.Equal(originalEntry.BaseDefense, readEntry.BaseDefense);
            Assert.Equal(originalEntry.SkillId1, readEntry.SkillId1);
            Assert.Equal(originalEntry.SkillPts1, readEntry.SkillPts1);
        }

        #endregion

        #region StringFromPointer Tests

        [Fact]
        public void StringFromPointer_ReadsString()
        {
            // Create data with pointer to string
            string testString = "テスト文字列";
            byte[] stringBytes = Encoding.GetEncoding("shift-jis").GetBytes(testString);

            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            // Write pointer (pointing to offset 4, after the pointer)
            bw.Write(4);
            // Write string
            bw.Write(stringBytes);
            bw.Write((byte)0); // Null terminator

            ms.Position = 0;
            using var br = new BinaryReader(ms);

            // Act
            string result = _service.StringFromPointer(br);

            // Assert
            Assert.Equal(testString, result);
            Assert.Equal(4, br.BaseStream.Position); // Position should be after pointer
        }

        [Fact]
        public void StringFromPointer_ReplacesNewlines()
        {
            string testString = "Line1\nLine2";
            byte[] stringBytes = Encoding.GetEncoding("shift-jis").GetBytes(testString);

            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            bw.Write(4);
            bw.Write(stringBytes);
            bw.Write((byte)0);

            ms.Position = 0;
            using var br = new BinaryReader(ms);

            string result = _service.StringFromPointer(br);

            Assert.Equal("Line1\\nLine2", result);
        }

        #endregion

        #region Helper Methods

        private static byte[] CreateArmorEntryBytes(
            short modelIdMale = 0,
            short modelIdFemale = 0,
            byte bitfield = 0,
            byte rarity = 1,
            int zennyCost = 100,
            short baseDefense = 50,
            byte[]? skillIndices = null)
        {
            // Total size: 0x48 (72) bytes
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            bw.Write(modelIdMale);      // 0x00-0x01: Int16
            bw.Write(modelIdFemale);    // 0x02-0x03: Int16
            bw.Write(bitfield);         // 0x04: Byte (bitfield)
            bw.Write(rarity);           // 0x05: Byte (Rarity)
            bw.Write((byte)7);          // 0x06: Byte (MaxLevel)
            bw.Write((byte)0);          // 0x07: Byte (Unk1_1)
            bw.Write((byte)0);          // 0x08: Byte (Unk1_2)
            bw.Write((byte)0);          // 0x09: Byte (Unk1_3)
            bw.Write((byte)0);          // 0x0A: Byte (Unk1_4)
            bw.Write((byte)0);          // 0x0B: Byte (Unk2)
            bw.Write(zennyCost);        // 0x0C-0x0F: Int32
            bw.Write((short)0);         // 0x10-0x11: Int16 (Unk3)
            bw.Write(baseDefense);      // 0x12-0x13: Int16
            bw.Write((sbyte)0);         // 0x14: SByte (FireRes)
            bw.Write((sbyte)0);         // 0x15: SByte (WaterRes)
            bw.Write((sbyte)0);         // 0x16: SByte (ThunderRes)
            bw.Write((sbyte)0);         // 0x17: SByte (DragonRes)
            bw.Write((sbyte)0);         // 0x18: SByte (IceRes)
            bw.Write((short)0);         // 0x19-0x1A: Int16 (Unk3_1)
            bw.Write((byte)0);          // 0x1B: Byte (BaseSlots)
            bw.Write((byte)0);          // 0x1C: Byte (MaxSlots)
            bw.Write((byte)0);          // 0x1D: Byte (SthEventCrown)
            bw.Write((byte)0);          // 0x1E: Byte (Unk5)
            bw.Write((byte)0);          // 0x1F: Byte (Unk6)
            bw.Write((byte)0);          // 0x20: Byte (Unk7_1)
            bw.Write((byte)0);          // 0x21: Byte (Unk7_2)
            bw.Write((byte)0);          // 0x22: Byte (Unk7_3)
            bw.Write((byte)0);          // 0x23: Byte (Unk7_4)
            bw.Write((byte)0);          // 0x24: Byte (Unk8_1)
            bw.Write((byte)0);          // 0x25: Byte (Unk8_2)
            bw.Write((byte)0);          // 0x26: Byte (Unk8_3)
            bw.Write((byte)0);          // 0x27: Byte (Unk8_4)
            bw.Write((short)0);         // 0x28-0x29: Int16 (Unk10)

            // Skills: 5 skills × (1 byte index + 1 byte points) = 10 bytes
            skillIndices ??= new byte[] { 0, 0, 0, 0, 0 };
            for (int i = 0; i < 5; i++)
            {
                bw.Write(skillIndices[i]);  // Skill index
                bw.Write((sbyte)5);         // Skill points
            }
            // 0x2A-0x33: Skills (10 bytes)

            bw.Write(0);                // 0x34-0x37: Int32 (SthHiden)
            bw.Write(0);                // 0x38-0x3B: Int32 (Unk12)
            bw.Write((byte)0);          // 0x3C: Byte (Unk13)
            bw.Write((byte)0);          // 0x3D: Byte (Unk14)
            bw.Write((byte)0);          // 0x3E: Byte (Unk15)
            bw.Write((byte)0);          // 0x3F: Byte (Unk16)
            bw.Write(0);                // 0x40-0x43: Int32 (Unk17)
            bw.Write((short)0);         // 0x44-0x45: Int16 (Unk18)
            bw.Write((short)0);         // 0x46-0x47: Int16 (Unk19)

            return ms.ToArray();
        }

        private static byte[] CreateMeleeWeaponEntryBytes(
            short modelId = 0,
            byte rarity = 1,
            byte classIdx = 0,
            int zennyCost = 1000,
            short rawDamage = 100,
            sbyte affinity = 0,
            byte elementIdx = 0,
            byte eleDamage = 0,
            byte ailmentIdx = 0,
            byte ailDamage = 0)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            bw.Write(modelId);
            bw.Write(rarity);
            bw.Write(classIdx);
            bw.Write(zennyCost);
            bw.Write((short)0);         // SharpnessId
            bw.Write(rawDamage);
            bw.Write((short)0);         // Defense
            bw.Write(affinity);
            bw.Write(elementIdx);
            bw.Write(eleDamage);
            bw.Write(ailmentIdx);
            bw.Write(ailDamage);
            bw.Write((byte)0);          // Slots
            bw.Write(new byte[3]);      // Unk3-4
            bw.Write(new byte[6]);      // Unk5-7 (3 shorts)
            bw.Write(0);                // Unk8
            bw.Write(0);                // Unk9
            bw.Write(new byte[4]);      // Unk10-11 (2 shorts)
            bw.Write(new byte[4]);      // Unk12-15
            bw.Write(0);                // Unk16
            bw.Write(0);                // Unk17

            return ms.ToArray();
        }

        private static byte[] CreateRangedWeaponEntryBytes(
            short modelId = 0,
            byte rarity = 1,
            byte classIdx = 1,
            int zennyCost = 1000)
        {
            // Total size: 0x3C (60) bytes
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            bw.Write(modelId);          // 0x00-0x01: Int16 (ModelId)
            bw.Write(rarity);           // 0x02: Byte (Rarity)
            bw.Write((byte)0);          // 0x03: Byte (MaxSlotsMaybe)
            bw.Write(classIdx);         // 0x04: Byte (ClassId)
            bw.Write((byte)0);          // 0x05: Byte (Unk2_1)
            bw.Write((byte)0);          // 0x06: Byte (EqType)
            bw.Write((byte)0);          // 0x07: Byte (Unk2_3)
            bw.Write((byte)0);          // 0x08: Byte (Unk3_1)
            bw.Write((byte)0);          // 0x09: Byte (Unk3_2)
            bw.Write((byte)0);          // 0x0A: Byte (Unk3_3)
            bw.Write((byte)0);          // 0x0B: Byte (Unk3_4)
            bw.Write((byte)0);          // 0x0C: Byte (Unk4_1)
            bw.Write((byte)0);          // 0x0D: Byte (Unk4_2)
            bw.Write((byte)0);          // 0x0E: Byte (Unk4_3)
            bw.Write((byte)0);          // 0x0F: Byte (Unk4_4)
            bw.Write((byte)0);          // 0x10: Byte (Unk5_1)
            bw.Write((byte)0);          // 0x11: Byte (Unk5_2)
            bw.Write((byte)0);          // 0x12: Byte (Unk5_3)
            bw.Write((byte)0);          // 0x13: Byte (Unk5_4)
            bw.Write(zennyCost);        // 0x14-0x17: Int32 (ZennyCost)
            bw.Write((short)100);       // 0x18-0x19: Int16 (RawDamage)
            bw.Write((short)0);         // 0x1A-0x1B: Int16 (Defense)
            bw.Write((byte)0);          // 0x1C: Byte (RecoilMaybe)
            bw.Write((byte)0);          // 0x1D: Byte (Slots)
            bw.Write((sbyte)0);         // 0x1E: SByte (Affinity)
            bw.Write((byte)0);          // 0x1F: Byte (SortOrderMaybe)
            bw.Write((byte)0);          // 0x20: Byte (Unk6_1)
            bw.Write((byte)0);          // 0x21: Byte (ElementId)
            bw.Write((byte)0);          // 0x22: Byte (EleDamage)
            bw.Write((byte)0);          // 0x23: Byte (Unk6_4)
            bw.Write((byte)0);          // 0x24: Byte (Unk7_1)
            bw.Write((byte)0);          // 0x25: Byte (Unk7_2)
            bw.Write((byte)0);          // 0x26: Byte (Unk7_3)
            bw.Write((byte)0);          // 0x27: Byte (Unk7_4)
            bw.Write((byte)0);          // 0x28: Byte (Unk8_1)
            bw.Write((byte)0);          // 0x29: Byte (Unk8_2)
            bw.Write((byte)0);          // 0x2A: Byte (Unk8_3)
            bw.Write((byte)0);          // 0x2B: Byte (Unk8_4)
            bw.Write((byte)0);          // 0x2C: Byte (Unk9_1)
            bw.Write((byte)0);          // 0x2D: Byte (Unk9_2)
            bw.Write((byte)0);          // 0x2E: Byte (Unk9_3)
            bw.Write((byte)0);          // 0x2F: Byte (Unk9_4)
            bw.Write((byte)0);          // 0x30: Byte (Unk10_1)
            bw.Write((byte)0);          // 0x31: Byte (Unk10_2)
            bw.Write((byte)0);          // 0x32: Byte (Unk10_3)
            bw.Write((byte)0);          // 0x33: Byte (Unk10_4)
            bw.Write((byte)0);          // 0x34: Byte (Unk11_1)
            bw.Write((byte)0);          // 0x35: Byte (Unk11_2)
            bw.Write((byte)0);          // 0x36: Byte (Unk11_3)
            bw.Write((byte)0);          // 0x37: Byte (Unk11_4)
            bw.Write((byte)0);          // 0x38: Byte (Unk12_1)
            bw.Write((byte)0);          // 0x39: Byte (Unk12_2)
            bw.Write((byte)0);          // 0x3A: Byte (Unk12_3)
            bw.Write((byte)0);          // 0x3B: Byte (Unk12_4)

            return ms.ToArray();
        }

        #endregion
    }
}
