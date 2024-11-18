using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

using LibReFrontier;

namespace ReFrontier
{
    /// <summary>
    /// Main program for ReFrontier to pack and depack game files.
    /// </summary>
    internal class Program
    {
        private static bool _createLog = false;
        private static bool _recursive = true;
        private static bool _decryptOnly = false;
        private static bool _noDecryption = false;
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
        private static void Main(string[] args)
        {
            var parsedArgs = ArgumentsParser.ParseArguments(args);

            var assembly = Assembly.GetExecutingAssembly();
            var fileVersionAttribute = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>();
            ArgumentsParser.Print(
                $"ReFrontier v{fileVersionAttribute.Version} - " + 
                "A tool for editing Monster Hunter Frontier files, by MHVuze",
                false
            );
            var argKeys = parsedArgs.Keys;

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
                    "--close: Close window after finishing process\n" +
                    "--help: Print this window and leave.\n\n" +
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
            _recursive = !argKeys.Contains("--nonRecursive") && !argKeys.Contains("-nonRecursive");
            bool repack = argKeys.Contains("--pack") || argKeys.Contains("-pack");
            _decryptOnly = argKeys.Contains("--decryptOnly") || argKeys.Contains("-decryptOnly");
            
            _noDecryption = argKeys.Contains("--noDecryption") || argKeys.Contains("-noDecryption");
            bool encrypt = argKeys.Contains("--encrypt") || argKeys.Contains("-encrypt");
            _cleanUp = argKeys.Contains("--cleanUp") || argKeys.Contains("-cleanUp");
            
            _ignoreJPK = argKeys.Contains("--ignoreJPK") || argKeys.Contains("-ignoreJPK");
            _stageContainer = argKeys.Contains("--stageContainer") || argKeys.Contains("-stageContainer");
            _autoStage = argKeys.Contains("--autoStage") || argKeys.Contains("-autoStage");

            bool autoClose = argKeys.Contains("--close") || argKeys.Contains("-close");

            // For compression level we need a bit of text parsing
            int compressType = -1, compressLevel = -1;
            if (argKeys.Contains("--compress") || argKeys.Contains("-compress"))
            {
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
                    compressType = int.Parse(matches[0]);
                    compressLevel = int.Parse(matches[1]);
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
                    compressType = int.Parse(match.Groups[1].Value);
                    compressLevel = int.Parse(match.Groups[2].Value);
                }
                if (compressType == 0 || compressLevel == 0) {
                    throw new Exception("Check compression level and type!");
                }
            }

            // Start input processing
            StartProcessing(input, encrypt, repack, compressType, compressLevel);
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
        /// Start the input processing.
        /// </summary>
        /// <param name="input">File or directory path.</param>
        /// <param name="encrypt">True if input file should be encrypted.</param>
        /// <param name="repack">True if input directory should be packed as a file.</param>
        /// <param name="compressType">Compression type, -1 means no compression.</param>
        /// <param name="compressLevel">Compression level</param>
        /// <exception cref="InvalidOperationException">Thrown if the arguments are not coherent with the input.</exception>
        private static void StartProcessing(string input, bool encrypt, bool repack, int compressType = -1, int compressLevel = -1)
        {
            if (File.GetAttributes(input).HasFlag(FileAttributes.Directory))
            {
                // Directory
                if (compressType != -1)
                    throw new InvalidOperationException("Cannot compress a directory.");
                if (encrypt)
                    throw new InvalidOperationException("Cannot encrypt a directory.");
                if (repack)
                    Pack.ProcessPackInput(input);
                else
                {
                    // Decompress each file
                    string[] inputFiles = Directory.GetFiles(
                        input, "*.*", SearchOption.AllDirectories
                    );
                    ProcessMultipleLevels(inputFiles, _recursive, _noDecryption);
                }
            }
            else
            {
                // Single file
                if (repack)
                    throw new InvalidOperationException("A single file cannot be used while in repacking mode.");
                if (compressType != -1)
                {
                    Pack.JPKEncode(
                        compressType, input, $"output/{Path.GetFileName(input)}", compressLevel * 100
                    );
                } 
                else if (encrypt)
                {
                    EncryptEcdFile(input, $"{input}.meta");
                }
                else 
                {
                    // Try to depile the file as multiple files
                    string[] inputFiles = [input];
                    ProcessMultipleLevels(inputFiles, _recursive, _noDecryption);
                }
            }
        }


