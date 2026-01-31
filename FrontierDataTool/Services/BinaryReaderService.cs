using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using FrontierDataTool.Structs;

using LibReFrontier;

namespace FrontierDataTool.Services
{
    /// <summary>
    /// Service for parsing binary data structures from game files.
    /// Contains pure functions for reading armor, weapon, and quest entries.
    /// </summary>
    public class BinaryReaderService
    {
        /// <summary>
        /// Entry sizes for data structures (in bytes).
        /// </summary>
        public const int ARMOR_ENTRY_SIZE = 0x48;
        public const int MELEE_WEAPON_ENTRY_SIZE = 0x34;
        public const int RANGED_WEAPON_ENTRY_SIZE = 0x3C;
        public const int ITEM_ENTRY_SIZE = 0x24;
        public const int PEARL_ENTRY_SIZE = 0x30;
        public const int SHOP_ENTRY_SIZE = 8;

        // Lookup tables for weapon/armor data
        private static readonly string[] AilmentIds = ["なし", "毒", "麻痺", "睡眠", "爆破"];
        private static readonly string[] WeaponClassIds = ["大剣", "ヘビィボウガン", "ハンマー", "ランス", "片手剣", "ライトボウガン", "双剣", "太刀", "狩猟笛", "ガンランス", "弓", "穿龍棍", "スラッシュアックスＦ", "マグネットスパイク"];
        private static readonly string[] ElementIds = ["なし", "火", "水", "雷", "龍", "氷", "炎", "光", "雷極", "天翔", "熾凍", "黒焔", "奏", "闇", "紅魔", "風", "響", "灼零", "皇鳴"];

        /// <summary>
        /// Read a single armor entry from the binary reader.
        /// </summary>
        /// <param name="br">Binary reader positioned at the start of the entry.</param>
        /// <param name="skillLookup">Skill ID to name lookup.</param>
        /// <param name="equipClass">Armor class identifier (頭, 胴, 腕, 腰, 脚).</param>
        /// <returns>Parsed armor entry.</returns>
        public ArmorDataEntry ReadArmorEntry(BinaryReader br, IReadOnlyList<KeyValuePair<int, string>> skillLookup, string equipClass)
        {
            var entry = new ArmorDataEntry
            {
                EquipClass = equipClass,
                ModelIdMale = br.ReadInt16(),
                ModelIdFemale = br.ReadInt16()
            };

            byte bitfield = br.ReadByte();
            entry.IsMaleEquip = (bitfield & (1 << 0)) != 0;
            entry.IsFemaleEquip = (bitfield & (1 << 1)) != 0;
            entry.IsBladeEquip = (bitfield & (1 << 2)) != 0;
            entry.IsGunnerEquip = (bitfield & (1 << 3)) != 0;
            entry.Bool1 = (bitfield & (1 << 4)) != 0;
            entry.IsSPEquip = (bitfield & (1 << 5)) != 0;
            entry.Bool3 = (bitfield & (1 << 6)) != 0;
            entry.Bool4 = (bitfield & (1 << 7)) != 0;

            entry.Rarity = br.ReadByte();
            entry.MaxLevel = br.ReadByte();
            entry.Unk1_1 = br.ReadByte();
            entry.Unk1_2 = br.ReadByte();
            entry.Unk1_3 = br.ReadByte();
            entry.Unk1_4 = br.ReadByte();
            entry.Unk2 = br.ReadByte();
            entry.ZennyCost = br.ReadInt32();
            entry.Unk3 = br.ReadInt16();
            entry.BaseDefense = br.ReadInt16();
            entry.FireRes = br.ReadSByte();
            entry.WaterRes = br.ReadSByte();
            entry.ThunderRes = br.ReadSByte();
            entry.DragonRes = br.ReadSByte();
            entry.IceRes = br.ReadSByte();
            entry.Unk3_1 = br.ReadInt16();
            entry.BaseSlots = br.ReadByte();
            entry.MaxSlots = br.ReadByte();
            entry.SthEventCrown = br.ReadByte();
            entry.Unk5 = br.ReadByte();
            entry.Unk6 = br.ReadByte();
            entry.Unk7_1 = br.ReadByte();
            entry.Unk7_2 = br.ReadByte();
            entry.Unk7_3 = br.ReadByte();
            entry.Unk7_4 = br.ReadByte();
            entry.Unk8_1 = br.ReadByte();
            entry.Unk8_2 = br.ReadByte();
            entry.Unk8_3 = br.ReadByte();
            entry.Unk8_4 = br.ReadByte();
            entry.Unk10 = br.ReadInt16();

            // Read skill IDs and convert to names
            byte skillIdx1 = br.ReadByte();
            entry.SkillId1 = skillIdx1 < skillLookup.Count ? skillLookup[skillIdx1].Value : "";
            entry.SkillPts1 = br.ReadSByte();

            byte skillIdx2 = br.ReadByte();
            entry.SkillId2 = skillIdx2 < skillLookup.Count ? skillLookup[skillIdx2].Value : "";
            entry.SkillPts2 = br.ReadSByte();

            byte skillIdx3 = br.ReadByte();
            entry.SkillId3 = skillIdx3 < skillLookup.Count ? skillLookup[skillIdx3].Value : "";
            entry.SkillPts3 = br.ReadSByte();

            byte skillIdx4 = br.ReadByte();
            entry.SkillId4 = skillIdx4 < skillLookup.Count ? skillLookup[skillIdx4].Value : "";
            entry.SkillPts4 = br.ReadSByte();

            byte skillIdx5 = br.ReadByte();
            entry.SkillId5 = skillIdx5 < skillLookup.Count ? skillLookup[skillIdx5].Value : "";
            entry.SkillPts5 = br.ReadSByte();

            entry.SthHiden = br.ReadInt32();
            entry.Unk12 = br.ReadInt32();
            entry.Unk13 = br.ReadByte();
            entry.Unk14 = br.ReadByte();
            entry.Unk15 = br.ReadByte();
            entry.Unk16 = br.ReadByte();
            entry.Unk17 = br.ReadInt32();
            entry.Unk18 = br.ReadInt16();
            entry.Unk19 = br.ReadInt16();

            return entry;
        }

