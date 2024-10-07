using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrontierDataTool
{
    public class Structs
    {
        public class QuestData
        {
            public string title { get; set; }
            public string textMain { get; set; }
            public string textSubA { get; set; }
            public string textSubB { get; set; }

            public byte unk1 { get; set; }
            public byte unk2 { get; set; }
            public byte unk3 { get; set; }
            public byte unk4 { get; set; }
            public byte level { get; set; }
            public byte unk5 { get; set; }
            public byte courseType { get; set; }    // 6 = Premium, 18 = Free?, 19 = HLC?, 20 = Extra
            public byte unk7 { get; set; }
            public byte unk8 { get; set; }
            public byte unk9 { get; set; }
            public byte unk10 { get; set; }
            public byte unk11 { get; set; }
            public int fee { get; set; }
            public int zennyMain { get; set; }
            public int zennyKo { get; set; }
            public int zennySubA { get; set; }
            public int zennySubB { get; set; }
            public int time { get; set; }
            public int unk12 { get; set; }
            public byte unk13 { get; set; }
            public byte unk14 { get; set; }
            public byte unk15 { get; set; }
            public byte unk16 { get; set; }
            public byte unk17 { get; set; }
            public byte unk18 { get; set; }
            public byte unk19 { get; set; }
            public byte unk20 { get; set; }
            public string mainGoalType { get; set; }
            public short mainGoalTarget { get; set; }
            public short mainGoalCount { get; set; }
            public string subAGoalType { get; set; }
            public short subAGoalTarget { get; set; }
            public short subAGoalCount { get; set; }
            public string subBGoalType { get; set; }
            public short subBGoalTarget { get; set; }
            public short subBGoalCount { get; set; }

            public int mainGRP { get; set; }
            public int subAGRP { get; set; }
            public int subBGRP { get; set; }
        }

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

        public class ArmorDataEntry
        {
            public string equipClass { get; set; }
            public string name { get; set; }
            public short modelIdMale { get; set; }
            public short modelIdFemale { get; set; }
            public bool isMaleEquip { get; set; }
            public bool isFemaleEquip { get; set; }
            public bool isBladeEquip { get; set; }
            public bool isGunnerEquip { get; set; }
            public bool bool1 { get; set; }
            public bool isSPEquip { get; set; }
            public bool bool3 { get; set; }
            public bool bool4 { get; set; }
            public byte rarity { get; set; }
            public byte maxLevel { get; set; }
            public byte unk1_1 { get; set; }
            public byte unk1_2 { get; set; }
            public byte unk1_3 { get; set; }
            public byte unk1_4 { get; set; }
            public byte unk2 { get; set; }
            public int zennyCost { get; set; }
            public short unk3 { get; set; }
            public short baseDefense { get; set; }
            public sbyte fireRes { get; set; }
            public sbyte waterRes { get; set; }
            public sbyte thunderRes { get; set; }
            public sbyte dragonRes { get; set; }
            public sbyte iceRes { get; set; }
            public short unk3_1 { get; set; }
            public byte baseSlots { get; set; }
            public byte maxSlots { get; set; }
            public byte sthEventCrown { get; set; }
            public byte unk5 { get; set; }
            public byte unk6 { get; set; }
            public byte unk7_1 { get; set; }
            public byte unk7_2 { get; set; }
            public byte unk7_3 { get; set; }
            public byte unk7_4 { get; set; }
            public byte unk8_1 { get; set; }
            public byte unk8_2 { get; set; }
            public byte unk8_3 { get; set; }
            public byte unk8_4 { get; set; }
            public short unk10 { get; set; }
            public string skillId1 { get; set; }
            public sbyte skillPts1 { get; set; }
            public string skillId2 { get; set; }
            public sbyte skillPts2 { get; set; }
            public string skillId3 { get; set; }
            public sbyte skillPts3 { get; set; }
            public string skillId4 { get; set; }
            public sbyte skillPts4 { get; set; }
            public string skillId5 { get; set; }
            public sbyte skillPts5 { get; set; }
            public int sthHiden { get; set; }
            public int unk12 { get; set; }
            public byte unk13 { get; set; }
            public byte unk14 { get; set; }
            public byte unk15 { get; set; }
            public byte unk16 { get; set; }
            public int unk17 { get; set; }
            public short unk18 { get; set; }
            public short unk19 { get; set; }
        }

        public class MeleeWeaponEntry
        {
            public string name { get; set; }
            public short modelId { get; set; }
            public string modelIdData { get; set; }
            public byte rarity { get; set; }
            public string classId { get; set; }
            public int zennyCost { get; set; }
            public short sharpnessId { get; set; }
            public short rawDamage { get; set; }
            public short defense { get; set; }
            public sbyte affinity { get; set; }
            public string elementId { get; set; }
            public int eleDamage { get; set; }
            public string ailmentId { get; set; }
            public int ailDamage { get; set; }
            public byte slots { get; set; }
            public byte unk3 { get; set; }
            public byte unk4 { get; set; }
            public short unk5 { get; set; }
            public short unk6 { get; set; }
            public short unk7 { get; set; }
            public int unk8 { get; set; }
            public int unk9 { get; set; }
            public short unk10 { get; set; }
            public short unk11 { get; set; }
            public byte unk12 { get; set; }
            public byte unk13 { get; set; }
            public byte unk14 { get; set; }
            public byte unk15 { get; set; }
            public int unk16 { get; set; }
            public int unk17 { get; set; }
        }

        public class RangedWeaponEntry
        {
            public string name { get; set; }
            public short modelId { get; set; }
            public string modelIdData { get; set; }
            public byte rarity { get; set; }
            public byte maxSlotsMaybe { get; set; }
            public string classId { get; set; }
            public byte unk2_1 { get; set; }
            public string eqType { get; set; }
            public byte unk2_3 { get; set; }
            public byte unk3_1 { get; set; }
            public byte unk3_2 { get; set; }
            public byte unk3_3 { get; set; }
            public byte unk3_4 { get; set; }
            public byte unk4_1 { get; set; }
            public byte unk4_2 { get; set; }
            public byte unk4_3 { get; set; }
            public byte unk4_4 { get; set; }
            public byte unk5_1 { get; set; }
            public byte unk5_2 { get; set; }
            public byte unk5_3 { get; set; }
            public byte unk5_4 { get; set; }
            public int zennyCost { get; set; }
            public short rawDamage { get; set; }
            public short defense { get; set; }
            public byte recoilMaybe { get; set; }
            public byte slots { get; set; }
            public sbyte affinity { get; set; }
            public byte sortOrderMaybe { get; set; }
            public byte unk6_1 { get; set; }
            public string elementId { get; set; }
            public int eleDamage { get; set; }
            public byte unk6_4 { get; set; }
            public byte unk7_1 { get; set; }
            public byte unk7_2 { get; set; }
            public byte unk7_3 { get; set; }
            public byte unk7_4 { get; set; }
            public byte unk8_1 { get; set; }
            public byte unk8_2 { get; set; }
            public byte unk8_3 { get; set; }
            public byte unk8_4 { get; set; }
            public byte unk9_1 { get; set; }
            public byte unk9_2 { get; set; }
            public byte unk9_3 { get; set; }
            public byte unk9_4 { get; set; }
            public byte unk10_1 { get; set; }
            public byte unk10_2 { get; set; }
            public byte unk10_3 { get; set; }
            public byte unk10_4 { get; set; }
            public byte unk11_1 { get; set; }
            public byte unk11_2 { get; set; }
            public byte unk11_3 { get; set; }
            public byte unk11_4 { get; set; }
            public byte unk12_1 { get; set; }
            public byte unk12_2 { get; set; }
            public byte unk12_3 { get; set; }
            public byte unk12_4 { get; set; }
        }
    }
}
