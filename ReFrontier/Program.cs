using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using LibReFrontier;
using LibReFrontier.Abstractions;
using LibReFrontier.Exceptions;

using ReFrontier.Jpk;
using ReFrontier.Services;

namespace ReFrontier
{
    /// <summary>
    /// Main accepted arguments from the CLI of ReFrontier.
    /// </summary>
    public struct InputArguments : IEquatable<InputArguments>
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

        public override bool Equals(object? obj)
        {
            return obj is InputArguments other && Equals(other);
        }

        public bool Equals(InputArguments other)
        {
            return createLog == other.createLog
                && recursive == other.recursive
                && repack == other.repack
                && decryptOnly == other.decryptOnly
                && noDecryption == other.noDecryption
                && encrypt == other.encrypt
                && cleanUp == other.cleanUp
                && ignoreJPK == other.ignoreJPK
                && stageContainer == other.stageContainer
                && autoStage == other.autoStage
                && rewriteOldFile == other.rewriteOldFile
                && compression.Equals(other.compression);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(createLog);
            hash.Add(recursive);
            hash.Add(repack);
            hash.Add(decryptOnly);
            hash.Add(noDecryption);
            hash.Add(encrypt);
            hash.Add(cleanUp);
            hash.Add(ignoreJPK);
            hash.Add(stageContainer);
            hash.Add(autoStage);
            hash.Add(rewriteOldFile);
            hash.Add(compression);
            return hash.ToHashCode();
        }

