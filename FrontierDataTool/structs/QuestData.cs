namespace FrontierDataTool.Structs
{
    /// <summary>
    /// All quest data.
    /// </summary>
    public class QuestData
    {
        public string Title { get; set; }
        public string TextMain { get; set; }
        public string TextSubA { get; set; }
        public string TextSubB { get; set; }

        public byte Unk1 { get; set; }
        public byte Unk2 { get; set; }
        public byte Unk3 { get; set; }
        public byte Unk4 { get; set; }
        public byte Level { get; set; }
        public byte Unk5 { get; set; }
        public byte CourseType { get; set; }    // 6 = Premium, 18 = Free?, 19 = HLC?, 20 = Extra
        public byte Unk7 { get; set; }
        public byte Unk8 { get; set; }
        public byte Unk9 { get; set; }
        public byte Unk10 { get; set; }
        public byte Unk11 { get; set; }
        public int Fee { get; set; }
        public int ZennyMain { get; set; }
        public int ZennyKo { get; set; }
        public int ZennySubA { get; set; }
        public int ZennySubB { get; set; }
        public int Time { get; set; }
        public int Unk12 { get; set; }
        public byte Unk13 { get; set; }
        public byte Unk14 { get; set; }
        public byte Unk15 { get; set; }
        public byte Unk16 { get; set; }
        public byte Unk17 { get; set; }
        public byte Unk18 { get; set; }
        public byte Unk19 { get; set; }
        public byte Unk20 { get; set; }
        public string MainGoalType { get; set; }
        public short MainGoalTarget { get; set; }
        public short MainGoalCount { get; set; }
        public string SubAGoalType { get; set; }
        public short SubAGoalTarget { get; set; }
        public short SubAGoalCount { get; set; }
        public string SubBGoalType { get; set; }
        public short SubBGoalTarget { get; set; }
        public short SubBGoalCount { get; set; }

        public int MainGRP { get; set; }
        public int SubAGRP { get; set; }
        public int SubBGRP { get; set; }
    }

    /// <summary>
    /// Types of quests. 
    /// </summary>
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
}
