using System.Text;

namespace ReFrontier.Tests
{
    /// <summary>
    /// Factory for creating test data structures.
    /// </summary>
    public static class TestDataFactory
    {
        /// <summary>
        /// Create a minimal mhfpac.bin structure with skill data.
        /// </summary>
        /// <param name="skillNames">Array of skill names to include.</param>
        /// <returns>Binary data representing minimal mhfpac.bin.</returns>
        public static byte[] CreateMinimalMhfpac(string[] skillNames)
        {
            // Build mhfpac with skill strings
            // Offset structure:
            // 0xA20 = skill names start offset pointer
            // 0xA1C = skill names end offset pointer (points to string data end)
            // String data starts after header

            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            // Header area (needs to be large enough for offset pointers)
            // Write zeroes up to 0xA24
            bw.Write(new byte[0xA24]);

            // Calculate string section start
            int stringSectionStart = 0xA24;
            int pointerSectionStart = stringSectionStart;
            int stringSectionEnd = pointerSectionStart + (skillNames.Length * 4);

            // Write string pointers at start offset location
            ms.Seek(0xA20, SeekOrigin.Begin);
            bw.Write(pointerSectionStart);
            ms.Seek(0xA1C, SeekOrigin.Begin);
            bw.Write(stringSectionEnd);

            // Write pointer section
            ms.Seek(pointerSectionStart, SeekOrigin.Begin);
            int currentStringOffset = stringSectionEnd;
            foreach (var name in skillNames)
            {
                bw.Write(currentStringOffset);
                currentStringOffset += Encoding.GetEncoding("shift-jis").GetBytes(name).Length + 1;
            }

            // Write string section
            foreach (var name in skillNames)
            {
                byte[] nameBytes = Encoding.GetEncoding("shift-jis").GetBytes(name);
                bw.Write(nameBytes);
                bw.Write((byte)0); // Null terminator
            }

            return ms.ToArray();
        }

        /// <summary>
        /// Create a minimal mhfdat.bin structure with armor data.
        /// </summary>
        /// <param name="armorCount">Number of armor entries per class.</param>
        /// <returns>Binary data representing minimal mhfdat.bin.</returns>
        public static byte[] CreateMinimalMhfdat(int armorCount = 2)
        {
            const int ARMOR_ENTRY_SIZE = 0x48;

            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            // Header area - write zeroes
            bw.Write(new byte[0x200]);

            // Calculate data offsets
            int dataStart = 0x200;
            int headStart = dataStart;
            int bodyStart = headStart + (armorCount * ARMOR_ENTRY_SIZE);
            int armStart = bodyStart + (armorCount * ARMOR_ENTRY_SIZE);
            int waistStart = armStart + (armorCount * ARMOR_ENTRY_SIZE);
            int legStart = waistStart + (armorCount * ARMOR_ENTRY_SIZE);
            int legEnd = legStart + (armorCount * ARMOR_ENTRY_SIZE);

            // Write armor data offset pointers
            // Start offsets (0x50, 0x54, 0x58, 0x5C, 0x60)
            ms.Seek(0x50, SeekOrigin.Begin);
            bw.Write(headStart);
            bw.Write(bodyStart);
            bw.Write(armStart);
            bw.Write(waistStart);
            bw.Write(legStart);

            // End offsets (0xE8, 0x50, 0x54, 0x58, 0x5C) - note the pattern
            ms.Seek(0xE8, SeekOrigin.Begin);
            bw.Write(bodyStart); // End of head
            ms.Seek(0x50, SeekOrigin.Begin);
            // Already written above, end pointers overlap with start pointers
            // This is the actual file structure

            // Write armor entry data
            ms.Seek(dataStart, SeekOrigin.End);

            // For each armor class, write dummy entries
            for (int classIdx = 0; classIdx < 5; classIdx++)
            {
                for (int i = 0; i < armorCount; i++)
                {
                    WriteArmorEntry(bw, (short)i, (short)i, (byte)(i + 1), 100 + i);
                }
            }

            return ms.ToArray();
        }

