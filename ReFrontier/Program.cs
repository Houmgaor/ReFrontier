﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

using LibReFrontier;

namespace ReFrontier
{
    class Program
    {
        static bool createLog = false;
        static bool recursive = true;
        static bool repack = false;
        static bool decryptOnly = false;
        static bool noDecryption = false;
        static bool encrypt = false;
        static bool autoClose = false;
        static bool cleanUp = false;
        static bool compress = false;
        static bool ignoreJPK = false;
        static bool stageContainer = false;
        static bool autoStage = false;

        /// <summary>
        /// Simple arguments parser.
        /// </summary>
        /// <param name="args">Input arguments from the CLI</param>
        /// <returns>Dictionary of arguments. Arguments with no value have a null value assigned.</returns>
        static Dictionary<string, string> ParseArguments(string[] args)
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
        /// Main interface to start the program.
        /// </summary>
        /// <param name="args">Input arguments from the CLI.</param>
        /// <exception cref="Exception">For wrong compression format.</exception>
        static void Main(string[] args)
        {
            var parsedArgs = ParseArguments(args); 
            Helpers.Print(
                "ReFrontier by MHVuze - " + 
                "A tool for editing Monster Hunter Frontier files", 
                false
            );
            var argKeys = parsedArgs.Keys;

            // Display help
            if (args.Length < 1 || argKeys.Contains("--help"))
            {
                Helpers.Print(
                    "Usage: ReFrontier <file> [options]\n" +
                    "\nUnpacking Options\n" +
                    "===================\n\n" + 
                    "--log: Write log file (required for crypting back)\n" +
                    "--cleanUp: Delete simple archives after unpacking\n" +
                    "--stageContainer: Unpack file as stage-specific container\n" +
                    "--autoStage: Automatically attempt to unpack containers that might be stage-specific\n" +
                    "--nonRecursive: Do not unpack recursively\n" +
                    "--decryptOnly: Decrypt ECD files without unpacking\n" +
                    "--noDecryption: Don't decrypt ECD files, no unpacking\n" +
                    "--ignoreJPK: Do not decompress JPK files\n" +
                    "\nPacking Options\n" +
                    "=================\n\n" + 
                    "--pack: Repack directory (requires log file)\n" +
                    "--compress=[type],[level]: Pack file with JPK [type] (int) at compression [level]\n" +
                    "--encrypt: Encrypt input file with ECD algorithm\n" +
                    "\nGeneral Options\n" +
                    "=================\n\n" + 
                    "--close: Close window after finishing process\n" +
                    "--help: Print this window and leave.\n\n" +
                    "You can use all arguments with a single dash \"-\" " +
                    "as in the original ReFrontier, but this is deprecated.",
                    false
                );
                Console.Read();
                return;
            }
            

            // Assign arguments
            if (argKeys.Contains("--log") || argKeys.Contains("-log"))
            { 
                createLog = true;
                repack = false;
            }
            if (argKeys.Contains("--nonRecursive") || argKeys.Contains("-nonRecursive"))
            {
                recursive = false;
                repack = false;
            }
            if (argKeys.Contains("--pack") || argKeys.Contains("-pack"))
                repack = true;
            if (argKeys.Contains("--decryptOnly") || argKeys.Contains("-decryptOnly"))
            {
                decryptOnly = true;
                repack = false;
            }
            if (argKeys.Contains("--noDecryption") || argKeys.Contains("-noDecryption"))
            {
                noDecryption = true;
                repack = false;
            }
            if (argKeys.Contains("--encrypt") || argKeys.Contains("-encrypt"))
            {
                encrypt = true;
                repack = false;
            }
            if (argKeys.Contains("--close") || argKeys.Contains("-close")) 
                autoClose = true;
            if (argKeys.Contains("--cleanUp") || argKeys.Contains("-cleanUp"))
                cleanUp = true;
            int[] compressArgs = null;
            if (argKeys.Contains("--compress") || argKeys.Contains("-compress"))
            {
                compress = true;
                repack = false;
                if (argKeys.Contains("--compress"))
                {
                    var matches = parsedArgs["--compress"].Split(",");
                    if (matches.Length != 2)
                    {
                        throw new ArgumentException(
                            "Check the input of compress! Received: " +
                            parsedArgs["--compress"] + ". " +
                            "Cannot split as compression [type],[level]. " +
                            "Example: --compress=3,50"
                        );
                    }
                    compressArgs = [
                        int.Parse(matches[0]),
                        int.Parse(matches[1]) * 100
                    ];
                }
                else
                {
                    string pattern = @"-compress (\d),(\d+)";
                    var matches = Regex.Matches(
                        string.Join(" ", args, 1, args.Length - 1),
                        pattern
                    );
                    if (matches.Count == 0)
                    {
                        throw new ArgumentException(
                            "Check compress input. Example: --compress=3,50"
                        );
                    }
                    var match = matches[0];
                    compressArgs = [
                        int.Parse(match.Groups[1].Value),
                        int.Parse(match.Groups[2].Value) * 100
                    ];
                }
                if (compressArgs == null) {
                    throw new Exception("Check compression level and type!");
                }
            }
            if (argKeys.Contains("--ignoreJPK") || argKeys.Contains("-ignoreJPK"))
            {
                ignoreJPK = true;
                repack = false;
            }
            if (argKeys.Contains("--stageContainer") || argKeys.Contains("-stageContainer"))
            {
                stageContainer = true;
                repack = false;
            }
            if (argKeys.Contains("--autoStage") || argKeys.Contains("-autoStage"))
            {
                autoStage = true;
                repack = false;
            }

            // Start input processing
            string input = args[0];
            if (File.Exists(input) || Directory.Exists(input))
            {
                StartProcessing(input, compressArgs);
                Console.WriteLine("Done.");
            }
            else {
                throw new FileNotFoundException("Input file does not exist.");
            }
            if (!autoClose)
                Console.Read();
        }