        public static bool operator ==(InputArguments left, InputArguments right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(InputArguments left, InputArguments right)
        {
            return !left.Equals(right);
        }
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
        /// <returns>Exit code (0 for success).</returns>
        private static int Main(string[] args)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var fileVersionAttribute = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ?? "unknown";
            var productName = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "ReFrontier";
            var description = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description ?? "";

            // Create root command
            var rootCommand = new RootCommand($"{productName} - {description}, by MHVuze, additions by Houmgaor");

            // Arguments
            var fileArgument = new Argument<string>(
                name: "file",
                description: "Input file or directory to process"
            );
            rootCommand.AddArgument(fileArgument);

            // Unpacking options
            var logOption = new Option<bool>(
                name: "--log",
                description: "Write log file (required for re-encryption)"
            );
            rootCommand.AddOption(logOption);

            var stageContainerOption = new Option<bool>(
                name: "--stageContainer",
                description: "Unpack file as stage-specific container"
            );
            rootCommand.AddOption(stageContainerOption);

            var autoStageOption = new Option<bool>(
                name: "--autoStage",
                description: "Automatically attempt to unpack containers that might be stage-specific"
            );
            rootCommand.AddOption(autoStageOption);

            var nonRecursiveOption = new Option<bool>(
                name: "--nonRecursive",
                description: "Do not unpack recursively"
            );
            rootCommand.AddOption(nonRecursiveOption);

            var decryptOnlyOption = new Option<bool>(
                name: "--decryptOnly",
                description: "Decrypt ECD files without unpacking"
            );
            rootCommand.AddOption(decryptOnlyOption);

            var noDecryptionOption = new Option<bool>(
                name: "--noDecryption",
                description: "Don't decrypt ECD files, no unpacking"
            );
            rootCommand.AddOption(noDecryptionOption);

            var ignoreJPKOption = new Option<bool>(
                name: "--ignoreJPK",
                description: "Do not decompress JPK files"
            );
            rootCommand.AddOption(ignoreJPKOption);

            var noFileRewriteOption = new Option<bool>(
                name: "--noFileRewrite",
                description: "Avoid rewriting original files"
            );
            rootCommand.AddOption(noFileRewriteOption);

            var cleanUpOption = new Option<bool>(
                name: "--cleanUp",
                description: "Delete simple archives after unpacking"
            );
            rootCommand.AddOption(cleanUpOption);

            // Packing options
            var packOption = new Option<bool>(
                name: "--pack",
                description: "Repack directory (requires log file)"
            );
            rootCommand.AddOption(packOption);

            var compressTypeOption = new Option<string>(
                name: "--compress",
                description: "Compression type: rw, hfirw, lz, hfi (or numeric: 0, 2, 3, 4)"
            );
            rootCommand.AddOption(compressTypeOption);

            var compressLevelOption = new Option<int>(
                name: "--level",
                description: "Compression level (e.g., 50, 100)",
                getDefaultValue: () => 0
            );
            rootCommand.AddOption(compressLevelOption);

            var encryptOption = new Option<bool>(
                name: "--encrypt",
                description: "Encrypt input file with ECD algorithm"
            );
            rootCommand.AddOption(encryptOption);

            // General options
            var closeOption = new Option<bool>(
                name: "--close",
                description: "Close window after finishing process"
            );
            rootCommand.AddOption(closeOption);

            // Set handler
            rootCommand.SetHandler((InvocationContext context) =>
            {
                var file = context.ParseResult.GetValueForArgument(fileArgument);
                var log = context.ParseResult.GetValueForOption(logOption);
                var stageContainer = context.ParseResult.GetValueForOption(stageContainerOption);
                var autoStage = context.ParseResult.GetValueForOption(autoStageOption);
                var nonRecursive = context.ParseResult.GetValueForOption(nonRecursiveOption);
                var decryptOnly = context.ParseResult.GetValueForOption(decryptOnlyOption);
                var noDecryption = context.ParseResult.GetValueForOption(noDecryptionOption);
                var ignoreJPK = context.ParseResult.GetValueForOption(ignoreJPKOption);
                var noFileRewrite = context.ParseResult.GetValueForOption(noFileRewriteOption);
                var cleanUp = context.ParseResult.GetValueForOption(cleanUpOption);
                var pack = context.ParseResult.GetValueForOption(packOption);
                var compressType = context.ParseResult.GetValueForOption(compressTypeOption);
                var compressLevel = context.ParseResult.GetValueForOption(compressLevelOption);
                var encrypt = context.ParseResult.GetValueForOption(encryptOption);
                var close = context.ParseResult.GetValueForOption(closeOption);

                Console.WriteLine($"{productName} v{fileVersionAttribute} - {description}, by MHVuze, additions by Houmgaor");
                Console.WriteLine("==============================");

                // Validate file exists
                if (!File.Exists(file) && !Directory.Exists(file))
                {
                    Console.Error.WriteLine($"Error: '{file}' does not exist.");
                    context.ExitCode = 1;
                    return;
                }

                // Parse compression if specified
                Compression compression = new();
                if (!string.IsNullOrEmpty(compressType))
                {
                    if (compressLevel <= 0)
                    {
                        Console.Error.WriteLine("Error: --level is required when using --compress. Example: --compress lz --level 100");
                        context.ExitCode = 1;
                        return;
                    }
                    try
                    {
                        compression = ArgumentsParser.ParseCompression(compressType, compressLevel);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Error: {ex.Message}");
                        context.ExitCode = 1;
                        return;
                    }
                }

                // Build input arguments
                InputArguments inputArguments = new()
                {
                    createLog = log,
                    recursive = !nonRecursive,
                    repack = pack,
                    decryptOnly = decryptOnly,
                    noDecryption = noDecryption,
                    encrypt = encrypt,
                    cleanUp = cleanUp,
                    ignoreJPK = ignoreJPK,
                    stageContainer = stageContainer,
                    autoStage = autoStage,
                    rewriteOldFile = !noFileRewrite,
                    compression = compression
                };

                // Create program instance and start processing
                var program = new Program();

                try
                {
                    // Start input processing
                    if (File.GetAttributes(file).HasFlag(FileAttributes.Directory))
                    {
                        // Input is directory
                        if (compression.Level != 0)
                            throw new InvalidOperationException("Cannot compress a directory.");
                        if (inputArguments.encrypt)
                            throw new InvalidOperationException("Cannot encrypt a directory.");
                        program.StartProcessingDirectory(file, inputArguments);
                    }
                    else
                    {
                        // Input is a file
                        if (inputArguments.repack)
                            throw new InvalidOperationException("A single file cannot be used while in repacking mode.");
                        program.StartProcessingFile(file, inputArguments);
                    }
                    Console.WriteLine("Done.");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error: {ex.Message}");
                    context.ExitCode = 1;
                }
                finally
                {
                    if (!close)
                        Console.Read();
                }
            });

            return rootCommand.Invoke(args);
        }

