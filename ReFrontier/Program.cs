using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using LibReFrontier;

namespace ReFrontier
{
    /// <summary>
    /// Main accepted arguments from the CLI of ReFrontier.
    /// </summary>
    public struct InputArguments
    {
        public bool createLog;
        public bool recursive;
        public bool repack;
        public bool decryptOnly;
        public bool noDecryption;
        public bool encrypt;
        public bool cleanUp;
        public bool ignoreJPK;
        public bool stageContainer;
        public bool autoStage;
        /// <summary>
        /// Rewrite files after decrypting, for compatibility 
        /// </summary>
        public bool rewriteOldFile;
        public Compression compression;
    }

    /// <summary>
    /// Main program for ReFrontier to pack and depack game files.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Number of parallel processes on reading folders.
        /// </summary>
        const int MAX_PARALLEL_PROCESSES = 4;

        /// <summary>
        /// Main interface to start the program.
        /// </summary>
        /// <param name="args">Input arguments from the CLI.</param>
        /// <exception cref="FileNotFoundException">The input does not exist.</exception>
        /// <exception cref="ArgumentException">Compression argument are ill-formed.</exception>
        /// <exception cref="Exception">For wrong compression format.</exception>
        /// <exception cref="InvalidOperationException">Forbidden operation for this input.</exception>
        private static void Main(string[] args)
        {
            var parsedArgs = ArgumentsParser.ParseArguments(args);

            var assembly = Assembly.GetExecutingAssembly();
            var fileVersionAttribute = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>().Version;
            var argKeys = parsedArgs.Keys;

            if (argKeys.Contains("--version"))
            {
                Console.WriteLine("v" + fileVersionAttribute);
                return;
            }

            ArgumentsParser.Print(
                assembly.GetCustomAttribute<AssemblyProductAttribute>().Product +
                $" v{fileVersionAttribute} - " +
                assembly.GetCustomAttribute<AssemblyDescriptionAttribute>().Description +
                ", by MHVuze, additions by Houmgaor",
                false
            );

            // Display help
            if (args.Length < 1 || argKeys.Contains("--help"))
            {
                ArgumentsParser.Print(
                    "Usage: ReFrontier <file> [options]\n" +
                    "\nUnpacking Options\n" +
                    "===================\n\n" +
                    "--log: Write log file (required for crypting back)\n" +
                    "--stageContainer: Unpack file as stage-specific container\n" +
                    "--autoStage: Automatically attempt to unpack containers that might be stage-specific\n" +
                    "--nonRecursive: Do not unpack recursively\n" +
                    "--decryptOnly: Decrypt ECD files without unpacking\n" +
                    "--noDecryption: Don't decrypt ECD files, no unpacking\n" +
                    "--ignoreJPK: Do not decompress JPK files\n" +
                    "--noFileRewrite: avoid rewriting original files\n" +
                    "--cleanUp: Delete simple archives after unpacking\n" +
                    "\nPacking Options\n" +
                    "=================\n\n" +
                    "--pack: Repack directory (requires log file)\n" +
                    "--compress=[type],[level]: Pack file with JPK [type] (int) at compression [level]\n" +
                    "--encrypt: Encrypt input file with ECD algorithm\n" +
                    "\nGeneral Options\n" +
                    "=================\n\n" +
                    "--version: Show the current program version\n" +
                    "--close: Close window after finishing process\n" +
                    "--help: Print this window and leave\n\n" +
                    "You can use all arguments with a single dash \"-\" " +
                    "as in the original ReFrontier, but this is deprecated.",
                    false
                );
                Console.Read();
                return;
            }
            string input = args[0];
            if (!File.Exists(input) && !Directory.Exists(input))
                throw new FileNotFoundException($"{input} do not exist.");

            // Assign arguments
            InputArguments inputArguments = new()
            {
                createLog = argKeys.Contains("--log") || argKeys.Contains("-log"),
                recursive = !argKeys.Contains("--nonRecursive") && !argKeys.Contains("-nonRecursive"),
                repack = argKeys.Contains("--pack") || argKeys.Contains("-pack"),
                decryptOnly = argKeys.Contains("--decryptOnly") || argKeys.Contains("-decryptOnly"),
                noDecryption = argKeys.Contains("--noDecryption") || argKeys.Contains("-noDecryption"),
                encrypt = argKeys.Contains("--encrypt") || argKeys.Contains("-encrypt"),
                cleanUp = argKeys.Contains("--cleanUp") || argKeys.Contains("-cleanUp"),
                ignoreJPK = argKeys.Contains("--ignoreJPK") || argKeys.Contains("-ignoreJPK"),
                stageContainer = argKeys.Contains("--stageContainer") || argKeys.Contains("-stageContainer"),
                autoStage = argKeys.Contains("--autoStage") || argKeys.Contains("-autoStage"),
                rewriteOldFile = !argKeys.Contains("--noFileRewrite"),
            };

            bool autoClose = argKeys.Contains("--close") || argKeys.Contains("-close");

            // For compression level we need a bit of text parsing
            Compression compression = new();
            if (argKeys.Contains("--compress") || argKeys.Contains("-compress"))
            {
                if (argKeys.Contains("--compress"))
                {
                    compression = ArgumentsParser.ParseCompression(parsedArgs["--compress"]);
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
                    compression = ArgumentsParser.ParseCompression(
                        match.Groups[1].Value + "," + match.Groups[2].Value
                    );
                }
            }
            if (compression.level != 0)
                inputArguments.compression = compression;

            // Start input processing
            if (File.GetAttributes(input).HasFlag(FileAttributes.Directory))
            {
                // Input is directory
                if (compression.level != 0)
                    throw new InvalidOperationException("Cannot compress a directory.");
                if (inputArguments.encrypt)
                    throw new InvalidOperationException("Cannot encrypt a directory.");
                StartProcessingDirectory(input, inputArguments);
            }
            else
            {
                // Input is a file
                if (inputArguments.repack)
                    throw new InvalidOperationException("A single file cannot be used while in repacking mode.");
                StartProcessingFile(input, inputArguments);
            }
            Console.WriteLine("Done.");
            if (!autoClose)
                Console.Read();
        }


