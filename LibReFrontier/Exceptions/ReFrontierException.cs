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
    }
}
