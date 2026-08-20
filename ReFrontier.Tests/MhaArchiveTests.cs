using System.Text;

using LibReFrontier;

using ReFrontier.Jpk;
using ReFrontier.Services;
using ReFrontier.Tests.Mocks;

namespace ReFrontier.Tests
{
    /// <summary>
    /// Tests for MHA archives, whose entries are padded to a 512-byte boundary.
    ///
    /// <para>The padded size is stored per entry and is not derivable from the entry size:
    /// most entries pad to the next boundary past their data, but around 4% of the entries
    /// in the game's archives reserve more. Packing therefore carries the recorded value
    /// through rather than recomputing it.</para>
    /// </summary>
    public class MhaArchiveTests
    {
        private readonly InMemoryFileSystem _fileSystem;
        private readonly TestLogger _logger;
        private readonly FileProcessingConfig _config;
        private readonly UnpackingService _unpacking;
        private readonly PackingService _packing;

        public MhaArchiveTests()
        {
            _fileSystem = new InMemoryFileSystem();
            _logger = new TestLogger();
            _config = FileProcessingConfig.Default();
            var codecFactory = new DefaultCodecFactory();
            _unpacking = new UnpackingService(_fileSystem, _logger, codecFactory, _config);
            _packing = new PackingService(_fileSystem, _logger, codecFactory, _config);
        }

        [Fact]
        public void UnpackMha_RecordsThePaddedSizeInTheLog()
        {
            byte[] archive = BuildMha([("a.bin", 100, 512), ("b.bin", 600, 1024)]);
            Unpack(archive);

            string[] log = _fileSystem.ReadAllLines("/test/arc.abn.log");
            // MHA, name, count, unk1, unk2, then one line per entry
            Assert.Equal("a.bin,1,512", log[5]);
            Assert.Equal("b.bin,2,1024", log[6]);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(4)]
        public void MhaRoundTrip_ProducesAByteIdenticalArchive(int entryCount)
        {
            var entries = new (string, int, int)[entryCount];
            for (int i = 0; i < entryCount; i++)
            {
                int size = 300 + i * 517;
                // Alternate minimal padding and a reserved extra block, as the game does.
                int padded = Pad(size) + (i % 2 == 0 ? 0 : 512);
                entries[i] = ($"entry{i}.bin", size, padded);
            }
            byte[] archive = BuildMha(entries);
            Unpack(archive);

            _packing.ProcessPackInput("/test/arc.abn.unpacked");

            Assert.Equal(archive, _fileSystem.ReadAllBytes("output/arc.abn"));
        }

        [Fact]
        public void MhaRoundTrip_EmptyArchive_IsPreserved()
        {
            byte[] archive = BuildMha([]);
            Unpack(archive);

            _packing.ProcessPackInput("/test/arc.abn.unpacked");

            Assert.Equal(archive, _fileSystem.ReadAllBytes("output/arc.abn"));
        }

        [Fact]
        public void PackMha_PadsEveryEntryWithZeros()
        {
            byte[] archive = BuildMha([("a.bin", 100, 512), ("b.bin", 600, 1536)]);
            Unpack(archive);

            _packing.ProcessPackInput("/test/arc.abn.unpacked");

            byte[] packed = _fileSystem.ReadAllBytes("output/arc.abn");
            int pMeta = BitConverter.ToInt32(packed, 4);
            int count = BitConverter.ToInt32(packed, 8);
            for (int i = 0; i < count; i++)
            {
                int meta = pMeta + i * FileFormatConstants.MhaEntryMetadataSize;
                int offset = BitConverter.ToInt32(packed, meta + 4);
                int size = BitConverter.ToInt32(packed, meta + 8);
                int padded = BitConverter.ToInt32(packed, meta + 12);
                Assert.True(padded > size, "every entry carries at least one padding byte");
                Assert.Equal(0, padded % FileFormatConstants.MhaEntryAlignment);
                for (int j = offset + size; j < offset + padded; j++)
                    Assert.Equal(0, packed[j]);
            }
        }

        [Fact]
        public void PackMha_EntryGrewBeyondItsPadding_RecomputesIt()
        {
            byte[] archive = BuildMha([("a.bin", 100, 512)]);
            Unpack(archive);

            // The user edited the entry to be larger than the space reserved for it.
            _fileSystem.WriteAllBytes("/test/arc.abn.unpacked/a.bin", new byte[900]);
            _packing.ProcessPackInput("/test/arc.abn.unpacked");

            byte[] packed = _fileSystem.ReadAllBytes("output/arc.abn");
            int pMeta = BitConverter.ToInt32(packed, 4);
            Assert.Equal(900, BitConverter.ToInt32(packed, pMeta + 8));
            Assert.Equal(1024, BitConverter.ToInt32(packed, pMeta + 12));
        }

