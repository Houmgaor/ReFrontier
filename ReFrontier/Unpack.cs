using System;
using System.IO;
using System.Text;

using LibReFrontier;
using ReFrontier.Jpk;

namespace ReFrontier
{
    /// <summary>
    /// File unpacking: multiple file from one.
    /// </summary>
    internal class Unpack
    {

        /// <summary>
        /// Unpack a simple archive file container.
        /// </summary>
        /// <param name="input">Input file name to read from.</param>
        /// <param name="brInput">Binary reader to the input file.</param>
        /// <param name="magicSize">File magic size, depends on file type.</param>
        /// <param name="createLog">true is a log file should be created.</param>
        /// <param name="cleanUp">Remove the initial input file.</param>
        /// <param name="autoStage">Unpack stage container if true.</param>
        /// <returns>Output folder path.</returns>
        public static string UnpackSimpleArchive(
            string input, BinaryReader brInput, int magicSize, bool createLog,
            bool cleanUp, bool autoStage
        )
        {
            FileInfo fileInfo = new(input);
            string outputDir = $"{input}.unpacked";

            // Abort if too small
            if (fileInfo.Length < 16)
            {
                Console.WriteLine("File is too small. Skipping.");
                return null;
            }

            uint count = brInput.ReadUInt32();
            uint tempCount = count;

            // Calculate complete size of extracted data to avoid extracting plausible files that aren't archives
            int completeSize = magicSize;
            for (int i = 0; i < count; i++)
            {
                brInput.BaseStream.Seek(magicSize, SeekOrigin.Current);
                if (brInput.BaseStream.Position + 4 > brInput.BaseStream.Length)
                {
                    Console.WriteLine($"File terminated early ({i}/{count}) in simple container check.");
                    count = (uint)i;
                    break;
                }
                completeSize += brInput.ReadInt32();
            }

            // Very fragile check for stage container
            const int headerSize = 4;
            brInput.BaseStream.Seek(headerSize, SeekOrigin.Begin);
            int checkUnk = brInput.ReadInt32();
            long checkZero = brInput.ReadInt64();
            if (checkUnk < 9999 && checkZero == 0)
            {
                if (autoStage)
                {
                    brInput.BaseStream.Seek(0, SeekOrigin.Begin);
                    UnpackStageContainer(input, brInput, createLog, cleanUp);
                }
                else
                {
                    Console.WriteLine(
                        "Skipping. Not a valid simple container, but could be stage-specific. Try:\n" +
                        $"ReFrontier.exe {fileInfo.FullName} --stageContainer"
                    );
                }
                return null;
            }

            if (completeSize > fileInfo.Length || tempCount == 0 || tempCount > 9999)
            {
                Console.WriteLine("Skipping. Not a valid simple container.");
                return null;
            }

            Console.WriteLine("Trying to unpack as generic simple container.");
            brInput.BaseStream.Seek(magicSize, SeekOrigin.Begin);

            // Write to log file if desired
            // Needs some other solution because it creates useless logs even if !createLog
            Directory.CreateDirectory(outputDir);
            StreamWriter logOutput = new($"{input}.log");
            if (createLog)
            {
                logOutput.WriteLine("SimpleArchive");
                logOutput.WriteLine(input.Remove(0, input.LastIndexOf('/') + 1));
                logOutput.WriteLine(count);
            }

            for (int i = 0; i < count; i++)
            {
                int entryOffset = brInput.ReadInt32();
                int entrySize = brInput.ReadInt32();

                // Check bad entries
                if (
                    entrySize < headerSize ||
                    entryOffset < headerSize ||
                    entryOffset + entrySize > brInput.BaseStream.Length
                )
                {
                    Console.WriteLine($"Offset: 0x{entryOffset:X8}, Size: 0x{entrySize:X8} (SKIPPED)");
                    if (createLog)
                        logOutput.WriteLine($"null,{entryOffset},{entrySize},0");
                    continue;
                }

                // Read file to array
                brInput.BaseStream.Seek(entryOffset, SeekOrigin.Begin);
                byte[] entryData = brInput.ReadBytes(entrySize);

                // Check file header and get extension
                byte[] header = new byte[headerSize];
                Array.Copy(entryData, header, headerSize);
                uint headerInt = BitConverter.ToUInt32(header, 0);
                string extension = Enum.GetName(typeof(FileExtension), headerInt);
                extension ??= ByteOperations.CheckForMagic(headerInt, entryData);
                extension ??= "bin";

                // Print info
                Console.WriteLine($"Offset: 0x{entryOffset:X8}, Size: 0x{entrySize:X8} ({extension})");
                if (createLog)
                    logOutput.WriteLine($"{i + 1:D4}_{entryOffset:X8}.{extension},{entryOffset},{entrySize},{headerInt}");

                // Extract file
                File.WriteAllBytes($"{outputDir}/{i + 1:D4}_{entryOffset:X8}.{extension}", entryData);

                // Move to next entry block
                brInput.BaseStream.Seek(magicSize + (i + 1) * 0x08, SeekOrigin.Begin);
            }
            // Clean up
            logOutput.Close();
            if (!createLog)
                File.Delete($"{input}.log");
            if (cleanUp)
                File.Delete(input);
            return outputDir;
        }

