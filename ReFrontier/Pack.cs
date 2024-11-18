﻿using LibReFrontier;
using ReFrontier.jpk;
using System;
using System.Collections.Generic;
using System.IO;

namespace ReFrontier
{
    public class Pack
    {
        /// <summary>
        /// Standard processing of an input pack file.
        /// </summary>
        /// <param name="input">Input file path.</param>
        public static void ProcessPackInput(string input)
        {
            string logFile = $"{input}/{input.Remove(0, input.LastIndexOf('/')+1)}.log";
            if (!File.Exists(logFile))
            {
                logFile = $"{input}.log";
                if (!File.Exists(logFile))
                    Console.WriteLine("ERROR: Log file does not exist.");
                    return;
            }
            string[] logContent = File.ReadAllLines(logFile);

            // Simple Archive
            if (logContent[0] == "SimpleArchive")
            {
                string fileName = logContent[1];
                int count = int.Parse(logContent[2]);
                Console.WriteLine($"Simple archive with {count} entries.");

                // Entries
                List<string> listFileNames = [];

                for (int i = 3; i < logContent.Length; i++)
                {
                    string[] columns = logContent[i].Split(',');
                    listFileNames.Add(columns[0]);
                }

                Directory.CreateDirectory("output");
                fileName = $"output/{fileName}";
                using (BinaryWriter bwOutput = new(File.Open(fileName, FileMode.Create)))
                {
                    bwOutput.Write(count);
                    int offset = 0x04 + count * 0x08;
                    for (int i = 0; i < count; i++)
                    {
                        Console.WriteLine($"{input}/{listFileNames[i]}");
                        byte[] fileData = [];
                        if (listFileNames[i] != "null") {
                            fileData = File.ReadAllBytes($"{input}/{listFileNames[i]}");
                        }
                        bwOutput.BaseStream.Seek(0x04 + i * 0x08, SeekOrigin.Begin);
                        bwOutput.Write(offset);
                        bwOutput.Write(fileData.Length);
                        bwOutput.BaseStream.Seek(offset, SeekOrigin.Begin);
                        bwOutput.Write(fileData);
                        offset += fileData.Length;
                    }
                }
                Helpers.GetUpdateEntry(fileName);
            }
            // MHA; this doesn't do file data padding for now, but seems the game works just fine without it
            else if (logContent[0] == "MHA")
            {
                string fileName = logContent[1];
                int count = int.Parse(logContent[2]);
                short unk1 = short.Parse(logContent[3]);
                short unk2 = short.Parse(logContent[4]);
                Console.WriteLine($"MHA with {count} entries (unk1: {unk1}, unk2: {unk2}).");

                // Entries
                List<string> listFileNames = [];
                List<int> listFileIds = [];

                for (int i = 0; i < count; i++)
                {
                    string[] columns = logContent[i + 5].Split(',');  // 5 = Account for meta data entries before
                    listFileNames.Add(columns[0]);
                    listFileIds.Add(int.Parse(columns[1]));
                }

                // Set up memory streams for segments
                MemoryStream entryMetaBlock = new();
                MemoryStream entryNamesBlock = new();

                Directory.CreateDirectory("output");
                fileName = $"output/{fileName}";
                using BinaryWriter bwOutput = new(File.Open(fileName, FileMode.Create));
                // Header
                bwOutput.Write(23160941);    // MHA magic
                bwOutput.Write(0);           // pointerEntryMetaBlock
                bwOutput.Write(count);
                bwOutput.Write(0);           // pointerEntryNamesBlock
                bwOutput.Write(0);           // entryNamesBlockLength
                bwOutput.Write(unk1);
                bwOutput.Write(unk2);

                int pointerEntryNamesBlock = 0x18;   // 0x18 = Header length
                int stringOffset = 0;
                for (int i = 0; i < count; i++)
                {
                    Console.WriteLine($"{input}/{listFileNames[i]}");
                    byte[] fileData = File.ReadAllBytes($"{input}/{listFileNames[i]}");
                    bwOutput.Write(fileData);

                    entryMetaBlock.Write(BitConverter.GetBytes(stringOffset), 0, 4);
                    entryMetaBlock.Write(BitConverter.GetBytes(pointerEntryNamesBlock), 0, 4);
                    entryMetaBlock.Write(BitConverter.GetBytes(fileData.Length), 0, 4);
                    entryMetaBlock.Write(BitConverter.GetBytes(fileData.Length), 0, 4); // write psize if necessary
                    entryMetaBlock.Write(BitConverter.GetBytes(listFileIds[i]), 0, 4);

                    System.Text.UTF8Encoding enc = new();
                    byte[] arrayFileName = enc.GetBytes(listFileNames[i]);
                    entryNamesBlock.Write(arrayFileName, 0, arrayFileName.Length);
                    entryNamesBlock.WriteByte(0);
                    stringOffset += arrayFileName.Length + 1;

                    pointerEntryNamesBlock += fileData.Length; // update with psize if necessary
                }

                bwOutput.Write(entryNamesBlock.ToArray());
                bwOutput.Write(entryMetaBlock.ToArray());

                // Update offsets
                bwOutput.Seek(4, SeekOrigin.Begin);
                bwOutput.Write((int)(pointerEntryNamesBlock + entryNamesBlock.Length));
                bwOutput.Write(count);
                bwOutput.Write(pointerEntryNamesBlock);
                bwOutput.Write((int)entryNamesBlock.Length);
            }
            // Stage Container
            else if (logContent[0] == "StageContainer")
            {
                string fileName = logContent[1];

                // Entries
                List<string> listFileNames = [];

                // For first three segments
                for (int i = 2; i < 5; i++)
                {
                    string[] columns = logContent[i].Split(',');
                    listFileNames.Add(columns[0]);
                }

                // For rest of files
                int restCount = int.Parse(logContent[5].Split(',')[0]);
                int restUnkHeader = int.Parse(logContent[5].Split(',')[1]);

                for (int i = 6; i < 6 + restCount; i++)
                {
                    string[] columns = logContent[i].Split(',');
                    listFileNames.Add(columns[0]);
                }

                Console.WriteLine($"Stage Container with {listFileNames.Count} entries.");

                Directory.CreateDirectory("output");
                fileName = $"output/{fileName}";
                using BinaryWriter bwOutput = new(File.Open(fileName, FileMode.Create));
                // Write temp dir
                // + 8 = rest count and unk header int
                // the directory in the requested test file is padded to 16 bytes
                // not sure if necessary and if this applies to all
                byte[] tempDir = new byte[(3 * 8 + restCount * 0x0C + 8 + 15) & ~15];
                bwOutput.Write(tempDir);

                int offset = tempDir.Length;

                // For first three segments
                for (int i = 0; i < 3; i++)
                {
                    byte[] fileData = [];
                    bwOutput.BaseStream.Seek(i * 0x08, SeekOrigin.Begin);

                    if (listFileNames[i] != "null")
                    {
                        Console.WriteLine($"{input}/{listFileNames[i]}");
                        fileData = File.ReadAllBytes($"{input}/{listFileNames[i]}");
                        bwOutput.Write(offset);
                        bwOutput.Write(fileData.Length);
                        bwOutput.BaseStream.Seek(offset, SeekOrigin.Begin);
                        bwOutput.Write(fileData);
                        offset += fileData.Length;

                        // data segments are padded to 16 or 32 bytes supposedly?
                        // not sure if necessary and if this applies to all
                        // byte[] finalDataArray = new byte[(fileData.Length + 31) & ~31];
                        // fileData.CopyTo(finalDataArray, 0);
                        // bwOutput.Write(finalDataArray);
                        // offset += finalDataArray.Length;
                    }
                    else
                    {
                        Console.WriteLine("Writing null entry");
                        bwOutput.Write((long)0);
                    }
                }

                // For rest
                bwOutput.BaseStream.Seek(3 * 0x08, SeekOrigin.Begin);
                bwOutput.Write(restCount);
                bwOutput.Write(restUnkHeader);

                for (int i = 3; i < restCount + 3; i++)
                {
                    byte[] fileData = [];
                    bwOutput.BaseStream.Seek(3 * 8 + (i - 3) * 0x0C + 8, SeekOrigin.Begin); // + 8 = rest count and unk header int

                    if (listFileNames[i] != "null")
                    {
                        Console.WriteLine($"{input}/{listFileNames[i]}");
                        fileData = File.ReadAllBytes($"{input}/{listFileNames[i]}");
                        bwOutput.Write(offset);
                        bwOutput.Write(fileData.Length);
                        bwOutput.Write(int.Parse(logContent[6 + i - 3].Split(',')[3]));
                        bwOutput.BaseStream.Seek(offset, SeekOrigin.Begin);
                        bwOutput.Write(fileData);
                        offset += fileData.Length;
                    }
                    else
                    {
                        Console.WriteLine("Writing null entry");
                        bwOutput.Write((long)0);
                        bwOutput.Write(0);
                    }
                }
            }
            Console.WriteLine("==============================");
        }

