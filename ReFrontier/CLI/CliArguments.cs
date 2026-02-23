namespace ReFrontier.CLI
{
    /// <summary>
    /// Immutable DTO containing parsed CLI arguments for the application.
    /// </summary>
    public readonly struct CliArguments
    {
        /// <summary>
        /// Path to the input file or directory to process.
        /// </summary>
        public string FilePath { get; init; }

        /// <summary>
        /// Processing arguments to control file operations.
        /// </summary>
        public InputArguments ProcessingArgs { get; init; }

        /// <summary>
        /// Number of parallel threads to use (0 = auto-detect).
        /// </summary>
        public int Parallelism { get; init; }

        /// <summary>
        /// Whether to suppress progress bar during processing.
        /// </summary>
        public bool Quiet { get; init; }

        /// <summary>
        /// Whether to show per-file processing messages.
        /// </summary>
        public bool Verbose { get; init; }

        /// <summary>
        /// Whether to validate file integrity without extracting.
        /// </summary>
        public bool Validate { get; init; }
    }
}