        /// <summary>
        /// Unpack a MHA file container.
        /// </summary>
        /// <param name="input">Input file name to read from.</param>
        /// <param name="brInput">Binary reader to the input file.</param>
        /// <param name="createLog">true is a log file should be created.</param>
        /// <returns>Output folder path.</returns>
        public static string UnpackMHA(string input, BinaryReader brInput, bool createLog)
        {
            string outputDir = $"{input}.unpacked";
            Directory.CreateDirectory(outputDir);

            StreamWriter logOutput = new($"{input}.log");
            if (createLog)
            {
                logOutput.WriteLine("MHA");
                logOutput.WriteLine(input.Remove(0, input.LastIndexOf('/') + 1));
            }

            // Read header
            int pointerEntryMetaBlock = brInput.ReadInt32();
            int count = brInput.ReadInt32();
            int pointerEntryNamesBlock = brInput.ReadInt32();
            brInput.ReadInt32(); // entryNamesBlockLength
            short unk1 = brInput.ReadInt16();
            short unk2 = brInput.ReadInt16();
            if (createLog)
            {
                logOutput.WriteLine(count);
                logOutput.WriteLine(unk1);
                logOutput.WriteLine(unk2);
            }

            // File Data
            for (int i = 0; i < count; i++)
            {
                // Get meta
                brInput.BaseStream.Seek(pointerEntryMetaBlock + i * 0x14, SeekOrigin.Begin);
                int stringOffset = brInput.ReadInt32();
                int entryOffset = brInput.ReadInt32();
                int entrySize = brInput.ReadInt32();
                int pSize = brInput.ReadInt32();        // Padded size
                int fileId = brInput.ReadInt32();

                // Get name
                brInput.BaseStream.Seek(pointerEntryNamesBlock + stringOffset, SeekOrigin.Begin);
                string entryName = FileOperations.ReadNullterminatedString(brInput, Encoding.UTF8);
                if (createLog)
                    logOutput.WriteLine(entryName + "," + fileId);

                // Extract file
                brInput.BaseStream.Seek(entryOffset, SeekOrigin.Begin);
                byte[] entryData = brInput.ReadBytes(entrySize);
                File.WriteAllBytes($"{outputDir}/{entryName}", entryData);

                Console.WriteLine(
                    $"{entryName}, String Offset: 0x{stringOffset:X8}, Offset: 0x{entryOffset:X8}, Size: 0x{entrySize:X8}, pSize: 0x{pSize:X8}, File ID: 0x{fileId:X8}"
                );
            }

            logOutput.Close();
            if (!createLog)
                File.Delete($"{input}.log");
            return outputDir;
        }

        /// <summary>
        /// Unpack, decompress, a JPK file.
        /// </summary>
        /// <param name="input">Input file path.</param>
        /// <returns>Output folder path.</returns>
        public static string UnpackJPK(string input)
        {
            byte[] buffer = File.ReadAllBytes(input);
            MemoryStream ms = new(buffer);
            BinaryReader br = new(ms);
            string output = null;
            // Check for JKR header
            if (br.ReadUInt32() == 0x1A524B4A)
            {
                ms.Seek(0x2, SeekOrigin.Current);
                int type = br.ReadUInt16();
                var compressionType = Enum.GetValues<CompressionType>()[type];
                Console.WriteLine($"JPK {compressionType} (type {type})");
                IJPKDecode decoder = compressionType switch
                {
                    CompressionType.RW => new JPKDecodeRW(),
                    CompressionType.HFIRW => new JPKDecodeHFIRW(),
                    CompressionType.LZ => new JPKDecodeLz(),
                    CompressionType.HFI => new JPKDecodeHFI(),
                    _ => throw new NotImplementedException($"JPK type {type} is not supported."),
                };

                // Decompress file
                int startOffset = br.ReadInt32();
                int outSize = br.ReadInt32();
                byte[] outBuffer = new byte[outSize];
                ms.Seek(startOffset, SeekOrigin.Begin);
                decoder.ProcessOnDecode(ms, outBuffer);

                // Get extension
                byte[] header = new byte[4];
                Array.Copy(outBuffer, header, 4);
                uint headerInt = BitConverter.ToUInt32(header, 0);
                string extension = Enum.GetName(typeof(FileExtension), headerInt);
                extension ??= ByteOperations.CheckForMagic(headerInt, outBuffer);
                extension ??= "bin";

                output = $"{input}.{extension}";
                File.Delete(input);
                File.WriteAllBytes(output, outBuffer);
            }
            br.Close();
            ms.Close();
            return output;
        }