        /// <summary>
        /// Read a single melee weapon entry from the binary reader.
        /// </summary>
        /// <param name="br">Binary reader positioned at the start of the entry.</param>
        /// <returns>Parsed melee weapon entry.</returns>
        public MeleeWeaponEntry ReadMeleeWeaponEntry(BinaryReader br)
        {
            var entry = new MeleeWeaponEntry
            {
                ModelId = br.ReadInt16()
            };
            entry.ModelIdData = GetModelIdData(entry.ModelId);
            entry.Rarity = br.ReadByte();

            byte classIdx = br.ReadByte();
            entry.ClassId = classIdx < WeaponClassIds.Length ? WeaponClassIds[classIdx] : classIdx.ToString();

            entry.ZennyCost = br.ReadInt32();
            entry.SharpnessId = br.ReadInt16();
            entry.RawDamage = br.ReadInt16();
            entry.Defense = br.ReadInt16();
            entry.Affinity = br.ReadSByte();

            byte elementIdx = br.ReadByte();
            entry.ElementId = elementIdx < ElementIds.Length ? ElementIds[elementIdx] : elementIdx.ToString();
            entry.EleDamage = br.ReadByte() * 10;

            byte ailmentIdx = br.ReadByte();
            entry.AilmentId = ailmentIdx < AilmentIds.Length ? AilmentIds[ailmentIdx] : ailmentIdx.ToString();
            entry.AilDamage = br.ReadByte() * 10;

            entry.Slots = br.ReadByte();
            entry.Unk3 = br.ReadByte();
            entry.Unk4 = br.ReadByte();
            entry.Unk5 = br.ReadInt16();
            entry.Unk6 = br.ReadInt16();
            entry.Unk7 = br.ReadInt16();
            entry.Unk8 = br.ReadInt32();
            entry.Unk9 = br.ReadInt32();
            entry.Unk10 = br.ReadInt16();
            entry.Unk11 = br.ReadInt16();
            entry.Unk12 = br.ReadByte();
            entry.Unk13 = br.ReadByte();
            entry.Unk14 = br.ReadByte();
            entry.Unk15 = br.ReadByte();
            entry.Unk16 = br.ReadInt32();
            entry.Unk17 = br.ReadInt32();

            return entry;
        }

