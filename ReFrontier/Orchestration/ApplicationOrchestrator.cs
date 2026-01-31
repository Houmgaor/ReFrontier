using System;

using LibReFrontier.Abstractions;

using ReFrontier.CLI;
using ReFrontier.Jpk;
using ReFrontier.Services;

using Spectre.Console;

namespace ReFrontier.Orchestration
{
    /// <summary>
    /// Orchestrates the high-level application flow for ReFrontier.
    /// </summary>
    public class ApplicationOrchestrator
    {
        private readonly IFileSystem _fileSystem;
        private readonly ILogger _logger;
        private readonly Program _program;
        private readonly string _productName;
        private readonly string _version;
        private readonly string _description;

        /// <summary>
        /// Create a new ApplicationOrchestrator instance with default dependencies.
        /// </summary>
        public ApplicationOrchestrator()
            : this(new RealFileSystem(), new ConsoleLogger(), new DefaultCodecFactory(), FileProcessingConfig.Default())
        {
        }

        /// <summary>
        /// Create a new ApplicationOrchestrator instance with injectable dependencies.
        /// </summary>
        /// <param name="fileSystem">File system abstraction.</param>
        /// <param name="logger">Logger abstraction.</param>
        /// <param name="codecFactory">Codec factory.</param>
        /// <param name="config">Configuration settings.</param>
        public ApplicationOrchestrator(IFileSystem fileSystem, ILogger logger, ICodecFactory codecFactory, FileProcessingConfig config)
            : this(fileSystem, logger, codecFactory, config, "ReFrontier", "unknown", "")
        {
        }

        /// <summary>
        /// Create a new ApplicationOrchestrator instance with full configuration.
        /// </summary>
        /// <param name="fileSystem">File system abstraction.</param>
        /// <param name="logger">Logger abstraction.</param>
        /// <param name="codecFactory">Codec factory.</param>
        /// <param name="config">Configuration settings.</param>
        /// <param name="productName">Product name for display.</param>
        /// <param name="version">Application version.</param>
        /// <param name="description">Application description.</param>
        public ApplicationOrchestrator(
            IFileSystem fileSystem,
            ILogger logger,
            ICodecFactory codecFactory,
            FileProcessingConfig config,
            string productName,
            string version,
            string description)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _program = new Program(fileSystem, logger, codecFactory, config);
            _productName = productName;
            _version = version;
            _description = description;
        }

        /// <summary>
        /// Execute the application with the provided CLI arguments.
        /// </summary>
        /// <param name="args">Parsed CLI arguments.</param>
        /// <returns>Exit code (0 for success, 1 for failure).</returns>
        public int Execute(CliArguments args)
        {
            // Print header
            _logger.WriteLine($"{_productName} v{_version} - {_description}, by MHVuze, additions by Houmgaor");
            _logger.WriteLine("==============================");

            // Validate file exists
            if (!_fileSystem.FileExists(args.FilePath) && !_fileSystem.DirectoryExists(args.FilePath))
            {
                _logger.WriteLine($"Error: '{args.FilePath}' does not exist.");
                return 1;
            }

            // Resolve parallelism value (0 = auto-detect)
            int effectiveParallelism = args.Parallelism == 0
                ? Environment.ProcessorCount
                : args.Parallelism;

            // Cap at reasonable maximum
            if (effectiveParallelism > 64)
                effectiveParallelism = 64;

            // Update processing args with resolved parallelism and quiet flag
            var processingArgs = args.ProcessingArgs;
            processingArgs.parallelism = effectiveParallelism;
            processingArgs.quiet = args.Quiet;

            // Start input processing
            ProcessingStatistics? stats = null;

            if (_fileSystem.DirectoryExists(args.FilePath))
            {
                // Input is directory
                if (processingArgs.compression.Level != 0)
                {
                    _logger.WriteLine("Error: Cannot compress a directory.");
                    return 1;
                }
                if (processingArgs.encrypt)
                {
                    _logger.WriteLine("Error: Cannot encrypt a directory.");
                    return 1;
                }

                // Use progress bar for directory processing
                stats = ProcessDirectoryWithProgress(args.FilePath, processingArgs);
            }
            else
            {
                // Input is a file
                if (processingArgs.repack)
                {
                    _logger.WriteLine("Error: A single file cannot be used while in repacking mode.");
                    return 1;
                }
                _program.StartProcessingFile(args.FilePath, processingArgs);
            }

            // Print summary
            PrintSummary(stats);
            return 0;
        }

        /// <summary>
        /// Process a directory with progress bar display.
        /// </summary>
        private ProcessingStatistics ProcessDirectoryWithProgress(string directoryPath, InputArguments processingArgs)
        {
            ProcessingStatistics? stats = null;

            // Check if we can use interactive progress (not redirected output)
            if (!Console.IsOutputRedirected && !processingArgs.quiet)
            {
                AnsiConsole.Progress()
                    .AutoClear(true)
                    .HideCompleted(false)
                    .Columns(
                        new TaskDescriptionColumn(),
                        new ProgressBarColumn(),
                        new PercentageColumn(),
                        new SpinnerColumn()
                    )
                    .Start(ctx =>
                    {
                        var task = ctx.AddTask("[green]Processing files[/]", maxValue: 100);
                        task.IsIndeterminate = true;

                        stats = _program.StartProcessingDirectory(directoryPath, processingArgs, (current, total) =>
                        {
                            if (total > 0)
                            {
                                task.IsIndeterminate = false;
                                task.MaxValue = total;
                                task.Value = current;
                            }
                        });

                        task.Value = task.MaxValue;
                    });
            }
            else
            {
                // Non-interactive mode: just process without progress bar
                stats = _program.StartProcessingDirectory(directoryPath, processingArgs);
            }

            return stats ?? new ProcessingStatistics();
        }

        /// <summary>
        /// Print processing summary.
        /// </summary>
        private void PrintSummary(ProcessingStatistics? stats)
        {
            if (stats == null || stats.TotalFiles == 0)
            {
                _logger.WriteLine("Done.");
                return;
            }

            var parts = new System.Collections.Generic.List<string>();

            if (stats.ProcessedFiles > 0)
                parts.Add($"{stats.ProcessedFiles} processed");
            if (stats.SkippedFiles > 0)
                parts.Add($"{stats.SkippedFiles} skipped");
            if (stats.ErrorFiles > 0)
                parts.Add($"{stats.ErrorFiles} errors");
            if (stats.GeneratedFiles > 0)
                parts.Add($"{stats.GeneratedFiles} generated");

            if (parts.Count > 0)
                _logger.WriteLine($"Done: {string.Join(", ", parts)}.");
            else
                _logger.WriteLine("Done.");
        }
    }
}
