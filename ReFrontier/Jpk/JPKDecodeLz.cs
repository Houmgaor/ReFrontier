using System;
using System.IO;

namespace ReFrontier.Jpk
{
    /// <summary>
    /// Base class for LZ decompression.
    /// </summary>
    internal class JPKDecodeLz : IJPKDecode
    {
        private int m_shiftIndex = 0;
        private byte m_flag = 0;

        /// <summary>
        /// Copy length bytes to buffer at position index.
        /// Bytes are copied from position index - offset - 1.
        /// </summary>
        /// <param name="buffer">Buffer to rewrite</param>
        /// <param name="offset">Offset position to the left.</param>
        /// <param name="length">Number of bytes to write.</param>
        /// <param name="index">Initial position to start copying bytes.</param>
        private static int JpkCopyLz(byte[] buffer, int offset, int length, int index)
        {
            int noOverlapSpan = Math.Min(length, offset);
            // Copy in block
            Buffer.BlockCopy(buffer, index - offset - 1, buffer, index, noOverlapSpan);
            // Add repeated elements
            for (int i = index + noOverlapSpan; i < length + index; i++)
            {
                buffer[i] = buffer[i - offset - 1];
            }
            return length;
        }

        /// <summary>
        /// Return the value of the next byte from stream.
        /// </summary>
        /// <param name="s">Input stream</param>
        /// <returns>If byte is true or not</returns>
        private bool JpkBitLz(Stream s)
        {
            if (m_shiftIndex <= 0)
            {
                m_shiftIndex = 7;
                m_flag = ReadByte(s);
            }
            else
            {
                m_shiftIndex--;
            }
            return ((m_flag >> m_shiftIndex) & 1) == 1;
        }

        /// <summary>
        /// JPK decompression, implements JpkDecLz
        /// </summary>
        /// <param name="inStream">Stream to read from.</param>
        /// <param name="outBuffer">Buffer of decompressed data to write to.</param>
        public virtual void ProcessOnDecode(Stream inStream, byte[] outBuffer)
        {
            int outIndex = 0;
            while (inStream.Position < inStream.Length && outIndex < outBuffer.Length)
            {
                if (!JpkBitLz(inStream))
                {
                    outBuffer[outIndex++] = ReadByte(inStream);
                    continue;
                }

                int length, offset;

                if (!JpkBitLz(inStream))
                {
                    // Case 0
                    length = (JpkBitLz(inStream) ? 2 : 0) + (JpkBitLz(inStream) ? 1 : 0);
                    offset = ReadByte(inStream);
                    outIndex += JpkCopyLz(outBuffer, offset, length + 3, outIndex);
                    continue;
                }

                byte hi = ReadByte(inStream);
                byte lo = ReadByte(inStream);
                length = (hi & 0xE0) >> 5;
                offset = ((hi & 0x1F) << 8) | lo;
                if (length != 0)
                {
                    // Case 1, use length directly 
                    outIndex += JpkCopyLz(outBuffer, offset, length + 2, outIndex);
                    continue;
                }

                if (!JpkBitLz(inStream))
                {
                    // Case 2, compute bytes to copy length
                    length = 0;
                    for (int i = 3; i > -1; i--)
                        length += JpkBitLz(inStream) ? 1 << i : 0;
                    outIndex += JpkCopyLz(outBuffer, offset, length + 2 + 8, outIndex);
                    continue;
                }

                byte temp = ReadByte(inStream);
                if (temp == 0xFF)
                {
                    // Case 3
                    for (int i = 0; i < offset + 0x1B; i++)
                        outBuffer[outIndex++] = ReadByte(inStream);
                    continue;
                }
                // Case 4
                outIndex += JpkCopyLz(outBuffer, offset, temp + 0x1a, outIndex);
            }
        }

        /// <summary>
        /// Read a single byte from the stream at the current position.
        /// </summary>
        /// <param name="stream">Stream to read from.</param>
        /// <returns>Read byte.</returns>
        /// <exception cref="EndOfStreamException">Exception when end of file is reached.</exception>
        public virtual byte ReadByte(Stream stream)
        {
            int value = stream.ReadByte();
            if (value < 0)
                throw new EndOfStreamException("Reached end of file too early!");
            return (byte)value;
        }
    }
}
