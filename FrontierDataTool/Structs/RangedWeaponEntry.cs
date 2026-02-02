namespace FrontierDataTool.Structs
{
    /// <summary>
    /// Ranged weapon data (0x3C = 60 bytes per entry).
    /// </summary>
    public class RangedWeaponEntry
    {
        public string? Name { get; set; }
        public short ModelId { get; set; }
        public string? ModelIdData { get; set; }
        public byte Rarity { get; set; }
        public byte MaxSlotsMaybe { get; set; }
        public string? ClassId { get; set; }

        /// <summary>
        /// Unknown - possibly gun-specific parameter.
        /// </summary>
        public byte Unk05 { get; set; }

        /// <summary>
        /// Equipment type (0=General, 1=SP, 2=Gou, 4=Evolution, 8=HC, 0x24=Ravi).
        /// </summary>
        public string? EqType { get; set; }

        /// <summary>
        /// Unknown - possibly reload speed or deviation.
        /// </summary>
        public byte Unk07 { get; set; }

        /// <summary>
        /// Unknown 4-byte block - possibly ammo configuration.
        /// </summary>
        public byte Unk08_1 { get; set; }
        public byte Unk08_2 { get; set; }
        public byte Unk08_3 { get; set; }
        public byte Unk08_4 { get; set; }

        /// <summary>
        /// Weapon tier/type classification (zenith, prayer, g-rank, exotic, gou, etc.).
        /// Stored as 4 individual bytes for CSV compatibility.
        /// </summary>
        public byte WeaponType1 { get; set; }
        public byte WeaponType2 { get; set; }
        public byte WeaponType3 { get; set; }
        public byte WeaponType4 { get; set; }

        /// <summary>
        /// Unknown 4-byte block - possibly bullet level data (tamalv).
        /// Based on Wii U symbol: get_ex_g_weapon_tamalv.
        /// </summary>
        public byte Unk10_1 { get; set; }
        public byte Unk10_2 { get; set; }
        public byte Unk10_3 { get; set; }
        public byte Unk10_4 { get; set; }

        public int ZennyCost { get; set; }
        public short RawDamage { get; set; }
        public short Defense { get; set; }
        public byte RecoilMaybe { get; set; }
        public byte Slots { get; set; }
        public sbyte Affinity { get; set; }
        public byte SortOrderMaybe { get; set; }

        /// <summary>
        /// Weapon attribute - defines gunner shot type?
        /// </summary>
        public byte WeaponAttribute { get; set; }

        public string? ElementId { get; set; }
        public int EleDamage { get; set; }

        /// <summary>
        /// Unknown - possibly gun level (gunlv) or type (guntype).
        /// Based on Wii U symbols: get_ex_g_weapon_guntype, get_ex_g_weapon_gunlv.
        /// </summary>
        public byte Unk23 { get; set; }

        /// <summary>
        /// Unknown 4-byte block - possibly rapid fire or special shot data.
        /// </summary>
        public byte Unk24_1 { get; set; }
        public byte Unk24_2 { get; set; }
        public byte Unk24_3 { get; set; }
        public byte Unk24_4 { get; set; }

        /// <summary>
        /// Bullet/ammo configuration data.
        /// Stored as 4 individual bytes for CSV compatibility.
        /// </summary>
        public byte Bullet1 { get; set; }
        public byte Bullet2 { get; set; }
        public byte Bullet3 { get; set; }
        public byte Bullet4 { get; set; }

        /// <summary>
        /// Unknown 4-byte block - possibly internal ammo capacity.
        /// </summary>
        public byte Unk2C_1 { get; set; }
        public byte Unk2C_2 { get; set; }
        public byte Unk2C_3 { get; set; }
        public byte Unk2C_4 { get; set; }

        /// <summary>
        /// Unknown 4-byte block - possibly additional ammo data.
        /// </summary>
        public byte Unk30_1 { get; set; }
        public byte Unk30_2 { get; set; }
        public byte Unk30_3 { get; set; }
        public byte Unk30_4 { get; set; }

        /// <summary>
        /// Unknown 4-byte block - possibly special ammo or siege mode data.
        /// </summary>
        public byte Unk34_1 { get; set; }
        public byte Unk34_2 { get; set; }
        public byte Unk34_3 { get; set; }
        public byte Unk34_4 { get; set; }

        /// <summary>
        /// Unknown 4-byte block - possibly bow-specific data (charge levels, coatings).
        /// </summary>
        public byte Unk38_1 { get; set; }
        public byte Unk38_2 { get; set; }
        public byte Unk38_3 { get; set; }
        public byte Unk38_4 { get; set; }
    }
}
