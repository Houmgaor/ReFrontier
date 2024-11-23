using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using LibReFrontier;

namespace ReFrontier
{
    /// <summary>
    /// Main program for ReFrontier to pack and depack game files.
    /// </summary>
    internal class Program
    {
        /// <summary>
        /// Number of parallel processes on reading folders.
        /// </summary>
        const int MAX_PARALLEL_PROCESSES = 4;

        private static bool _createLog = false;
        private static bool _decryptOnly = false;
        private static bool _cleanUp = false;
        private static bool _ignoreJPK = false;
        private static bool _stageContainer = false;
        private static bool _autoStage = false;


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
                ", by MHVuze",
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
            _createLog = argKeys.Contains("--log") || argKeys.Contains("-log");
            bool recursive = !argKeys.Contains("--nonRecursive") && !argKeys.Contains("-nonRecursive");
            bool repack = argKeys.Contains("--pack") || argKeys.Contains("-pack");
            _decryptOnly = argKeys.Contains("--decryptOnly") || argKeys.Contains("-decryptOnly");
            
            bool noDecryption = argKeys.Contains("--noDecryption") || argKeys.Contains("-noDecryption");
            bool encrypt = argKeys.Contains("--encrypt") || argKeys.Contains("-encrypt");
            _cleanUp = argKeys.Contains("--cleanUp") || argKeys.Contains("-cleanUp");
            
            _ignoreJPK = argKeys.Contains("--ignoreJPK") || argKeys.Contains("-ignoreJPK");
            _stageContainer = argKeys.Contains("--stageContainer") || argKeys.Contains("-stageContainer");
            _autoStage = argKeys.Contains("--autoStage") || argKeys.Contains("-autoStage");

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

            // Start input processing
            if (File.GetAttributes(input).HasFlag(FileAttributes.Directory))
            {
                // Input is directory
                if (compression.level != 0)
                    throw new InvalidOperationException("Cannot compress a directory.");
                if (encrypt)
                    throw new InvalidOperationException("Cannot encrypt a directory.");
                StartProcessingDirectory(input, repack, recursive, noDecryption);
            }
            else
            {
                // Input is a file
                if (repack)
                    throw new InvalidOperationException("A single file cannot be used while in repacking mode.");
                StartProcessingFile(input, encrypt, compression, recursive, noDecryption);
            }
            Console.WriteLine("Done.");
            if (!autoClose)
                Console.Read();
        }


        /// <summary>
        /// Encrypt a single file using 
        /// </summary>
        /// <param name="input">Input file to encrypt.</param>
        /// <param name="metaFile">Data to use for encryption.</param>
        /// <exception cref="FileNotFoundException">Thrown if the meta file does not exist.</exception>
        private static void EncryptEcdFile(string input, string metaFile)
        {
            byte[] buffer = File.ReadAllBytes(input);
            if (!File.Exists(metaFile)) {
                throw new FileNotFoundException(
                    $"META file {input}.meta does not exist, " +
                    $"cannot encryt {input}." +
                    "Make sure to decryt the initial file with the -log option, " +
                    "and to place the generate meta file in the same folder as the file " +
                    "to encypt."
                );
            }
            byte[] bufferMeta = File.ReadAllBytes(metaFile);
            buffer = Crypto.EncodeEcd(buffer, bufferMeta);
            File.WriteAllBytes(input, buffer);
            ArgumentsParser.Print($"File encrypted to {input}.", false);
            FileOperations.GetUpdateEntry(input);
        }