        /// <summary>
        /// Read a single ranged weapon entry from the binary reader.
        /// </summary>
        /// <param name="br">Binary reader positioned at the start of the entry.</param>
        /// <returns>Parsed ranged weapon entry.</returns>
        public RangedWeaponEntry ReadRangedWeaponEntry(BinaryReader br)
        {
            var entry = new RangedWeaponEntry
            {
                ModelId = br.ReadInt16()
            };
            entry.ModelIdData = GetModelIdData(entry.ModelId);
            entry.Rarity = br.ReadByte();
            entry.MaxSlotsMaybe = br.ReadByte();

            byte classIdx = br.ReadByte();
            entry.ClassId = classIdx < WeaponClassIds.Length ? WeaponClassIds[classIdx] : classIdx.ToString();

            entry.Unk2_1 = br.ReadByte();
            entry.EqType = br.ReadByte().ToString();
            entry.Unk2_3 = br.ReadByte();
            entry.Unk3_1 = br.ReadByte();
            entry.Unk3_2 = br.ReadByte();
            entry.Unk3_3 = br.ReadByte();
            entry.Unk3_4 = br.ReadByte();
            entry.Unk4_1 = br.ReadByte();
            entry.Unk4_2 = br.ReadByte();
            entry.Unk4_3 = br.ReadByte();
            entry.Unk4_4 = br.ReadByte();
            entry.Unk5_1 = br.ReadByte();
            entry.Unk5_2 = br.ReadByte();
            entry.Unk5_3 = br.ReadByte();
            entry.Unk5_4 = br.ReadByte();
            entry.ZennyCost = br.ReadInt32();
            entry.RawDamage = br.ReadInt16();
            entry.Defense = br.ReadInt16();
            entry.RecoilMaybe = br.ReadByte();
            entry.Slots = br.ReadByte();
            entry.Affinity = br.ReadSByte();
            entry.SortOrderMaybe = br.ReadByte();
            entry.Unk6_1 = br.ReadByte();

            byte elementIdx = br.ReadByte();
            entry.ElementId = elementIdx < ElementIds.Length ? ElementIds[elementIdx] : elementIdx.ToString();
            entry.EleDamage = br.ReadByte() * 10;

            entry.Unk6_4 = br.ReadByte();
            entry.Unk7_1 = br.ReadByte();
            entry.Unk7_2 = br.ReadByte();
            entry.Unk7_3 = br.ReadByte();
            entry.Unk7_4 = br.ReadByte();
            entry.Unk8_1 = br.ReadByte();
            entry.Unk8_2 = br.ReadByte();
            entry.Unk8_3 = br.ReadByte();
            entry.Unk8_4 = br.ReadByte();
            entry.Unk9_1 = br.ReadByte();
            entry.Unk9_2 = br.ReadByte();
            entry.Unk9_3 = br.ReadByte();
            entry.Unk9_4 = br.ReadByte();
            entry.Unk10_1 = br.ReadByte();
            entry.Unk10_2 = br.ReadByte();
            entry.Unk10_3 = br.ReadByte();
            entry.Unk10_4 = br.ReadByte();
            entry.Unk11_1 = br.ReadByte();
            entry.Unk11_2 = br.ReadByte();
            entry.Unk11_3 = br.ReadByte();
            entry.Unk11_4 = br.ReadByte();
            entry.Unk12_1 = br.ReadByte();
            entry.Unk12_2 = br.ReadByte();
            entry.Unk12_3 = br.ReadByte();
            entry.Unk12_4 = br.ReadByte();

            return entry;
        }

