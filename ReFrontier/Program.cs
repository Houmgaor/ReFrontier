using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using LibReFrontier;
using LibReFrontier.Abstractions;
using ReFrontier.Jpk;
using ReFrontier.Services;

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

        private static readonly Lazy<Program> DefaultInstance = new(() => new Program());

        private readonly IFileSystem _fileSystem;
        private readonly ILogger _logger;
        private readonly FileProcessingService _fileProcessingService;
        private readonly PackingService _packingService;
        private readonly UnpackingService _unpackingService;
        private readonly FileProcessingConfig _config;

        /// <summary>
        /// Create a new Program instance with default dependencies.
        /// </summary>
        public Program()
            : this(new RealFileSystem(), new ConsoleLogger(), new DefaultCodecFactory(), FileProcessingConfig.Default())
        {
        }

        /// <summary>
        /// Create a new Program instance with injectable dependencies.
        /// </summary>
        /// <param name="fileSystem">File system abstraction.</param>
        /// <param name="logger">Logger abstraction.</param>
        /// <param name="codecFactory">Codec factory.</param>
        /// <param name="config">Configuration settings.</param>
        public Program(IFileSystem fileSystem, ILogger logger, ICodecFactory codecFactory, FileProcessingConfig config)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _fileProcessingService = new FileProcessingService(fileSystem, logger, config);
            _packingService = new PackingService(fileSystem, logger, codecFactory, config);
            _unpackingService = new UnpackingService(fileSystem, logger, codecFactory, config);
        }

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
        /// Static version for backward compatibility.
        ///
        /// If inputFile is "mhfdat.bin.decd",
        /// metaFile should be "mhfdat.bin.meta" and
        /// the output file will be "mhfdat.bin".
        /// </summary>
        /// <param name="inputFile">Input file to encrypt.</param>
        /// <param name="metaFile">Data to use for encryption.</param>
        /// <param name="cleanUp">Remove both inputFile and metaFile.</param>
        /// <returns>Encrypted file path.</returns>
        /// <exception cref="FileNotFoundException">Thrown if the meta file does not exist.</exception>
        private static string EncryptEcdFile(string inputFile, string metaFile, bool cleanUp)
        {
            return DefaultInstance.Value._fileProcessingService.EncryptEcdFile(inputFile, metaFile, cleanUp);
        }

        /// <summary>
        /// Encrypt a single file to a new file.
        /// Instance method for testability.
        /// </summary>
        /// <param name="inputFile">Input file to encrypt.</param>
        /// <param name="metaFile">Data to use for encryption.</param>
        /// <param name="cleanUp">Remove both inputFile and metaFile.</param>
        /// <returns>Encrypted file path.</returns>
        public string EncryptEcdFileInstance(string inputFile, string metaFile, bool cleanUp)
        {
            return _fileProcessingService.EncryptEcdFile(inputFile, metaFile, cleanUp);
        }

        /// <summary>
        /// Decrypt an ECD encoded file to a new file.
        /// Static version for backward compatibility.
        /// </summary>
        /// <param name="inputFile">Input file path.</param>
        /// <param name="createLog">True if we should create a log file with the header.</param>
        /// <param name="cleanUp">true if the original file should be deleted.</param>
        /// <param name="rewriteOldFile">Should we overwrite inputFile.</param>
        /// <returns>Path to the decrypted file, in the form inputFile.decd</returns>
        private static string DecryptEcdFile(string inputFile, bool createLog, bool cleanUp, bool rewriteOldFile)
        {
            return DefaultInstance.Value._fileProcessingService.DecryptEcdFile(inputFile, createLog, cleanUp, rewriteOldFile);
        }

        /// <summary>
        /// Decrypt an ECD encoded file to a new file.
        /// Instance method for testability.
        /// </summary>
        /// <param name="inputFile">Input file path.</param>
        /// <param name="createLog">True if we should create a log file with the header.</param>
        /// <param name="cleanUp">true if the original file should be deleted.</param>
        /// <param name="rewriteOldFile">Should we overwrite inputFile.</param>
        /// <returns>Path to the decrypted file, in the form inputFile.decd</returns>
        public string DecryptEcdFileInstance(string inputFile, bool createLog, bool cleanUp, bool rewriteOldFile)
        {
            return _fileProcessingService.DecryptEcdFile(inputFile, createLog, cleanUp, rewriteOldFile);
        }

        /// <summary>
        /// Decrypt an Exf file.
        /// Static version for backward compatibility.
        /// </summary>
        /// <param name="inputFile">Input file path.</param>
        /// <param name="cleanUp">Should the original file be removed.</param>
        /// <returns>Output file at {inputFile}.dexf</returns>
        private static string DecryptExfFile(string inputFile, bool cleanUp)
        {
            return DefaultInstance.Value._fileProcessingService.DecryptExfFile(inputFile, cleanUp);
        }

        /// <summary>
        /// Decrypt an Exf file.
        /// Instance method for testability.
        /// </summary>
        /// <param name="inputFile">Input file path.</param>
        /// <param name="cleanUp">Should the original file be removed.</param>
        /// <returns>Output file at {inputFile}.dexf</returns>
        public string DecryptExfFileInstance(string inputFile, bool cleanUp)
        {
            return _fileProcessingService.DecryptExfFile(inputFile, cleanUp);
        }

        /// <summary>
        /// Launches a directory processing.
        /// Static version for backward compatibility.
        ///
        /// It can either pack the directory as a file, or uncompress the content.
        /// </summary>
        /// <param name="directoryPath">Directory path.</param>
        /// <param name="inputArguments">Configuration arguments from CLI.</param>
        public static void StartProcessingDirectory(string directoryPath, InputArguments inputArguments)
        {
            DefaultInstance.Value.StartProcessingDirectoryInstance(directoryPath, inputArguments);
        }

        /// <summary>
        /// Launches a directory processing.
        /// Instance method for testability.
        ///
        /// It can either pack the directory as a file, or uncompress the content.
        /// </summary>
        /// <param name="directoryPath">Directory path.</param>
        /// <param name="inputArguments">Configuration arguments from CLI.</param>
        public void StartProcessingDirectoryInstance(string directoryPath, InputArguments inputArguments)
        {
            if (inputArguments.repack)
                _packingService.ProcessPackInput(directoryPath);
            else
            {
                // Process each element in directory
                string[] inputFiles = _fileSystem.GetFiles(
                    directoryPath, "*.*", SearchOption.AllDirectories
                );
                ProcessMultipleLevelsInstance(inputFiles, inputArguments);
            }
        }

        /// <summary>
        /// Start the processing for a file.
        /// Static version for backward compatibility.
        ///
        /// It will either compress the file, encrypt it or depack it as many files.
        /// </summary>
        /// <param name="filePath">File path.</param>
        /// <param name="inputArguments">Configuration arguments from CLI.</param>
        public static void StartProcessingFile(string filePath, InputArguments inputArguments)
        {
            DefaultInstance.Value.StartProcessingFileInstance(filePath, inputArguments);
        }

        /// <summary>
        /// Start the processing for a file.
        /// Instance method for testability.
        ///
        /// It will either compress the file, encrypt it or depack it as many files.
        /// </summary>
        /// <param name="filePath">File path.</param>
        /// <param name="inputArguments">Configuration arguments from CLI.</param>
        public void StartProcessingFileInstance(string filePath, InputArguments inputArguments)
        {
            if (inputArguments.compression.level != 0)
            {
                // From mhfdat.bin.decd.bin to output/mhfdat.bin.decd
                _packingService.JPKEncode(
                    inputArguments.compression, filePath, $"{_config.OutputDirectory}/{Path.GetFileNameWithoutExtension(filePath)}"
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
                    Path.GetFileNameWithoutExtension(decompressedFilePath) + _config.MetaSuffix
                );
                _fileProcessingService.EncryptEcdFile(decompressedFilePath, metaFilePath, inputArguments.cleanUp);
            }

            // Try to depack the file as multiple files
            if (inputArguments.compression.level == 0 && !inputArguments.encrypt)
                ProcessMultipleLevelsInstance([filePath], inputArguments);
        }

        /// <summary>
        /// Unpack or (decrypt and decompress) a single file.
        /// Static version for backward compatibility.
        ///
        /// If the file was an ECD, decompress it as well.
        /// </summary>
        /// <param name="filePath">Input file path.</param>
        /// <param name="inputArguments">Configuration arguments from CLI.</param>
        /// <returns>Output path, can be file or folder.</returns>
        private static string ProcessFile(string filePath, InputArguments inputArguments)
        {
            return DefaultInstance.Value.ProcessFileInstance(filePath, inputArguments);
        }

        /// <summary>
        /// Unpack or (decrypt and decompress) a single file.
        /// Instance method for testability.
        ///
        /// If the file was an ECD, decompress it as well.
        /// </summary>
        /// <param name="filePath">Input file path.</param>
        /// <param name="inputArguments">Configuration arguments from CLI.</param>
        /// <returns>Output path, can be file or folder.</returns>
        public string ProcessFileInstance(string filePath, InputArguments inputArguments)
        {
            _logger.PrintWithSeparator($"Processing {filePath}", false);

            // Read file to memory
            MemoryStream msInput = new(_fileSystem.ReadAllBytes(filePath));
            BinaryReader brInput = new(msInput);
            string outputPath;
            if (msInput.Length == 0)
            {
                _logger.WriteLine("File is empty. Skipping.");
                return null;
            }
            int fileMagic = brInput.ReadInt32();

            // Since stage containers have no file magic, check for them first
            if (inputArguments.stageContainer)
            {
                brInput.BaseStream.Seek(0, SeekOrigin.Begin);
                outputPath = _unpackingService.UnpackStageContainer(filePath, brInput, inputArguments.createLog, inputArguments.cleanUp);
            }
            else if (fileMagic == 0x4F4D4F4D)
            {
                // MOMO Header: snp, snd
                _logger.WriteLine("MOMO Header detected.");
                outputPath = _unpackingService.UnpackSimpleArchive(
                    filePath, brInput, 8, inputArguments.createLog, inputArguments.cleanUp, inputArguments.autoStage
                );
            }
            else if (fileMagic == 0x1A646365)
            {
                // ECD Header
                _logger.WriteLine("ECD Header detected.");
                if (inputArguments.noDecryption)
                {
                    _logger.PrintWithSeparator("Not decrypting due to flag.", false);
                    return null;
                }
                outputPath = _fileProcessingService.DecryptEcdFile(
                    filePath,
                    inputArguments.createLog,
                    inputArguments.cleanUp,
                    inputArguments.rewriteOldFile
                );
            }
            else if (fileMagic == 0x1A667865)
            {
                // EXF Header
                _logger.WriteLine("EXF Header detected.");
                outputPath = _fileProcessingService.DecryptExfFile(filePath, inputArguments.cleanUp);
            }
            else if (fileMagic == 0x1A524B4A)
            {
                // JKR Header
                _logger.WriteLine("JKR Header detected.");
                outputPath = filePath;
                if (!inputArguments.ignoreJPK)
                {
                    outputPath = _unpackingService.UnpackJPK(filePath);
                    _logger.WriteLine($"File decompressed to {outputPath}.");

                    // Replace input file, deprecated behavior, will be removed in 2.0.0
                    if (
                        inputArguments.rewriteOldFile && outputPath != filePath &&
                        _fileSystem.GetAttributes(outputPath).HasFlag(FileAttributes.Normal)
                    )
                        _fileSystem.Copy(outputPath, filePath);
                }
            }
            else if (fileMagic == 0x0161686D)
            {
                // MHA Header
                _logger.WriteLine("MHA Header detected.");
                outputPath = _unpackingService.UnpackMHA(filePath, brInput, inputArguments.createLog);
            }
            else if (fileMagic == 0x000B0000)
            {
                // MHF Text file
                _logger.WriteLine("MHF Text file detected.");
                outputPath = _unpackingService.PrintFTXT(filePath, brInput);
            }
            else
            {
                // Try to unpack as simple container: i.e. txb, bin, pac, gab
                brInput.BaseStream.Seek(0, SeekOrigin.Begin);
                outputPath = _unpackingService.UnpackSimpleArchive(
                    filePath, brInput, 4, inputArguments.createLog, inputArguments.cleanUp, inputArguments.autoStage
                );
            }

            _logger.WriteSeparator();
            // Decompress file if it was an ECD (encrypted)
            if (fileMagic == 0x1A646365 && !inputArguments.decryptOnly)
            {
                string decdFilePath = outputPath;
                outputPath = ProcessFileInstance(decdFilePath, inputArguments);
                if (inputArguments.cleanUp)
                    _fileSystem.DeleteFile(decdFilePath);
            }
            return outputPath;
        }

        /// <summary>
        /// Process files on multiple container level.
        /// Static version for backward compatibility.
        ///
        /// Try to use each file is considered a container of multiple files.
        /// </summary>
        /// <param name="filePathes">Files to process.</param>
        /// <param name="inputArguments">Configuration arguments from CLI.</param>
        private static void ProcessMultipleLevels(string[] filePathes, InputArguments inputArguments)
        {
            DefaultInstance.Value.ProcessMultipleLevelsInstance(filePathes, inputArguments);
        }

        /// <summary>
        /// Process files on multiple container level.
        /// Instance method for testability.
        ///
        /// Try to use each file is considered a container of multiple files.
        /// </summary>
        /// <param name="filePathes">Files to process.</param>
        /// <param name="inputArguments">Configuration arguments from CLI.</param>
        public void ProcessMultipleLevelsInstance(string[] filePathes, InputArguments inputArguments)
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
                    string outputPath = ProcessFileInstance(inputFile, inputArguments);

                    // Disable stage processing files unpacked from parent
                    if (stageContainer)
                        stageContainer = false;

                    // Check if a new directory was created
                    if (inputArguments.recursive && _fileSystem.DirectoryExists(outputPath))
                    {
                        AddNewFilesInstance(outputPath, filesToProcess);
                    }
                });
            }
        }

        /// <summary>
        /// Add new files in the directory to filesQueue.
        /// Static version for backward compatibility.
        ///
        /// It is a task producer in Task Parallel Library paradigm.
        /// </summary>
        /// <param name="directoryPath">Directory to search into.</param>
        /// <param name="filesQueue">Thread-safe queue where to add files to.</param>
        private static void AddNewFiles(string directoryPath, ConcurrentQueue<string> filesQueue)
        {
            DefaultInstance.Value.AddNewFilesInstance(directoryPath, filesQueue);
        }

        /// <summary>
        /// Add new files in the directory to filesQueue.
        /// Instance method for testability.
        ///
        /// It is a task producer in Task Parallel Library paradigm.
        /// </summary>
        /// <param name="directoryPath">Directory to search into.</param>
        /// <param name="filesQueue">Thread-safe queue where to add files to.</param>
        public void AddNewFilesInstance(string directoryPath, ConcurrentQueue<string> filesQueue)
        {
            // Limit file search to these patterns
            string[] patterns = ["*.bin", "*.jkr", "*.ftxt", "*.snd"];

            var fileOperations = new FileOperations(_fileSystem, _logger);
            var nextFiles = fileOperations.GetFilesInstance(directoryPath, patterns, SearchOption.TopDirectoryOnly);

            foreach (var nextFile in nextFiles)
            {
                filesQueue.Enqueue(nextFile);
            }
        }
    }
}
