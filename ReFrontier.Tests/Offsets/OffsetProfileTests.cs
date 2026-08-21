using System;
using System.IO;
using System.Linq;
using System.Text.Json;

using FrontierDataTool.Offsets;

namespace ReFrontier.Tests.Offsets
{
    /// <summary>
    /// Tests for the offset profiles that replaced the compiled-in constants.
    /// </summary>
    public class OffsetProfileTests
    {
        private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

        [Fact]
        public void BuiltIn_ContainsTheDefaultProfile()
        {
            Assert.Contains(OffsetProfiles.BuiltIn, p => p.Id == OffsetProfiles.DefaultId);
            Assert.Equal("zz", OffsetProfiles.Default.Id);
            Assert.False(string.IsNullOrWhiteSpace(OffsetProfiles.Default.Description));
        }

        [Fact]
        public void BuiltIn_EveryProfileValidates()
        {
            foreach (var profile in OffsetProfiles.BuiltIn)
            {
                Assert.Empty(OffsetProfileValidator.Validate(profile));
            }
        }

        [Fact]
        public void Default_KeepsTheValuesTheConstantsHeld()
        {
            var zz = OffsetProfiles.Default;

            // The values MhfDataOffsets held before the offsets became data. If one of these
            // changes, a dump changes with it.
            Assert.Equal(5, zz.MhfDat.Armor.DataPointers.Count);
            Assert.Equal(5, zz.MhfDat.Armor.StringPointers.Count);
            Assert.Equal(new[] { "頭", "胴", "腕", "腰", "脚" }, zz.MhfDat.Armor.SlotNames);
            Assert.Equal(0x50, zz.MhfDat.Armor.DataPointers[0].Start);
            Assert.Equal(0xE8, zz.MhfDat.Armor.DataPointers[0].End);
            Assert.Equal(0x7C, zz.MhfDat.Weapons.MeleeStart);
            Assert.Equal(0xA20, zz.MhfPac.Skills.TreeNameStart);
            Assert.Equal(0x100, zz.MhfDat.Items.StringStart);
            Assert.Equal(13, zz.MhfInf.QuestSections.Count);
            Assert.Equal(0x6BD40, zz.MhfInf.QuestSections[0].Offset);
            Assert.Equal(1092, zz.MhfInf.TotalQuestCount);
        }

        [Fact]
        public void Default_QuestEntrySizeIsTheStrideTheReaderConsumes()
        {
            // The importer used to step 0x128 between entries while the reader consumed
            // 0x160, so writing quests back moved 754 of 1092 of them onto each other.
            Assert.Equal(0x160, OffsetProfiles.Default.MhfInf.QuestEntrySize);
        }

        [Fact]
        public void Resolve_FindsBuiltInProfilesByIdAndRejectsUnknownNames()
        {
            Assert.Equal("zz", OffsetProfiles.Resolve("zz").Id);
            Assert.Equal("zz", OffsetProfiles.Resolve("ZZ").Id);

            var ex = Assert.Throws<InvalidOperationException>(() => OffsetProfiles.Resolve("nope"));
            Assert.Contains("neither a built-in offset profile nor a file", ex.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("\"0x160\"", 0x160)]
        [InlineData("\"0X6BD40\"", 0x6BD40)]
        [InlineData("352", 352)]
        [InlineData("\"352\"", 352)]
        public void HexIntConverter_AcceptsHexStringsAndPlainNumbers(string json, int expected)
        {
            Assert.Equal(expected, JsonSerializer.Deserialize<int>(json, WithConverter()));
        }

        [Fact]
        public void HexIntConverter_RejectsSomethingThatIsNotAnOffset()
        {
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<int>("\"lots\"", WithConverter()));
        }

        [Fact]
        public void HexIntConverter_WritesHexSoAProfileStaysReadable()
        {
            Assert.Equal("\"0x6BD40\"", JsonSerializer.Serialize(0x6BD40, WithConverter()));
        }

        [Fact]
        public void Validator_CatchesAPointerListThatDoesNotCoverEverySlot()
        {
            var profile = OffsetProfiles.Default with
            {
                MhfDat = OffsetProfiles.Default.MhfDat with
                {
                    Armor = OffsetProfiles.Default.MhfDat.Armor with
                    {
                        DataPointers = OffsetProfiles.Default.MhfDat.Armor.DataPointers.Take(3).ToList()
                    }
                }
            };

            Assert.Contains(
                OffsetProfileValidator.Validate(profile),
                message => message.Contains("dataPointers", StringComparison.Ordinal));
        }

        [Fact]
        public void Validator_CatchesSectionsThatClaimTheSameBytes()
        {
            var profile = OffsetProfiles.Default with
            {
                MhfInf = OffsetProfiles.Default.MhfInf with
                {
                    QuestSections = [new QuestSection(0x1000, 10), new QuestSection(0x1100, 10)]
                }
            };

            Assert.Contains(
                OffsetProfileValidator.Validate(profile),
                message => message.Contains("overlaps", StringComparison.Ordinal));
        }

        [Fact]
        public void Validator_CatchesANegativeOffset()
        {
            var profile = OffsetProfiles.Default with
            {
                MhfPac = new MhfPacOffsets { Skills = new SkillOffsets { TreeNameStart = -4 } }
            };

            Assert.Contains(
                OffsetProfileValidator.Validate(profile),
                message => message.Contains("treeNameStart", StringComparison.Ordinal));
        }

        [Fact]
        public void Load_RejectsAFileThatIsNotAProfile()
        {
            string path = Path.Combine(Path.GetTempPath(), $"offsets-{Guid.NewGuid():N}.json");
            File.WriteAllText(path, "{ not json");
            try
            {
                var ex = Assert.Throws<InvalidOperationException>(() => OffsetProfiles.Load(path));
                Assert.Contains("not valid JSON", ex.Message, StringComparison.Ordinal);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void Profile_SurvivesARoundTripThroughJson()
        {
            string json = JsonSerializer.Serialize(OffsetProfiles.Default, Options);
            var back = JsonSerializer.Deserialize<OffsetProfile>(json, Options);

            Assert.NotNull(back);
            Assert.Equal(OffsetProfiles.Default.MhfInf.QuestSections, back.MhfInf.QuestSections);
            Assert.Equal(OffsetProfiles.Default.MhfDat.Armor.SlotNames, back.MhfDat.Armor.SlotNames);
        }

        private static JsonSerializerOptions WithConverter()
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new HexIntConverter());
            return options;
        }
    }
}
