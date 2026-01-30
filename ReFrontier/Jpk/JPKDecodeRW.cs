using System.IO;

using LibReFrontier.Exceptions;

namespace ReFrontier.Jpk
{
    /// <summary>
    /// Raw JPK "decoding", do not decode anything.
    /// </summary>
    internal class JPKDecodeRW : IJPKDecode
    {
        /// <summary>
        /// Read bytes directly without decoding.
        /// </summary>
        /// <param name="inStream">Input stream to read bytes from.</param>
        /// <param name="outBuffer">Buffer to write to.</param>
        public void ProcessOnDecode(Stream inStream, byte[] outBuffer)
        {
            for (int index = 0; index < outBuffer.Length; index++)
            {
                if (inStream.Position >= inStream.Length)
                    break;
                outBuffer[index] = ReadByte(inStream);
            }
        }

        /// <summary>
        /// Read a single byte from the stream at the current position.
        /// </summary>
        /// <param name="s"></param>
        /// <param name="stream">Stream to read from.</param>
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