        /// <summary>
        /// Read a quest entry from the binary reader.
        /// </summary>
        /// <param name="br">Binary reader positioned at the start of the entry.</param>
        /// <returns>Parsed quest data.</returns>
        public QuestData ReadQuestEntry(BinaryReader br)
        {
            var entry = new QuestData
            {
                Unk1 = br.ReadByte(),
                Unk2 = br.ReadByte(),
                Unk3 = br.ReadByte(),
                Unk4 = br.ReadByte(),
                Level = br.ReadByte(),
                Unk5 = br.ReadByte(),
                CourseType = br.ReadByte(),
                Unk7 = br.ReadByte(),
                Unk8 = br.ReadByte(),
                Unk9 = br.ReadByte(),
                Unk10 = br.ReadByte(),
                Unk11 = br.ReadByte(),
                Fee = br.ReadInt32(),
                ZennyMain = br.ReadInt32(),
                ZennyKo = br.ReadInt32(),
                ZennySubA = br.ReadInt32(),
                ZennySubB = br.ReadInt32(),
                Time = br.ReadInt32(),
                Unk12 = br.ReadInt32(),
                Unk13 = br.ReadByte(),
                Unk14 = br.ReadByte(),
                Unk15 = br.ReadByte(),
                Unk16 = br.ReadByte(),
                Unk17 = br.ReadByte(),
                Unk18 = br.ReadByte(),
                Unk19 = br.ReadByte(),
                Unk20 = br.ReadByte()
            };

            int questType = br.ReadInt32();
            entry.MainGoalType = Enum.GetName(typeof(QuestTypes), questType) ?? questType.ToString("X8");
            entry.MainGoalTarget = br.ReadInt16();
            entry.MainGoalCount = br.ReadInt16();

            questType = br.ReadInt32();
            entry.SubAGoalType = Enum.GetName(typeof(QuestTypes), questType) ?? questType.ToString("X8");
            entry.SubAGoalTarget = br.ReadInt16();
            entry.SubAGoalCount = br.ReadInt16();

            questType = br.ReadInt32();
            entry.SubBGoalType = Enum.GetName(typeof(QuestTypes), questType) ?? questType.ToString("X8");
            entry.SubBGoalTarget = br.ReadInt16();
            entry.SubBGoalCount = br.ReadInt16();

            br.BaseStream.Seek(0x5C, SeekOrigin.Current);
            entry.MainGRP = br.ReadInt32();
            entry.SubAGRP = br.ReadInt32();
            entry.SubBGRP = br.ReadInt32();

            br.BaseStream.Seek(0x90, SeekOrigin.Current);
            entry.Title = StringFromPointer(br);
            entry.TextMain = StringFromPointer(br);
            entry.TextSubA = StringFromPointer(br);
            entry.TextSubB = StringFromPointer(br);
            br.BaseStream.Seek(0x10, SeekOrigin.Current);

            return entry;
        }

        /// <summary>
        /// Read a null-terminated Shift-JIS string by following a pointer.
        /// </summary>
        /// <param name="br">Binary reader.</param>
        /// <returns>Decoded string with special characters escaped.</returns>
        public string StringFromPointer(BinaryReader br)
        {
            int off = br.ReadInt32();
            long pos = br.BaseStream.Position;
            br.BaseStream.Seek(off, SeekOrigin.Begin);
            string str = FileOperations.ReadNullterminatedString(br, Encoding.GetEncoding("shift-jis"))
                .Replace("\\", "\\\\")
                .Replace("\t", "\\t")
                .Replace("\r\n", "\\r\\n")
                .Replace("\n", "\\n");
            br.BaseStream.Seek(pos, SeekOrigin.Begin);
            return str;
        }

        /// <summary>
        /// Get weapon model ID data string from numeric ID.
        /// </summary>
        /// <param name="id">Numeric model ID.</param>
        /// <returns>Model ID string (e.g., "we001", "wf002").</returns>
        public static string GetModelIdData(int id)
        {
            string str;
            if (id >= 0 && id < 1000) str = $"we{id:D3}";
            else if (id < 2000) str = $"wf{id - 1000:D3}";
            else if (id < 3000) str = $"wg{id - 2000:D3}";
            else if (id < 4000) str = $"wh{id - 3000:D3}";
            else if (id < 5000) str = $"wi{id - 4000:D3}";
            else if (id < 7000) str = $"wk{id - 6000:D3}";
            else if (id < 8000) str = $"wl{id - 7000:D3}";
            else if (id < 9000) str = $"wm{id - 8000:D3}";
            else if (id < 10000) str = $"wg{id - 9000:D3}";
            else str = "Unmapped";
            return str;
        }

        /// <summary>
        /// Reconstruct the bitfield byte from individual boolean fields.
        /// </summary>
        /// <param name="entry">Armor entry with boolean fields.</param>
        /// <returns>Bitfield byte.</returns>
        public static byte ReconstructArmorBitfield(ArmorDataEntry entry)
        {
            byte bitfield = 0;
            if (entry.IsMaleEquip) bitfield |= (1 << 0);
            if (entry.IsFemaleEquip) bitfield |= (1 << 1);
            if (entry.IsBladeEquip) bitfield |= (1 << 2);
            if (entry.IsGunnerEquip) bitfield |= (1 << 3);
            if (entry.Bool1) bitfield |= (1 << 4);
            if (entry.IsSPEquip) bitfield |= (1 << 5);
            if (entry.Bool3) bitfield |= (1 << 6);
            if (entry.Bool4) bitfield |= (1 << 7);
            return bitfield;
        }