        /// <summary>
        /// Launches a directory processing.
        ///
        /// It can either pack the directory as a file, or uncompress the content.
        /// </summary>
        /// <param name="directoryPath">Directory path.</param>
        /// <param name="inputArguments">Configuration arguments from CLI.</param>
        public void StartProcessingDirectory(string directoryPath, InputArguments inputArguments)
        {
            if (inputArguments.repack)
                _packingService.ProcessPackInput(directoryPath);
            else
            {
                // Process each element in directory
                string[] inputFiles = _fileSystem.GetFiles(
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
        public void StartProcessingFile(string filePath, InputArguments inputArguments)
        {
            if (inputArguments.compression.Level != 0)
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
            if (inputArguments.compression.Level == 0 && !inputArguments.encrypt)
                ProcessMultipleLevels([filePath], inputArguments);
        }

        /// <summary>
        /// Unpack or (decrypt and decompress) a single file.
        ///
        /// If the file was an ECD, decompress it as well.
        /// </summary>
        /// <param name="filePath">Input file path.</param>
        /// <param name="inputArguments">Configuration arguments from CLI.</param>
        /// <returns>Result indicating success with output path, or skipped with reason.</returns>
        public ProcessFileResult ProcessFile(string filePath, InputArguments inputArguments)
        {
            _logger.PrintWithSeparator($"Processing {filePath}", false);

            // Read file to memory
            using MemoryStream msInput = new(_fileSystem.ReadAllBytes(filePath));
            using BinaryReader brInput = new(msInput);
            string outputPath;
            if (msInput.Length == 0)
            {
                _logger.WriteLine("File is empty. Skipping.");
                return ProcessFileResult.Skipped("File is empty");
            }
            uint fileMagic = brInput.ReadUInt32();

            // Since stage containers have no file magic, check for them first
            if (inputArguments.stageContainer)
            {
                brInput.BaseStream.Seek(0, SeekOrigin.Begin);
                outputPath = _unpackingService.UnpackStageContainer(filePath, brInput, inputArguments.createLog, inputArguments.cleanUp);
            }
            else if (fileMagic == FileMagic.MOMO)
            {
                // MOMO Header: snp, snd
                _logger.WriteLine("MOMO Header detected.");
                outputPath = _unpackingService.UnpackSimpleArchive(
                    filePath, brInput, 8, inputArguments.createLog, inputArguments.cleanUp, inputArguments.autoStage
                );
            }
            else if (fileMagic == FileMagic.ECD)
            {
                // ECD Header
                _logger.WriteLine("ECD Header detected.");
                if (inputArguments.noDecryption)
                {
                    _logger.PrintWithSeparator("Not decrypting due to flag.", false);
                    return ProcessFileResult.Skipped("Decryption disabled");
                }
                outputPath = _fileProcessingService.DecryptEcdFile(
                    filePath,
                    inputArguments.createLog,
                    inputArguments.cleanUp,
                    inputArguments.rewriteOldFile
                );
            }
            else if (fileMagic == FileMagic.EXF)
            {
                // EXF Header
                _logger.WriteLine("EXF Header detected.");
                outputPath = _fileProcessingService.DecryptExfFile(filePath, inputArguments.cleanUp);
            }
            else if (fileMagic == FileMagic.JKR)
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
            else if (fileMagic == FileMagic.MHA)
            {
                // MHA Header
                _logger.WriteLine("MHA Header detected.");
                outputPath = _unpackingService.UnpackMHA(filePath, brInput, inputArguments.createLog);
            }
            else if (fileMagic == FileMagic.FTXT)
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
            if (fileMagic == FileMagic.ECD && !inputArguments.decryptOnly)
            {
                string decdFilePath = outputPath;
                var result = ProcessFile(decdFilePath, inputArguments);
                if (inputArguments.cleanUp)
                    _fileSystem.DeleteFile(decdFilePath);
                outputPath = result.OutputPath ?? outputPath;
            }
            return ProcessFileResult.Success(outputPath);
        }

        /// <summary>
        /// Process files on multiple container level.
        ///
        /// Try to use each file is considered a container of multiple files.
        /// </summary>
        /// <param name="filePathes">Files to process.</param>
        /// <param name="inputArguments">Configuration arguments from CLI.</param>
        public void ProcessMultipleLevels(string[] filePathes, InputArguments inputArguments)
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
                while (filesToProcess.TryDequeue(out string? tempInputFile))
                {
                    if (tempInputFile != null)
                        fileWorkers.Add(tempInputFile);
                }

                // Use Interlocked to safely disable stage processing after first file
                int stageContainerFlag = inputArguments.stageContainer ? 1 : 0;
                Parallel.ForEach(fileWorkers, parallelOptions, inputFile =>
                {
                    // Check if this is the first file to process with stage container
                    bool useStageContainer = Interlocked.Exchange(ref stageContainerFlag, 0) == 1;
                    var localArgs = inputArguments;
                    localArgs.stageContainer = useStageContainer;

                    try
                    {
                        ProcessFileResult result = ProcessFile(inputFile, localArgs);

                        // Check if a new directory was created
                        if (inputArguments.recursive && result.OutputPath != null && _fileSystem.DirectoryExists(result.OutputPath))
                        {
                            AddNewFiles(result.OutputPath, filesToProcess);
                        }
                    }
                    catch (ReFrontierException ex)
                    {
                        // Log the error and continue processing other files
                        _logger.WriteLine($"Skipping {inputFile}: {ex.Message}");
                    }
                });
            }
        }

        /// <summary>
        /// Add new files in the directory to filesQueue.
        ///
        /// It is a task producer in Task Parallel Library paradigm.
        /// </summary>
        /// <param name="directoryPath">Directory to search into.</param>
        /// <param name="filesQueue">Thread-safe queue where to add files to.</param>
        public void AddNewFiles(string directoryPath, ConcurrentQueue<string> filesQueue)
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
