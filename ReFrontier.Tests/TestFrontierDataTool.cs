namespace ReFrontier.Tests
{
    /// <summary>
    /// Tests for FrontierDataTool utility methods.
    /// </summary>
    public class TestFrontierDataTool
    {
        #region GetModelIdData Tests

        [Theory]
        [InlineData(0, "we000")]
        [InlineData(1, "we001")]
        [InlineData(500, "we500")]
        [InlineData(999, "we999")]
        public void GetModelIdData_Range0To999_ReturnsWePrefix(int id, string expected)
        {
            string result = FrontierDataTool.Program.GetModelIdData(id);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(1000, "wf000")]
        [InlineData(1001, "wf001")]
        [InlineData(1500, "wf500")]
        [InlineData(1999, "wf999")]
        public void GetModelIdData_Range1000To1999_ReturnsWfPrefix(int id, string expected)
        {
            string result = FrontierDataTool.Program.GetModelIdData(id);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(2000, "wg000")]
        [InlineData(2001, "wg001")]
        [InlineData(2500, "wg500")]
        [InlineData(2999, "wg999")]
        public void GetModelIdData_Range2000To2999_ReturnsWgPrefix(int id, string expected)
        {
            string result = FrontierDataTool.Program.GetModelIdData(id);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(3000, "wh000")]
        [InlineData(3001, "wh001")]
        [InlineData(3500, "wh500")]
        [InlineData(3999, "wh999")]
        public void GetModelIdData_Range3000To3999_ReturnsWhPrefix(int id, string expected)
        {
            string result = FrontierDataTool.Program.GetModelIdData(id);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(4000, "wi000")]
        [InlineData(4001, "wi001")]
        [InlineData(4500, "wi500")]
        [InlineData(4999, "wi999")]
        public void GetModelIdData_Range4000To4999_ReturnsWiPrefix(int id, string expected)
        {
            string result = FrontierDataTool.Program.GetModelIdData(id);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(5000, "wk-1000")]
        [InlineData(5500, "wk-500")]
        [InlineData(5999, "wk-001")]
        public void GetModelIdData_Range5000To5999_ReturnsWkWithNegativeOffset(int id, string expected)
        {
            // Note: This range uses (id - 6000) which produces negative offsets
            // This appears to be intentional based on the original code design
            string result = FrontierDataTool.Program.GetModelIdData(id);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(6000, "wk000")]
        [InlineData(6001, "wk001")]
        [InlineData(6500, "wk500")]
        [InlineData(6999, "wk999")]
        public void GetModelIdData_Range6000To6999_ReturnsWkPrefix(int id, string expected)
        {
            string result = FrontierDataTool.Program.GetModelIdData(id);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(7000, "wl000")]
        [InlineData(7001, "wl001")]
        [InlineData(7500, "wl500")]
        [InlineData(7999, "wl999")]
        public void GetModelIdData_Range7000To7999_ReturnsWlPrefix(int id, string expected)
        {
            string result = FrontierDataTool.Program.GetModelIdData(id);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(8000, "wm000")]
        [InlineData(8001, "wm001")]
        [InlineData(8500, "wm500")]
        [InlineData(8999, "wm999")]
        public void GetModelIdData_Range8000To8999_ReturnsWmPrefix(int id, string expected)
        {
            string result = FrontierDataTool.Program.GetModelIdData(id);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(9000, "wg000")]
        [InlineData(9001, "wg001")]
        [InlineData(9500, "wg500")]
        [InlineData(9999, "wg999")]
        public void GetModelIdData_Range9000To9999_ReturnsWgPrefix(int id, string expected)
        {
            // Note: This range also uses wg prefix (same as 2000-2999)
            string result = FrontierDataTool.Program.GetModelIdData(id);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(10000, "Unmapped")]
        [InlineData(15000, "Unmapped")]
        [InlineData(int.MaxValue, "Unmapped")]
        public void GetModelIdData_Above9999_ReturnsUnmapped(int id, string expected)
        {
            string result = FrontierDataTool.Program.GetModelIdData(id);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(-1, "wf-1001")]
        [InlineData(-100, "wf-1100")]
        public void GetModelIdData_NegativeValues_FallsThroughToWfBranch(int id, string expected)
        {
            // Note: Negative values fail the first condition (id >= 0 && id < 1000)
            // but pass (id < 2000), so they fall through to wf branch
            // This is current behavior - may need review
            string result = FrontierDataTool.Program.GetModelIdData(id);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void GetModelIdData_BoundaryValues_AreCorrect()
        {
            // Test exact boundaries between ranges
            Assert.Equal("we999", FrontierDataTool.Program.GetModelIdData(999));
            Assert.Equal("wf000", FrontierDataTool.Program.GetModelIdData(1000));

            Assert.Equal("wf999", FrontierDataTool.Program.GetModelIdData(1999));
            Assert.Equal("wg000", FrontierDataTool.Program.GetModelIdData(2000));

            Assert.Equal("wg999", FrontierDataTool.Program.GetModelIdData(2999));
            Assert.Equal("wh000", FrontierDataTool.Program.GetModelIdData(3000));

            Assert.Equal("wh999", FrontierDataTool.Program.GetModelIdData(3999));
            Assert.Equal("wi000", FrontierDataTool.Program.GetModelIdData(4000));

            Assert.Equal("wi999", FrontierDataTool.Program.GetModelIdData(4999));
            Assert.Equal("wk-1000", FrontierDataTool.Program.GetModelIdData(5000)); // Uses wk with negative offset

            Assert.Equal("wk-001", FrontierDataTool.Program.GetModelIdData(5999)); // Uses wk with negative offset
            Assert.Equal("wk000", FrontierDataTool.Program.GetModelIdData(6000));

            Assert.Equal("wk999", FrontierDataTool.Program.GetModelIdData(6999));
            Assert.Equal("wl000", FrontierDataTool.Program.GetModelIdData(7000));

            Assert.Equal("wl999", FrontierDataTool.Program.GetModelIdData(7999));
            Assert.Equal("wm000", FrontierDataTool.Program.GetModelIdData(8000));

            Assert.Equal("wm999", FrontierDataTool.Program.GetModelIdData(8999));
            Assert.Equal("wg000", FrontierDataTool.Program.GetModelIdData(9000));

            Assert.Equal("wg999", FrontierDataTool.Program.GetModelIdData(9999));
            Assert.Equal("Unmapped", FrontierDataTool.Program.GetModelIdData(10000));
        }

        #endregion
    }
}
