namespace FrontierDataTool.Structs
{
    /// <summary>
    /// Melee weapon data (0x34 = 52 bytes per entry).
    /// </summary>
    public class MeleeWeaponEntry
    {
        public string? Name { get; set; }
        public short ModelId { get; set; }
        public string? ModelIdData { get; set; }
        public byte Rarity { get; set; }
        public string? ClassId { get; set; }
        public int ZennyCost { get; set; }
        public short SharpnessId { get; set; }
        public short RawDamage { get; set; }
        public short Defense { get; set; }
        public sbyte Affinity { get; set; }
        public string? ElementId { get; set; }
        public int EleDamage { get; set; }
        public string? AilmentId { get; set; }
        public int AilDamage { get; set; }
        public byte Slots { get; set; }

        /// <summary>
        /// Secondary weapon attribute (e.g., Switch Axe F phial type).
        /// </summary>
        public byte WeaponAttribute { get; set; }

        /// <summary>
        /// Particle effect type (1=ice?, 2=dark?).
        /// </summary>
        public byte ParticleEffect { get; set; }

        /// <summary>
        /// Upgrade tree reference - how many entries back to look for parent weapon. 0xFF = none.
        /// </summary>
        public short UpgradePath { get; set; }

        /// <summary>
        /// Alternate/drawn model ID (often same as ModelId).
        /// </summary>
        public short DrawnModelId { get; set; }

        /// <summary>
        /// Equipment type flags (0=General, 1=SP, 2=Gou, 4=Evolution, 8=HC, 0x24=Ravi).
        /// Combined from eqType (low byte) and unknown flag (high byte).
        /// </summary>
        public short EqType { get; set; }

        /// <summary>
        /// Weapon reach/length parameter.
        /// </summary>
        public int Length { get; set; }

        /// <summary>
        /// Weapon tier/type classification (zenith, prayer, g-rank, exotic, gou, etc.).
        /// </summary>
        public int WeaponType { get; set; }

        /// <summary>
        /// Visual effect ID for the weapon.
        /// </summary>
        public short VisualEffects { get; set; }

        public short Unk11 { get; set; }
        public byte Unk12 { get; set; }
        public byte Unk13 { get; set; }
        public byte Unk14 { get; set; }

        /// <summary>
        /// Usually 0x0F - possibly flags or padding.
        /// </summary>
        public byte ZeroF { get; set; }

        public int Unk16 { get; set; }

        /// <summary>
        /// Zenith skill ID (u16 stored in lower 2 bytes, upper 2 bytes are padding).
        /// </summary>
        public int ZenithSkill { get; set; }
    }
}
