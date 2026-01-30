using System;
using System.Linq;

namespace LibReFrontier
{
    /// <summary>
    /// Raw bytes operations.
    /// </summary>
    public static class ByteOperations
    {
        /// <summary>
        /// Search for byte array
        /// </summary>
        /// <param name="haystack">Bytes to search into.</param>
        /// <param name="needle">Bytes to search.</param>
        /// <returns>Offset if found, -1 otherwise.</returns>
        public static int GetOffsetOfArray(byte[] haystack, byte[] needle)
        {
            // Edge case: empty needle
            if (needle.Length == 0)
                return 0;
            // If haystack is too small
            if (haystack.Length < needle.Length)
                return -1;

            Span<byte> haystackSpan = haystack;
            Span<byte> needleSpan = needle;

            for (int i = 0; i <= haystack.Length - needle.Length; i++)
            {
                if (haystackSpan.Slice(i, needle.Length).SequenceEqual(needleSpan))
                {
                    return i;
                }
            }
            return -1;
        }


        /// <summary>
        /// Detect file extension from data header.
        /// </summary>
        /// <param name="data">File data buffer (must be at least 4 bytes).</param>
        /// <param name="headerInt">Output parameter containing the header as uint32.</param>
        /// <returns>File extension string (without dot), defaults to "bin" if unknown.</returns>
        public static string DetectExtension(byte[] data, out uint headerInt)
        {
            headerInt = 0;
            if (data == null || data.Length < 4)
                return "bin";

            headerInt = BitConverter.ToUInt32(data, 0);
            string? extension = Enum.GetName(typeof(FileExtension), headerInt);
            extension ??= CheckForMagic(headerInt, data);
            return extension ?? "bin";
        }

        /// <summary>
        /// Get file extension for files without unique 4-byte magic
        /// </summary>
        /// <param name="headerInt"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public static string? CheckForMagic(uint headerInt, byte[] data)
        {
            byte[] header;
            string? extension = null;

            if (headerInt == 1)
            {
                header = new byte[12];
                Array.Copy(data, header, 12);
                headerInt = BitConverter.ToUInt32(header, 8);
                if (headerInt == data.Length)
                    extension = "fmod";
            }
            else if (headerInt == 0xC0000000)
            {
                header = new byte[12];
                Array.Copy(data, header, 12);
                headerInt = BitConverter.ToUInt32(header, 8);
                if (headerInt == data.Length)
                    extension = "fskl";
            }

            return extension;
        }
    }

    /// <summary>
    /// From file header magic number to corresponding file extensions
    /// </summary>
    public enum FileExtension
    {
        dds = 542327876,
        /// <summary>
        /// Custom extension
        /// </summary>
        ftxt = 0x000B0000,
        /// <summary>
        /// WiiU texture
        /// </summary>
        gfx2 = 846751303,
        jkr = 0x1A524B4A,
        ogg = 0x5367674F,
        /// <summary>
        /// iOS MHFU model
        /// </summary>
        pmo = 7302512,
        png = 0x474e5089,
        /// <summary>
        /// iOS MHFU texture
        /// </summary>
        tmh = 1213027374
    }
}
