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
        /// Whether to close the application window after completion.
        /// </summary>
        public bool CloseAfterCompletion { get; init; }
    }
}
