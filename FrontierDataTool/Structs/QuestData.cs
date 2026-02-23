namespace FrontierDataTool.Structs
{
    /// <summary>
    /// Quest data from mhfinf.bin.
    /// </summary>
    public class QuestData
    {
        public string? Title { get; set; }
        public string? TextMain { get; set; }
        public string? TextSubA { get; set; }
        public string? TextSubB { get; set; }

        /// <summary>
        /// File offset of the Title string pointer (for reimport).
        /// </summary>
        public long TitlePtrFileOffset { get; set; }

        /// <summary>
        /// File offset of the TextMain string pointer (for reimport).
        /// </summary>
        public long TextMainPtrFileOffset { get; set; }

        /// <summary>
        /// File offset of the TextSubA string pointer (for reimport).
        /// </summary>
        public long TextSubAPtrFileOffset { get; set; }

        /// <summary>
        /// File offset of the TextSubB string pointer (for reimport).
        /// </summary>
        public long TextSubBPtrFileOffset { get; set; }

        // First 11 bytes - partially identified
        public byte Unk1 { get; set; }
        public byte Unk2 { get; set; }
        public byte Unk3 { get; set; }
        public byte Unk4 { get; set; }

        /// <summary>
        /// Quest difficulty level (stars).
        /// </summary>
        public byte Level { get; set; }

        public byte Unk5 { get; set; }

        /// <summary>
        /// Course requirement type (6 = Premium, 18 = Free?, 19 = HLC?, 20 = Extra).
        /// </summary>
        public byte CourseType { get; set; }

        public byte Unk7 { get; set; }
        public byte Unk8 { get; set; }
        public byte Unk9 { get; set; }
        public byte Unk10 { get; set; }

        /// <summary>
        /// Maximum number of players for this quest.
        /// </summary>
        public byte MaxPlayers { get; set; }

        public int Fee { get; set; }
        public int ZennyMain { get; set; }
        public int ZennyKo { get; set; }
        public int ZennySubA { get; set; }
        public int ZennySubB { get; set; }
        public int Time { get; set; }

        /// <summary>
        /// Map/location ID for the quest.
        /// </summary>
        public int MapId { get; set; }

        /// <summary>
        /// Pointer to quest text strings.
        /// </summary>
        public int QuestStringPtr { get; set; }

        /// <summary>
        /// Quest restriction flags.
        /// </summary>
        public short QuestRestrictions { get; set; }

        /// <summary>
        /// Quest identifier.
        /// </summary>
        public short QuestId { get; set; }

        public string? MainGoalType { get; set; }
        public short MainGoalTarget { get; set; }
        public short MainGoalCount { get; set; }
        public string? SubAGoalType { get; set; }
        public short SubAGoalTarget { get; set; }
        public short SubAGoalCount { get; set; }
        public string? SubBGoalType { get; set; }
        public short SubBGoalTarget { get; set; }
        public short SubBGoalCount { get; set; }

        public int MainGRP { get; set; }
        public int SubAGRP { get; set; }
        public int SubBGRP { get; set; }
    }

    /// <summary>
    /// Types of quest objectives.
    /// </summary>
    public enum QuestTypes
    {
        None = 0,
        Hunt = 0x00000001,
        Capture = 0x00000101,
        Slay = 0x00000201,
        Delivery = 0x00000002,
        GuildFlag = 0x00001002,
        Damaging = 0x00008004,
        SlayOrDamage = 0x00018004,
        SlayTotal = 0x00020000,
        SlayAll = 0x00040000,
        BreakPart = 0x00004004,
        EsotericAction = 0x00000010
    }
}