        /// <summary>
        /// Start the input processing.
        /// </summary>
        /// <param name="input">File or directory path.</param>
        /// <param name="compressArgs">Compression type and level, in this order</param>
        /// <exception cref="FileNotFoundException">Raises if META file for encryption is missing.</exception>
        static void StartProcessing(string input, int[] compressArgs = null)
        {

            FileAttributes inputAttr = File.GetAttributes(input);
            // Directories
            if (inputAttr.HasFlag(FileAttributes.Directory))
            {
                if (!repack && !encrypt)
                {
                    string[] inputFiles = Directory.GetFiles(
                        input, "*.*", SearchOption.AllDirectories
                    );
                    ProcessMultipleLevels(inputFiles);
                }
                else if (repack)
                    Pack.ProcessPackInput(input);
                else if (compress)
                    Console.WriteLine(
                        "A directory was specified while in compression mode. Stopping."
                    );
                else if (encrypt)
                    Console.WriteLine(
                        "A directory was specified while in encryption mode. Stopping."
                    );
            }
            // Single file
            else
            {
                if (!repack && !encrypt && !compress)
                {
                    string[] inputFiles = [input];
                    ProcessMultipleLevels(inputFiles);
                }
                else if (repack) 
                    Console.WriteLine(
                        "A single file was specified while in repacking mode. Stopping."
                    );
                else if (compress) 
                {
                    Pack.JPKEncode(
                            (ushort)compressArgs[0],
                            input,
                            $"output/{Path.GetFileName(input)}",
                            compressArgs[1]
                    );
                }
                else if (encrypt)
                {
                    byte[] buffer = File.ReadAllBytes(input);
                    if (!File.Exists($"{input}.meta")) {
                        throw new FileNotFoundException(
                            $"META file {input}.meta does not exist, " +
                            $"cannot encryt {input}." +
                            "Make sure to decryt the initial file with the -log option, " +
                            "and to place the generate meta file in the same folder as the file " +
                            "to encypt."
                        );
                    }
                    byte[] bufferMeta = File.ReadAllBytes($"{input}.meta");
                    buffer = Crypto.EncodeEcd(buffer, bufferMeta);
                    File.WriteAllBytes(input, buffer);
                    Helpers.Print($"File encrypted to {input}.", false);
                    Helpers.GetUpdateEntry(input);
                }
            }
        }

