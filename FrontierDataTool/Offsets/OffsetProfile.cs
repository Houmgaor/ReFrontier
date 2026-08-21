using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FrontierDataTool.Offsets
{
    /// <summary>
    /// A pair of pointers bounding a region: the file holds an offset at each.
    /// </summary>
    /// <param name="Start">Offset of the pointer to the first byte of the region.</param>
    /// <param name="End">Offset of the pointer to the byte after the region.</param>
    public sealed record PointerPair(
        [property: JsonConverter(typeof(HexIntConverter))] int Start,
        [property: JsonConverter(typeof(HexIntConverter))] int End);

    /// <summary>
    /// Where one block of quests begins and how many it holds.
    /// </summary>
    /// <param name="Offset">Offset of the first entry, after decryption and decompression.</param>
    /// <param name="Count">Number of entries in the block.</param>
    public sealed record QuestSection(
        [property: JsonConverter(typeof(HexIntConverter))] int Offset,
        int Count);

    /// <summary>
    /// Armor pointers, one entry per slot, in the order the slots are dumped.
    /// </summary>
    public sealed record ArmorOffsets
    {
        /// <summary>Pointer pairs bounding the armor data of each slot.</summary>
        public IReadOnlyList<PointerPair> DataPointers { get; init; } = [];

        /// <summary>Pointer pairs bounding the armor name strings of each slot.</summary>
        public IReadOnlyList<PointerPair> StringPointers { get; init; } = [];

        /// <summary>The game's identifier for each slot, used to label the dump.</summary>
        public IReadOnlyList<string> SlotNames { get; init; } = [];
    }

    /// <summary>
    /// Weapon pointers. Melee and ranged share the file but not the layout.
    /// </summary>
    public sealed record WeaponOffsets
    {
        /// <summary>Pointer to the first melee entry.</summary>
        [JsonConverter(typeof(HexIntConverter))] public int MeleeStart { get; init; }

        /// <summary>Pointer to the byte after the last melee entry.</summary>
        [JsonConverter(typeof(HexIntConverter))] public int MeleeEnd { get; init; }

        /// <summary>Pointer to the first melee name string.</summary>
        [JsonConverter(typeof(HexIntConverter))] public int MeleeStringStart { get; init; }

        /// <summary>Pointer to the first ranged entry.</summary>
        [JsonConverter(typeof(HexIntConverter))] public int RangedStart { get; init; }

        /// <summary>Pointer to the byte after the last ranged entry.</summary>
        [JsonConverter(typeof(HexIntConverter))] public int RangedEnd { get; init; }

        /// <summary>Pointer to the first ranged name string.</summary>
        [JsonConverter(typeof(HexIntConverter))] public int RangedStringStart { get; init; }
    }

    /// <summary>
    /// Item name and description pointers.
    /// </summary>
    public sealed record ItemOffsets
    {
        /// <summary>Pointer to the first item name.</summary>
        [JsonConverter(typeof(HexIntConverter))] public int StringStart { get; init; }

        /// <summary>Pointer to the byte after the last item name.</summary>
        [JsonConverter(typeof(HexIntConverter))] public int StringEnd { get; init; }

        /// <summary>Pointer to the first item description.</summary>
        [JsonConverter(typeof(HexIntConverter))] public int DescriptionStart { get; init; }

        /// <summary>Pointer to the byte after the last item description.</summary>
        [JsonConverter(typeof(HexIntConverter))] public int DescriptionEnd { get; init; }
    }

    /// <summary>
    /// Skill pointers, held in mhfpac.
    /// </summary>
    public sealed record SkillOffsets
    {
        /// <summary>Pointer to the first skill tree name.</summary>
        [JsonConverter(typeof(HexIntConverter))] public int TreeNameStart { get; init; }

        /// <summary>Pointer to the byte after the last skill tree name.</summary>
        [JsonConverter(typeof(HexIntConverter))] public int TreeNameEnd { get; init; }

        /// <summary>Pointer to the first active skill name.</summary>
        [JsonConverter(typeof(HexIntConverter))] public int ActiveNameStart { get; init; }

        /// <summary>Pointer to the byte after the last active skill name.</summary>
        [JsonConverter(typeof(HexIntConverter))] public int ActiveNameEnd { get; init; }

        /// <summary>Pointer to the first skill description.</summary>
        [JsonConverter(typeof(HexIntConverter))] public int DescriptionStart { get; init; }

        /// <summary>Pointer to the byte after the last skill description.</summary>
        [JsonConverter(typeof(HexIntConverter))] public int DescriptionEnd { get; init; }

        /// <summary>Pointer to the first Z-skill name.</summary>
        [JsonConverter(typeof(HexIntConverter))] public int ZSkillNameStart { get; init; }

        /// <summary>Pointer to the byte after the last Z-skill name.</summary>
        [JsonConverter(typeof(HexIntConverter))] public int ZSkillNameEnd { get; init; }
    }

    /// <summary>
    /// Everything located inside mhfdat.bin.
    /// </summary>
    public sealed record MhfDatOffsets
    {
        /// <summary>Armor pointers.</summary>
        public ArmorOffsets Armor { get; init; } = new();

        /// <summary>Weapon pointers.</summary>
        public WeaponOffsets Weapons { get; init; } = new();

        /// <summary>Item pointers.</summary>
        public ItemOffsets Items { get; init; } = new();
    }

    /// <summary>
    /// Everything located inside mhfpac.bin.
    /// </summary>
    public sealed record MhfPacOffsets
    {
        /// <summary>Skill pointers.</summary>
        public SkillOffsets Skills { get; init; } = new();
    }

    /// <summary>
    /// Everything located inside mhfinf.bin.
    /// </summary>
    public sealed record MhfInfOffsets
    {
        /// <summary>
        /// Size of one quest entry.
        /// </summary>
        /// <remarks>
        /// Sections do not start on a multiple of this -- they are separated by gaps of
        /// 0x1A0 and, once, 0x98100 -- so it cannot be used to check an offset on its own.
        /// It says how far one entry is from the next inside a section, which is what makes
        /// a section's span, and therefore whether two sections overlap, computable. The
        /// offsets were once each 0x20 too high, and 0x20 is not a multiple of 0x160, so
        /// every read began mid-entry.
        /// </remarks>
        [JsonConverter(typeof(HexIntConverter))] public int QuestEntrySize { get; init; } = 0x160;

        /// <summary>Where the quest blocks are and how many entries each holds.</summary>
        public IReadOnlyList<QuestSection> QuestSections { get; init; } = [];

        /// <summary>Total number of quests across every section.</summary>
        [JsonIgnore]
        public int TotalQuestCount
        {
            get
            {
                int total = 0;
                foreach (var section in QuestSections)
                {
                    total += section.Count;
                }
                return total;
            }
        }
    }

    /// <summary>
    /// Where the data lives in one version of the game's files.
    /// </summary>
    /// <remarks>
    /// Offsets differ between game versions, so they are data rather than constants: the
    /// built-in profiles are embedded in the executable and a file can be given with
    /// --offsets. See FrontierDataTool/Offsets/Profiles for the ones that ship.
    /// </remarks>
    public sealed record OffsetProfile
    {
        /// <summary>Short identifier, matching the file name of a built-in profile.</summary>
        public string Id { get; init; } = "";

        /// <summary>Which game versions this profile is known to read.</summary>
        public string Description { get; init; } = "";

        /// <summary>Pointers into mhfdat.bin.</summary>
        public MhfDatOffsets MhfDat { get; init; } = new();

        /// <summary>Pointers into mhfpac.bin.</summary>
        public MhfPacOffsets MhfPac { get; init; } = new();

        /// <summary>Offsets into mhfinf.bin.</summary>
        public MhfInfOffsets MhfInf { get; init; } = new();
    }
}
