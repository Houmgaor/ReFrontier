using System;
using System.IO;

using LibReFrontier;

using ReFrontier.Jpk;
using ReFrontier.Services;
using ReFrontier.Tests.Mocks;

namespace ReFrontier.Tests
{
    /// <summary>
    /// Tests for MOMO archives, whose header is a 4-byte magic followed by the entry
    /// count, then a table of offset and size pairs.
    ///
    /// <para>Reading the count at the stream's position instead of after the magic took
    /// the magic itself as the entry count, so every MOMO archive in the game failed to
    /// unpack. The layout here, including the 64-byte alignment, is the one all 615 MOMO
    /// archives in the PC client's dat/sound directory use.</para>
    /// </summary>
    public class MomoArchiveTests
    {
        private readonly InMemoryFileSystem _fileSystem;
        private readonly TestLogger _logger;
        private readonly FileProcessingConfig _config;
        private readonly ICodecFactory _codecFactory;
        private readonly UnpackingService _unpacking;
        private readonly PackingService _packing;

        public MomoArchiveTests()
        {
            _fileSystem = new InMemoryFileSystem();
            _logger = new TestLogger();
            _config = FileProcessingConfig.Default();
            _codecFactory = new DefaultCodecFactory();
            _unpacking = new UnpackingService(_fileSystem, _logger, _codecFactory, _config);
            _packing = new PackingService(_fileSystem, _logger, _codecFactory, _config);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(8)]
        public void UnpackSimpleArchive_Momo_ReadsCountAfterTheMagic(int entryCount)
        {
            byte[] archive = BuildMomo(entryCount, out _);
            string dir = Unpack(archive);

            string[] entries = _fileSystem.GetFiles(dir, "*", SearchOption.TopDirectoryOnly);
            Assert.Equal(entryCount, entries.Length);
        }

        [Fact]
        public void UnpackSimpleArchive_Momo_ExtractsEntryContents()
        {
            byte[] archive = BuildMomo(3, out byte[][] payloads);
            string dir = Unpack(archive);

            string[] entries = _fileSystem.GetFiles(dir, "*", SearchOption.TopDirectoryOnly);
            Array.Sort(entries, StringComparer.Ordinal);
            for (int i = 0; i < payloads.Length; i++)
                Assert.Equal(payloads[i], _fileSystem.ReadAllBytes(entries[i]));
        }

        [Fact]
        public void UnpackSimpleArchive_Momo_RecordsMomoInTheLog()
        {
            byte[] archive = BuildMomo(2, out _);
            Unpack(archive);

            // Packing reads this to decide whether to write the MOMO header back.
            string[] log = _fileSystem.ReadAllLines("/test/arc.snd.log");
            Assert.Equal("MOMO", log[0]);
        }

        [Fact]
        public void UnpackSimpleArchive_HeaderlessArchive_StillReadsCountAtOffsetZero()
        {
            // The same rule covers both shapes: the count sits just before the entry table.
            byte[] payload = CreateTestData(32);
            using var ms = new MemoryStream();
            using (var bw = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                bw.Write(1);            // count at offset 0
                bw.Write(12);           // entry offset
                bw.Write(payload.Length);
                bw.Write(payload);
            }
            byte[] archive = ms.ToArray();
            _fileSystem.AddFile("/test/plain.bin", archive);

            using var reader = new BinaryReader(new MemoryStream(archive));
            string dir = _unpacking.UnpackSimpleArchive(
                "/test/plain.bin", reader, 4, createLog: true, cleanUp: false, autoStage: false
            );

            string[] entries = _fileSystem.GetFiles(dir, "*", SearchOption.TopDirectoryOnly);
            Assert.Single(entries);
            Assert.Equal(payload, _fileSystem.ReadAllBytes(entries[0]));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(8)]
        public void MomoRoundTrip_ProducesAByteIdenticalArchive(int entryCount)
        {
            byte[] archive = BuildMomo(entryCount, out _);
            Unpack(archive);

            _packing.ProcessPackInput("/test/arc.snd.unpacked");

            Assert.Equal(archive, _fileSystem.ReadAllBytes("output/arc.snd"));
        }

        [Fact]
        public void PackMomo_WritesMagicAndCountInTheHeader()
        {
            byte[] archive = BuildMomo(3, out _);
            Unpack(archive);

            _packing.ProcessPackInput("/test/arc.snd.unpacked");

            byte[] packed = _fileSystem.ReadAllBytes("output/arc.snd");
            Assert.Equal(FileMagic.MOMO, BitConverter.ToUInt32(packed, 0));
            Assert.Equal(3, BitConverter.ToInt32(packed, 4));
        }

        [Fact]
        public void PackMomo_AlignsEveryEntryAndPadsTheFile()
        {
            byte[] archive = BuildMomo(4, out _);
            Unpack(archive);

            _packing.ProcessPackInput("/test/arc.snd.unpacked");

            byte[] packed = _fileSystem.ReadAllBytes("output/arc.snd");
            int count = BitConverter.ToInt32(packed, 4);
            for (int i = 0; i < count; i++)
            {
                int offset = BitConverter.ToInt32(
                    packed, FileFormatConstants.MomoHeaderSize + i * FileFormatConstants.SimpleArchiveEntrySize
                );
                Assert.Equal(0, offset % FileFormatConstants.MomoEntryAlignment);
            }
            Assert.Equal(0, packed.Length % FileFormatConstants.MomoEntryAlignment);
        }