        [Fact]
        public void PackMha_LogWithoutPaddedSize_StillPacks()
        {
            // Logs written before the padded size was recorded have two columns.
            _fileSystem.AddFile("/test/old.abn.unpacked/a.bin", new byte[100]);
            _fileSystem.AddFile(
                "/test/old.abn.log",
                string.Join("\n", ["MHA", "old.abn", "1", "0", "1000", "a.bin,1"])
            );

            _packing.ProcessPackInput("/test/old.abn.unpacked");

            byte[] packed = _fileSystem.ReadAllBytes("output/old.abn");
            int pMeta = BitConverter.ToInt32(packed, 4);
            Assert.Equal(100, BitConverter.ToInt32(packed, pMeta + 8));
            Assert.Equal(512, BitConverter.ToInt32(packed, pMeta + 12));
        }

        [Fact]
        public void PackMha_EntryExactlyOnTheBoundary_StillGainsAFullBlock()
        {
            _fileSystem.AddFile("/test/old.abn.unpacked/a.bin", new byte[512]);
            _fileSystem.AddFile(
                "/test/old.abn.log",
                string.Join("\n", ["MHA", "old.abn", "1", "0", "1000", "a.bin,1"])
            );

            _packing.ProcessPackInput("/test/old.abn.unpacked");

            byte[] packed = _fileSystem.ReadAllBytes("output/old.abn");
            int pMeta = BitConverter.ToInt32(packed, 4);
            Assert.Equal(1024, BitConverter.ToInt32(packed, pMeta + 12));
        }

        #region Helpers

        private static int Pad(int size) =>
            size - (size % FileFormatConstants.MhaEntryAlignment) + FileFormatConstants.MhaEntryAlignment;

        private string Unpack(byte[] archive)
        {
            _fileSystem.AddFile("/test/arc.abn", archive);
            using var reader = new BinaryReader(new MemoryStream(archive));
            return _unpacking.UnpackMHA("/test/arc.abn", reader, createLog: true);
        }

        /// <summary>
        /// Build an MHA archive laid out the way the game's own archives are: entry data
        /// from the header's end, each padded, then the names block and the metadata block.
        /// </summary>
        private static byte[] BuildMha((string Name, int Size, int PaddedSize)[] entries)
        {
            int dataEnd = FileFormatConstants.MhaHeaderSize;
            foreach (var e in entries)
                dataEnd += e.PaddedSize;

            var names = new MemoryStream();
            var stringOffsets = new int[entries.Length];
            for (int i = 0; i < entries.Length; i++)
            {
                stringOffsets[i] = (int)names.Length;
                byte[] raw = Encoding.UTF8.GetBytes(entries[i].Name);
                names.Write(raw, 0, raw.Length);
                names.WriteByte(0);
            }

            int namesLength = (int)names.Length;
            int metaStart = dataEnd + namesLength;
            byte[] buffer = new byte[metaStart + entries.Length * FileFormatConstants.MhaEntryMetadataSize];

            BitConverter.GetBytes(FileMagic.MHA).CopyTo(buffer, 0);
            BitConverter.GetBytes(metaStart).CopyTo(buffer, 4);
            BitConverter.GetBytes(entries.Length).CopyTo(buffer, 8);
            BitConverter.GetBytes(dataEnd).CopyTo(buffer, 12);
            BitConverter.GetBytes(namesLength).CopyTo(buffer, 16);
            BitConverter.GetBytes((short)0).CopyTo(buffer, 20);      // unk1
            BitConverter.GetBytes((short)1000).CopyTo(buffer, 22);   // unk2

            names.ToArray().CopyTo(buffer, dataEnd);

            int offset = FileFormatConstants.MhaHeaderSize;
            for (int i = 0; i < entries.Length; i++)
            {
                for (int j = 0; j < entries[i].Size; j++)
                    buffer[offset + j] = (byte)((i + j) % 251);

                int meta = metaStart + i * FileFormatConstants.MhaEntryMetadataSize;
                BitConverter.GetBytes(stringOffsets[i]).CopyTo(buffer, meta);
                BitConverter.GetBytes(offset).CopyTo(buffer, meta + 4);
                BitConverter.GetBytes(entries[i].Size).CopyTo(buffer, meta + 8);
                BitConverter.GetBytes(entries[i].PaddedSize).CopyTo(buffer, meta + 12);
                BitConverter.GetBytes(i + 1).CopyTo(buffer, meta + 16);   // file id
                offset += entries[i].PaddedSize;
            }

            return buffer;
        }

        #endregion
    }
}