        /// <summary>
        /// Process a file
        /// </summary>
        /// <param name="input">File path</param>
        static void ProcessFile(string input)
        {
            Helpers.Print($"Processing {input}", false);

            // Read file to memory
            MemoryStream msInput = new(File.ReadAllBytes(input));
            BinaryReader brInput = new(msInput);
            if (msInput.Length == 0) {
                Console.WriteLine("File is empty. Skipping.");
                return;
            }
            int fileMagic = brInput.ReadInt32();

            // Since stage containers have no file magic, check for them first
            if (stageContainer == true)
            {
                brInput.BaseStream.Seek(0, SeekOrigin.Begin);
                try {
                    Unpack.UnpackStageContainer(input, brInput, createLog, cleanUp); 
                } catch (Exception error) {
                    Console.WriteLine(error);
                }
            }
            // MOMO Header: snp, snd
            else if (fileMagic == 0x4F4D4F4D)
            {
                Console.WriteLine("MOMO Header detected.");
                Unpack.UnpackSimpleArchive(
                    input, brInput, 8, createLog, cleanUp, autoStage
                );
            }
            // ECD Header
            else if (fileMagic == 0x1A646365)
            {
                Console.WriteLine("ECD Header detected.");
                if (noDecryption) 
                {
                    Helpers.Print("Not decrypting due to flag.", false);
                    return;
                }
                byte[] buffer = File.ReadAllBytes(input);
                Crypto.DecodeEcd(buffer);

                byte[] ecdHeader = new byte[0x10];
                Array.Copy(buffer, 0, ecdHeader, 0, 0x10);
                byte[] bufferStripped = new byte[buffer.Length - 0x10];
                Array.Copy(buffer, 0x10, bufferStripped, 0, buffer.Length - 0x10);

                File.WriteAllBytes(input, bufferStripped);
                string logInfo = "";
                if (createLog) {
                    File.WriteAllBytes($"{input}.meta", ecdHeader);
                    logInfo = ", log file written at [filepath].meta";
                }
                Console.WriteLine($"File decrypted to {input}{logInfo}.");
            }
            // EXF Header
            else if (fileMagic == 0x1A667865)
            {
                Console.WriteLine("EXF Header detected.");
                byte[] buffer = File.ReadAllBytes(input);
                Crypto.DecodeExf(buffer);
                byte[] bufferStripped = new byte[buffer.Length - 0x10];
                Array.Copy(buffer, 0x10, bufferStripped, 0, buffer.Length - 0x10);
                File.WriteAllBytes(input, bufferStripped);
                Console.WriteLine("File decrypted.");
            }
            // JKR Header
            else if (fileMagic == 0x1A524B4A)
            {
                Console.WriteLine("JKR Header detected.");
                if (!ignoreJPK) {
                    Unpack.UnpackJPK(input);
                    Console.WriteLine("File decompressed.");
                }
            }
            // MHA Header
            else if (fileMagic == 0x0161686D)
            {
                Console.WriteLine("MHA Header detected.");
                Unpack.UnpackMHA(input, brInput, createLog);
            }
            // MHF Text file
            else if (fileMagic == 0x000B0000)
            {
                Console.WriteLine("MHF Text file detected.");
                Unpack.PrintFTXT(input, brInput);
            }
            // Try to unpack as simple container: i.e. txb, bin, pac, gab
            else
            {
                brInput.BaseStream.Seek(0, SeekOrigin.Begin);
                try {
                    Unpack.UnpackSimpleArchive(
                        input, brInput, 4, createLog, cleanUp, autoStage
                    );
                } catch (Exception e) {
                    Console.WriteLine(e);
                }
            }

            Console.WriteLine("==============================");
            if (fileMagic == 0x1A646365 && !decryptOnly) {
                ProcessFile(input);
            }
        }

        /// <summary>
        /// Process file(s) on multiple levels
        /// </summary>
        /// <param name="inputFiles">Files to process</param>
        static void ProcessMultipleLevels(string[] inputFiles)
        {
            // CurrentLevel        
            foreach (string inputFile in inputFiles)
            {
                ProcessFile(inputFile);

                // Disable stage processing files unpacked from parent
                if (stageContainer == true)
                    stageContainer = false;

                FileInfo fileInfo = new(inputFile);
                string[] patterns = ["*.bin", "*.jkr", "*.ftxt", "*.snd"];
                string directory = Path.Join(
                    fileInfo.DirectoryName, 
                    Path.GetFileNameWithoutExtension(inputFile)
                );

                if (Directory.Exists(directory) && recursive)
                {
                    //Process All Successive Levels
                    ProcessMultipleLevels(
                        Helpers.MyDirectory.GetFiles(directory, patterns, SearchOption.TopDirectoryOnly)
                    );
                }
            }
        }
    }
}
