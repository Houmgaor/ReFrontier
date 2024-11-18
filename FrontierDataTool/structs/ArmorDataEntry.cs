namespace FrontierDataTool.structs
{
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
}