        /// <summary>
        /// Encrypt a single file to a new file.
        /// 
        /// If <paramref name="inputFile"/> is "mhfdat.bin.decd",
        /// <paramref name="metaFile"/> should be "mhfdat.bin.meta" and 
        /// the output file will be "mhfdat.bin".
        /// </summary>
        /// <param name="inputFile">Input file to encrypt.</param>
        /// <param name="metaFile">Data to use for encryption.</param>
        /// <param name="cleanUp">Remove both <paramref name="inputFile"/> and <paramref name="metaFile"/>.</param>
        /// <returns>Encrypted file path.</returns>
        /// <exception cref="FileNotFoundException">Thrown if the meta file does not exist.</exception>
        private static string EncryptEcdFile(string inputFile, string metaFile, bool cleanUp)
        {
            byte[] buffer = File.ReadAllBytes(inputFile);
            // From mhfdat.bin.decd to mhdat.bin
            string encryptedFilePath = Path.Join(
                Path.GetDirectoryName(inputFile),
                Path.GetFileNameWithoutExtension(inputFile)
            );
            if (!File.Exists(metaFile))
            {
                throw new FileNotFoundException(
                    $"META file {metaFile} does not exist, " +
                    $"cannot encryt {inputFile}." +
                    "Make sure to decryt the initial file with the -log option, " +
                    "and to place the generate meta file in the same folder as the file " +
                    "to encypt."
                );
            }
            byte[] bufferMeta = File.ReadAllBytes(metaFile);
            buffer = Crypto.EncodeEcd(buffer, bufferMeta);
            File.WriteAllBytes(encryptedFilePath, buffer);
            ArgumentsParser.Print($"File encrypted to {encryptedFilePath}.", false);
            FileOperations.GetUpdateEntry(inputFile);
            if (cleanUp)
            {
                File.Delete(inputFile);
                File.Delete(metaFile);
            }
            return encryptedFilePath;
        }

        /// <summary>
        /// Decrypt an ECD encoded file to a new file.
        /// </summary>
        /// <param name="inputFile">Input file path.</param>
        /// <param name="createLog">True if we should create a log file with the header.</param>
        /// <param name="cleanUp">true if the original file should be deleted.</param>
        /// <param name="rewriteOldFile">Should we overwrite <paramref name="inputFile"/>.</param>
        /// <returns>Path to the decrypted file, in the form <paramref name="inputFile" />.decd</returns>
        private static string DecryptEcdFile(string inputFile, bool createLog, bool cleanUp, bool rewriteOldFile)
        {
            byte[] buffer = File.ReadAllBytes(inputFile);
            Crypto.DecodeEcd(buffer);
            const int headerLength = 0x10;

            byte[] ecdHeader = new byte[headerLength];
            Array.Copy(buffer, 0, ecdHeader, 0, headerLength);
            byte[] bufferStripped = new byte[buffer.Length - headerLength];
            Array.Copy(buffer, headerLength, bufferStripped, 0, buffer.Length - headerLength);

            string outputFile = inputFile + ".decd";
            File.WriteAllBytes(outputFile, bufferStripped);
            Console.Write($"File decrypted to {outputFile}");
            if (createLog)
            {
                string metaFile = $"{inputFile}.meta";
                File.WriteAllBytes(metaFile, ecdHeader);
                Console.Write($", log file at {metaFile}");
            }
            Console.Write(".\n");
            if (rewriteOldFile)
            {
                File.WriteAllBytes(inputFile, bufferStripped);
                Console.WriteLine(
                    $"Rewriting original file {inputFile}. " +
                    "This behavior is deprecated and will be removed in 2.0.0. " +
                    "Use --noFileRewrite to remove this warning."
                );
            }
            else if (cleanUp)
                File.Delete(inputFile);

            return outputFile;
        }

