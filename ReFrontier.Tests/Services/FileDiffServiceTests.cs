using LibReFrontier;

using ReFrontier.Jpk;
using ReFrontier.Services;
using ReFrontier.Tests.Mocks;

namespace ReFrontier.Tests.Services
{
    /// <summary>
    /// Tests for FileDiffService.
    /// </summary>
    public class FileDiffServiceTests
    {
        private readonly TestLogger _logger = new();
        private readonly FileDiffService _service;

        public FileDiffServiceTests()
        {
            _service = new FileDiffService(_logger, new DefaultCodecFactory());
        }

        #region Helper Methods

        private static byte[] BuildEcdBuffer(byte[] plaintext, int keyIndex = 4)
        {
            return Crypto.EncodeEcd(plaintext, keyIndex);
        }

        private static byte[] BuildExfBuffer(byte[] plaintext)
        {
            byte[] meta = new byte[16];
            meta[0] = 0x65; meta[1] = 0x78; meta[2] = 0x66; meta[3] = 0x1A;
            meta[4] = 0x00; meta[5] = 0x00;
            meta[12] = 0x42; meta[13] = 0x00; meta[14] = 0x00; meta[15] = 0x00;
            return Crypto.EncodeExf(plaintext, meta);
        }

        private static byte[] BuildJpkRwBuffer(byte[] plaintext)
        {
            var encoder = new JPKEncodeRW();
            using var compressedStream = new System.IO.MemoryStream();
            encoder.ProcessOnEncode(plaintext, compressedStream, 16);
            byte[] compressed = compressedStream.ToArray();

            using var ms = new System.IO.MemoryStream();
            using var bw = new System.IO.BinaryWriter(ms);
            bw.Write((uint)FileMagic.JKR);
            bw.Write((ushort)0);
            bw.Write((ushort)0); // CompressionType = RW (0)
            bw.Write((int)0x10);
            bw.Write(plaintext.Length);
            bw.Write(compressed);
            return ms.ToArray();
        }

        private static byte[] BuildSimpleArchive(int entryCount, byte dataSeed = 0)
        {
            int magicSize = 4;
            int entryTableSize = entryCount * 8;
            int dataPerEntry = 16;
            int dataStart = magicSize + 4 + entryTableSize;

            using var ms = new System.IO.MemoryStream();
            using var bw = new System.IO.BinaryWriter(ms);

            bw.Write((uint)0x00000001);
            bw.Write(entryCount);

            for (int i = 0; i < entryCount; i++)
            {
                bw.Write(dataStart + i * dataPerEntry);
                bw.Write(dataPerEntry);
            }

            for (int i = 0; i < entryCount * dataPerEntry; i++)
            {
                bw.Write((byte)((i + dataSeed) & 0xFF));
            }

            return ms.ToArray();
        }

        private static byte[] BuildMomoArchive(int entryCount)
        {
            int momoHeaderSize = 8;
            int entryTableSize = entryCount * 8;
            int dataPerEntry = 16;
            int dataStart = momoHeaderSize + 4 + entryTableSize;

            using var ms = new System.IO.MemoryStream();
            using var bw = new System.IO.BinaryWriter(ms);

            bw.Write(FileMagic.MOMO);
            bw.Write((uint)0);
            bw.Write(entryCount);

            for (int i = 0; i < entryCount; i++)
            {
                bw.Write(dataStart + i * dataPerEntry);
                bw.Write(dataPerEntry);
            }

            for (int i = 0; i < entryCount * dataPerEntry; i++)
            {
                bw.Write((byte)(i & 0xFF));
            }

            return ms.ToArray();
        }

        private static byte[] BuildMhaArchive(int entryCount, string namePrefix = "entry")
        {
            int headerSize = 24;
            int metaBlockSize = entryCount * 0x14;
            int metaStart = headerSize;
            int namesStart = metaStart + metaBlockSize;

            var names = new string[entryCount];
            var nameBytes = new byte[entryCount][];
            int namesSize = 0;
            for (int i = 0; i < entryCount; i++)
            {
                names[i] = $"{namePrefix}_{i:D4}.bin";
                nameBytes[i] = System.Text.Encoding.UTF8.GetBytes(names[i]);
                namesSize += nameBytes[i].Length + 1;
            }

            int dataStart = namesStart + namesSize;
            int dataPerEntry = 32;

            using var ms = new System.IO.MemoryStream();
            using var bw = new System.IO.BinaryWriter(ms);

            bw.Write(FileMagic.MHA);
            bw.Write(metaStart);
            bw.Write(entryCount);
            bw.Write(namesStart);
            bw.Write(namesSize);
            bw.Write((short)0);
            bw.Write((short)0);

            int currentNameOffset = 0;
            for (int i = 0; i < entryCount; i++)
            {
                bw.Write(currentNameOffset);
                bw.Write(dataStart + i * dataPerEntry);
                bw.Write(dataPerEntry);
                bw.Write(dataPerEntry);
                bw.Write(i);
                currentNameOffset += nameBytes[i].Length + 1;
            }

            for (int i = 0; i < entryCount; i++)
            {
                bw.Write(nameBytes[i]);
                bw.Write((byte)0);
            }

            for (int i = 0; i < entryCount * dataPerEntry; i++)
            {
                bw.Write((byte)(i & 0xFF));
            }

            return ms.ToArray();
        }

