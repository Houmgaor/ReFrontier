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
        /// Simple arguments parser.
        /// </summary>
        /// <param name="args">Input arguments from the CLI</param>
        /// <returns>Dictionary of arguments. Arguments with no value have a null value assigned.</returns>
        public static Dictionary<string, string> ParseArguments(string[] args)
        {
            var arguments = new Dictionary<string, string>();
            foreach (var arg in args)
            {
                string[] parts = arg.Split('=');
                if (parts.Length == 2)
                {
                    arguments[parts[0]] = parts[1];
                }
                else
                {
                    arguments[arg] = null;
                }
            }
            return arguments;
        }

        /// <summary>
        /// Read null-terminated string
        /// </summary>
        /// <param name="brInput">Reader to read from, we stop at the first null termination.</param>
        /// <param name="encoding">Encoding to use.</param>
        /// <returns>The decoded string found.</returns>
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
        /// Find files corresponding to multiple filters. 
        /// 
        /// Source: https://stackoverflow.com/a/3754470/5343630
        /// </summary>
        /// <param name="path">Directory to search into.</param>
        /// <param name="searchPatterns">Patterns to search for.</param>
        /// <param name="searchOption">Options</param>
        /// <returns>All files found.</returns>
        public static string[] GetFiles(
            string path,
            string[] searchPatterns,
            SearchOption searchOption = SearchOption.TopDirectoryOnly
        )
        {
            return searchPatterns.AsParallel()
                    .SelectMany(searchPattern => 
                    Directory.EnumerateFiles(path, searchPattern, searchOption)
                    )
                    .ToArray();
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
        /// From file header magic number to corresponding file extensions
        /// </summary>
        public enum Extensions
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

        /// <summary>
        /// Get file extension for files without unique 4-byte magic
        /// </summary>
        /// <param name="headerInt"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public static string CheckForMagic(uint headerInt, byte[] data)
        {
            byte[] header;
            string extension = null;

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
}
