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
        private int m_shiftIndex;
        /// <summary>
        /// Index in m_inputBuffer
        /// </summary>
        private int m_index;

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

        Stream m_outStream;
        readonly byte[] m_toWrite = new byte[1000];
        /// <summary>
        /// Index in <code>m_towrite</code>
        /// </summary>
        int m_indexToWrite;

        /// <summary>
        /// Search for the longest repeated sequence in the input data and returns its length.
        /// </summary>
        /// <param name="inputDataIndex">Index in the input data buffer.</param>
        /// <param name="offset">Offset value</param>
        /// <returns>Length of the longest repeated sequence.</returns>
        private unsafe int LongestRepetition(int inputDataIndex, out uint offset)
        {
            int nLength = Math.Min(m_compressionLevel, m_inputBuffer.Length - inputDataIndex);
            offset = 0;
            if (inputDataIndex == 0 || nLength < 3)
            {
                return 0;
            }
            int inputStart = Math.Max(0, inputDataIndex - m_maxIndexDist);
            fixed (byte* inputBufferPointer = m_inputBuffer)
            {
                byte* currentPointer = inputBufferPointer + inputDataIndex;
                int maxLength = 0;
                for (byte* startPointer = inputBufferPointer + inputStart; startPointer < currentPointer; startPointer++)
                {
                    int currentLength = 0;
                    byte* endPointer = startPointer + nLength;

                    for (byte* pb = startPointer, pb2 = currentPointer; pb < endPointer; pb++, pb2++)
                    {
                        if (*pb != *pb2)
                            break;
                        currentLength++;
                    }
                    if (currentLength > maxLength && currentLength >= 3)
                    {
                        maxLength = currentLength;
                        offset = (uint)(currentPointer - startPointer - 1);
                        if (maxLength >= nLength)
                            break;
                    }
                }
                return maxLength;
            }
        }

        private void FlushFlag(bool final)
        {
            if (!final || m_indexToWrite > 0)
                WriteByte(m_outStream, m_flag);
            m_flag = 0;
            for (int i = 0; i < m_indexToWrite; i++)
                WriteByte(m_outStream, m_toWrite[i]);
            m_indexToWrite = 0;
        }

        private void SetFlag(byte b)
        {
            m_shiftIndex--;
            if (m_shiftIndex < 0)
            {
                m_shiftIndex = 7;
                FlushFlag(false);
            }
            m_flag |= (byte)(b << m_shiftIndex);
        }

        private void SetFlagsL(byte b, int cnt)
        {
            for (int i = cnt - 1; i >= 0; i--)
            {
                SetFlag((byte)((b >> i) & 1));
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
            m_index = 0;
            while (m_index < inBuffer.Length)
            {
                perc = percbord + (100 - percbord) * m_index / inBuffer.Length;
                if (perc > perc0)
                {
                    perc0 = perc;
                    progress?.Invoke(perc);
                }
                int maxLength = LongestRepetition(m_index, out uint offset);
                
                if (maxLength == 0)
                {
                    SetFlag(0);
                    m_toWrite[m_indexToWrite++] = inBuffer[m_index];
                    m_index++;
                }
                else
                {
                    SetFlag(1);
                    if (maxLength <= 6 && offset <= 0xff)
                    {
                        SetFlag(0);
                        SetFlagsL((byte)(maxLength - 3), 2);
                        m_toWrite[m_indexToWrite++] = (byte)offset;
                        m_index += maxLength;
                    }
                    else
                    {
                        SetFlag(1);
                        ushort u16 = (ushort)offset;
                        byte hi, lo;
                        if (maxLength <= 9)
                            u16 |= (ushort)((maxLength - 2) << 13);
                        hi = (byte)(u16 >> 8);
                        lo = (byte)(u16 & 0xff);
                        m_toWrite[m_indexToWrite++] = hi;
                        m_toWrite[m_indexToWrite++] = lo;
                        m_index += maxLength;
                        if (maxLength > 9)
                        {
                            if (maxLength <= 25)
                            {
                                SetFlag(0);
                                SetFlagsL((byte)(maxLength - 10), 4);
                            }
                            else
                            {
                                SetFlag(1);
                                m_toWrite[m_indexToWrite++] = (byte)(maxLength - 0x1a);
                            }
                        }
                    }
                }
            }
            FlushFlag(true);
            progress?.Invoke(100);
        }

        /// <summary>
        /// Write a single byte directly.
        /// </summary>
        /// <param name="stream">Stream to write to</param>
        /// <param name="inputByte">byte to write</param>
        public virtual void WriteByte(Stream stream, byte inputByte)
        {
            stream.WriteByte(inputByte);
        }
    }
}
