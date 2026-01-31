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
        /// </summary>
        public static readonly IReadOnlyList<(int Offset, int Count)> QuestSections =
        [
            (0x6BD60, 95),
            (0x74100, 62),
            (0x797E0, 99),
            (0x821A0, 98),
            (0x8AA00, 99),
            (0x933C0, 99),
            (0x9BD80, 99),
            (0xA4740, 99),
            (0xAD100, 99),
            (0xB5B40, 36),
            (0xB8E60, 96),
            (0xC1400, 91),
            (0x161220, 20)
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
