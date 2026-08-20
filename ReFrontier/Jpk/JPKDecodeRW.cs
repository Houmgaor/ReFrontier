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
                // Stopping here instead of reading would leave the tail of outBuffer
                // zero-filled and report the truncated result as a complete file.
                if (inStream.Position >= inStream.Length)
                    throw new CompressionException(
                        "Unexpected end of stream. " +
                        $"Read {index} of {outSize} declared bytes " +
                        $"({(outSize > 0 ? 100.0 * index / outSize : 0):F2}%) " +
                        $"before running out of input at offset {inStream.Position} " +
                        $"of {inStream.Length}. The stream is truncated, or the decompressed " +
                        "size in the JPK header does not match the data."
                    );
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
                throw new CompressionException("Unexpected end of stream.");
            return (byte)value;
        }
    }
}