        /// <summary>
        /// Create a minimal mhfinf.bin structure for quest data.
        /// </summary>
        /// <returns>Binary data representing minimal mhfinf.bin.</returns>
        public static byte[] CreateMinimalMhfinf()
        {
            // Just create a buffer large enough with zeroes
            return new byte[0x200000]; // 2MB
        }

        /// <summary>
        /// Create test armor CSV content.
        /// </summary>
        /// <param name="count">Number of entries per armor class.</param>
        /// <returns>CSV content as string.</returns>
        public static string CreateArmorCsv(int count)
        {
            var sb = new StringBuilder();
            sb.AppendLine("EquipClass\tName\tModelIdMale\tModelIdFemale\tIsMaleEquip\tIsFemaleEquip\tIsBladeEquip\tIsGunnerEquip\tBool1\tIsSPEquip\tBool3\tBool4\tRarity\tMaxLevel\tUnk1_1\tUnk1_2\tUnk1_3\tUnk1_4\tUnk2\tZennyCost\tUnk3\tBaseDefense\tFireRes\tWaterRes\tThunderRes\tDragonRes\tIceRes\tUnk3_1\tBaseSlots\tMaxSlots\tSthEventCrown\tUnk5\tUnk6\tUnk7_1\tUnk7_2\tUnk7_3\tUnk7_4\tUnk8_1\tUnk8_2\tUnk8_3\tUnk8_4\tUnk10\tSkillId1\tSkillPts1\tSkillId2\tSkillPts2\tSkillId3\tSkillPts3\tSkillId4\tSkillPts4\tSkillId5\tSkillPts5\tSthHiden\tUnk12\tUnk13\tUnk14\tUnk15\tUnk16\tUnk17\tUnk18\tUnk19");

            string[] classes = ["頭", "胴", "腕", "腰", "脚"];

            foreach (var cls in classes)
            {
                for (int i = 0; i < count; i++)
                {
                    sb.AppendLine($"{cls}\tTestArmor{i}\t{i}\t{i}\tTrue\tTrue\tTrue\tFalse\tFalse\tFalse\tFalse\tFalse\t1\t7\t0\t0\t0\t0\t0\t100\t0\t50\t0\t0\t0\t0\t0\t0\t1\t3\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t攻撃\t5\t\t0\t\t0\t\t0\t\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0");
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Create test string database CSV content.
        /// </summary>
        /// <param name="entries">Array of (offset, jString, eString) tuples.</param>
        /// <returns>CSV content as string.</returns>
        public static string CreateStringDatabaseCsv(params (uint offset, string jString, string eString)[] entries)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Offset\tHash\tJString\tEString");

            foreach (var (offset, jString, eString) in entries)
            {
                // Calculate CRC32 hash
                uint hash = LibReFrontier.Crypto.GetCrc32(Encoding.GetEncoding("shift-jis").GetBytes(jString));
                sb.AppendLine($"{offset}\t{hash}\t{jString}\t{eString}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Create a binary file with string data for text tool testing.
        /// </summary>
        /// <param name="strings">Array of strings to include.</param>
        /// <returns>Binary data with sequential null-terminated strings.</returns>
        public static byte[] CreateBinaryWithStrings(params string[] strings)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            foreach (var str in strings)
            {
                byte[] bytes = Encoding.GetEncoding("shift-jis").GetBytes(str);
                bw.Write(bytes);
                bw.Write((byte)0); // Null terminator
            }

            return ms.ToArray();
        }

        /// <summary>
        /// Create a binary file with string pointers followed by string data.
        /// </summary>
        /// <param name="strings">Array of strings to include.</param>
        /// <returns>Binary data with pointer table and string data.</returns>
        /// <remarks>
        /// The string data starts at offset 16 to satisfy the validation check
        /// in DumpAndHashInternal which requires strPos >= 10.
        /// Structure: [pointer table (n*4 bytes)] [padding to offset 16] [string data]
        /// </remarks>
        public static byte[] CreateBinaryWithStringPointers(params string[] strings)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            // Calculate string data start - must be >= 10 for validation
            int pointerTableSize = strings.Length * 4;
            int stringDataStart = Math.Max(pointerTableSize, 16); // Ensure >= 16 to pass strPos < 10 check

            // Build string data first to know offsets
            var stringOffsets = new int[strings.Length];
            var stringBytes = new byte[strings.Length][];
            int currentOffset = stringDataStart;

            for (int i = 0; i < strings.Length; i++)
            {
                stringOffsets[i] = currentOffset;
                stringBytes[i] = Encoding.GetEncoding("shift-jis").GetBytes(strings[i]);
                currentOffset += stringBytes[i].Length + 1; // +1 for null terminator
            }

            // Write pointer table
            foreach (var offset in stringOffsets)
            {
                bw.Write(offset);
            }

            // Write padding if needed
            int paddingNeeded = stringDataStart - pointerTableSize;
            if (paddingNeeded > 0)
            {
                bw.Write(new byte[paddingNeeded]);
            }

            // Write string data
            foreach (var bytes in stringBytes)
            {
                bw.Write(bytes);
                bw.Write((byte)0); // Null terminator
            }

            return ms.ToArray();
        }

        /// <summary>
        /// Create an ECD encrypted file header.
        /// </summary>
        /// <returns>16-byte ECD header.</returns>
        public static byte[] CreateEcdHeader()
        {
            return new byte[]
            {
                0x65, 0x63, 0x64, 0x1A, // Magic: ecd\x1A
                0x00, 0x00, 0x00, 0x00, // Reserved
                0x00, 0x00, 0x00, 0x00, // Reserved
                0x00, 0x00, 0x00, 0x00  // Reserved
            };
        }

        /// <summary>
        /// Create a JPK compressed file header.
        /// </summary>
        /// <param name="compressionType">Compression type (0=RW, 1=None, 2=HFIRW, 3=LZ, 4=HFI).</param>
        /// <param name="decompressedSize">Original uncompressed size.</param>
        /// <returns>JPK header bytes.</returns>
        public static byte[] CreateJpkHeader(int compressionType, int decompressedSize)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            bw.Write((uint)0x1A524B4A); // Magic: JKR\x1A
            bw.Write((ushort)0);        // Skip 2 bytes
            bw.Write((ushort)compressionType);
            bw.Write((int)0x10);        // Start offset (after header)
            bw.Write(decompressedSize);

            return ms.ToArray();
        }

        /// <summary>
        /// Write a single armor entry to a binary writer.
        /// </summary>
        private static void WriteArmorEntry(BinaryWriter bw, short modelIdMale, short modelIdFemale, byte rarity, int zennyCost)
        {
            bw.Write(modelIdMale);      // 0x00
            bw.Write(modelIdFemale);    // 0x02
            bw.Write((byte)0x03);       // 0x04: bitfield (male + female equip)
            bw.Write(rarity);           // 0x05
            bw.Write((byte)7);          // 0x06: MaxLevel
            bw.Write(new byte[5]);      // 0x07-0x0B: Unk1_1-4, Unk2
            bw.Write(zennyCost);        // 0x0C
            bw.Write((short)0);         // 0x10: Unk3
            bw.Write((short)50);        // 0x12: BaseDefense
            bw.Write(new byte[5]);      // 0x14-0x18: Resistances
            bw.Write((short)0);         // 0x19: Unk3_1
            bw.Write((byte)1);          // 0x1B: BaseSlots
            bw.Write((byte)3);          // 0x1C: MaxSlots
            bw.Write(new byte[10]);     // 0x1D-0x26: Various bytes
            bw.Write((short)0);         // 0x27: Unk10

            // Skills (5 skills, each 2 bytes: id + points)
            for (int i = 0; i < 5; i++)
            {
                bw.Write((byte)0);      // Skill ID
                bw.Write((sbyte)0);     // Skill points
            }

            bw.Write(0);                // 0x34: SthHiden
            bw.Write(0);                // 0x38: Unk12
            bw.Write(new byte[4]);      // 0x3C-0x3F: Unk13-16
            bw.Write(0);                // 0x40: Unk17
            bw.Write((short)0);         // 0x44: Unk18
            bw.Write((short)0);         // 0x46: Unk19
        }
    }
}