        /// <summary>
        /// Decrypt an ECD encoded file.
        /// </summary>
        /// <param name="inputFile">Input file path.</param>
        /// <param name="createLog">True if we should create a log file with the header.</param>
        /// <returns>Path to the meta file if created.</returns>
        private static string DecryptEcdFile(string inputFile, bool createLog)
        {
            byte[] buffer = File.ReadAllBytes(inputFile);
            Crypto.DecodeEcd(buffer);

            byte[] ecdHeader = new byte[0x10];
            Array.Copy(buffer, 0, ecdHeader, 0, 0x10);
            byte[] bufferStripped = new byte[buffer.Length - 0x10];
            Array.Copy(buffer, 0x10, bufferStripped, 0, buffer.Length - 0x10);

            File.WriteAllBytes(inputFile, bufferStripped);
            if (createLog) {
                string metaFile = $"{inputFile}.meta";
                File.WriteAllBytes(metaFile, ecdHeader);
                return metaFile;
            }
            return null;
        }



        /// <summary>
        /// Lauches a directory processing.
        /// 
        /// It can either pack the directory as a file, or uncompress the content.
        /// </summary>
        /// <param name="inputDir">Directory path.</param>
        /// <param name="repack">True if input directory should be packed as a file.</param>
        /// <param name="recursive">Recursive decompression flag.</param>
        /// <param name="noDecryption">Do not decrypt file flag.</param>
        private static void StartProcessingDirectory(string inputDir, bool repack, bool recursive, bool noDecryption)
        {
            if (repack)
                Pack.ProcessPackInput(inputDir);
            else
            {
                // Decompress each file
                string[] inputFiles = Directory.GetFiles(
                    inputDir, "*.*", SearchOption.AllDirectories
                );
                ProcessMultipleLevels(inputFiles, recursive, noDecryption, _decryptOnly);
            }
        }

        /// <summary>
        /// Start the processing for a file.
        /// 
        /// It will either compress the file, encrypt it or depack it as many files.
        /// </summary>
        /// <param name="filePath">File path.</param>
        /// <param name="encrypt">True if input file should be encrypted.</param>
        /// <param name="compression">Compression to use.</param>
        /// <param name="recursive">Recursive decompression flag.</param>
        /// <param name="noDecryption">Do not decrypt file flag.</param>
        private static void StartProcessingFile(string filePath, bool encrypt, Compression compression, bool recursive, bool noDecryption)
        {
            if (compression.level != 0)
            {
                Pack.JPKEncode(
                    compression, filePath, $"output/{Path.GetFileName(filePath)}"
                );
            }
            
            if (encrypt)
                EncryptEcdFile(filePath, $"{filePath}.meta");

            // Try to depack the file as multiple files
            if (compression.level == 0 && !encrypt) 
                ProcessMultipleLevels([filePath], recursive, noDecryption, _decryptOnly);
        }