        /// <summary>
        /// Decrypt an Exf file.
        /// </summary>
        /// <param name="inputFile">Input file path.</param>
        /// <param name="cleanUp">Should the original file be removed.</param>
        /// <returns>Output file at {inputFile}.dexf</returns>
        private static string DecryptExfFile(string inputFile, bool cleanUp)
        {
            byte[] buffer = File.ReadAllBytes(inputFile);
            Crypto.DecodeExf(buffer);
            const int headerLength = 0x10;
            byte[] bufferStripped = new byte[buffer.Length - headerLength];
            Array.Copy(buffer, headerLength, bufferStripped, 0, buffer.Length - headerLength);
            string outputFile = inputFile + ".dexf";
            File.WriteAllBytes(outputFile, bufferStripped);
            if (cleanUp)
                File.Delete(inputFile);
            Console.WriteLine($"File decrypted to {outputFile}.");
            return outputFile;
        }

        /// <summary>
        /// Lauches a directory processing.
        /// 
        /// It can either pack the directory as a file, or uncompress the content.
        /// </summary>
        /// <param name="directoryPath">Directory path.</param>
        /// <param name="inputArguments">Configuration arguments from CLI.</param>
        public static void StartProcessingDirectory(string directoryPath, InputArguments inputArguments)
        {
            if (inputArguments.repack)
                Pack.ProcessPackInput(directoryPath);
            else
            {
                // Process each element in directory
                string[] inputFiles = Directory.GetFiles(
                    directoryPath, "*.*", SearchOption.AllDirectories
                );
                ProcessMultipleLevels(inputFiles, inputArguments);
            }
        }

        /// <summary>
        /// Start the processing for a file.
        /// 
        /// It will either compress the file, encrypt it or depack it as many files.
        /// </summary>
        /// <param name="filePath">File path.</param>
        /// <param name="inputArguments">Configuration arguments from CLI.</param>
        public static void StartProcessingFile(string filePath, InputArguments inputArguments)
        {
            if (inputArguments.compression.level != 0)
            {
                // From mhfdat.bin.decd.bin to output/mhfdat.bin.decd
                Pack.JPKEncode(
                    inputArguments.compression, filePath, $"output/{Path.GetFileNameWithoutExtension(filePath)}"
                );
            }

            if (inputArguments.encrypt)
            {
                string decompressedFilePath = Path.Join(
                    Path.GetDirectoryName(filePath),
                    Path.GetFileNameWithoutExtension(filePath)
                );
                string metaFilePath = Path.Join(
                    Path.GetDirectoryName(filePath),
                    Path.GetFileNameWithoutExtension(decompressedFilePath) + ".meta"
                );
                EncryptEcdFile(decompressedFilePath, metaFilePath, inputArguments.cleanUp);
            }

            // Try to depack the file as multiple files
            if (inputArguments.compression.level == 0 && !inputArguments.encrypt)
                ProcessMultipleLevels([filePath], inputArguments);
        }

