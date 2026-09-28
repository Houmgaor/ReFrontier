using System;
using System.Collections.Generic;
using System.Linq;

namespace FrontierDataTool.Offsets
{
    /// <summary>
    /// Finds the quest entries of mhfinf.bin through the index the file carries in its header.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The header holds two pointers: <c>0x10</c> to a block of per-table quantities whose
    /// first u16 is the number of quest sections, and <c>0x14</c> to the section table. Each
    /// section is an 8-byte row <c>{u16 endId, u16 slots, u32 list}</c>, rows in ascending
    /// <c>endId</c>, and <c>list</c> holds <c>slots</c> u32 pointers to quest entries. Slot 0
    /// is always null and a slot may repeat a pointer, so entries are the distinct non-null
    /// pointers.
    /// </para>
    /// <para>
    /// The ZZ PC client has 44 sections and 2839 distinct entries; the hand-listed sections of
    /// the offset profile named 1092 of them. The running client relocates exactly these
    /// pointers after loading the file, so this is the index the game itself reads.
    /// </para>
    /// </remarks>
    public static class QuestTable
    {
        /// <summary>Header field pointing to the quantities block (section count first).</summary>
        public const int QuantitiesPointer = 0x10;

        /// <summary>Header field pointing to the section table.</summary>
        public const int SectionTablePointer = 0x14;

        /// <summary>Size of one section table row.</summary>
        public const int SectionRowSize = 8;

        /// <summary>
        /// Read the quest entry offsets from the section table in the file header.
        /// </summary>
        /// <param name="file">Decrypted, decompressed mhfinf.bin.</param>
        /// <param name="entrySize">Size of one quest entry.</param>
        /// <param name="problem">Why the header was rejected, when it was.</param>
        /// <returns>Distinct entry offsets in file order, or null when the header does not
        /// describe a quest table.</returns>
        public static IReadOnlyList<int>? TryReadEntryOffsets(byte[] file, int entrySize, out string? problem)
        {
            ArgumentNullException.ThrowIfNull(file);
            problem = null;

            if (!TryReadInt32(file, QuantitiesPointer, out int quantities)
                || !TryReadInt32(file, SectionTablePointer, out int table))
            {
                problem = $"a 0x{file.Length:X}-byte file is too short for a header.";
                return null;
            }

            if (quantities < 0 || quantities > file.Length - 2)
            {
                problem = $"the quantities pointer 0x{quantities:X} is outside the file.";
                return null;
            }

            int sectionCount = BitConverter.ToUInt16(file, quantities);
            if (sectionCount == 0)
            {
                problem = "the header lists no quest sections.";
                return null;
            }

            if (table < 0 || (long)table + ((long)sectionCount * SectionRowSize) > file.Length)
            {
                problem = $"a table of {sectionCount} sections at 0x{table:X} runs past the end of the file.";
                return null;
            }

            var entries = new HashSet<int>();
            int previousEndId = -1;
            for (int i = 0; i < sectionCount; i++)
            {
                int row = table + (i * SectionRowSize);
                int endId = BitConverter.ToUInt16(file, row);
                int slots = BitConverter.ToUInt16(file, row + 2);
                int list = BitConverter.ToInt32(file, row + 4);

                if (endId <= previousEndId)
                {
                    problem = $"section {i} ends at ID {endId}, not after the previous section's {previousEndId}.";
                    return null;
                }
                previousEndId = endId;

                if (slots == 0)
                {
                    continue;
                }

                if (list < 0 || (long)list + ((long)slots * 4) > file.Length)
                {
                    problem = $"section {i} lists {slots} slots at 0x{list:X}, past the end of the file.";
                    return null;
                }

                for (int slot = 0; slot < slots; slot++)
                {
                    int entry = BitConverter.ToInt32(file, list + (slot * 4));
                    if (entry == 0)
                    {
                        continue;
                    }

                    if (entry < 0 || (long)entry + entrySize > file.Length)
                    {
                        problem = $"section {i} slot {slot} points to 0x{entry:X}, which does not hold a whole entry.";
                        return null;
                    }
                    entries.Add(entry);
                }
            }

            if (entries.Count == 0)
            {
                problem = "the sections hold no quest entries.";
                return null;
            }

            var sorted = entries.Order().ToList();
            for (int i = 1; i < sorted.Count; i++)
            {
                if (sorted[i] - sorted[i - 1] < entrySize)
                {
                    problem = $"entries at 0x{sorted[i - 1]:X} and 0x{sorted[i]:X} overlap.";
                    return null;
                }
            }

            return sorted;
        }

        /// <summary>
        /// Quest entry offsets from the file's own table, or from the profile when the file has
        /// no usable table.
        /// </summary>
        /// <param name="file">Decrypted, decompressed mhfinf.bin.</param>
        /// <param name="offsets">The profile's mhfinf offsets, used as the fallback.</param>
        /// <param name="source">Which of the two was used, for the log.</param>
        /// <returns>One offset per entry.</returns>
        public static IReadOnlyList<int> Resolve(byte[] file, MhfInfOffsets offsets, out string source)
        {
            ArgumentNullException.ThrowIfNull(offsets);
            var fromHeader = TryReadEntryOffsets(file, offsets.QuestEntrySize, out string? problem);
            if (fromHeader is not null)
            {
                source = $"Quest table in the mhfinf header lists {fromHeader.Count} entries.";
                return fromHeader;
            }

            var fromSections = EntryOffsetsFromSections(offsets);
            source = $"No quest table in the mhfinf header ({problem}) " +
                $"Using the profile's {offsets.QuestSections.Count} quest sections ({fromSections.Count} entries).";
            return fromSections;
        }

        /// <summary>
        /// Entry offsets as listed by the profile's quest sections, in the profile's order.
        /// </summary>
        /// <param name="offsets">The profile's mhfinf offsets.</param>
        /// <returns>One offset per entry.</returns>
        public static IReadOnlyList<int> EntryOffsetsFromSections(MhfInfOffsets offsets)
        {
            ArgumentNullException.ThrowIfNull(offsets);
            var result = new List<int>(offsets.TotalQuestCount);
            foreach (var section in offsets.QuestSections)
            {
                for (int i = 0; i < section.Count; i++)
                {
                    result.Add(section.Offset + (i * offsets.QuestEntrySize));
                }
            }
            return result;
        }

        private static bool TryReadInt32(byte[] file, int offset, out int value)
        {
            if (offset < 0 || offset > file.Length - 4)
            {
                value = 0;
                return false;
            }
            value = BitConverter.ToInt32(file, offset);
            return true;
        }
    }
}
