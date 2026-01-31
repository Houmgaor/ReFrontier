using System.IO;

namespace ReFrontier.Routing
{
    /// <summary>
    /// Interface for handlers that process specific file types based on magic numbers.
    /// </summary>
    public interface IFileTypeHandler
    {
        /// <summary>
        /// Determines if this handler can process a file with the given magic number and arguments.
        /// </summary>
        /// <param name="fileMagic">The magic number read from the file header.</param>
        /// <param name="args">Processing arguments from CLI.</param>
        /// <returns>True if this handler can process the file.</returns>
        bool CanHandle(uint fileMagic, InputArguments args);

        /// <summary>
        /// Process the file and return the result.
        /// </summary>
        /// <param name="filePath">Path to the file to process.</param>
        /// <param name="reader">Binary reader positioned at the start of the file.</param>
        /// <param name="args">Processing arguments from CLI.</param>
        /// <returns>Result indicating success or skip with output path.</returns>
        ProcessFileResult Handle(string filePath, BinaryReader reader, InputArguments args);

        /// <summary>
        /// Priority for handler selection (higher values = higher priority).
        /// Used when multiple handlers can process the same file type.
        /// </summary>
        int Priority { get; }
    }
}