        /// <summary>
        /// Write a single armor entry to the binary stream.
        /// </summary>
        /// <param name="bw">Binary writer positioned at the entry offset.</param>
        /// <param name="entry">Armor entry to write.</param>
        /// <param name="skillLookup">Dictionary mapping skill names to IDs.</param>
        public void WriteArmorEntry(BinaryWriter bw, ArmorDataEntry entry, Dictionary<string, byte> skillLookup)
        {
            bw.Write(entry.ModelIdMale);
            bw.Write(entry.ModelIdFemale);
            bw.Write(ReconstructArmorBitfield(entry));
            bw.Write(entry.Rarity);
            bw.Write(entry.MaxLevel);
            bw.Write(entry.Unk1_1);
            bw.Write(entry.Unk1_2);
            bw.Write(entry.Unk1_3);
            bw.Write(entry.Unk1_4);
            bw.Write(entry.Unk2);
            bw.Write(entry.ZennyCost);
            bw.Write(entry.Unk3);
            bw.Write(entry.BaseDefense);
            bw.Write(entry.FireRes);
            bw.Write(entry.WaterRes);
            bw.Write(entry.ThunderRes);
            bw.Write(entry.DragonRes);
            bw.Write(entry.IceRes);
            bw.Write(entry.Unk3_1);
            bw.Write(entry.BaseSlots);
            bw.Write(entry.MaxSlots);
            bw.Write(entry.SthEventCrown);
            bw.Write(entry.Unk5);
            bw.Write(entry.Unk6);
            bw.Write(entry.Unk7_1);
            bw.Write(entry.Unk7_2);
            bw.Write(entry.Unk7_3);
            bw.Write(entry.Unk7_4);
            bw.Write(entry.Unk8_1);
            bw.Write(entry.Unk8_2);
            bw.Write(entry.Unk8_3);
            bw.Write(entry.Unk8_4);
            bw.Write(entry.Unk10);

            bw.Write(LookupSkillId(entry.SkillId1, skillLookup));
            bw.Write(entry.SkillPts1);
            bw.Write(LookupSkillId(entry.SkillId2, skillLookup));
            bw.Write(entry.SkillPts2);
            bw.Write(LookupSkillId(entry.SkillId3, skillLookup));
            bw.Write(entry.SkillPts3);
            bw.Write(LookupSkillId(entry.SkillId4, skillLookup));
            bw.Write(entry.SkillPts4);
            bw.Write(LookupSkillId(entry.SkillId5, skillLookup));
            bw.Write(entry.SkillPts5);

            bw.Write(entry.SthHiden);
            bw.Write(entry.Unk12);
            bw.Write(entry.Unk13);
            bw.Write(entry.Unk14);
            bw.Write(entry.Unk15);
            bw.Write(entry.Unk16);
            bw.Write(entry.Unk17);
            bw.Write(entry.Unk18);
            bw.Write(entry.Unk19);
        }

        /// <summary>
        /// Write a single melee weapon entry to the binary stream.
        /// </summary>
        /// <param name="bw">Binary writer positioned at the entry offset.</param>
        /// <param name="entry">Melee weapon entry to write.</param>
        public void WriteMeleeWeaponEntry(BinaryWriter bw, MeleeWeaponEntry entry)
        {
            bw.Write(entry.ModelId);
            bw.Write(entry.Rarity);
            bw.Write(LookupWeaponClassId(entry.ClassId));
            bw.Write(entry.ZennyCost);
            bw.Write(entry.SharpnessId);
            bw.Write(entry.RawDamage);
            bw.Write(entry.Defense);
            bw.Write(entry.Affinity);
            bw.Write(LookupElementId(entry.ElementId));
            bw.Write((byte)(entry.EleDamage / 10));
            bw.Write(LookupAilmentId(entry.AilmentId));
            bw.Write((byte)(entry.AilDamage / 10));
            bw.Write(entry.Slots);
            bw.Write(entry.Unk3);
            bw.Write(entry.Unk4);
            bw.Write(entry.Unk5);
            bw.Write(entry.Unk6);
            bw.Write(entry.Unk7);
            bw.Write(entry.Unk8);
            bw.Write(entry.Unk9);
            bw.Write(entry.Unk10);
            bw.Write(entry.Unk11);
            bw.Write(entry.Unk12);
            bw.Write(entry.Unk13);
            bw.Write(entry.Unk14);
            bw.Write(entry.Unk15);
            bw.Write(entry.Unk16);
            bw.Write(entry.Unk17);
        }

