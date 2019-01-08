﻿using System;
using System.IO;
using System.Text;

namespace ReFrontier
{
    class Unpack
    {
        public static void UnpackSimpleArchive(string input, BinaryReader brInput, int magicSize, bool createLog)
        {
            FileInfo fileInfo = new FileInfo(input);
            string outputDir = $"{fileInfo.DirectoryName}\\{Path.GetFileNameWithoutExtension(input)}";

            int count = brInput.ReadInt32();

            // Calculate complete size of extracted data to avoid extracting plausible files that aren't archives
            int completeSize = 4;
            for (int i = 0; i < count; i++)
            {
                brInput.BaseStream.Seek(4, SeekOrigin.Current);
                int entrySize = brInput.ReadInt32();
                completeSize += entrySize;
            }
            brInput.BaseStream.Seek(4, SeekOrigin.Begin);
            
            if (completeSize > fileInfo.Length) { Console.WriteLine("Impossible container. Skipping."); return; }

            // Write to log file if desired
            Directory.CreateDirectory(outputDir);
            StreamWriter logOutput = new StreamWriter($"{outputDir}\\{Path.GetFileNameWithoutExtension(input)}.log");
            if (createLog) { logOutput.WriteLine("SimpleArchive"); logOutput.WriteLine(input.Remove(0, input.LastIndexOf('\\') + 1)); logOutput.WriteLine(count); }

            for (int i = 0; i < count; i++)
            {
                int entryOffset = brInput.ReadInt32();
                int entrySize = brInput.ReadInt32();

                // Skip if size is zero
                if (entrySize == 0)
                {
                    Console.WriteLine($"Offset: 0x{entryOffset.ToString("X8")}, Size: 0x{entrySize.ToString("X8")} (SKIPPED)");
                    if (createLog) logOutput.WriteLine($"null,{entryOffset},{entrySize},0");
                    continue;
                }

                // Read file to array
                brInput.BaseStream.Seek(entryOffset, SeekOrigin.Begin);
                byte[] entryData = brInput.ReadBytes(entrySize);

                // Check file header
                byte[] header = new byte[4];
                Array.Copy(entryData, header, 4);
                int headerInt = BitConverter.ToInt32(header, 0);

                string extension = Enum.GetName(typeof(Helpers.Extensions), headerInt);
                if (extension == null) extension = ".bin";

                // Print info
                Console.WriteLine($"Offset: 0x{entryOffset.ToString("X8")}, Size: 0x{entrySize.ToString("X8")} ({extension})");
                if (createLog) logOutput.WriteLine($"{(i + 1).ToString("D4")}_{entryOffset.ToString("X8")}.{extension},{entryOffset},{entrySize},{headerInt}");

                // Extract file
                File.WriteAllBytes($"{outputDir}\\{(i + 1).ToString("D4")}_{entryOffset.ToString("X8")}.{extension}", entryData);

                // Move to next entry block
                brInput.BaseStream.Seek(magicSize + (i + 1) * 0x08, SeekOrigin.Begin);
            }
            logOutput.Close();
        }

        public static void UnpackMHA(string input, BinaryReader brInput)
        {
            FileInfo fileInfo = new FileInfo(input);
            string outputDir = $"{fileInfo.DirectoryName}\\{Path.GetFileNameWithoutExtension(input)}";
            Directory.CreateDirectory(outputDir);

            // Read header
            int pointerEntryMetaBlock = brInput.ReadInt32();
            int count = brInput.ReadInt32();
            int pointerEntryNamesBlock = brInput.ReadInt32();
            int entryNamesBlockLength = brInput.ReadInt32();

            // File Data
            int entryNamesLength = 0;
            for (int i = 0; i < count; i++)
            {
                // Get meta
                brInput.BaseStream.Seek(pointerEntryMetaBlock + i * 0x14, SeekOrigin.Begin);
                int entryUnk1 = brInput.ReadInt32();
                int entryOffset = brInput.ReadInt32();
                int entrySize = brInput.ReadInt32();
                int entryUnk2 = brInput.ReadInt32();
                int entryUnk3 = brInput.ReadInt32();

                // Get name
                brInput.BaseStream.Seek(pointerEntryNamesBlock + entryNamesLength, SeekOrigin.Begin);
                string entryName = Helpers.ReadNullterminatedString(brInput, Encoding.UTF8);
                entryNamesLength += (entryName.Length + 1);

                // Extract file
                brInput.BaseStream.Seek(entryOffset, SeekOrigin.Begin);
                byte[] entryData = brInput.ReadBytes(entrySize);
                File.WriteAllBytes($"{outputDir}\\{entryName}", entryData);

                Console.WriteLine($"{entryName}, 0x{entryUnk1.ToString("X8")}, Offset: 0x{entryOffset.ToString("X8")}, Size: 0x{entrySize.ToString("X8")}, pSize: 0x{entryUnk2.ToString("X8")}, 0x{entryUnk3.ToString("X8")}");
            }
        }

