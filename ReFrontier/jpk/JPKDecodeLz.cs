using System;
using System.IO;

namespace ReFrontier.jpk
{
    /// <summary>
    /// Base class for LZ decompression.
    /// </summary>
    class JPKDecodeLz : IJPKDecode
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
        /// <param name="index">Initial position.</param>
        private static void JpkCopyLz(byte[] buffer, int offset, int length, ref int index)
        {
            for (int i = 0; i < length; i++)
            {
                buffer[index] = buffer[index - offset - 1];
                index++;
            }
        }

        private byte JpkBitLz(Stream s)
        {
            m_shiftIndex--;
            if (m_shiftIndex < 0)
            {
                m_shiftIndex = 7;
                m_flag = ReadByte(s);
            }
            return (byte)((m_flag >> m_shiftIndex) & 1);
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
                if (JpkBitLz(inStream) == 0)
                {
                    outBuffer[outIndex++] = ReadByte(inStream);
                    continue;
                }
                
                int length, offset;

                if (JpkBitLz(inStream) == 0)
                {
                    // Case 0
                    length = (byte)((JpkBitLz(inStream) << 1) | JpkBitLz(inStream));
                    offset = ReadByte(inStream);
                    JpkCopyLz(outBuffer, offset, length + 3, ref outIndex);
                    continue;
                }

                byte hi = ReadByte(inStream);
                byte lo = ReadByte(inStream);
                length = (hi & 0xE0) >> 5;
                offset = ((hi & 0x1F) << 8) | lo;
                if (length != 0)
                {
                    // Case 1, use length directly 
                    JpkCopyLz(outBuffer, offset, length + 2, ref outIndex);
                    continue;
                }

                if (JpkBitLz(inStream) == 0)
                {
                    // Case 2, compute bytes to copy length
                    length = (byte)((JpkBitLz(inStream) << 3) | (JpkBitLz(inStream) << 2) | (JpkBitLz(inStream) << 1) | JpkBitLz(inStream));
                    JpkCopyLz(outBuffer, offset, length + 2 + 8, ref outIndex);
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
                JpkCopyLz(outBuffer, offset, temp + 0x1a, ref outIndex);
            }
        }

        /// <summary>
        /// Read a single byte from the stream at the current position.
        /// </summary>
        /// <param name="stream">Stream to read from.</param>
        /// <returns>Read byte.</returns>
        /// <exception cref="NotImplementedException">Exception when end of file is reached.</exception>
        public virtual byte ReadByte(Stream stream)
        {
            int value = stream.ReadByte();
            if (value < 0)
                throw new NotImplementedException("Reached end of file too early!");
            return (byte)value;
        }
    }
}
