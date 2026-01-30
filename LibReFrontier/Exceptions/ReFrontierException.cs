#nullable enable

using System;

namespace LibReFrontier.Exceptions
{
    /// <summary>
    /// Base exception for all ReFrontier operations.
    /// </summary>
    public class ReFrontierException : Exception
    {
        /// <summary>
        /// File path associated with the error, if any.
        /// </summary>
        public string? FilePath { get; set; }

        public ReFrontierException() : base()
        {
        }

        public ReFrontierException(string message) : base(message)
        {
        }

        public ReFrontierException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public ReFrontierException(string message, string filePath) : base(message)
        {
            FilePath = filePath;
        }

        public ReFrontierException(string message, string filePath, Exception innerException) : base(message, innerException)
        {
            FilePath = filePath;
        }

        /// <summary>
        /// Sets the file path if not already set and returns this exception.
        /// Useful for enriching exceptions caught from lower layers that don't have file context.
        /// </summary>
        /// <param name="filePath">File path to associate with this exception.</param>
        /// <returns>This exception instance for chaining.</returns>
        public ReFrontierException WithFilePath(string? filePath)
        {
            if (FilePath == null && filePath != null)
            {
                FilePath = filePath;
            }
            return this;
        }
    }
}