        public static void UnpackJPK(string input, BinaryReader brInput)
        {
            FileInfo fileInfo = new FileInfo(input);
            string outputDir = $"{fileInfo.DirectoryName}\\{Path.GetFileNameWithoutExtension(input)}";
            //Directory.CreateDirectory(outputDir);

            // Read header
            brInput.BaseStream.Seek(2, SeekOrigin.Current);
            int jpkType = brInput.ReadInt16();
            int unk_r12 = brInput.ReadInt32();
            int jpkSize = brInput.ReadInt32();

            brInput.BaseStream.Seek(unk_r12, SeekOrigin.Begin);
            int unk_v5_value = brInput.ReadInt32();

            Console.WriteLine($"JPK Type: {jpkType}, Data Offset: 0x{unk_r12.ToString("X8")}, Size: 0x{jpkSize.ToString("X8")}");       
        }

        public static void PrintFTXT(string input, BinaryReader brInput)
        {
            FileInfo fileInfo = new FileInfo(input);
            string outputPath = $"{fileInfo.DirectoryName}\\{Path.GetFileNameWithoutExtension(input)}.txt";
            if (File.Exists(outputPath)) File.Delete(outputPath);
            StreamWriter txtOutput = new StreamWriter(outputPath, true, Encoding.GetEncoding("shift-jis"));

            // Read header
            brInput.BaseStream.Seek(10, SeekOrigin.Current);
            int stringCount = brInput.ReadInt16();
            int textBlockSize = brInput.ReadInt32();

            for (int i = 0; i < stringCount; i++)
            {
                string str = Helpers.ReadNullterminatedString(brInput, Encoding.GetEncoding("shift-jis"));
                txtOutput.WriteLine(str.Replace("\n", "<NEWLINE>"));
            }

            txtOutput.Close();
        }

        // Create archive based on log
        /*else if (mode == "c")
        {
            Directory.CreateDirectory(createDir);

            // Read info from log file
            StreamReader log = new StreamReader(input, Encoding.UTF8, false);
            string archive_type = log.ReadLine();
            int count = Convert.ToInt32(log.ReadLine());
            string create_file = createDir + "\\" + Path.GetFileNameWithoutExtension(input) + "." + archive_type;

            // Read entries from log file
            List<string> l_entry_no = new List<string>();
            List<string> l_entry_type = new List<string>();
            List<string> l_entry_offset = new List<string>();
            List<string> l_entry_size = new List<string>();

            while (!log.EndOfStream)
            {
                var line = log.ReadLine();
                var columns = line.Split(',');

                l_entry_no.Add(columns[0]);
                l_entry_type.Add(columns[1]);
                l_entry_offset.Add(columns[2]);
                l_entry_size.Add(columns[3]);
            }

            Console.WriteLine("Archive type: " + archive_type + ", File count: " + count);

            // Write txb
            if (archive_type == "txb")
            {
                File.WriteAllBytes(create_file, new byte[0x04 + count * 0x08]);
                BinaryWriter writer = new BinaryWriter(File.Open(create_file, FileMode.Open));
                writer.Write(count);

                // Process entries
                int offset = 0x04 + count * 0x08;
                for (int i = 0; i < count; i++)
                {
                    // REMLATER Add support for size zero files
                    byte[] entry_data = File.ReadAllBytes(outputDir + "\\" + l_entry_no[i] + "_" + l_entry_offset[i] + l_entry_type[i]);

                    writer.BaseStream.Seek(0x04 + i * 0x08, SeekOrigin.Begin);
                    writer.Write(offset);
                    writer.Write(entry_data.Length);

                    writer.BaseStream.Seek(offset, SeekOrigin.Begin);
                    writer.Write(entry_data);

                    Console.WriteLine("Offset: 0x" + offset.ToString("X8") + ", Size: 0x" + entry_data.Length.ToString("X8"));

                    offset += entry_data.Length;
                }
            }

            // More archive types here...
        }*/
    }
}