        /// <summary>
        /// Write a single ranged weapon entry to the binary stream.
        /// </summary>
        /// <param name="bw">Binary writer positioned at the entry offset.</param>
        /// <param name="entry">Ranged weapon entry to write.</param>
        public void WriteRangedWeaponEntry(BinaryWriter bw, RangedWeaponEntry entry)
        {
            bw.Write(entry.ModelId);
            bw.Write(entry.Rarity);
            bw.Write(entry.MaxSlotsMaybe);
            bw.Write(LookupWeaponClassId(entry.ClassId));
            bw.Write(entry.Unk2_1);
            bw.Write(byte.TryParse(entry.EqType, out byte eqType) ? eqType : (byte)0);
            bw.Write(entry.Unk2_3);
            bw.Write(entry.Unk3_1);
            bw.Write(entry.Unk3_2);
            bw.Write(entry.Unk3_3);
            bw.Write(entry.Unk3_4);
            bw.Write(entry.Unk4_1);
            bw.Write(entry.Unk4_2);
            bw.Write(entry.Unk4_3);
            bw.Write(entry.Unk4_4);
            bw.Write(entry.Unk5_1);
            bw.Write(entry.Unk5_2);
            bw.Write(entry.Unk5_3);
            bw.Write(entry.Unk5_4);
            bw.Write(entry.ZennyCost);
            bw.Write(entry.RawDamage);
            bw.Write(entry.Defense);
            bw.Write(entry.RecoilMaybe);
            bw.Write(entry.Slots);
            bw.Write(entry.Affinity);
            bw.Write(entry.SortOrderMaybe);
            bw.Write(entry.Unk6_1);
            bw.Write(LookupElementId(entry.ElementId));
            bw.Write((byte)(entry.EleDamage / 10));
            bw.Write(entry.Unk6_4);
            bw.Write(entry.Unk7_1);
            bw.Write(entry.Unk7_2);
            bw.Write(entry.Unk7_3);
            bw.Write(entry.Unk7_4);
            bw.Write(entry.Unk8_1);
            bw.Write(entry.Unk8_2);
            bw.Write(entry.Unk8_3);
            bw.Write(entry.Unk8_4);
            bw.Write(entry.Unk9_1);
            bw.Write(entry.Unk9_2);
            bw.Write(entry.Unk9_3);
            bw.Write(entry.Unk9_4);
            bw.Write(entry.Unk10_1);
            bw.Write(entry.Unk10_2);
            bw.Write(entry.Unk10_3);
            bw.Write(entry.Unk10_4);
            bw.Write(entry.Unk11_1);
            bw.Write(entry.Unk11_2);
            bw.Write(entry.Unk11_3);
            bw.Write(entry.Unk11_4);
            bw.Write(entry.Unk12_1);
            bw.Write(entry.Unk12_2);
            bw.Write(entry.Unk12_3);
            bw.Write(entry.Unk12_4);
        }

