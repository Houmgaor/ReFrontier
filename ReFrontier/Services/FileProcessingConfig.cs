namespace ReFrontier.Services
{
    /// <summary>
    /// Configuration for file processing services.
    /// Allows customization of paths and suffixes for testability.
    /// </summary>
    public class FileProcessingConfig
    {
        /// <summary>
        /// Default output directory for packed/encrypted files.
        /// </summary>
        public string OutputDirectory { get; set; } = "output";

        /// <summary>
        /// Suffix for decrypted ECD files.
        /// </summary>
        public string DecryptedSuffix { get; set; } = ".decd";

        /// <summary>
        /// Suffix for decrypted EXF files.
        /// </summary>
        public string DecryptedExfSuffix { get; set; } = ".dexf";

        /// <summary>
        /// Suffix for metadata files.
        /// </summary>
        public string MetaSuffix { get; set; } = ".meta";

        /// <summary>
        /// Suffix for log files.
        /// </summary>
        public string LogSuffix { get; set; } = ".log";

        /// <summary>
        /// Suffix for unpacked directories.
        /// </summary>
        public string UnpackedSuffix { get; set; } = ".unpacked";

        /// <summary>
        /// Suffix for text output files.
        /// </summary>
        public string TextSuffix { get; set; } = ".txt";

        /// <summary>
        /// Create a default configuration.
        /// </summary>
        /// <returns>Default config instance.</returns>
        public static FileProcessingConfig Default() => new FileProcessingConfig();
    }
}
