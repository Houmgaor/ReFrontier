using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace FrontierDataTool.Offsets
{
    /// <summary>
    /// Checks that a profile is self-consistent before anything reads a file with it.
    /// </summary>
    /// <remarks>
    /// Moving the offsets out of C# trades a compile error for a runtime one, so the checks
    /// a compiler used to make have to happen on load instead. These are the ones that need
    /// no game file; <see cref="OffsetProfileDetector"/> makes the rest against a real one.
    /// </remarks>
    public static class OffsetProfileValidator
    {
        /// <summary>
        /// Describe everything wrong with a profile.
        /// </summary>
        /// <param name="profile">Profile to check.</param>
        /// <returns>One message per problem; empty when the profile is usable.</returns>
        public static IReadOnlyList<string> Validate(OffsetProfile profile)
        {
            ArgumentNullException.ThrowIfNull(profile);
            var problems = new List<string>();

            if (string.IsNullOrWhiteSpace(profile.Id))
            {
                problems.Add("The profile has no id.");
            }

            var armor = profile.MhfDat.Armor;
            int slots = armor.SlotNames.Count;
            if (slots == 0)
            {
                problems.Add("mhfDat.armor.slotNames is empty: there is nothing to dump.");
            }
            if (armor.DataPointers.Count != slots)
            {
                problems.Add(
                    $"mhfDat.armor has {armor.DataPointers.Count} dataPointers for {slots} slots; " +
                    "there must be one per slot.");
            }
            if (armor.StringPointers.Count != slots)
            {
                problems.Add(
                    $"mhfDat.armor has {armor.StringPointers.Count} stringPointers for {slots} slots; " +
                    "there must be one per slot.");
            }

            foreach (var (name, value) in EveryOffset(profile))
            {
                if (value < 0)
                {
                    problems.Add($"{name} is negative (0x{value:X}).");
                }
            }

            int entrySize = profile.MhfInf.QuestEntrySize;
            if (entrySize <= 0)
            {
                problems.Add($"mhfInf.questEntrySize must be positive, not 0x{entrySize:X}.");
            }

            var sections = profile.MhfInf.QuestSections;
            for (int i = 0; i < sections.Count; i++)
            {
                if (sections[i].Count <= 0)
                {
                    problems.Add($"mhfInf.questSections[{i}] holds {sections[i].Count} quests.");
                }
            }

            if (entrySize > 0)
            {
                problems.AddRange(OverlappingSections(sections, entrySize));
            }

            return problems;
        }

        /// <summary>
        /// Throw unless the profile is usable.
        /// </summary>
        /// <param name="profile">Profile to check.</param>
        /// <param name="source">Where it came from, for the message.</param>
        /// <exception cref="InvalidOperationException">The profile is not usable.</exception>
        public static void ThrowIfInvalid(OffsetProfile profile, string source)
        {
            var problems = Validate(profile);
            if (problems.Count > 0)
            {
                throw new InvalidOperationException(
                    $"The offset profile in {source} cannot be used:{Environment.NewLine}  " +
                    string.Join(Environment.NewLine + "  ", problems));
            }
        }

        /// <summary>
        /// Two sections claiming the same bytes means one of them is wrong.
        /// </summary>
        private static IEnumerable<string> OverlappingSections(
            IReadOnlyList<QuestSection> sections, int entrySize)
        {
            var ordered = sections
                .Select((section, index) => (section, index))
                .OrderBy(pair => pair.section.Offset)
                .ToList();

            for (int i = 1; i < ordered.Count; i++)
            {
                var (previous, previousIndex) = ordered[i - 1];
                var (current, currentIndex) = ordered[i];
                long previousEnd = (long)previous.Offset + ((long)previous.Count * entrySize);
                if (previousEnd > current.Offset)
                {
                    yield return string.Format(
                        CultureInfo.InvariantCulture,
                        "mhfInf.questSections[{0}] runs to 0x{1:X} and overlaps [{2}], which starts at 0x{3:X}.",
                        previousIndex, previousEnd, currentIndex, current.Offset);
                }
            }
        }

        /// <summary>
        /// Every offset in the profile, named as it is written in JSON.
        /// </summary>
        internal static IEnumerable<(string Name, int Value)> EveryOffset(OffsetProfile profile)
        {
            var armor = profile.MhfDat.Armor;
            for (int i = 0; i < armor.DataPointers.Count; i++)
            {
                yield return ($"mhfDat.armor.dataPointers[{i}].start", armor.DataPointers[i].Start);
                yield return ($"mhfDat.armor.dataPointers[{i}].end", armor.DataPointers[i].End);
            }
            for (int i = 0; i < armor.StringPointers.Count; i++)
            {
                yield return ($"mhfDat.armor.stringPointers[{i}].start", armor.StringPointers[i].Start);
                yield return ($"mhfDat.armor.stringPointers[{i}].end", armor.StringPointers[i].End);
            }

            var w = profile.MhfDat.Weapons;
            yield return ("mhfDat.weapons.meleeStart", w.MeleeStart);
            yield return ("mhfDat.weapons.meleeEnd", w.MeleeEnd);
            yield return ("mhfDat.weapons.meleeStringStart", w.MeleeStringStart);
            yield return ("mhfDat.weapons.rangedStart", w.RangedStart);
            yield return ("mhfDat.weapons.rangedEnd", w.RangedEnd);
            yield return ("mhfDat.weapons.rangedStringStart", w.RangedStringStart);

            var items = profile.MhfDat.Items;
            yield return ("mhfDat.items.stringStart", items.StringStart);
            yield return ("mhfDat.items.stringEnd", items.StringEnd);
            yield return ("mhfDat.items.descriptionStart", items.DescriptionStart);
            yield return ("mhfDat.items.descriptionEnd", items.DescriptionEnd);

            var s = profile.MhfPac.Skills;
            yield return ("mhfPac.skills.treeNameStart", s.TreeNameStart);
            yield return ("mhfPac.skills.treeNameEnd", s.TreeNameEnd);
            yield return ("mhfPac.skills.activeNameStart", s.ActiveNameStart);
            yield return ("mhfPac.skills.activeNameEnd", s.ActiveNameEnd);
            yield return ("mhfPac.skills.descriptionStart", s.DescriptionStart);
            yield return ("mhfPac.skills.descriptionEnd", s.DescriptionEnd);
            yield return ("mhfPac.skills.zSkillNameStart", s.ZSkillNameStart);
            yield return ("mhfPac.skills.zSkillNameEnd", s.ZSkillNameEnd);

            for (int i = 0; i < profile.MhfInf.QuestSections.Count; i++)
            {
                yield return ($"mhfInf.questSections[{i}].offset", profile.MhfInf.QuestSections[i].Offset);
            }
        }
    }
}
