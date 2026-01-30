#nullable enable

using System;

namespace LibReFrontier.Exceptions
{
    /// <summary>
    /// Exception thrown when compression or decompression operations fail.
    /// </summary>
    public class CompressionException : ReFrontierException
    {
        /// <summary>
        /// The compression type being used, if known.
        /// </summary>
        public string? CompressionType { get; set; }

        public CompressionException() : base()
        {
        }

        public CompressionException(string message) : base(message)
        {
        }

        public CompressionException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public CompressionException(string message, string filePath) : base(message, filePath)
        {
        }

        public CompressionException(string message, string filePath, Exception innerException) : base(message, filePath, innerException)
        {
        }

        public CompressionException(string message, string filePath, string compressionType) : base(message, filePath)
        {
            CompressionType = compressionType;
        }

        public CompressionException(string message, string filePath, string compressionType, Exception innerException) : base(message, filePath, innerException)
        {
            CompressionType = compressionType;
        }
    }
}