        [Fact]
        public void UnpackFacade_Momo_RecordsMomoInTheLog()
        {
            // The Unpack facade is a separate public surface from UnpackingService, and
            // packing decides how to write the header from what the log says.
            byte[] archive = BuildMomo(2, out _);
            _fileSystem.AddFile("/test/arc.snd", archive);
            var facade = new Unpack(_fileSystem, _logger, _codecFactory, _config);

            using var reader = new BinaryReader(new MemoryStream(archive));
            facade.UnpackSimpleArchive(
                "/test/arc.snd", reader, FileFormatConstants.MomoHeaderSize,
                createLog: true, cleanUp: false, autoStage: false, containerType: "MOMO"
            );

            Assert.Equal("MOMO", _fileSystem.ReadAllLines("/test/arc.snd.log")[0]);
        }

        [Fact]
        public void UnpackFacade_Momo_RoundTripsThroughTheFacade()
        {
            byte[] archive = BuildMomo(3, out _);
            _fileSystem.AddFile("/test/arc.snd", archive);
            var facade = new Unpack(_fileSystem, _logger, _codecFactory, _config);

            using (var reader = new BinaryReader(new MemoryStream(archive)))
            {
                facade.UnpackSimpleArchive(
                    "/test/arc.snd", reader, FileFormatConstants.MomoHeaderSize,
                    createLog: true, cleanUp: false, autoStage: false, containerType: "MOMO"
                );
            }

            _packing.ProcessPackInput("/test/arc.snd.unpacked");

            Assert.Equal(archive, _fileSystem.ReadAllBytes("output/arc.snd"));
        }

        [Fact]
        public void UnpackFacade_WithoutContainerType_StaysHeaderless()
        {
            // Existing callers that omit the argument keep the previous behaviour.
            byte[] payload = CreateTestData(32);
            using var ms = new MemoryStream();
            using (var bw = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                bw.Write(1);
                bw.Write(12);
                bw.Write(payload.Length);
                bw.Write(payload);
            }
            byte[] archive = ms.ToArray();
            _fileSystem.AddFile("/test/plain.bin", archive);
            var facade = new Unpack(_fileSystem, _logger, _codecFactory, _config);

            using var reader = new BinaryReader(new MemoryStream(archive));
            facade.UnpackSimpleArchive("/test/plain.bin", reader, 4, createLog: true, cleanUp: false, autoStage: false);

            Assert.Equal("SimpleArchive", _fileSystem.ReadAllLines("/test/plain.bin.log")[0]);
        }

        #region Helpers

        private string Unpack(byte[] archive)
        {
            _fileSystem.AddFile("/test/arc.snd", archive);
            using var reader = new BinaryReader(new MemoryStream(archive));
            return _unpacking.UnpackSimpleArchive(
                "/test/arc.snd", reader, FileFormatConstants.MomoHeaderSize,
                createLog: true, cleanUp: false, autoStage: false, verbose: false, containerType: "MOMO"
            );
        }

        /// <summary>
        /// Build a MOMO archive laid out the way the game's own archives are.
        /// </summary>
        private static byte[] BuildMomo(int entryCount, out byte[][] payloads)
        {
            static int AlignUp(int value) =>
                (value + FileFormatConstants.MomoEntryAlignment - 1)
                & ~(FileFormatConstants.MomoEntryAlignment - 1);

            payloads = new byte[entryCount][];
            var offsets = new int[entryCount];
            int position = AlignUp(
                FileFormatConstants.MomoHeaderSize + entryCount * FileFormatConstants.SimpleArchiveEntrySize
            );
            for (int i = 0; i < entryCount; i++)
            {
                // Varying sizes so the alignment padding is not uniform.
                payloads[i] = CreateTestData(48 + i * 37);
                offsets[i] = position;
                position = AlignUp(position + payloads[i].Length);
            }

            byte[] buffer = new byte[position];
            BitConverter.GetBytes(FileMagic.MOMO).CopyTo(buffer, 0);
            BitConverter.GetBytes(entryCount).CopyTo(buffer, 4);
            for (int i = 0; i < entryCount; i++)
            {
                int entryBase = FileFormatConstants.MomoHeaderSize
                    + i * FileFormatConstants.SimpleArchiveEntrySize;
                BitConverter.GetBytes(offsets[i]).CopyTo(buffer, entryBase);
                BitConverter.GetBytes(payloads[i].Length).CopyTo(buffer, entryBase + 4);
                payloads[i].CopyTo(buffer, offsets[i]);
            }
            return buffer;
        }

        private static byte[] CreateTestData(int size)
        {
            byte[] data = new byte[size];
            for (int i = 0; i < size; i++)
                data[i] = (byte)(i % 251);
            return data;
        }

        #endregion
    }
}
