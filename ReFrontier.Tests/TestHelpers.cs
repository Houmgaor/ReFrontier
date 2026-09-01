using System;

namespace ReFrontier.Tests
{
    /// <summary>
    /// Utility methods for generating test data and assertions.
    /// </summary>
    public static class TestHelpers
    {
        /// <summary>
        /// Reads the Huffman code length for each byte from a JPK HFI table.
        /// </summary>
        public static int[] ReadHuffmanCodeLengths(byte[] encoded)
        {
            const int huffmanTableHeaderSize = sizeof(short);
            short tableLength = BitConverter.ToInt16(encoded, 0);
            short[] table = new short[tableLength];

            for (int i = 0; i < table.Length; i++)
                table[i] = BitConverter.ToInt16(encoded, huffmanTableHeaderSize + i * sizeof(short));

            int[] codeLengths = new int[256];
            ReadHuffmanCodeLengths(table, tableLength, 0, codeLengths);
            return codeLengths;
        }

        private static void ReadHuffmanCodeLengths(short[] table, int value, int depth, int[] codeLengths)
        {
            if (value < 0x100)
            {
                codeLengths[value] = depth;
                return;
            }

            int childIndex = (value - 0x100) * 2;
            ReadHuffmanCodeLengths(table, table[childIndex], depth + 1, codeLengths);
            ReadHuffmanCodeLengths(table, table[childIndex + 1], depth + 1, codeLengths);
        }

        /// <summary>
        /// Returns an empty byte array.
        /// </summary>
        public static byte[] EmptyData() => [];

        /// <summary>
        /// Returns a single-byte array containing the specified value.
        /// </summary>
        public static byte[] SingleByte(byte value) => [value];

        /// <summary>
        /// Returns an array of repeated 'A' (0x41) bytes.
        /// </summary>
        public static byte[] RepetitiveData(int length)
        {
            byte[] data = new byte[length];
            for (int i = 0; i < length; i++)
                data[i] = 0x41; // 'A'
            return data;
        }

        /// <summary>
        /// Returns deterministic pseudo-random data using the given seed.
        /// </summary>
        public static byte[] RandomData(int length, int seed)
        {
            byte[] data = new byte[length];
            Random rng = new(seed);
            rng.NextBytes(data);
            return data;
        }

        /// <summary>
        /// Returns data with alternating pattern and random sections.
        /// Pattern: 16 bytes of incrementing values, then 16 bytes of random.
        /// </summary>
        public static byte[] MixedData(int length, int seed)
        {
            byte[] data = new byte[length];
            Random rng = new(seed);
            for (int i = 0; i < length; i++)
            {
                int section = (i / 16) % 2;
                if (section == 0)
                    data[i] = (byte)(i % 256);
                else
                    data[i] = (byte)rng.Next(256);
            }
            return data;
        }

        /// <summary>
        /// Asserts that two byte arrays are equal, with detailed error messages.
        /// </summary>
        public static void AssertBytesEqual(byte[] expected, byte[] actual, string context = "")
        {
            string prefix = string.IsNullOrEmpty(context) ? "" : $"{context}: ";

            Assert.True(
                expected.Length == actual.Length,
                $"{prefix}Length mismatch. Expected {expected.Length}, got {actual.Length}."
            );

            for (int i = 0; i < expected.Length; i++)
            {
                if (expected[i] != actual[i])
                {
                    Assert.Fail(
                        $"{prefix}Mismatch at index {i}. Expected 0x{expected[i]:X2}, got 0x{actual[i]:X2}."
                    );
                }
            }
        }

        /// <summary>
        /// Normalizes a path to use forward slashes for cross-platform testing.
        /// </summary>
        public static string NormalizePath(string path)
        {
            return path.Replace('\\', '/');
        }

        /// <summary>
        /// Asserts that two paths are equal after normalization.
        /// </summary>
        public static void AssertPathsEqual(string expected, string actual)
        {
            Assert.Equal(NormalizePath(expected), NormalizePath(actual));
        }
    }
}
