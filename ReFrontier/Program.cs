using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using LibReFrontier;
using LibReFrontier.Abstractions;
using LibReFrontier.Exceptions;

using ReFrontier.CLI;
using ReFrontier.Jpk;
using ReFrontier.Orchestration;
using ReFrontier.Routing;
using ReFrontier.Routing.Handlers;
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
        private readonly FileRouter _fileRouter;

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

            // Initialize file router with handlers
            _fileRouter = new FileRouter(logger);
            RegisterHandlers();
        }

        /// <summary>
        /// Register all file type handlers with the router.
        /// </summary>
        private void RegisterHandlers()
        {
            // Register handlers in order (priority is set in each handler)
            // Higher priority handlers are checked first for same magic number
            _fileRouter.RegisterHandler(new StageContainerHandler(_logger, _unpackingService));
            _fileRouter.RegisterHandler(new NoDecryptionHandler(_logger));
            _fileRouter.RegisterHandler(new EcdEncryptionHandler(_logger, _fileProcessingService));
            _fileRouter.RegisterHandler(new ExfEncryptionHandler(_logger, _fileProcessingService));
            _fileRouter.RegisterHandler(new JkrCompressionHandler(_fileSystem, _logger, _unpackingService));
            _fileRouter.RegisterHandler(new MomoArchiveHandler(_logger, _unpackingService));
            _fileRouter.RegisterHandler(new MhaArchiveHandler(_logger, _unpackingService));
            _fileRouter.RegisterHandler(new FtxtTextHandler(_logger, _unpackingService));
            _fileRouter.RegisterHandler(new SimpleArchiveHandler(_logger, _unpackingService));
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

            // Create CLI schema and root command
            var cliSchema = new CliSchema();
            var rootCommand = cliSchema.CreateRootCommand(fileVersionAttribute, productName, description);

            // Set handler
            rootCommand.SetAction(parseResult =>
            {
                int exitCode = 0;
                CliArguments cliArgs;

                try
                {
                    cliArgs = cliSchema.ExtractArguments(parseResult);
                }
                catch (InvalidOperationException ex)
                {
                    Console.Error.WriteLine(ex.Message);
                    return 1;
                }

                try
                {
                    // Create orchestrator and execute
                    var orchestrator = new ApplicationOrchestrator(
                        new RealFileSystem(),
                        new ConsoleLogger(),
                        new DefaultCodecFactory(),
                        FileProcessingConfig.Default(),
                        productName,
                        fileVersionAttribute,
                        description
                    );
                    exitCode = orchestrator.Execute(cliArgs);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error: {ex.Message}");
                    exitCode = 1;
                }
                finally
                {
                    if (!cliArgs.CloseAfterCompletion)
                        Console.Read();
                }
                return exitCode;
            });

            return rootCommand.Parse(args).Invoke();
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

            // Route file to appropriate handler
            brInput.BaseStream.Seek(0, SeekOrigin.Begin);
            var routerResult = _fileRouter.Route(filePath, fileMagic, brInput, inputArguments);

            if (!routerResult.WasProcessed)
            {
                return routerResult; // Return skip result if no handler found
            }

            outputPath = routerResult.OutputPath!;

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