        private static byte[] BuildFtxtBuffer(params string[] strings)
        {
            using var ms = new System.IO.MemoryStream();
            using var bw = new System.IO.BinaryWriter(ms);

            bw.Write((uint)FileMagic.FTXT);
            bw.Write(new byte[6]); // padding to offset 10
            bw.Write((short)strings.Length);
            bw.Write((int)0); // text block size placeholder

            foreach (var s in strings)
            {
                bw.Write(System.Text.Encoding.ASCII.GetBytes(s));
                bw.Write((byte)0);
            }

            return ms.ToArray();
        }

        #endregion

        #region ECD Tests

        [Fact]
        public void Compare_IdenticalEcdFiles_NoDifferences()
        {
            byte[] plaintext = TestHelpers.RandomData(256, seed: 42);
            byte[] ecd1 = BuildEcdBuffer(plaintext);
            byte[] ecd2 = BuildEcdBuffer(plaintext);

            var result = _service.CompareBuffers(ecd1, ecd2, "file1.bin", "file2.bin");

            Assert.True(result.AreIdentical);
        }

        [Fact]
        public void Compare_DifferentEcdPayload_ReportsCrc32Diff()
        {
            byte[] plaintext1 = TestHelpers.RandomData(256, seed: 42);
            byte[] plaintext2 = TestHelpers.RandomData(256, seed: 99);
            byte[] ecd1 = BuildEcdBuffer(plaintext1);
            byte[] ecd2 = BuildEcdBuffer(plaintext2);

            var result = _service.CompareBuffers(ecd1, ecd2, "file1.bin", "file2.bin");

            Assert.False(result.AreIdentical);
            Assert.Contains(result.Differences, d => d.Layer == "ECD" && d.Property == "CRC32");
        }

        [Fact]
        public void Compare_DifferentEcdKeyIndex_ReportsKeyDiff()
        {
            byte[] plaintext = TestHelpers.RandomData(256, seed: 42);
            byte[] ecd1 = BuildEcdBuffer(plaintext, keyIndex: 0);
            byte[] ecd2 = BuildEcdBuffer(plaintext, keyIndex: 3);

            var result = _service.CompareBuffers(ecd1, ecd2, "file1.bin", "file2.bin");

            Assert.False(result.AreIdentical);
            Assert.Contains(result.Differences, d => d.Layer == "ECD" && d.Property == "KeyIndex");
        }

        #endregion

        #region Format Mismatch Tests

        [Fact]
        public void Compare_EcdVsJpk_ReportsFormatMismatch()
        {
            byte[] plaintext = TestHelpers.RandomData(128, seed: 42);
            byte[] ecd = BuildEcdBuffer(plaintext);
            byte[] jpk = BuildJpkRwBuffer(plaintext);

            var result = _service.CompareBuffers(ecd, jpk, "file1.bin", "file2.bin");

            Assert.False(result.AreIdentical);
            Assert.Contains(result.Differences, d => d.Property.Contains("Layer"));
        }

        #endregion

        #region JPK Tests

        [Fact]
        public void Compare_IdenticalJpkFiles_NoDifferences()
        {
            byte[] plaintext = TestHelpers.RandomData(128, seed: 10);
            byte[] jpk1 = BuildJpkRwBuffer(plaintext);
            byte[] jpk2 = BuildJpkRwBuffer(plaintext);

            var result = _service.CompareBuffers(jpk1, jpk2, "file1.bin", "file2.bin");

            Assert.True(result.AreIdentical);
        }

        [Fact]
        public void Compare_DifferentJpkCompressionType_ReportsDiff()
        {
            byte[] plaintext = TestHelpers.RandomData(128, seed: 10);
            byte[] jpk1 = BuildJpkRwBuffer(plaintext);

            // Build a JPK with different compression type (HFI=4) by modifying the header
            byte[] jpk2 = BuildJpkRwBuffer(plaintext);
            // Change compression type from RW (0) to HFI (4) at offset 6
            byte[] typeBytes = BitConverter.GetBytes((ushort)4);
            Array.Copy(typeBytes, 0, jpk2, 6, 2);

            var result = _service.CompareBuffers(jpk1, jpk2, "file1.bin", "file2.bin");

            Assert.False(result.AreIdentical);
            Assert.Contains(result.Differences, d => d.Layer == "JPK" && d.Property == "CompressionType");
        }