        /// <summary>
        /// Write quest entry numeric fields to the binary stream.
        /// Note: String fields (Title, TextMain, TextSubA, TextSubB) are READ-ONLY
        /// and cannot be modified - they live in a separate string table.
        /// </summary>
        /// <param name="bw">Binary writer positioned at the entry offset.</param>
        /// <param name="entry">Quest entry to write.</param>
        public void WriteQuestEntry(BinaryWriter bw, QuestData entry)
        {
            // Write header bytes (12 bytes)
            bw.Write(entry.Unk1);
            bw.Write(entry.Unk2);
            bw.Write(entry.Unk3);
            bw.Write(entry.Unk4);
            bw.Write(entry.Level);
            bw.Write(entry.Unk5);
            bw.Write(entry.CourseType);
            bw.Write(entry.Unk7);
            bw.Write(entry.Unk8);
            bw.Write(entry.Unk9);
            bw.Write(entry.Unk10);
            bw.Write(entry.Unk11);

            // Write monetary values (24 bytes)
            bw.Write(entry.Fee);
            bw.Write(entry.ZennyMain);
            bw.Write(entry.ZennyKo);
            bw.Write(entry.ZennySubA);
            bw.Write(entry.ZennySubB);
            bw.Write(entry.Time);

            // Write Unk12 (4 bytes) and Unk13-20 (8 bytes)
            bw.Write(entry.Unk12);
            bw.Write(entry.Unk13);
            bw.Write(entry.Unk14);
            bw.Write(entry.Unk15);
            bw.Write(entry.Unk16);
            bw.Write(entry.Unk17);
            bw.Write(entry.Unk18);
            bw.Write(entry.Unk19);
            bw.Write(entry.Unk20);

            // Write goal data (24 bytes - 3 goals × 8 bytes each)
            bw.Write(LookupQuestType(entry.MainGoalType));
            bw.Write(entry.MainGoalTarget);
            bw.Write(entry.MainGoalCount);

            bw.Write(LookupQuestType(entry.SubAGoalType));
            bw.Write(entry.SubAGoalTarget);
            bw.Write(entry.SubAGoalCount);

            bw.Write(LookupQuestType(entry.SubBGoalType));
            bw.Write(entry.SubBGoalTarget);
            bw.Write(entry.SubBGoalCount);

            // Skip 0x5C bytes (preserve original data)
            bw.BaseStream.Seek(0x5C, SeekOrigin.Current);

            // Write GRP data (12 bytes)
            bw.Write(entry.MainGRP);
            bw.Write(entry.SubAGRP);
            bw.Write(entry.SubBGRP);
        }

        /// <summary>
        /// Look up a skill ID by name, returning 0 if not found.
        /// </summary>
        /// <param name="skillName">Skill name to look up.</param>
        /// <param name="skillLookup">Dictionary mapping skill names to IDs.</param>
        /// <returns>Skill ID byte.</returns>
        public static byte LookupSkillId(string? skillName, Dictionary<string, byte> skillLookup)
        {
            if (string.IsNullOrEmpty(skillName))
                return 0;
            if (skillLookup.TryGetValue(skillName, out byte id))
                return id;
            return 0;
        }

        /// <summary>
        /// Look up an element ID by name, returning 0 if not found.
        /// </summary>
        /// <param name="name">Element name (e.g., "火", "水").</param>
        /// <returns>Element ID byte.</returns>
        public static byte LookupElementId(string? name)
        {
            if (string.IsNullOrEmpty(name))
                return 0;
            int index = Array.IndexOf(ElementIds, name);
            return index >= 0 ? (byte)index : (byte)0;
        }

        /// <summary>
        /// Look up an ailment ID by name, returning 0 if not found.
        /// </summary>
        /// <param name="name">Ailment name (e.g., "毒", "麻痺").</param>
        /// <returns>Ailment ID byte.</returns>
        public static byte LookupAilmentId(string? name)
        {
            if (string.IsNullOrEmpty(name))
                return 0;
            int index = Array.IndexOf(AilmentIds, name);
            return index >= 0 ? (byte)index : (byte)0;
        }

        /// <summary>
        /// Look up a weapon class ID by name, returning 0 if not found.
        /// </summary>
        /// <param name="name">Weapon class name (e.g., "大剣", "太刀").</param>
        /// <returns>Weapon class ID byte.</returns>
        public static byte LookupWeaponClassId(string? name)
        {
            if (string.IsNullOrEmpty(name))
                return 0;
            int index = Array.IndexOf(WeaponClassIds, name);
            return index >= 0 ? (byte)index : (byte)0;
        }

        /// <summary>
        /// Look up a quest type by name or hex string, returning 0 if not found.
        /// </summary>
        /// <param name="typeName">Quest type name (e.g., "Hunt") or hex string (e.g., "00000001").</param>
        /// <returns>Quest type int value.</returns>
        public static int LookupQuestType(string? typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return 0;

            // If it starts with a digit, treat it as a hex string (e.g., "00000001")
            // This prevents Enum.TryParse from treating numeric strings as integers
            if (char.IsDigit(typeName[0]))
            {
                if (int.TryParse(typeName, System.Globalization.NumberStyles.HexNumber, null, out int hexValue))
                    return hexValue;
                return 0;
            }

            // Try to parse as enum name (e.g., "Hunt", "Capture")
            if (Enum.TryParse<QuestTypes>(typeName, out var questType))
                return (int)questType;

            return 0;
        }
    }
}
