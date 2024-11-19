using Force.Crc32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace LibReFrontier
{
    /// <summary>
    /// Operation relative to file reading and creation.
    /// </summary>
    public class FileOperations
    {
        /// <summary>
        /// Read bytes from position until meeting null (0x00) or end-of-file.
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

    }
}
