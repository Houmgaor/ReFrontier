namespace FrontierDataTool.Structs
{
    /// <summary>
    /// Armor data (0x48 = 72 bytes per entry).
    /// </summary>
    public class ArmorDataEntry
    {
        public string? EquipClass { get; set; }
        public string? Name { get; set; }
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

        public byte Unk07 { get; set; }
        public byte Unk08 { get; set; }
        public byte Unk09 { get; set; }
        public byte Unk0A { get; set; }
        public byte Unk0B { get; set; }

        public int ZennyCost { get; set; }

        public short Unk10 { get; set; }

        public short BaseDefense { get; set; }
        public sbyte FireRes { get; set; }
        public sbyte WaterRes { get; set; }
        public sbyte ThunderRes { get; set; }
        public sbyte DragonRes { get; set; }
        public sbyte IceRes { get; set; }

        public byte Unk19 { get; set; }
        public byte Unk1A { get; set; }

        public byte BaseSlots { get; set; }
        public byte MaxSlots { get; set; }
        public byte SthEventCrown { get; set; }

        public short Unk1E { get; set; }

        public byte Unk20_1 { get; set; }
        public byte Unk20_2 { get; set; }
        public byte Unk20_3 { get; set; }
        public byte Unk20_4 { get; set; }

        public byte Unk24_1 { get; set; }
        public byte Unk24_2 { get; set; }
        public byte Unk24_3 { get; set; }
        public byte Unk24_4 { get; set; }

        public short Unk28 { get; set; }

        public string? SkillId1 { get; set; }
        public sbyte SkillPts1 { get; set; }
        public string? SkillId2 { get; set; }
        public sbyte SkillPts2 { get; set; }
        public string? SkillId3 { get; set; }
        public sbyte SkillPts3 { get; set; }
        public string? SkillId4 { get; set; }
        public sbyte SkillPts4 { get; set; }
        public string? SkillId5 { get; set; }
        public sbyte SkillPts5 { get; set; }

        public int SthHiden { get; set; }

        public int Unk38 { get; set; }

        public byte Unk3C { get; set; }
        public byte Unk3D { get; set; }
        public byte Unk3E { get; set; }
        public byte Unk3F { get; set; }

        public int Unk40 { get; set; }

        public short Unk44 { get; set; }

        /// <summary>
        /// Zenith skill ID.
        /// </summary>
        public short ZenithSkill { get; set; }
    }
}