        /// <summary>
        /// Unpack or (decrypt and decompress) a single file.
        /// 
        /// If the file was an ECD, decompress it as well.
        /// </summary>
        /// <param name="filePath">Input file path.</param>
        /// <param name="inputArguments">Configuration arguments from CLI.</param>
        /// <returns>Output path, can be file or folder.</returns>
        private static string ProcessFile(string filePath, InputArguments inputArguments)
        {
            ArgumentsParser.Print($"Processing {filePath}", false);

            // Read file to memory
            MemoryStream msInput = new(File.ReadAllBytes(filePath));
            BinaryReader brInput = new(msInput);
            string outputPath;
            if (msInput.Length == 0)
            {
                Console.WriteLine("File is empty. Skipping.");
                return null;
            }
            int fileMagic = brInput.ReadInt32();

            // Since stage containers have no file magic, check for them first
            if (inputArguments.stageContainer)
            {
                brInput.BaseStream.Seek(0, SeekOrigin.Begin);
                outputPath = Unpack.UnpackStageContainer(filePath, brInput, inputArguments.createLog, inputArguments.cleanUp);
            }
            else if (fileMagic == 0x4F4D4F4D)
            {
                // MOMO Header: snp, snd
                Console.WriteLine("MOMO Header detected.");
                outputPath = Unpack.UnpackSimpleArchive(
                    filePath, brInput, 8, inputArguments.createLog, inputArguments.cleanUp, inputArguments.autoStage
                );
            }
            else if (fileMagic == 0x1A646365)
            {
                // ECD Header
                Console.WriteLine("ECD Header detected.");
                if (inputArguments.noDecryption)
                {
                    ArgumentsParser.Print("Not decrypting due to flag.", false);
                    return null;
                }
                outputPath = DecryptEcdFile(
                    filePath,
                    inputArguments.createLog,
                    inputArguments.cleanUp,
                    inputArguments.rewriteOldFile
                );
            }
            else if (fileMagic == 0x1A667865)
            {
                // EXF Header
                Console.WriteLine("EXF Header detected.");
                outputPath = DecryptExfFile(filePath, inputArguments.cleanUp);
            }
            else if (fileMagic == 0x1A524B4A)
            {
                // JKR Header
                Console.WriteLine("JKR Header detected.");
                outputPath = filePath;
                if (!inputArguments.ignoreJPK)
                {
                    outputPath = Unpack.UnpackJPK(filePath);
                    Console.WriteLine($"File decompressed to {outputPath}.");

                    // Replace input file, deprecated behavior, will be removed in 2.0.0 
                    if (
                        inputArguments.rewriteOldFile && outputPath != filePath &&
                        File.GetAttributes(outputPath).HasFlag(FileAttributes.Normal)
                    )
                        File.Copy(outputPath, filePath);
                }
            }
            else if (fileMagic == 0x0161686D)
            {
                // MHA Header
                Console.WriteLine("MHA Header detected.");
                outputPath = Unpack.UnpackMHA(filePath, brInput, inputArguments.createLog);
            }
            else if (fileMagic == 0x000B0000)
            {
                // MHF Text file
                Console.WriteLine("MHF Text file detected.");
                outputPath = Unpack.PrintFTXT(filePath, brInput);
            }
            else
            {
                // Try to unpack as simple container: i.e. txb, bin, pac, gab
                brInput.BaseStream.Seek(0, SeekOrigin.Begin);
                outputPath = Unpack.UnpackSimpleArchive(
                    filePath, brInput, 4, inputArguments.createLog, inputArguments.cleanUp, inputArguments.autoStage
                );
            }

            Console.WriteLine("==============================");
            // Decompress file if it was an ECD (encypted)
            if (fileMagic == 0x1A646365 && !inputArguments.decryptOnly)
            {
                string decdFilePath = outputPath;
                outputPath = ProcessFile(decdFilePath, inputArguments);
                if (inputArguments.cleanUp)
                    File.Delete(decdFilePath);
            }
            return outputPath;
        }

        /// <summary>
        /// Process files on multiple container level.
        /// 
        /// Try to use each file is considered a container of multiple files.
        /// </summary>
        /// <param name="filePathes">Files to process.</param>
        /// <param name="inputArguments">Configuration arguments from CLI.</param>
        private static void ProcessMultipleLevels(string[] filePathes, InputArguments inputArguments)
        {
            var parallelOptions = new ParallelOptions()
            {
                MaxDegreeOfParallelism = MAX_PARALLEL_PROCESSES
            };

            // Use a concurrent queue to manage files/directories to process
            ConcurrentQueue<string> filesToProcess = new(filePathes);

            // Consume (process) input files
            while (!filesToProcess.IsEmpty)
            {
                // Ugly way to have a functional forEach on expanding Queue
                // The TPL library may be better suited
                List<string> fileWorkers = [];
                while (filesToProcess.TryDequeue(out string tempInputFile))
                {
                    fileWorkers.Add(tempInputFile);
                }

                bool stageContainer = inputArguments.stageContainer;
                Parallel.ForEach(fileWorkers, parallelOptions, inputFile =>
                {
                    string outputPath = ProcessFile(inputFile, inputArguments);

                    // Disable stage processing files unpacked from parent
                    if (stageContainer)
                        stageContainer = false;

                    // Check if a new directory was created
                    if (inputArguments.recursive && Directory.Exists(outputPath))
                    {
                        AddNewFiles(outputPath, filesToProcess);
                    }
                });
            }
        }

        /// <summary>
        /// Add new files in the directory to <paramref name="filesQueue" />.
        /// 
        /// It is a task producer in Task Parallel Library paradigm.
        /// </summary>
        /// <param name="directoryPath">Directory to search into.</param>
        /// <param name="filesQueue">Thread-safe queue where to add files to.</param>
        private static void AddNewFiles(string directoryPath, ConcurrentQueue<string> filesQueue)
        {
            // Limit file search to these patterns
            string[] patterns = ["*.bin", "*.jkr", "*.ftxt", "*.snd"];

            var nextFiles = FileOperations.GetFiles(directoryPath, patterns, SearchOption.TopDirectoryOnly);

            foreach (var nextFile in nextFiles)
            {
                filesQueue.Enqueue(nextFile);
            }
        }
    }
}