        /// <summary>
        /// Compress a JPK file to a JKR type
        /// </summary>
        /// <param name="atype">JPK type, between 0 and 4</param>
        /// <param name="inPath">Input file path.</param>
        /// <param name="otPath">Output file path.</param>
        /// <param name="level">Compression level between 0 and 10000.</param>
        public static void JPKEncode(int atype, string inPath, string otPath, int level)
        {
            Directory.CreateDirectory("output");

            byte[] buffer = File.ReadAllBytes(inPath);
            int insize = buffer.Length;
            if (File.Exists(otPath))
                File.Delete(otPath);
            Console.WriteLine(
                $"Starting file compression, type {atype}, level {level / 100} to {otPath}"
            );
            FileStream fsot = File.Create(otPath);
            BinaryWriter br = new(fsot);
            // JKR header
            br.Write((uint) 0x1A524B4A);
            br.Write((ushort) 0x108);
            br.Write((ushort) atype);
            br.Write((uint) 0x10);
            br.Write(insize);
            IJPKEncode encoder;
            switch (atype)
            {
                case 0:
                    encoder = new JPKEncodeRW();
                    break;
                case 3:
                    encoder = new JPKEncodeLz();
                    break;
                case 4:
                    encoder = new JPKEncodeHFI();
                    break;
                default:
                    // For level 2 encoding use: encoder = new JPKEncodeHFIRW();
                    fsot.Close();
                    File.Delete(otPath);
                    throw new InvalidOperationException("Unsupported/invalid type: " + atype);
            }

            DateTime start, finnish;
            start = DateTime.Now;
            encoder.ProcessOnEncode(buffer, fsot, level, null);
            finnish = DateTime.Now;
            Helpers.Print(
                $"File compressed using type {atype} (level {level / 100}): " + 
                $"{fsot.Length} bytes ({1 - (decimal)fsot.Length / insize:P} saved) in {finnish - start:%m\\:ss\\.ff}",
                false
            );
            fsot.Close();
        }
    }
}
