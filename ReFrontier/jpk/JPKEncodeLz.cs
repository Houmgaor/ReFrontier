using System;
using System.IO;

namespace ReFrontier.jpk
{
    /// <summary>
    /// Variant of the LZ77 compression algorithm.
    /// </summary>
    class JPKEncodeLz : IJPKEncode
    {
        private byte m_flag;

        /// <summary>
        /// Counter between 0 and 8 that indicate a shift (when negative).
        /// </summary>
        private int m_shiftIndex;

        /// <summary>
        /// Index in m_inputBuffer
        /// </summary>
        private int m_bufferIndex;

        /// <summary>
        /// Buffer of data to compress.
        /// </summary>
        private byte[] m_inputBuffer;

        /// <summary>
        /// Compression level, between 280 and 8191 (0x1fff)
        /// </summary>
        private int m_compressionLevel = 280;

        /// <summary>
        /// Maximum index distance in which to find repetitions.
        /// 
        /// Max value is 0x1fff
        /// </summary>
        private readonly int m_maxIndexDist = 0x300;

        /// <summary>
        /// Stream to write data to.
        /// </summary>
        Stream m_outStream;

        /// <summary>
        /// Temporary buffer of data to write to 
        /// </summary>
        readonly byte[] m_toWrite = new byte[1000];

        /// <summary>
        /// Index in <cref>m_toWrite</cref>
        /// </summary>
        int m_indexToWrite;

        /// <summary>
        /// Search for the longest repeated sequence in the input data and returns its length.
        /// </summary>
        /// <param name="inputDataIndex">Index in the input data buffer.</param>
        /// <param name="offset">Position of the sequence in the input.</param>
        /// <returns>Length of the longest repeated sequence.</returns>
        private int LongestRepetition(int inputDataIndex, out uint offset)
        {
            // Limit length compression level, truncate if the length is above remaining data length
            int lengthThreshold = Math.Min(m_compressionLevel, m_inputBuffer.Length - inputDataIndex);
            offset = 0;
            if (inputDataIndex == 0 || lengthThreshold < 3)
            {
                return 0;
            }
            // Start position to find a repeated element, minimum is 0 
            int inputStart = Math.Max(inputDataIndex - m_maxIndexDist, 0);
            
            // Translation of <cref>inputDataIndex</cref> in pointers
            int maxLength = 0;
            // Start identical sequence search
            for (int leftIterator = inputStart; leftIterator < inputDataIndex; leftIterator++)
            {
                int currentLength = 0;

                for (int i = 0; i < lengthThreshold; i++)
                {
                    if (m_inputBuffer[leftIterator + i] != m_inputBuffer[inputDataIndex + i])
                        break;
                    currentLength++;
                }

                // Check if the length is longer than the previous one
                if (currentLength > maxLength && currentLength >= 3)
                {
                    maxLength = currentLength;
                    offset = (uint)(inputDataIndex - leftIterator - 1);
                    // Stop the algorithm if above the length limit
                    if (maxLength >= lengthThreshold)
                        break;
                }
            }
            return maxLength;
        }


        /// <summary>
        /// Write data and flag to the stream.
        /// </summary>
        /// <param name="final">If true, prevent writing flags.</param>
        private void FlushFlag(bool final)
        {
            // Write current flag to the stream, and reset it
            if (!final || m_indexToWrite > 0)
                WriteByte(m_outStream, m_flag);
            m_flag = 0;
            // Write temporary data buffer
            for (int i = 0; i < m_indexToWrite; i++)
                WriteByte(m_outStream, m_toWrite[i]);
            m_indexToWrite = 0;
        }

        /// <summary>
        /// Accumulate data in <cref>m_flag</cref> and flush every 8 bytes.
        /// </summary>
        /// <param name="value">Byte to write.</param>
        private void SetFlag(byte value)
        {
            m_shiftIndex--;
            // On eigth byte, write to stream
            if (m_shiftIndex < 0)
            {
                m_shiftIndex = 7;
                FlushFlag(false);
            }
            // Accumulate in <cref>m_flag</cref>
            m_flag |= (byte)(value << m_shiftIndex);
        }

