using System;
using System.Linq;

using FrontierDataTool.Offsets;

namespace ReFrontier.Tests.Offsets
{
    /// <summary>
    /// Tests for working out which game version a set of files came from.
    /// </summary>
    public class OffsetProfileDetectorTests
    {
        /// <summary>
        /// A file whose every pointer holds an offset inside itself, so any profile
        /// pointing into it resolves. Big enough to hold the last quest section, which
        /// ends at 0x162D80.
        /// </summary>
        private static byte[] SelfConsistentFile(int size = 0x200000)
        {
            // Every 4-byte slot reads as 0, which is inside the file and never runs backwards.
            return new byte[size];
        }

        [Fact]
        public void Score_CountsNothingWhenThereAreNoFilesToJudge()
        {
            var score = OffsetProfileDetector.Score(OffsetProfiles.Default, null, null, null);

            Assert.Equal(0, score.Checked);
            Assert.False(score.Matches);
            Assert.Equal(0, score.Ratio);
        }

        [Fact]
        public void Score_AcceptsAFileEveryPointerFits()
        {
            var file = SelfConsistentFile();

            var score = OffsetProfileDetector.Score(OffsetProfiles.Default, file, file, file);

            Assert.True(score.Matches, score.FirstProblem);
            Assert.Equal(score.Checked, score.Plausible);
        }

        [Fact]
        public void Score_RejectsAFileTooSmallToHoldTheData()
        {
            var tiny = new byte[0x40];

            var score = OffsetProfileDetector.Score(OffsetProfiles.Default, tiny, tiny, tiny);

            Assert.False(score.Matches);
            Assert.NotNull(score.FirstProblem);
        }

        [Fact]
        public void Score_RejectsARegionThatEndsBeforeItStarts()
        {
            var file = SelfConsistentFile();
            var skills = OffsetProfiles.Default.MhfPac.Skills;
            // Make the tree-name region run backwards: start after end.
            BitConverter.GetBytes(0x2000).CopyTo(file, skills.TreeNameStart);
            BitConverter.GetBytes(0x1000).CopyTo(file, skills.TreeNameEnd);

            var score = OffsetProfileDetector.Score(OffsetProfiles.Default, null, file, null);

            Assert.False(score.Matches);
            Assert.Contains("back to", score.FirstProblem, StringComparison.Ordinal);
        }

        [Fact]
        public void Detect_ExplainsItselfWhenNothingMatches()
        {
            var tiny = new byte[0x40];

            var ex = Assert.Throws<InvalidOperationException>(
                () => OffsetProfileDetector.Detect(tiny, tiny, tiny));

            // The message has to name the problem, since it replaces a bare
            // "attempt was made to move the position before the beginning of the stream".
            Assert.Contains("No known offset profile matches", ex.Message, StringComparison.Ordinal);
            Assert.Contains("--offsets", ex.Message, StringComparison.Ordinal);
            Assert.Contains("zz", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Detect_ReturnsTheProfileThatFits()
        {
            var file = SelfConsistentFile();

            Assert.Equal("zz", OffsetProfileDetector.Detect(file, file, file).Id);
        }

        [Fact]
        public void Score_RanksEveryBuiltInProfile()
        {
            var file = SelfConsistentFile();

            var scores = OffsetProfileDetector.Score(file, file, file);

            Assert.Equal(OffsetProfiles.BuiltIn.Count, scores.Count);
            Assert.True(scores.SequenceEqual(scores.OrderByDescending(s => s.Ratio)));
        }
    }
}
