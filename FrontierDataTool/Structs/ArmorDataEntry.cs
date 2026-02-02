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

        /// <summary>
        /// Unknown - possibly armor grade (based on grade__5ArmorCFv symbol).
        /// </summary>
        public byte Unk07 { get; set; }

        /// <summary>
        /// Unknown - possibly upgrade cost multiplier or material index.
        /// </summary>
        public byte Unk08 { get; set; }

        /// <summary>
        /// Unknown - possibly upgrade-related parameter.
        /// </summary>
        public byte Unk09 { get; set; }

        /// <summary>
        /// Unknown - possibly upgrade-related parameter.
        /// </summary>
        public byte Unk0A { get; set; }

        /// <summary>
        /// Unknown - possibly upgrade-related parameter.
        /// </summary>
        public byte Unk0B { get; set; }

        public int ZennyCost { get; set; }

        /// <summary>
        /// Equipment type flags (0=General, 1=SP, 2=Gou, 4=Evolution, 8=HC, 0x24=Ravi).
        /// Based on Gousyu armor symbols: getFXGousyuArmorCount, getFGousyuArmorCount.
        /// </summary>
        public short EqType { get; set; }

        public short BaseDefense { get; set; }
        public sbyte FireRes { get; set; }
        public sbyte WaterRes { get; set; }
        public sbyte ThunderRes { get; set; }
        public sbyte DragonRes { get; set; }
        public sbyte IceRes { get; set; }

        /// <summary>
        /// Unknown - possibly additional resistance or status flags.
        /// </summary>
        public byte Unk19 { get; set; }

        /// <summary>
        /// Unknown - possibly additional resistance or status flags.
        /// </summary>
        public byte Unk1A { get; set; }

        public byte BaseSlots { get; set; }
        public byte MaxSlots { get; set; }
        public byte SthEventCrown { get; set; }

        /// <summary>
        /// Unknown - possibly upgrade tree reference or sort order.
        /// </summary>
        public short Unk1E { get; set; }

        /// <summary>
        /// Unknown 4-byte block - possibly armor type flags (Gousyu/evolution/zenith).
        /// Based on symbols: getFXGousyuArmorCount, getFGousyuArmorCount.
        /// </summary>
        public byte Unk20_1 { get; set; }
        public byte Unk20_2 { get; set; }
        public byte Unk20_3 { get; set; }
        public byte Unk20_4 { get; set; }

        /// <summary>
        /// Unknown 4-byte block - possibly visual effect or model variant data.
        /// </summary>
        public byte Unk24_1 { get; set; }
        public byte Unk24_2 { get; set; }
        public byte Unk24_3 { get; set; }
        public byte Unk24_4 { get; set; }

        /// <summary>
        /// Unknown - possibly skill activation threshold or decoration-related.
        /// Based on symbol: get_armor_deco_no.
        /// </summary>
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

        /// <summary>
        /// Hiden (秘伝) skill related data.
        /// Based on symbols: hasHidenEquips, isHidenDualArmorSkill.
        /// </summary>
        public int SthHiden { get; set; }

        /// <summary>
        /// Unknown - possibly additional Hiden data or G-Rank parameters.
        /// </summary>
        public int Unk38 { get; set; }

        /// <summary>
        /// Unknown - possibly visual effect or particle parameters.
        /// </summary>
        public byte Unk3C { get; set; }
        public byte Unk3D { get; set; }
        public byte Unk3E { get; set; }
        public byte Unk3F { get; set; }

        /// <summary>
        /// Armor tier/type classification (zenith, prayer, g-rank, exotic, gou, etc.).
        /// Same as weapon WeaponType field.
        /// </summary>
        public int ArmorType { get; set; }

        /// <summary>
        /// Unknown - possibly Zenith-related activation parameter.
        /// </summary>
        public short Unk44 { get; set; }

        /// <summary>
        /// Zenith skill ID.
        /// </summary>
        public short ZenithSkill { get; set; }
    }
}
