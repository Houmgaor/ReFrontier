using System;

namespace LibReFrontier.Exceptions
{
    /// <summary>
    /// Exception thrown when decryption operations fail.
    /// </summary>
    public class DecryptionException : ReFrontierException
    {
        public DecryptionException() : base()
        {
        }

        public DecryptionException(string message) : base(message)
        {
        }

        public DecryptionException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public DecryptionException(string message, string filePath) : base(message, filePath)
        {
        }

        public DecryptionException(string message, string filePath, Exception innerException) : base(message, filePath, innerException)
        {
        }
    }
}
