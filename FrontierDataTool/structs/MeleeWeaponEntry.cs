namespace FrontierDataTool.structs
{
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
}
