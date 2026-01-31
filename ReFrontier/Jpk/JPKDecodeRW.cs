using System.IO;

using LibReFrontier.Exceptions;

namespace ReFrontier.Jpk
{
    /// <summary>
    /// Raw Writing (RW) decoder - reads data without decompression.
    ///
    /// <para><b>Algorithm:</b></para>
    /// <para>No transformation is applied. Input bytes are copied directly to the output
    /// buffer in sequence. This handles both CompressionType.RW and CompressionType.None.</para>
    ///
    /// <para><b>Error Handling:</b></para>
    /// <para>Throws <see cref="CompressionException"/> if the stream ends before
    /// <c>outSize</c> bytes are read (unexpected truncation).</para>
    ///
    /// <para><b>Performance:</b></para>
    /// <para>O(n) time, O(1) space. Fastest decoder available.</para>
    /// </summary>
    internal class JPKDecodeRW : IJPKDecode
    {
        /// <summary>
        /// Read bytes directly without decoding.
        /// </summary>
        /// <param name="inStream">Input stream to read bytes from.</param>
        /// <param name="outBuffer">Buffer to write to.</param>
        /// <param name="outSize">Actual output size.</param>
        public void ProcessOnDecode(Stream inStream, byte[] outBuffer, int outSize)
        {
            for (int index = 0; index < outSize; index++)
            {
                if (inStream.Position >= inStream.Length)
                    break;
                outBuffer[index] = ReadByte(inStream);
            }
        }

        /// <summary>
        /// Read a single byte from the stream at the current position.
        /// </summary>
        /// <param name="s">Stream to read from.</param>
        /// <returns>Read byte.</returns>
        /// <exception cref="CompressionException">Exception when end of file is reached unexpectedly.</exception>
        public byte ReadByte(Stream s)
        {
            int value = s.ReadByte();
            if (value < 0)
                throw new CompressionException("Decompression failed: unexpected end of stream.");
            return (byte)value;
        }
    }
}
