using System.IO;

namespace ReFrontier.Jpk
{
    /// <summary>
    /// Base interface for JPK decoding.
    /// </summary>
    public interface IJPKDecode
    {
        /// <summary>
        /// Read byte from stream.
        /// </summary>
        /// <param name="stream">Input stream.</param>
        /// <returns>Read byte.</returns>
        byte ReadByte(Stream stream);
        /// <summary>
        /// File processing.
        /// </summary>
        /// <param name="inStream">Input stream.</param>
        /// <param name="outBuffer">Output buffer.</param>
        /// <param name="outSize">Actual output size (may be less than buffer length when using pooled buffers).</param>
        void ProcessOnDecode(Stream inStream, byte[] outBuffer, int outSize);
    }
}
