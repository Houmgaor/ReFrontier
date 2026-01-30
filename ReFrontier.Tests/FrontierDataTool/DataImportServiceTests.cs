using System.Text;

using FrontierDataTool.Services;

using ReFrontier.Tests.Mocks;

namespace ReFrontier.Tests.DataToolTests
{
    /// <summary>
    /// Tests for DataImportService.
    /// </summary>
    public class DataImportServiceTests
    {
        private readonly InMemoryFileSystem _fileSystem;
        private readonly TestLogger _logger;
        private readonly DataImportService _service;

        static DataImportServiceTests()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public DataImportServiceTests()
        {
            _fileSystem = new InMemoryFileSystem();
            _logger = new TestLogger();
            _service = new DataImportService(_fileSystem, _logger);
        }

        #region BuildSkillLookup Tests

        [Fact]
        public void BuildSkillLookup_ParsesSkillNames()
        {
            // Arrange
            byte[] mhfpac = TestDataFactory.CreateMinimalMhfpac(new[] { "攻撃", "防御", "回避" });
            _fileSystem.AddFile("/test/mhfpac.bin", mhfpac);

            // Act
            var lookup = _service.BuildSkillLookup("/test/mhfpac.bin");

            // Assert
            Assert.Equal(3, lookup.Count);
            Assert.Equal(0, lookup["攻撃"]);
            Assert.Equal(1, lookup["防御"]);
            Assert.Equal(2, lookup["回避"]);
        }

        [Fact]
        public void BuildSkillLookup_HandlesDuplicateNames()
        {
            // Arrange - file with duplicate skill names
            byte[] mhfpac = TestDataFactory.CreateMinimalMhfpac(new[] { "攻撃", "攻撃", "防御" });
            _fileSystem.AddFile("/test/mhfpac.bin", mhfpac);

            // Act
            var lookup = _service.BuildSkillLookup("/test/mhfpac.bin");

            // Assert - first occurrence wins
            Assert.Equal(2, lookup.Count);
            Assert.Equal(0, lookup["攻撃"]); // First one
        }

        #endregion

        #region LoadArmorCsv Tests

        [Fact]
        public void LoadArmorCsv_ParsesEntries()
        {
            // Arrange
            string csv = TestDataFactory.CreateArmorCsv(2);
            _fileSystem.AddFile("/test/Armor.csv", Encoding.GetEncoding("shift-jis").GetBytes(csv));

            // Act
            var entries = _service.LoadArmorCsv("/test/Armor.csv");

            // Assert
            Assert.Equal(10, entries.Count); // 5 classes × 2 entries each

            // Check first entry
            Assert.Equal("頭", entries[0].EquipClass);
            Assert.Equal("TestArmor0", entries[0].Name);
            Assert.True(entries[0].IsMaleEquip);
            Assert.True(entries[0].IsFemaleEquip);
            Assert.Equal(100, entries[0].ZennyCost);
        }

        [Fact]
        public void LoadArmorCsv_ParsesAllArmorClasses()
        {
            // Arrange
            string csv = TestDataFactory.CreateArmorCsv(1);
            _fileSystem.AddFile("/test/Armor.csv", Encoding.GetEncoding("shift-jis").GetBytes(csv));

            // Act
            var entries = _service.LoadArmorCsv("/test/Armor.csv");

            // Assert - should have one entry per class
            Assert.Contains(entries, e => e.EquipClass == "頭");
            Assert.Contains(entries, e => e.EquipClass == "胴");
            Assert.Contains(entries, e => e.EquipClass == "腕");
            Assert.Contains(entries, e => e.EquipClass == "腰");
            Assert.Contains(entries, e => e.EquipClass == "脚");
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullFileSystem_ThrowsArgumentNullException()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
                new DataImportService(null, _logger));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
                new DataImportService(_fileSystem, null));
        }

        [Fact]
        public void DefaultConstructor_CreatesValidInstance()
        {
            var service = new DataImportService();
            Assert.NotNull(service);
        }

        #endregion
    }
}