        /// <summary>
        /// Unpack or (decrypt and decompress) a single file.
        /// 
        /// If the file was an ECD, decompress it as well.
        /// </summary>
        /// <param name="input">Input file path.</param>
        /// <param name="noDecryption">Do not decrypt ECD files.</param>
        /// <param name="decryptOnly">Decrypt file without decompressing.</param>
        /// <param name="createLog">Add to log file flag.</param>
        /// <returns>Output path, can be file or folder.</returns>
        private static string ProcessFile(string input, bool noDecryption, bool decryptOnly, bool createLog)
        {
            ArgumentsParser.Print($"Processing {input}", false);

            // Read file to memory
            MemoryStream msInput = new(File.ReadAllBytes(input));
            BinaryReader brInput = new(msInput);
            string outputPath;
            if (msInput.Length == 0) {
                Console.WriteLine("File is empty. Skipping.");
                return null;
            }
            int fileMagic = brInput.ReadInt32();

            // Since stage containers have no file magic, check for them first
            if (_stageContainer)
            {
                brInput.BaseStream.Seek(0, SeekOrigin.Begin);
                outputPath = Unpack.UnpackStageContainer(input, brInput, createLog, _cleanUp);
            }
            else if (fileMagic == 0x4F4D4F4D)
            {
                // MOMO Header: snp, snd
                Console.WriteLine("MOMO Header detected.");
                outputPath = Unpack.UnpackSimpleArchive(
                    input, brInput, 8, createLog, _cleanUp, _autoStage
                );
            }
            else if (fileMagic == 0x1A646365)
            {
                // ECD Header
                Console.WriteLine("ECD Header detected.");
                if (noDecryption) 
                {
                    ArgumentsParser.Print("Not decrypting due to flag.", false);
                    return null;
                }
                DecryptEcdFile(input, createLog);
                outputPath = input;
                string logInfo = "";
                if (createLog) {
                    logInfo = ", log file written at [filepath].meta";
                }

                Console.WriteLine($"File decrypted to {input}{logInfo}.");
            }
            else if (fileMagic == 0x1A667865)
            {
                // EXF Header
                Console.WriteLine("EXF Header detected.");
                byte[] buffer = File.ReadAllBytes(input);
                Crypto.DecodeExf(buffer);
                byte[] bufferStripped = new byte[buffer.Length - 0x10];
                Array.Copy(buffer, 0x10, bufferStripped, 0, buffer.Length - 0x10);
                File.WriteAllBytes(input, bufferStripped);
                Console.WriteLine("File decrypted.");
                outputPath = input;
            }
            else if (fileMagic == 0x1A524B4A)
            {
                // JKR Header
                Console.WriteLine("JKR Header detected.");
                outputPath = input;
                if (!_ignoreJPK) {
                    outputPath = Unpack.UnpackJPK(input);
                    Console.WriteLine("File decompressed.");
                }
            }
            else if (fileMagic == 0x0161686D)
            {
                // MHA Header
                Console.WriteLine("MHA Header detected.");
                outputPath = Unpack.UnpackMHA(input, brInput, createLog);
            }
            else if (fileMagic == 0x000B0000)
            {
                // MHF Text file
                Console.WriteLine("MHF Text file detected.");
                outputPath = Unpack.PrintFTXT(input, brInput);
            }
            else
            {
                // Try to unpack as simple container: i.e. txb, bin, pac, gab
                brInput.BaseStream.Seek(0, SeekOrigin.Begin);
                outputPath = Unpack.UnpackSimpleArchive(
                    input, brInput, 4, createLog, _cleanUp, _autoStage
                );
            }

            Console.WriteLine("==============================");
            // Decompress file if it was an ECD (encypted)
            if (fileMagic == 0x1A646365 && !decryptOnly) {
                ProcessFile(input, noDecryption, decryptOnly, createLog);
                outputPath = input;
            }
            return outputPath;
        }

        /// <summary>
        /// Process file(s) on multiple container level.
        /// 
        /// Try to use each file is considered a container of multiple files.
        /// </summary>
        /// <param name="inputFiles">Files to process.</param>
        /// <param name="recursive">True to process newly created files recursively.</param>
        /// <param name="noDecryption">Do not decrypt ECD files.</param>
        /// <param name="decryptOnly">Decrypt file without depacking.</param>
        private static void ProcessMultipleLevels(string[] inputFiles, bool recursive, bool noDecryption, bool decryptOnly)
        {
            // Limit file search to these patterns
            string[] patterns = ["*.bin", "*.jkr", "*.ftxt", "*.snd"];
            
            var parallelOptions = new ParallelOptions() {
                MaxDegreeOfParallelism = MAX_PARALLEL_PROCESSES
            };

            Parallel.ForEach(inputFiles, parallelOptions, inputFile =>
            {
                string outputPath = ProcessFile(inputFile, noDecryption, decryptOnly, _createLog);

                // Disable stage processing files unpacked from parent
                if (_stageContainer)
                    _stageContainer = false;

                if (!recursive)
                    return;

                // Check if a new directory was created
                if (!Directory.Exists(outputPath))
                    return;

                // Recursively go to next levels if a directory was created from file
                string[] nextFiles = FileOperations.GetFiles(outputPath, patterns, SearchOption.TopDirectoryOnly);
                ProcessMultipleLevels(nextFiles, recursive, noDecryption, decryptOnly);
            });
        }
    }
}