        /// <summary>
        /// Unpack a stage file container.
        /// </summary>
        /// <param name="input">Input file name to read from.</param>
        /// <param name="brInput">Binary reader to the input file.</param>
        /// <param name="createLog">true is a log file should be created.</param>
        /// <param name="cleanUp">Remove the initial input file.</param>
        /// <returns>Output folder path.</returns>
        public static string UnpackStageContainer(string input, BinaryReader brInput, bool createLog, bool cleanUp)
        {
            Console.WriteLine("Trying to unpack as stage-specific container.");

            string outputDir = $"{input}.unpacked";
            Directory.CreateDirectory(outputDir);

            StreamWriter logOutput = new($"{input}.log");
            if (createLog)
            {
                logOutput.WriteLine("StageContainer");
                logOutput.WriteLine(input.Remove(0, input.LastIndexOf('/') + 1));
            }

            // First three segments
            for (int i = 0; i < 3; i++)
            {
                int offset = brInput.ReadInt32();
                int size = brInput.ReadInt32();

                if (size == 0)
                {
                    Console.WriteLine(
                        $"Offset: 0x{offset:X8}, Size: 0x{size:X8} (SKIPPED)"
                    );
                    if (createLog)
                        logOutput.WriteLine($"null,{offset},{size},0");
                    continue;
                }

                brInput.BaseStream.Seek(offset, SeekOrigin.Begin);
                byte[] data = brInput.ReadBytes(size);

                // Get extension
                byte[] header = new byte[4];
                Array.Copy(data, header, 4);
                uint headerInt = BitConverter.ToUInt32(header, 0);
                string extension = Enum.GetName(typeof(FileExtension), headerInt);
                extension ??= ByteOperations.CheckForMagic(headerInt, data);
                extension ??= "bin";

                // Print info
                Console.WriteLine(
                    $"Offset: 0x{offset:X8}, Size: 0x{size:X8} ({extension})"
                );
                if (createLog)
                    logOutput.WriteLine(
                        $"{i + 1:D4}_{offset:X8}.{extension},{offset},{size},{headerInt}"
                    );

                // Extract file
                File.WriteAllBytes($"{outputDir}/{i + 1:D4}_{offset:X8}.{extension}", data);

                // Move to next entry block
                brInput.BaseStream.Seek((i + 1) * 0x08, SeekOrigin.Begin);
            }

            // Rest
            int restCount = brInput.ReadInt32();
            int unkHeader = brInput.ReadInt32();
            if (createLog)
                logOutput.WriteLine(restCount + "," + unkHeader);
            for (int i = 3; i < restCount + 3; i++)
            {
                int offset = brInput.ReadInt32();
                int size = brInput.ReadInt32();
                int unk = brInput.ReadInt32();

                if (size == 0)
                {
                    Console.WriteLine(
                        $"Offset: 0x{offset:X8}, Size: 0x{size:X8}, Unk: 0x{unk:X8} (SKIPPED)"
                    );
                    if (createLog)
                        logOutput.WriteLine($"null,{offset},{size},{unk},0");
                    continue;
                }

                brInput.BaseStream.Seek(offset, SeekOrigin.Begin);
                byte[] data = brInput.ReadBytes(size);

                // Get extension
                byte[] header = new byte[4];
                Array.Copy(data, header, 4);
                uint headerInt = BitConverter.ToUInt32(header, 0);
                string extension = Enum.GetName(typeof(FileExtension), headerInt);
                extension ??= ByteOperations.CheckForMagic(headerInt, data);
                extension ??= "bin";

                // Print info
                Console.WriteLine($"Offset: 0x{offset:X8}, Size: 0x{size:X8}, Unk: 0x{unk:X8} ({extension})");
                if (createLog)
                    logOutput.WriteLine($"{i + 1:D4}_{offset:X8}.{extension},{offset},{size},{unk},{headerInt}");

                // Extract file
                File.WriteAllBytes($"{outputDir}/{i + 1:D4}_{offset:X8}.{extension}", data);

                // Move to next entry block
                brInput.BaseStream.Seek(0x18 + 0x08 + (i - 3 + 1) * 0x0C, SeekOrigin.Begin); // 0x18 = first three segments, 0x08 = header for this segment
            }

            // Clean up
            logOutput.Close();
            if (!createLog)
                File.Delete($"{input}.log");
            if (cleanUp)
                File.Delete(input);
            return outputDir;
        }

        /// <summary>
        /// Write output to txt file.
        /// </summary>
        /// <param name="input">Input ftxt file, usually has MHF header.</param>
        /// <param name="brInput">Binary reader to the file.</param>
        /// <returns>Output file path.</returns>
        public static string PrintFTXT(string input, BinaryReader brInput)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            string outputPath = $"{input}.txt";
            if (File.Exists(outputPath))
                File.Delete(outputPath);
            StreamWriter txtOutput = new(outputPath, true, Encoding.GetEncoding("shift-jis"));

            // Read header
            brInput.BaseStream.Seek(10, SeekOrigin.Current);
            int stringCount = brInput.ReadInt16();
            brInput.ReadInt32(); // textBlockSize

            for (int i = 0; i < stringCount; i++)
            {
                string str = FileOperations.ReadNullterminatedString(brInput, Encoding.GetEncoding("shift-jis"));
                txtOutput.WriteLine(str.Replace("\n", "<NEWLINE>"));
            }

            txtOutput.Close();
            return outputPath;
        }
    }
}
