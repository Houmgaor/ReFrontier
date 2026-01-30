using System;

namespace LibReFrontier.Exceptions
{
    /// <summary>
    /// Exception thrown when packing or unpacking operations fail.
    /// </summary>
    public class PackingException : ReFrontierException
    {
        public PackingException() : base()
        {
        }

        public PackingException(string message) : base(message)
        {
        }

        public PackingException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public PackingException(string message, string filePath) : base(message, filePath)
        {
        }

        public PackingException(string message, string filePath, Exception innerException) : base(message, filePath, innerException)
        {
        }
    }
}
