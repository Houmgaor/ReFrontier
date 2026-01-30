using System.Text;

using FrontierDataTool.Services;

using ReFrontier.Tests.Mocks;

namespace ReFrontier.Tests.DataToolTests
{
    /// <summary>
    /// Tests for DataExtractionService.
    /// </summary>
    public class DataExtractionServiceTests
    {
        private readonly InMemoryFileSystem _fileSystem;
        private readonly TestLogger _logger;
        private readonly DataExtractionService _service;

        static DataExtractionServiceTests()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public DataExtractionServiceTests()
        {
            _fileSystem = new InMemoryFileSystem();
            _logger = new TestLogger();
            _service = new DataExtractionService(_fileSystem, _logger);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullFileSystem_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new DataExtractionService(null!, _logger));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new DataExtractionService(_fileSystem, null!));
        }

        [Fact]
        public void DefaultConstructor_CreatesValidInstance()
        {
            var service = new DataExtractionService();
            Assert.NotNull(service);
        }

        #endregion

        #region DumpSkillSystem Tests

        [Fact]
        public void DumpSkillSystem_ExtractsSkillNames()
        {
            // Arrange
            byte[] mhfpac = CreateMhfpacWithSkills("攻撃", "防御", "回避");
            _fileSystem.AddFile("/test/mhfpac.bin", mhfpac);

            // Act
            var skillId = _service.DumpSkillSystem("/test/mhfpac.bin", "test");

            // Assert
            Assert.Equal(3, skillId.Count);
            Assert.True(_fileSystem.FileExists("mhsx_SkillSys_test.txt"));
        }

        [Fact]
        public void DumpSkillSystem_LogsProgress()
        {
            // Arrange
            byte[] mhfpac = CreateMhfpacWithSkills("攻撃");
            _fileSystem.AddFile("/test/mhfpac.bin", mhfpac);

            // Act
            _service.DumpSkillSystem("/test/mhfpac.bin", "test");

            // Assert
            Assert.True(_logger.ContainsMessage("Dumping skill tree names"));
        }

        #endregion

        #region DumpSkillData Tests

        [Fact]
        public void DumpSkillData_CreatesMultipleOutputFiles()
        {
            // Arrange
            byte[] mhfpac = CreateMhfpacWithFullSkillData();
            _fileSystem.AddFile("/test/mhfpac.bin", mhfpac);

            // Act
            _service.DumpSkillData("/test/mhfpac.bin", "test");

            // Assert
            Assert.True(_fileSystem.FileExists("mhsx_SkillActivate_test.txt"));
            Assert.True(_fileSystem.FileExists("mhsx_SkillDesc_test.txt"));
            Assert.True(_fileSystem.FileExists("mhsx_SkillZ_test.txt"));
        }

        [Fact]
        public void DumpSkillData_LogsAllSections()
        {
            // Arrange
            byte[] mhfpac = CreateMhfpacWithFullSkillData();
            _fileSystem.AddFile("/test/mhfpac.bin", mhfpac);

            // Act
            _service.DumpSkillData("/test/mhfpac.bin", "test");

            // Assert
            Assert.True(_logger.ContainsMessage("active skill names"));
            Assert.True(_logger.ContainsMessage("active skill descriptions"));
            Assert.True(_logger.ContainsMessage("Z skill names"));
        }

        #endregion

        #region DumpItemData Tests

        [Fact]
        public void DumpItemData_CreatesOutputFiles()
        {
            // Arrange
            byte[] mhfdat = CreateMhfdatWithItems("アイテム1", "アイテム2");
            _fileSystem.AddFile("/test/mhfdat.bin", mhfdat);

            // Act
            _service.DumpItemData("/test/mhfdat.bin", "test");

            // Assert
            Assert.True(_fileSystem.FileExists("mhsx_Items_test.txt"));
            Assert.True(_fileSystem.FileExists("Items_Desc_test.txt"));
        }

        [Fact]
        public void DumpItemData_LogsProgress()
        {
            // Arrange
            byte[] mhfdat = CreateMhfdatWithItems("アイテム1");
            _fileSystem.AddFile("/test/mhfdat.bin", mhfdat);

            // Act
            _service.DumpItemData("/test/mhfdat.bin", "test");

            // Assert
            Assert.True(_logger.ContainsMessage("item names"));
            Assert.True(_logger.ContainsMessage("item descriptions"));
        }

        #endregion

        #region DataPointersArmor Tests

        [Fact]
        public void DataPointersArmor_HasFiveEntries()
        {
            // Assert
            Assert.Equal(5, DataExtractionService.DataPointersArmor.Count);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Create a minimal mhfpac.bin with skill tree data.
        /// </summary>
        private static byte[] CreateMhfpacWithSkills(params string[] skillNames)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            // Write header area (need to reach 0xA24 for pointers)
            bw.Write(new byte[0xA24]);

            // Calculate offsets
            int pointerTableStart = 0xA24;
            int stringDataStart = pointerTableStart + (skillNames.Length * 4);

            // Write start pointer at 0xA20
            ms.Seek(0xA20, SeekOrigin.Begin);
            bw.Write(pointerTableStart);

            // Write end pointer at 0xA1C (end of pointer table)
            ms.Seek(0xA1C, SeekOrigin.Begin);
            bw.Write(stringDataStart);

            // Write pointer table
            ms.Seek(pointerTableStart, SeekOrigin.Begin);
            int currentOffset = stringDataStart;
            foreach (var name in skillNames)
            {
                bw.Write(currentOffset);
                currentOffset += Encoding.GetEncoding("shift-jis").GetBytes(name).Length + 1;
            }

            // Write string data
            foreach (var name in skillNames)
            {
                byte[] nameBytes = Encoding.GetEncoding("shift-jis").GetBytes(name);
                bw.Write(nameBytes);
                bw.Write((byte)0);
            }

            return ms.ToArray();
        }

        /// <summary>
        /// Create mhfpac.bin with full skill data (active skills, descriptions, Z skills).
        /// </summary>
        private static byte[] CreateMhfpacWithFullSkillData()
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            // Larger header area to accommodate all offsets
            bw.Write(new byte[0x1000]);

            // Skill activate data section
            int activatePointerStart = 0x1000;
            int activateStringStart = activatePointerStart + 8; // 2 pointers
            int activateStringEnd = activateStringStart + 20;

            // Write active skill offsets
            ms.Seek(0xA1C, SeekOrigin.Begin); // _soStringSkillActivate
            bw.Write(activatePointerStart);
            ms.Seek(0xBC0, SeekOrigin.Begin); // _eoStringSkillActivate
            bw.Write(activateStringStart);

            // Write active skill pointers and strings
            ms.Seek(activatePointerStart, SeekOrigin.Begin);
            bw.Write(activateStringStart); // Pointer 1
            bw.Write(activateStringStart + 10); // Pointer 2

            byte[] activeSkill1 = Encoding.GetEncoding("shift-jis").GetBytes("活性スキル");
            ms.Seek(activateStringStart, SeekOrigin.Begin);
            bw.Write(activeSkill1);
            bw.Write((byte)0);

            // Skill description section
            int descPointerStart = 0x2000;
            int descStringStart = descPointerStart + 4;

            ms.Seek(0xB8, SeekOrigin.Begin); // _soStringSkillDesc
            bw.Write(descPointerStart);
            ms.Seek(0xC0, SeekOrigin.Begin); // _eoStringSkillDesc
            bw.Write(descStringStart);

            ms.Seek(descPointerStart, SeekOrigin.Begin);
            bw.Write(descStringStart);

            ms.Seek(descStringStart, SeekOrigin.Begin);
            byte[] descBytes = Encoding.GetEncoding("shift-jis").GetBytes("説明");
            bw.Write(descBytes);
            bw.Write((byte)0);

            // Z skill section
            int zPointerStart = 0x3000;
            int zStringStart = zPointerStart + 4;

            ms.Seek(0xFBC, SeekOrigin.Begin); // _soStringZSkill
            bw.Write(zPointerStart);
            ms.Seek(0xFB0, SeekOrigin.Begin); // _eoStringZSkill
            bw.Write(zStringStart);

            ms.Seek(zPointerStart, SeekOrigin.Begin);
            bw.Write(zStringStart);

            ms.Seek(zStringStart, SeekOrigin.Begin);
            byte[] zBytes = Encoding.GetEncoding("shift-jis").GetBytes("Z技");
            bw.Write(zBytes);
            bw.Write((byte)0);

            return ms.ToArray();
        }

        /// <summary>
        /// Create mhfdat.bin with item data.
        /// </summary>
        private static byte[] CreateMhfdatWithItems(params string[] itemNames)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            // Header area
            bw.Write(new byte[0x200]);

            // Item name section
            int itemPointerStart = 0x200;
            int itemStringStart = itemPointerStart + (itemNames.Length * 4);

            // Write item name offsets
            ms.Seek(0x100, SeekOrigin.Begin); // _soStringItem
            bw.Write(itemPointerStart);
            ms.Seek(0xFC, SeekOrigin.Begin); // _eoStringItem
            bw.Write(itemStringStart);

            // Write item pointers and strings
            ms.Seek(itemPointerStart, SeekOrigin.Begin);
            int currentOffset = itemStringStart;
            foreach (var name in itemNames)
            {
                bw.Write(currentOffset);
                currentOffset += Encoding.GetEncoding("shift-jis").GetBytes(name).Length + 1;
            }

            foreach (var name in itemNames)
            {
                byte[] nameBytes = Encoding.GetEncoding("shift-jis").GetBytes(name);
                bw.Write(nameBytes);
                bw.Write((byte)0);
            }

            // Item description section
            int descPointerStart = currentOffset;
            int descStringStart = descPointerStart + (itemNames.Length * 4);

            ms.Seek(0x12C, SeekOrigin.Begin); // _soStringItemDesc
            bw.Write(descPointerStart);
            ms.Seek(0x100, SeekOrigin.Begin); // _eoStringItemDesc
            bw.Write(descStringStart);

            ms.Seek(descPointerStart, SeekOrigin.Begin);
            currentOffset = descStringStart;
            foreach (var name in itemNames)
            {
                bw.Write(currentOffset);
                currentOffset += Encoding.GetEncoding("shift-jis").GetBytes(name + "説明").Length + 1;
            }

            foreach (var name in itemNames)
            {
                byte[] descBytes = Encoding.GetEncoding("shift-jis").GetBytes(name + "説明");
                bw.Write(descBytes);
                bw.Write((byte)0);
            }

            return ms.ToArray();
        }

        #endregion
    }
}
