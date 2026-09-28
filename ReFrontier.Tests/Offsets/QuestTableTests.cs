using System;

using FrontierDataTool.Offsets;

namespace ReFrontier.Tests.Offsets
{
    /// <summary>
    /// Tests for reading quest entries through the section table in the mhfinf header.
    /// </summary>
    public class QuestTableTests
    {
        private const int EntrySize = 0x160;

        /// <summary>
        /// A small mhfinf laid out the way the ZZ client's is: null slot 0, entries listed in
        /// reverse file order, a repeated pointer and an empty section.
        /// </summary>
        internal static byte[] CreateMhfinfWithTable()
        {
            byte[] file = new byte[0x1000];
            Write32(file, QuestTable.QuantitiesPointer, 0x100);
            Write32(file, QuestTable.SectionTablePointer, 0x120);
            Write16(file, 0x100, 3);

            WriteRow(file, 0x120, endId: 100, slots: 3, list: 0x200);
            WriteRow(file, 0x128, endId: 200, slots: 0, list: 0);
            WriteRow(file, 0x130, endId: 300, slots: 3, list: 0x240);

            Write32(file, 0x204, 0x700);
            Write32(file, 0x208, 0x400);
            Write32(file, 0x244, 0x860);
            Write32(file, 0x248, 0x860);
            return file;
        }

        [Fact]
        public void TryReadEntryOffsets_ReturnsDistinctEntriesInFileOrder()
        {
            var offsets = QuestTable.TryReadEntryOffsets(CreateMhfinfWithTable(), EntrySize, out string? problem);

            Assert.Null(problem);
            Assert.Equal([0x400, 0x700, 0x860], offsets);
        }

        [Fact]
        public void TryReadEntryOffsets_ZeroedHeader_ReturnsNull()
        {
            var offsets = QuestTable.TryReadEntryOffsets(new byte[0x1000], EntrySize, out string? problem);

            Assert.Null(offsets);
            Assert.NotNull(problem);
        }

        [Fact]
        public void TryReadEntryOffsets_TooShortForHeader_ReturnsNull()
        {
            Assert.Null(QuestTable.TryReadEntryOffsets(new byte[0x10], EntrySize, out _));
        }

        [Fact]
        public void TryReadEntryOffsets_EntryPastEnd_ReturnsNull()
        {
            byte[] file = CreateMhfinfWithTable();
            Write32(file, 0x208, file.Length - 0x10);

            Assert.Null(QuestTable.TryReadEntryOffsets(file, EntrySize, out string? problem));
            Assert.Contains("whole entry", problem);
        }

        [Fact]
        public void TryReadEntryOffsets_OverlappingEntries_ReturnsNull()
        {
            byte[] file = CreateMhfinfWithTable();
            Write32(file, 0x208, 0x700 - 0x20);

            Assert.Null(QuestTable.TryReadEntryOffsets(file, EntrySize, out string? problem));
            Assert.Contains("overlap", problem);
        }

        [Fact]
        public void TryReadEntryOffsets_EndIdsNotAscending_ReturnsNull()
        {
            byte[] file = CreateMhfinfWithTable();
            Write16(file, 0x130, 50);

            Assert.Null(QuestTable.TryReadEntryOffsets(file, EntrySize, out _));
        }

        [Fact]
        public void TryReadEntryOffsets_ListPastEnd_ReturnsNull()
        {
            byte[] file = CreateMhfinfWithTable();
            Write32(file, 0x134, file.Length - 4);

            Assert.Null(QuestTable.TryReadEntryOffsets(file, EntrySize, out _));
        }

        [Fact]
        public void Resolve_WithTable_UsesTheTable()
        {
            var offsets = QuestTable.Resolve(CreateMhfinfWithTable(), OffsetProfiles.Default.MhfInf, out string source);

            Assert.Equal(3, offsets.Count);
            Assert.Contains("header lists 3 entries", source);
        }

        [Fact]
        public void Resolve_WithoutTable_FallsBackToProfileSections()
        {
            var mhfInf = OffsetProfiles.Default.MhfInf;

            var offsets = QuestTable.Resolve(new byte[0x1000], mhfInf, out string source);

            Assert.Equal(mhfInf.TotalQuestCount, offsets.Count);
            Assert.Equal(mhfInf.QuestSections[0].Offset, offsets[0]);
            Assert.Equal(mhfInf.QuestSections[0].Offset + EntrySize, offsets[1]);
            Assert.Contains("profile's", source);
        }

        private static void WriteRow(byte[] file, int at, int endId, int slots, int list)
        {
            Write16(file, at, endId);
            Write16(file, at + 2, slots);
            Write32(file, at + 4, list);
        }

        private static void Write16(byte[] file, int at, int value) =>
            BitConverter.GetBytes((ushort)value).CopyTo(file, at);

        private static void Write32(byte[] file, int at, int value) =>
            BitConverter.GetBytes(value).CopyTo(file, at);
    }
}