        /// <summary>
        /// Process a file
        /// </summary>
        /// <param name="input">Input file path.</param>
        /// <param name="noDecryption">Do not decrypt ECD files.</param>
        /// <param name="decryptOnly">Decrypt file without decompressing..</param>
        private static void ProcessFile(string input, bool noDecryption, bool decryptOnly)
        {
            ArgumentsParser.Print($"Processing {input}", false);

            // Read file to memory
            MemoryStream msInput = new(File.ReadAllBytes(input));
            BinaryReader brInput = new(msInput);
            if (msInput.Length == 0) {
                Console.WriteLine("File is empty. Skipping.");
                return;
            }
            int fileMagic = brInput.ReadInt32();

            // Since stage containers have no file magic, check for them first
            if (_stageContainer == true)
            {
                brInput.BaseStream.Seek(0, SeekOrigin.Begin);
                try {
                    Unpack.UnpackStageContainer(input, brInput, _createLog, _cleanUp); 
                } catch (Exception error) {
                    Console.WriteLine(error);
                }
            }
            // MOMO Header: snp, snd
            else if (fileMagic == 0x4F4D4F4D)
            {
                Console.WriteLine("MOMO Header detected.");
                Unpack.UnpackSimpleArchive(
                    input, brInput, 8, _createLog, _cleanUp, _autoStage
                );
            }
            // ECD Header
            else if (fileMagic == 0x1A646365)
            {
                Console.WriteLine("ECD Header detected.");
                if (noDecryption) 
                {
                    ArgumentsParser.Print("Not decrypting due to flag.", false);
                    return;
                }
                DecryptEcdFile(input, _createLog);
                string logInfo = "";
                if (_createLog) {
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
                if (!_ignoreJPK) {
                    Unpack.UnpackJPK(input);
                    Console.WriteLine("File decompressed.");
                }
            }
            // MHA Header
            else if (fileMagic == 0x0161686D)
            {
                Console.WriteLine("MHA Header detected.");
                Unpack.UnpackMHA(input, brInput, _createLog);
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
                        input, brInput, 4, _createLog, _cleanUp, _autoStage
                    );
                } catch (Exception error) {
                    Console.WriteLine(error);
                }
            }

            Console.WriteLine("==============================");
            // Decompress file if it was an ECD (encypted)
            if (fileMagic == 0x1A646365 && !decryptOnly) {
                ProcessFile(input, noDecryption, decryptOnly);
            }
        }

        /// <summary>
        /// Process file(s) on multiple container level.
        /// 
        /// Try to use each file is considered a container of multiple files.
        /// </summary>
        /// <param name="inputFiles">Files to process.</param>
        /// <param name="recursive">True to process newly created files recursively.</param>
        /// <param name="noDecryption">Do not decrypt ECD files.</param>
        private static void ProcessMultipleLevels(string[] inputFiles, bool recursive, bool noDecryption)
        {
            string[] patterns = ["*.bin", "*.jkr", "*.ftxt", "*.snd"];
            // CurrentLevel        
            foreach (string inputFile in inputFiles)
            {
                ProcessFile(inputFile, noDecryption, _decryptOnly);

                // Disable stage processing files unpacked from parent
                if (_stageContainer == true)
                    _stageContainer = false;

                FileInfo fileInfo = new(inputFile);
                string directory = Path.Join(
                    fileInfo.DirectoryName, 
                    Path.GetFileNameWithoutExtension(inputFile)
                );

                if (!Directory.Exists(directory) || !recursive)
                {
                    continue;
                }

                //Process All Successive Levels
                ProcessMultipleLevels(
                    FileOperations.GetFiles(directory, patterns, SearchOption.TopDirectoryOnly),
                    recursive,
                    noDecryption
                );
            }
        }
    }
}