        /// <summary>
        /// Write byte to flag in reverse order.
        /// </summary>
        /// <param name="value">Byte to write.</param>
        /// <param name="count">Initial index of the byte to write.</param>
        private void SetFlagsReverse(byte value, int count)
        {
            for (int i = count - 1; i >= 0; i--)
            {
                SetFlag((byte)((value >> i) & 1));
            }
        }

        /// <summary>
        /// Compress the file on the fly.
        /// </summary>
        /// <param name="inBuffer">Input bytes buffer.</param>
        /// <param name="outStream">Stream to write to.</param>
        /// <param name="level">Compression level. Level will be truncated between 6 and 8191.</param>
        /// <param name="progress">Progress bar object.</param>
        public virtual void ProcessOnEncode(byte[] inBuffer, Stream outStream, int level = 1000, ShowProgress progress = null)
        {
            long perc;
            long percbord = 0;
            progress?.Invoke(0);
            m_shiftIndex = 8;
            m_indexToWrite = 0;
            m_outStream = outStream;
            m_inputBuffer = inBuffer;
            // Tuncate level between 6 and 280
            m_compressionLevel = Math.Min(Math.Max(level, 6), 280);
            // Level between 50 and 0x1fff (8191)
            m_compressionLevel = Math.Min(Math.Max(level, 50), 0x1fff);
            long perc0 = percbord;
            progress?.Invoke(percbord);
            m_bufferIndex = 0;
            while (m_bufferIndex < inBuffer.Length)
            {
                perc = percbord + (100 - percbord) * m_bufferIndex / inBuffer.Length;
                if (perc > perc0)
                {
                    perc0 = perc;
                    progress?.Invoke(perc);
                }
                int repetitionLength = LongestRepetition(m_bufferIndex, out uint repetitionOffset);
                
                if (repetitionLength == 0)
                {
                    SetFlag(0);
                    m_toWrite[m_indexToWrite++] = inBuffer[m_bufferIndex];
                    m_bufferIndex++;
                }
                else
                {
                    SetFlag(1);
                    if (repetitionLength <= 6 && repetitionOffset <= 0xff)
                    {
                        SetFlag(0);
                        SetFlagsReverse((byte)(repetitionLength - 3), 2);
                        m_toWrite[m_indexToWrite++] = (byte)repetitionOffset;
                        m_bufferIndex += repetitionLength;
                    }
                    else
                    {
                        SetFlag(1);
                        ushort u16 = (ushort)repetitionOffset;
                        // Check if repetitionLength is below 3 bits
                        if (repetitionLength <= 9)
                            u16 |= (ushort)((repetitionLength - 2) << 13);
                        m_toWrite[m_indexToWrite++] = (byte)(u16 >> 8);
                        m_toWrite[m_indexToWrite++] = (byte)(u16 & 0xff);
                        m_bufferIndex += repetitionLength;
                        if (repetitionLength > 9)
                        {
                            if (repetitionLength <= 25)
                            {
                                SetFlag(0);
                                SetFlagsReverse((byte)(repetitionLength - 10), 4);
                            }
                            else
                            {
                                SetFlag(1);
                                m_toWrite[m_indexToWrite++] = (byte)(repetitionLength - 0x1a);
                            }
                        }
                    }
                }
            }
            FlushFlag(true);
            progress?.Invoke(100);
        }

        /// <summary>
        /// Write a single byte directly from inputByte to stream.
        /// </summary>
        /// <param name="stream">Stream to write to</param>
        /// <param name="inputByte">byte to write</param>
        public virtual void WriteByte(Stream stream, byte inputByte)
        {
            stream.WriteByte(inputByte);
        }
    }
}
