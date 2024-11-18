namespace FrontierDataTool
{
    /// <summary>
    /// Structure defining game data.
    /// </summary>
    public class Structs
    {

        /// <summary>
        /// All quest data.
        /// </summary>
        public class QuestData
        {
            public string Title { get; set; }
            public string TextMain { get; set; }
            public string TextSubA { get; set; }
            public string TextSubB { get; set; }

            public byte Unk1 { get; set; }
            public byte Unk2 { get; set; }
            public byte Unk3 { get; set; }
            public byte Unk4 { get; set; }
            public byte Level { get; set; }
            public byte Unk5 { get; set; }
            public byte CourseType { get; set; }    // 6 = Premium, 18 = Free?, 19 = HLC?, 20 = Extra
            public byte Unk7 { get; set; }
            public byte Unk8 { get; set; }
            public byte Unk9 { get; set; }
            public byte Unk10 { get; set; }
            public byte Unk11 { get; set; }
            public int Fee { get; set; }
            public int ZennyMain { get; set; }
            public int ZennyKo { get; set; }
            public int ZennySubA { get; set; }
            public int ZennySubB { get; set; }
            public int Time { get; set; }
            public int Unk12 { get; set; }
            public byte Unk13 { get; set; }
            public byte Unk14 { get; set; }
            public byte Unk15 { get; set; }
            public byte Unk16 { get; set; }
            public byte Unk17 { get; set; }
            public byte Unk18 { get; set; }
            public byte Unk19 { get; set; }
            public byte Unk20 { get; set; }
            public string MainGoalType { get; set; }
            public short MainGoalTarget { get; set; }
            public short MainGoalCount { get; set; }
            public string SubAGoalType { get; set; }
            public short SubAGoalTarget { get; set; }
            public short SubAGoalCount { get; set; }
            public string SubBGoalType { get; set; }
            public short SubBGoalTarget { get; set; }
            public short SubBGoalCount { get; set; }

            public int MainGRP { get; set; }
            public int SubAGRP { get; set; }
            public int SubBGRP { get; set; }
        }

        /// <summary>
        /// Types of quests. 
        /// </summary>
        public enum QuestTypes
        {
            None = 0,
            Hunt = 0x00000001,
            Capture = 0x00000101,
            Kill = 0x00000201,
            Delivery = 0x00000002,
            GuildFlag = 0x00001002,
            Damging = 0x00008004

        }

        /// <summary>
        /// Armor data.
        /// </summary>
        public class ArmorDataEntry
        {
            public string EquipClass { get; set; }
            public string Name { get; set; }
            public short ModelIdMale { get; set; }
            public short ModelIdFemale { get; set; }
            public bool IsMaleEquip { get; set; }
            public bool IsFemaleEquip { get; set; }
            public bool IsBladeEquip { get; set; }
            public bool IsGunnerEquip { get; set; }
            public bool Bool1 { get; set; }
            public bool IsSPEquip { get; set; }
            public bool Bool3 { get; set; }
            public bool Bool4 { get; set; }
            public byte Rarity { get; set; }
            public byte MaxLevel { get; set; }
            public byte Unk1_1 { get; set; }
            public byte Unk1_2 { get; set; }
            public byte Unk1_3 { get; set; }
            public byte Unk1_4 { get; set; }
            public byte Unk2 { get; set; }
            public int ZennyCost { get; set; }
            public short Unk3 { get; set; }
            public short BaseDefense { get; set; }
            public sbyte FireRes { get; set; }
            public sbyte WaterRes { get; set; }
            public sbyte ThunderRes { get; set; }
            public sbyte DragonRes { get; set; }
            public sbyte IceRes { get; set; }
            public short Unk3_1 { get; set; }
            public byte BaseSlots { get; set; }
            public byte MaxSlots { get; set; }
            public byte SthEventCrown { get; set; }
            public byte Unk5 { get; set; }
            public byte Unk6 { get; set; }
            public byte Unk7_1 { get; set; }
            public byte Unk7_2 { get; set; }
            public byte Unk7_3 { get; set; }
            public byte Unk7_4 { get; set; }
            public byte Unk8_1 { get; set; }
            public byte Unk8_2 { get; set; }
            public byte Unk8_3 { get; set; }
            public byte Unk8_4 { get; set; }
            public short Unk10 { get; set; }
            public string SkillId1 { get; set; }
            public sbyte SkillPts1 { get; set; }
            public string SkillId2 { get; set; }
            public sbyte SkillPts2 { get; set; }
            public string SkillId3 { get; set; }
            public sbyte SkillPts3 { get; set; }
            public string SkillId4 { get; set; }
            public sbyte SkillPts4 { get; set; }
            public string SkillId5 { get; set; }
            public sbyte SkillPts5 { get; set; }
            public int SthHiden { get; set; }
            public int Unk12 { get; set; }
            public byte Unk13 { get; set; }
            public byte Unk14 { get; set; }
            public byte Unk15 { get; set; }
            public byte Unk16 { get; set; }
            public int Unk17 { get; set; }
            public short Unk18 { get; set; }
            public short Unk19 { get; set; }
        }

        /// <summary>
        /// Melee weapon data.
        /// </summary>
        public class MeleeWeaponEntry
        {
            public string Name { get; set; }
            public short ModelId { get; set; }
            public string ModelIdData { get; set; }
            public byte Rarity { get; set; }
            public string ClassId { get; set; }
            public int ZennyCost { get; set; }
            public short SharpnessId { get; set; }
            public short RawDamage { get; set; }
            public short Defense { get; set; }
            public sbyte Affinity { get; set; }
            public string ElementId { get; set; }
            public int EleDamage { get; set; }
            public string AilmentId { get; set; }
            public int AilDamage { get; set; }
            public byte Slots { get; set; }
            public byte Unk3 { get; set; }
            public byte Unk4 { get; set; }
            public short Unk5 { get; set; }
            public short Unk6 { get; set; }
            public short Unk7 { get; set; }
            public int Unk8 { get; set; }
            public int Unk9 { get; set; }
            public short Unk10 { get; set; }
            public short Unk11 { get; set; }
            public byte Unk12 { get; set; }
            public byte Unk13 { get; set; }
            public byte Unk14 { get; set; }
            public byte Unk15 { get; set; }
            public int Unk16 { get; set; }
            public int Unk17 { get; set; }
        }

        /// <summary>
        /// Ranged weapon data.
        /// </summary>
        public class RangedWeaponEntry
        {
            public string Name { get; set; }
            public short ModelId { get; set; }
            public string ModelIdData { get; set; }
            public byte Rarity { get; set; }
            public byte MaxSlotsMaybe { get; set; }
            public string ClassId { get; set; }
            public byte Unk2_1 { get; set; }
            public string EqType { get; set; }
            public byte Unk2_3 { get; set; }
            public byte Unk3_1 { get; set; }
            public byte Unk3_2 { get; set; }
            public byte Unk3_3 { get; set; }
            public byte Unk3_4 { get; set; }
            public byte Unk4_1 { get; set; }
            public byte Unk4_2 { get; set; }
            public byte Unk4_3 { get; set; }
            public byte Unk4_4 { get; set; }
            public byte Unk5_1 { get; set; }
            public byte Unk5_2 { get; set; }
            public byte Unk5_3 { get; set; }
            public byte Unk5_4 { get; set; }
            public int ZennyCost { get; set; }
            public short RawDamage { get; set; }
            public short Defense { get; set; }
            public byte RecoilMaybe { get; set; }
            public byte Slots { get; set; }
            public sbyte Affinity { get; set; }
            public byte SortOrderMaybe { get; set; }
            public byte Unk6_1 { get; set; }
            public string ElementId { get; set; }
            public int EleDamage { get; set; }
            public byte Unk6_4 { get; set; }
            public byte Unk7_1 { get; set; }
            public byte Unk7_2 { get; set; }
            public byte Unk7_3 { get; set; }
            public byte Unk7_4 { get; set; }
            public byte Unk8_1 { get; set; }
            public byte Unk8_2 { get; set; }
            public byte Unk8_3 { get; set; }
            public byte Unk8_4 { get; set; }
            public byte Unk9_1 { get; set; }
            public byte Unk9_2 { get; set; }
            public byte Unk9_3 { get; set; }
            public byte Unk9_4 { get; set; }
            public byte Unk10_1 { get; set; }
            public byte Unk10_2 { get; set; }
            public byte Unk10_3 { get; set; }
            public byte Unk10_4 { get; set; }
            public byte Unk11_1 { get; set; }
            public byte Unk11_2 { get; set; }
            public byte Unk11_3 { get; set; }
            public byte Unk11_4 { get; set; }
            public byte Unk12_1 { get; set; }
            public byte Unk12_2 { get; set; }
            public byte Unk12_3 { get; set; }
            public byte Unk12_4 { get; set; }
        }
    }
}
