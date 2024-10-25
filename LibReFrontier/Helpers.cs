using Force.Crc32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace LibReFrontier
{
    public class Helpers
    {
        /// <summary>
        /// Read null-terminated string
        /// </summary>
        /// <param name="brInput"></param>
        /// <param name="encoding"></param>
        /// <returns></returns>
        public static string ReadNullterminatedString(BinaryReader brInput, Encoding encoding)
        {
            var charByteList = new List<byte>();
            string str;
            if (brInput.BaseStream.Position == brInput.BaseStream.Length)
            {
                byte[] charByteArray = [.. charByteList];
                str = encoding.GetString(charByteArray);
                return str;
            }
            byte b = brInput.ReadByte();
            while ((b != 0x00) && (brInput.BaseStream.Position != brInput.BaseStream.Length))
            {
                charByteList.Add(b);
                b = brInput.ReadByte();
            }
            byte[] char_bytes = [.. charByteList];
            str = encoding.GetString(char_bytes);
            return str;
        }

        /// <summary>
        /// Multi-filter GetFiles https://stackoverflow.com/a/3754470/5343630
        /// </summary>
        public static class MyDirectory
        {
            public static string[] GetFiles(
                string path,
                string[] searchPatterns,
                SearchOption searchOption = SearchOption.TopDirectoryOnly
            )
            {
                return searchPatterns.AsParallel()
                       .SelectMany(searchPattern =>
                              Directory.EnumerateFiles(path, searchPattern, searchOption))
                              .ToArray();
            }
        }

        /// <summary>
        /// Print to console with seperator
        /// </summary>
        /// <param name="input">Value to print</param>
        /// <param name="printBefore">Set to true to display input before the separator.</param>
        public static void Print(string input, bool printBefore)
        {
            if (printBefore)
            {
                Console.WriteLine("\n==============================");
                Console.WriteLine(input);
            }
            else
            {
                Console.WriteLine(input);
                Console.WriteLine("==============================");
            }
        }


        /// <summary>
        /// String to byte array
        /// </summary>
        /// <param name="hex"></param>
        /// <returns></returns>
        public static byte[] StringToByteArray(string hex)
        {
            int NumberChars = hex.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }

        // Byte array to string
        public static string ByteArrayToString(byte[] ba)
        {
            StringBuilder hex = new(ba.Length * 2);
            foreach (byte b in ba)
                hex.AppendFormat("{0:x2}", b);
            return hex.ToString();
        }

        /// <summary>
        /// CRC32 byte array - just to remove dependency from TextTool
        /// </summary>
        /// <param name="array"></param>
        /// <returns></returns>
        public static uint GetCrc32(byte[] array)
        {
            return Crc32Algorithm.Compute(array);
        }

        /// <summary>
        /// Get the update entry format for MHFUP_00.DAT.
        /// </summary>
        /// <param name="fileName">File that was updated</param>
        /// <returns>Modified data in custom format for MHFUP_00.DAT</returns>
        public static string GetUpdateEntry(string fileName)
        {
            DateTime date = File.GetLastWriteTime(fileName);
            string dateHex2 = date.Subtract(new DateTime(1601, 1, 1)).Ticks.ToString("X16")[..8];
            string dateHex1 = date.Subtract(new DateTime(1601, 1, 1)).Ticks.ToString("X16")[8..];
            byte[] repackData = File.ReadAllBytes(fileName);
            uint crc32 = Crc32Algorithm.Compute(repackData);
            Console.WriteLine($"{crc32:X8},{dateHex1},{dateHex2},{fileName.Replace("output", "dat")},{repackData.Length},0");
            return $"{crc32:X8},{dateHex1},{dateHex2},{fileName},{repackData.Length},0";
        }

        /// <summary>
        /// Search for byte array
        /// </summary>
        /// <param name="haystack">Bytes to search into.</param>
        /// <param name="needle">Bytes to search.</param>
        /// <returns>Offset if found, -1 otherwise.</returns>
        public static int GetOffsetOfArray(byte[] haystack, byte[] needle)
        {
            for (int i = 0; i <= haystack.Length - needle.Length; i++)
            {
                if (MatchArrays(haystack, needle, i))
                {
                    return i;
                }
            }
            return -1;
        }

        static bool MatchArrays(byte[] haystack, byte[] needle, int start)
        {
            if (needle.Length + start > haystack.Length)
            {
                return false;
            }
            for (int i = 0; i < needle.Length; i++)
            {
                if (needle[i] != haystack[i + start])
                {
                    return false;
                }
            }
            return true;
        }

        // Header <-> extensions
        public enum Extensions
        {
            dds = 542327876,
            ftxt = 0x000B0000,  // custom extension
            gfx2 = 846751303,   // WiiU texture
            jkr = 0x1A524B4A,
            ogg = 0x5367674F,
            pmo = 7302512,      // iOS MHFU model
            png = 0x474e5089,
            tmh = 1213027374    // iOS MHFU texture
        }

        // Get file extension for files without unique 4-byte magic
        public static string CheckForMagic(uint headerInt, byte[] data)
        {
            byte[] header;
            string extension = null;

            if (headerInt == 1)
            {
                header = new byte[12];
                Array.Copy(data, header, 12);
                headerInt = BitConverter.ToUInt32(header, 8);
                if (headerInt == data.Length) extension = "fmod";
            }
            else if (headerInt == 0xC0000000)
            {
                header = new byte[12];
                Array.Copy(data, header, 12);
                headerInt = BitConverter.ToUInt32(header, 8);
                if (headerInt == data.Length) extension = "fskl";
            }

            return extension;
        }
    }
}
