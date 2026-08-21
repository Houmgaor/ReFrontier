using System;
using System.Collections.Generic;
using System.Linq;

using FrontierDataTool.Enums;

namespace ReFrontier.Tests.DataToolTests
{
    /// <summary>
    /// Tests for the English skill tree name table.
    /// </summary>
    public class SkillLookupTests
    {
        [Fact]
        public void EnglishNamesById_IsInjective()
        {
            // Two skill trees share the display name "Passive" (0x01 and 0x5F). If the table
            // kept both, a name could not be read back to an ID and importing an English
            // dump would rewrite every 0x5F skill to 0x01.
            var names = SkillLookup.EnglishNamesById.Values.ToList();
            Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
        }

        [Fact]
        public void EnglishNamesById_DisambiguatesByIdKeepingTheFirst()
        {
            Assert.Equal("Passive", SkillLookup.EnglishNamesById[0x01]);
            Assert.Equal("Passive (0x5F)", SkillLookup.EnglishNamesById[0x5F]);
        }

        [Fact]
        public void IdsByEnglishName_RoundTripsEveryName()
        {
            foreach (var (id, name) in SkillLookup.EnglishNamesById)
            {
                Assert.True(SkillLookup.IdsByEnglishName.TryGetValue(name, out byte back),
                    $"'{name}' does not read back to an ID.");
                Assert.Equal(id, back);
            }
        }

        [Fact]
        public void ApplyEnglishNames_ReplacesKnownNamesAndKeepsTheRest()
        {
            // Index 0x00 and 0x01 are known; a name past the end of the table is left alone.
            int beyond = SkillLookup.EnglishNamesById.Keys.Max() + 1;
            var gameNames = new List<string>(new string[beyond + 1]);
            for (int i = 0; i <= beyond; i++)
            {
                gameNames[i] = $"game-{i}";
            }

            var result = SkillLookup.ApplyEnglishNames(gameNames);

            Assert.Equal("None", result[0x00]);
            Assert.Equal("Passive", result[0x01]);
            Assert.Equal($"game-{beyond}", result[beyond]);
            Assert.Equal(gameNames.Count, result.Count);
        }

        [Fact]
        public void ApplyEnglishNames_KeepsTheGameNameWhereNoEnglishOneExists()
        {
            // 0x29 has no entry in the table; the game's own string must survive.
            Assert.False(SkillLookup.EnglishNamesById.ContainsKey(0x29));

            var gameNames = Enumerable.Range(0, 0x2A).Select(i => $"game-{i}").ToList();
            var result = SkillLookup.ApplyEnglishNames(gameNames);

            Assert.Equal("game-41", result[0x29]);
        }

        [Fact]
        public void ApplyEnglishNames_RejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => SkillLookup.ApplyEnglishNames(null!));
        }
    }
}
