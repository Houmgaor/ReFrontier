using System.Collections.Generic;

namespace FrontierDataTool;

/// <summary>
/// Centralized offset constants for Monster Hunter Frontier data files.
/// Contains pointer offsets used to locate data sections within binary game files.
/// </summary>
public static class MhfDataOffsets
{
    /// <summary>
    /// Offset pointers for mhfdat.bin - main game data file.
    /// </summary>
    public static class MhfDat
    {
        /// <summary>
        /// Armor data section pointers.
        /// </summary>
        public static class Armor
        {
            /// <summary>Start offset pointers for armor data by slot.</summary>
            public const int HeadStart = 0x50;
            public const int BodyStart = 0x54;
            public const int ArmStart = 0x58;
            public const int WaistStart = 0x5C;
            public const int LegStart = 0x60;

            /// <summary>End offset pointers for armor data by slot.</summary>
            public const int HeadEnd = 0xE8;
            public const int BodyEnd = 0x50;
            public const int ArmEnd = 0x54;
            public const int WaistEnd = 0x58;
            public const int LegEnd = 0x5C;

            /// <summary>Start offset pointers for armor name strings by slot.</summary>
            public const int StringHeadStart = 0x64;
            public const int StringBodyStart = 0x68;
            public const int StringArmStart = 0x6C;
            public const int StringWaistStart = 0x70;
            public const int StringLegStart = 0x74;

            /// <summary>End offset pointers for armor name strings by slot.</summary>
            public const int StringHeadEnd = 0x60;
            public const int StringBodyEnd = 0x64;
            public const int StringArmEnd = 0x68;
            public const int StringWaistEnd = 0x6C;
            public const int StringLegEnd = 0x70;

            /// <summary>
            /// Data pointer pairs (start, end) for each armor slot.
            /// Order: Head, Body, Arm, Waist, Leg
            /// </summary>
            public static readonly IReadOnlyList<(int Start, int End)> DataPointers =
            [
                (HeadStart, HeadEnd),
                (BodyStart, BodyEnd),
                (ArmStart, ArmEnd),
                (WaistStart, WaistEnd),
                (LegStart, LegEnd)
            ];

            /// <summary>
            /// String pointer pairs (start, end) for each armor slot.
            /// Order: Head, Body, Arm, Waist, Leg
            /// </summary>
            public static readonly IReadOnlyList<(int Start, int End)> StringPointers =
            [
                (StringHeadStart, StringHeadEnd),
                (StringBodyStart, StringBodyEnd),
                (StringArmStart, StringArmEnd),
                (StringWaistStart, StringWaistEnd),
                (StringLegStart, StringLegEnd)
            ];

            /// <summary>
            /// Japanese identifiers for each armor slot.
            /// Order: Head, Body, Arm, Waist, Leg
            /// </summary>
            public static readonly IReadOnlyList<string> SlotNames = ["頭", "胴", "腕", "腰", "脚"];
        }

        /// <summary>
        /// Weapon data section pointers.
        /// </summary>
        public static class Weapons
        {
            /// <summary>Melee weapon data start offset pointer.</summary>
            public const int MeleeStart = 0x7C;
            /// <summary>Melee weapon data end offset pointer.</summary>
            public const int MeleeEnd = 0x90;
            /// <summary>Melee weapon name strings start offset pointer.</summary>
            public const int MeleeStringStart = 0x88;

            /// <summary>Ranged weapon data start offset pointer.</summary>
            public const int RangedStart = 0x80;
            /// <summary>Ranged weapon data end offset pointer.</summary>
            public const int RangedEnd = 0x7C;
            /// <summary>Ranged weapon name strings start offset pointer.</summary>
            public const int RangedStringStart = 0x84;
        }

        /// <summary>
        /// Item data section pointers.
        /// </summary>
        public static class Items
        {
            /// <summary>Item name strings start offset pointer.</summary>
            public const int StringStart = 0x100;
            /// <summary>Item name strings end offset pointer.</summary>
            public const int StringEnd = 0xFC;
            /// <summary>Item description strings start offset pointer.</summary>
            public const int DescriptionStart = 0x12C;
            /// <summary>Item description strings end offset pointer.</summary>
            public const int DescriptionEnd = 0x100;
        }
    }

    /// <summary>
    /// Offset pointers for mhfpac.bin - skill and ability data file.
    /// </summary>
    public static class MhfPac
    {
        /// <summary>
        /// Skill data section pointers.
        /// </summary>
        public static class Skills
        {
            /// <summary>Skill tree name strings start offset pointer.</summary>
            public const int TreeNameStart = 0xA20;
            /// <summary>Skill tree name strings end offset pointer.</summary>
            public const int TreeNameEnd = 0xA1C;

            /// <summary>Active skill name strings start offset pointer.</summary>
            public const int ActiveNameStart = 0xA1C;
            /// <summary>Active skill name strings end offset pointer.</summary>
            public const int ActiveNameEnd = 0xBC0;

            /// <summary>Skill description strings start offset pointer.</summary>
            public const int DescriptionStart = 0xB8;
            /// <summary>Skill description strings end offset pointer.</summary>
            public const int DescriptionEnd = 0xC0;

            /// <summary>Z-skill name strings start offset pointer.</summary>
            public const int ZSkillNameStart = 0xFBC;
            /// <summary>Z-skill name strings end offset pointer.</summary>
            public const int ZSkillNameEnd = 0xFB0;
        }
    }

    /// <summary>
    /// Offset and count data for mhfinf.bin - quest information file.
    /// </summary>
    public static class MhfInf
    {
        /// <summary>
        /// Quest data sections with (offset, count) pairs.
        /// Each entry defines where a quest block starts and how many quests it contains.
        /// <para>Offsets are into mhfinf.bin after decryption and JPK decompression, and
        /// every entry is <c>0x160</c> bytes. They were each <c>0x20</c> too high, which put
        /// the reader in the middle of an entry: the four string pointers at
        /// <c>entry + 0x140</c> came out as ordinary small integers, and the first one that
        /// happened to fall outside the file ended the dump. Every section moved down by
        /// <c>0x20</c>, so all 1092 quests now read with their titles, goal types and map
        /// IDs.</para>
        /// <para>Sections are not contiguous: each is followed by about <c>0x1A0</c> bytes
        /// that are not quest entries, so an offset cannot be derived from the one before
        /// it.</para>
        /// <para>These are version-specific. The current PC client's file holds roughly
        /// three times this many quest entries, in sections this table does not name.</para>
        /// </summary>
        public static readonly IReadOnlyList<(int Offset, int Count)> QuestSections =
        [
            (0x6BD40, 95),
            (0x740E0, 62),
            (0x797C0, 99),
            (0x82180, 98),
            (0x8A9E0, 99),
            (0x933A0, 99),
            (0x9BD60, 99),
            (0xA4720, 99),
            (0xAD0E0, 99),
            (0xB5B20, 36),
            (0xB8E40, 96),
            (0xC13E0, 91),
            (0x161200, 20)
        ];

        /// <summary>
        /// Total count of all quests across all sections.
        /// </summary>
        public static int TotalQuestCount
        {
            get
            {
                int total = 0;
                foreach (var section in QuestSections)
                    total += section.Count;
                return total;
            }
        }
    }
}
