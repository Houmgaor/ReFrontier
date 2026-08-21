using System;
using System.Collections.Generic;
using System.Linq;

namespace FrontierDataTool.Offsets
{
    /// <summary>
    /// How well a profile explains a particular set of game files.
    /// </summary>
    /// <param name="Profile">The profile scored.</param>
    /// <param name="Checked">Number of pointers examined.</param>
    /// <param name="Plausible">Number that led somewhere the file actually has.</param>
    /// <param name="FirstProblem">The first pointer that did not, if any.</param>
    public sealed record ProfileScore(
        OffsetProfile Profile, int Checked, int Plausible, string? FirstProblem)
    {
        /// <summary>
        /// Share of pointers that landed inside the file, from 0 to 1.
        /// </summary>
        public double Ratio => Checked == 0 ? 0 : (double)Plausible / Checked;

        /// <summary>
        /// Whether the profile is good enough to read the file with.
        /// </summary>
        /// <remarks>
        /// A profile for the wrong version does not miss by a little: its pointers land on
        /// unrelated bytes and read as wild offsets, so the ratio collapses rather than
        /// dipping. Anything short of every pointer resolving means the layout is not this
        /// one.
        /// </remarks>
        public bool Matches => Checked > 0 && Plausible == Checked;
    }

    /// <summary>
    /// Works out which offset profile describes a given set of game files.
    /// </summary>
    /// <remarks>
    /// The files carry no version field to read, so instead of fingerprinting them each
    /// profile is tried and judged on whether its pointers make sense: an offset held at a
    /// pointer has to land inside the file, and a region has to end after it starts. A
    /// profile from the wrong era fails this immediately, which is exactly how the quest
    /// offsets were found to be wrong in the first place.
    /// </remarks>
    public static class OffsetProfileDetector
    {
        /// <summary>
        /// Score every built-in profile against a set of files, best first.
        /// </summary>
        /// <param name="mhfDat">Decrypted, decompressed mhfdat.bin, or null to skip it.</param>
        /// <param name="mhfPac">Decrypted, decompressed mhfpac.bin, or null to skip it.</param>
        /// <param name="mhfInf">Decrypted, decompressed mhfinf.bin, or null to skip it.</param>
        /// <returns>One score per built-in profile, best first.</returns>
        public static IReadOnlyList<ProfileScore> Score(byte[]? mhfDat, byte[]? mhfPac, byte[]? mhfInf) =>
            OffsetProfiles.BuiltIn
                .Select(profile => Score(profile, mhfDat, mhfPac, mhfInf))
                .OrderByDescending(score => score.Ratio)
                .ThenBy(score => score.Profile.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();

        /// <summary>
        /// Pick the profile that reads these files, or explain why none does.
        /// </summary>
        /// <param name="mhfDat">Decrypted, decompressed mhfdat.bin, or null to skip it.</param>
        /// <param name="mhfPac">Decrypted, decompressed mhfpac.bin, or null to skip it.</param>
        /// <param name="mhfInf">Decrypted, decompressed mhfinf.bin, or null to skip it.</param>
        /// <returns>The profile to use.</returns>
        /// <exception cref="InvalidOperationException">No profile reads these files.</exception>
        public static OffsetProfile Detect(byte[]? mhfDat, byte[]? mhfPac, byte[]? mhfInf)
        {
            var scores = Score(mhfDat, mhfPac, mhfInf);
            var best = scores.FirstOrDefault(s => s.Matches);
            if (best is not null)
            {
                return best.Profile;
            }

            var closest = scores.Count > 0 ? scores[0] : null;
            string detail = closest is null
                ? "No offset profiles are built in."
                : $"The closest is '{closest.Profile.Id}', where {closest.Plausible} of " +
                  $"{closest.Checked} pointers resolve. {closest.FirstProblem}";

            throw new InvalidOperationException(
                "No known offset profile matches these files, so they are from a game version " +
                $"this tool cannot read yet. {detail}" + Environment.NewLine +
                "Built-in profiles:" + Environment.NewLine + OffsetProfiles.DescribeBuiltIn() +
                Environment.NewLine +
                "Give one with --offsets <id|file.json> to override this check.");
        }

        /// <summary>
        /// Judge one profile against one set of files.
        /// </summary>
        /// <param name="profile">Profile to judge.</param>
        /// <param name="mhfDat">Decrypted, decompressed mhfdat.bin, or null to skip it.</param>
        /// <param name="mhfPac">Decrypted, decompressed mhfpac.bin, or null to skip it.</param>
        /// <param name="mhfInf">Decrypted, decompressed mhfinf.bin, or null to skip it.</param>
        /// <returns>How well it explains them.</returns>
        public static ProfileScore Score(
            OffsetProfile profile, byte[]? mhfDat, byte[]? mhfPac, byte[]? mhfInf)
        {
            ArgumentNullException.ThrowIfNull(profile);

            int checkedCount = 0;
            int plausible = 0;
            string? firstProblem = null;

            void Check(bool ok, string describe)
            {
                checkedCount++;
                if (ok)
                {
                    plausible++;
                }
                else
                {
                    firstProblem ??= describe;
                }
            }

            if (mhfDat is not null)
            {
                var armor = profile.MhfDat.Armor;
                for (int i = 0; i < armor.DataPointers.Count; i++)
                {
                    CheckRegion(mhfDat, armor.DataPointers[i], $"mhfDat.armor.dataPointers[{i}]", Check);
                }
                for (int i = 0; i < armor.StringPointers.Count; i++)
                {
                    CheckRegion(mhfDat, armor.StringPointers[i], $"mhfDat.armor.stringPointers[{i}]", Check);
                }

                var w = profile.MhfDat.Weapons;
                CheckRegion(mhfDat, new PointerPair(w.MeleeStart, w.MeleeEnd), "mhfDat.weapons.melee", Check);
                CheckRegion(mhfDat, new PointerPair(w.RangedStart, w.RangedEnd), "mhfDat.weapons.ranged", Check);
                CheckPointer(mhfDat, w.MeleeStringStart, "mhfDat.weapons.meleeStringStart", Check);
                CheckPointer(mhfDat, w.RangedStringStart, "mhfDat.weapons.rangedStringStart", Check);

                var items = profile.MhfDat.Items;
                CheckRegion(mhfDat, new PointerPair(items.StringStart, items.StringEnd), "mhfDat.items.strings", Check);
                CheckRegion(
                    mhfDat,
                    new PointerPair(items.DescriptionStart, items.DescriptionEnd),
                    "mhfDat.items.descriptions",
                    Check);
            }

            if (mhfPac is not null)
            {
                var s = profile.MhfPac.Skills;
                CheckRegion(mhfPac, new PointerPair(s.TreeNameStart, s.TreeNameEnd), "mhfPac.skills.treeNames", Check);
                CheckRegion(
                    mhfPac, new PointerPair(s.ActiveNameStart, s.ActiveNameEnd), "mhfPac.skills.activeNames", Check);
                CheckRegion(
                    mhfPac, new PointerPair(s.ZSkillNameStart, s.ZSkillNameEnd), "mhfPac.skills.zSkillNames", Check);
                CheckPointer(mhfPac, s.DescriptionStart, "mhfPac.skills.descriptionStart", Check);
                CheckPointer(mhfPac, s.DescriptionEnd, "mhfPac.skills.descriptionEnd", Check);
            }

            if (mhfInf is not null)
            {
                var sections = profile.MhfInf.QuestSections;
                int entrySize = profile.MhfInf.QuestEntrySize;
                for (int i = 0; i < sections.Count; i++)
                {
                    long end = (long)sections[i].Offset + ((long)sections[i].Count * entrySize);
                    Check(
                        sections[i].Offset >= 0 && end <= mhfInf.Length,
                        $"mhfInf.questSections[{i}] runs to 0x{end:X}, past the end of a " +
                        $"0x{mhfInf.Length:X}-byte file.");
                }
            }

            return new ProfileScore(profile, checkedCount, plausible, firstProblem);
        }

        /// <summary>
        /// A pointer has to sit inside the file and hold an offset that also does.
        /// </summary>
        private static void CheckPointer(byte[] file, int pointer, string name, Action<bool, string> check)
        {
            if (!TryReadOffset(file, pointer, out int target))
            {
                check(false, $"{name} is at 0x{pointer:X}, past the end of a 0x{file.Length:X}-byte file.");
                return;
            }

            check(
                target >= 0 && target <= file.Length,
                $"{name} at 0x{pointer:X} holds 0x{target:X}, which is outside a 0x{file.Length:X}-byte file.");
        }

        /// <summary>
        /// Both ends of a region have to resolve, and it has to end after it starts.
        /// </summary>
        private static void CheckRegion(byte[] file, PointerPair pair, string name, Action<bool, string> check)
        {
            if (!TryReadOffset(file, pair.Start, out int start) || !TryReadOffset(file, pair.End, out int end))
            {
                check(false, $"{name} points past the end of a 0x{file.Length:X}-byte file.");
                return;
            }

            bool inside = start >= 0 && end >= 0 && start <= file.Length && end <= file.Length;
            check(
                inside && end >= start,
                inside
                    ? $"{name} runs from 0x{start:X} back to 0x{end:X}."
                    : $"{name} spans 0x{start:X}..0x{end:X}, outside a 0x{file.Length:X}-byte file.");
        }

        private static bool TryReadOffset(byte[] file, int position, out int value)
        {
            value = 0;
            if (position < 0 || position + sizeof(int) > file.Length)
            {
                return false;
            }

            value = BitConverter.ToInt32(file, position);
            return true;
        }
    }
}