        #endregion

        #region SimpleArchive Tests

        [Fact]
        public void Compare_SimpleArchive_DifferentEntryCount_ReportsDiff()
        {
            byte[] arch1 = BuildSimpleArchive(3);
            byte[] arch2 = BuildSimpleArchive(5);

            var result = _service.CompareBuffers(arch1, arch2, "file1.bin", "file2.bin");

            Assert.False(result.AreIdentical);
            Assert.Contains(result.Differences, d => d.Layer == "SimpleArchive" && d.Property == "EntryCount");
        }

        [Fact]
        public void Compare_SimpleArchive_DifferentEntryContent_ReportsCrc32Diff()
        {
            byte[] arch1 = BuildSimpleArchive(3, dataSeed: 0);
            byte[] arch2 = BuildSimpleArchive(3, dataSeed: 128);

            var result = _service.CompareBuffers(arch1, arch2, "file1.bin", "file2.bin");

            Assert.False(result.AreIdentical);
            Assert.Contains(result.Differences, d => d.Layer == "SimpleArchive" && d.Property.Contains("CRC32"));
        }

        #endregion

        #region MHA Tests

        [Fact]
        public void Compare_MhaArchive_DifferentEntryNames_ReportsDiff()
        {
            byte[] mha1 = BuildMhaArchive(3, namePrefix: "alpha");
            byte[] mha2 = BuildMhaArchive(3, namePrefix: "beta");

            var result = _service.CompareBuffers(mha1, mha2, "file1.bin", "file2.bin");

            Assert.False(result.AreIdentical);
            // Names differ so entries from file1 show as only-in-file1 and file2 entries as only-in-file2
            Assert.Contains(result.Differences, d => d.Layer == "MHA" && d.Value2 == null);
            Assert.Contains(result.Differences, d => d.Layer == "MHA" && d.Value1 == null);
        }

        #endregion

        #region FTXT Tests

        [Fact]
        public void Compare_Ftxt_DifferentStrings_ReportsDiff()
        {
            byte[] ftxt1 = BuildFtxtBuffer("hello", "world");
            byte[] ftxt2 = BuildFtxtBuffer("hello", "earth");

            var result = _service.CompareBuffers(ftxt1, ftxt2, "file1.bin", "file2.bin");

            Assert.False(result.AreIdentical);
            Assert.Contains(result.Differences, d => d.Layer == "FTXT" && d.Property == "String[1]");
        }

        #endregion

        #region Nested Layer Tests

        [Fact]
        public void Compare_NestedEcdJpk_IdenticalInnerPayload_NoDifferences()
        {
            byte[] inner = TestHelpers.RandomData(64, seed: 99);
            byte[] jpk1 = BuildJpkRwBuffer(inner);
            byte[] jpk2 = BuildJpkRwBuffer(inner);
            byte[] ecd1 = BuildEcdBuffer(jpk1);
            byte[] ecd2 = BuildEcdBuffer(jpk2);

            var result = _service.CompareBuffers(ecd1, ecd2, "file1.bin", "file2.bin");

            Assert.True(result.AreIdentical);
        }

        [Fact]
        public void Compare_NestedEcdJpk_DifferentInnerPayload_ReportsDiff()
        {
            byte[] inner1 = TestHelpers.RandomData(64, seed: 99);
            byte[] inner2 = TestHelpers.RandomData(64, seed: 100);
            byte[] jpk1 = BuildJpkRwBuffer(inner1);
            byte[] jpk2 = BuildJpkRwBuffer(inner2);
            byte[] ecd1 = BuildEcdBuffer(jpk1);
            byte[] ecd2 = BuildEcdBuffer(jpk2);

            var result = _service.CompareBuffers(ecd1, ecd2, "file1.bin", "file2.bin");

            Assert.False(result.AreIdentical);
            // Should report differences at the inner Raw layer (CRC32)
            Assert.Contains(result.Differences, d => d.Property == "CRC32");
        }

        #endregion

        #region DiffResult Properties

        [Fact]
        public void DiffResult_AreIdentical_TrueWhenNoDifferences()
        {
            var result = new DiffResult
            {
                File1 = "a.bin",
                File2 = "b.bin",
                Differences = new System.Collections.Generic.List<DiffEntry>()
            };

            Assert.True(result.AreIdentical);
        }

        [Fact]
        public void DiffResult_AreIdentical_FalseWhenDifferencesExist()
        {
            var result = new DiffResult
            {
                File1 = "a.bin",
                File2 = "b.bin",
                Differences = new System.Collections.Generic.List<DiffEntry>
                {
                    new() { Layer = "ECD", Property = "CRC32", Value1 = "0x1234", Value2 = "0x5678" }
                }
            };

            Assert.False(result.AreIdentical);
        }

        #endregion
    }
}
