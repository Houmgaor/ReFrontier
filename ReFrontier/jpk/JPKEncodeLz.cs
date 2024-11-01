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
        private int m_ind;
        private byte[] m_inputBuffer;
        private int m_level = 280;
        private int m_maxdist = 0x300;//0x1fff;
        Stream m_outstream;
        readonly byte[] m_towrite = new byte[1000];
        int m_itowrite;

        /// <summary>
        /// Search for repeated sequences in the input data and returns their length.
        /// </summary>
        /// <param name="inputData">Input data.</param>
        /// <param name="offset">Offset value</param>
        /// <returns>Length of the repeated sequence</returns>
        private unsafe int FindRepetitions(int inputData, out uint offset)
        {
            int nLength = Math.Min(m_level, m_inputBuffer.Length - inputData);
            offset = 0;
            if (inputData == 0 || nLength < 3)
            {
                return 0;
            }
            int ista = inputData < m_maxdist ? 0 : inputData - m_maxdist;
            fixed (byte* pinp = m_inputBuffer)
            {
                byte* startPointer = pinp + ista;
                byte* currentPointer = pinp + inputData;
                int len = 0;
                while (startPointer < currentPointer)
                {
                    int lenw = 0;
                    byte* endPointer = startPointer + nLength;

                    for (byte* pb = startPointer, pb2 = currentPointer; pb < endPointer; pb++, pb2++, lenw++)
                    {
                        if (*pb != *pb2)
                            break;
                    }
                    if (lenw > len && lenw >= 3)
                    {
                        len = lenw;
                        offset = (uint)(currentPointer - startPointer - 1);
                        if (len >= nLength)
                            break;
                    }
                    startPointer++;
                }
                return len;
            }
        }

        private void FlushFlag(bool final)
        {
            if (!final || m_itowrite > 0)
                WriteByte(m_outstream, m_flag);
            m_flag = 0;
            for (int i = 0; i < m_itowrite; i++)
                WriteByte(m_outstream, m_towrite[i]);
            m_itowrite = 0;
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
        /// <param name="inBuffer">Input bytes.</param>
        /// <param name="outStream">Stream to write to.</param>
        /// <param name="level">Compression level. Level will be truncated between 6 and 8191.</param>
        /// <param name="progress">Progress bar object.</param>
        public virtual void ProcessOnEncode(byte[] inBuffer, Stream outStream, int level = 1000, ShowProgress progress = null)
        {
            long perc;
            long percbord = 0;
            progress?.Invoke(0);
            m_shiftIndex = 8;
            m_itowrite = 0;
            m_outstream = outStream;
            m_inputBuffer = inBuffer;
            // Tuncate level between 6 and 280
            m_level = level < 6 ? 6 : level > 280 ? 280 : level;
            // Level between 50 and 0x1fff (8191)
            m_maxdist = level < 50 ? 50 : level > 0x1fff ? 0x1fff : level;
            long perc0 = percbord;
            progress?.Invoke(percbord);
            m_ind = 0;
            while (m_ind < inBuffer.Length)
            {
                perc = percbord + (100 - percbord) * m_ind / inBuffer.Length;
                if (perc > perc0)
                {
                    perc0 = perc;
                    progress?.Invoke(perc);
                }
                int len = FindRepetitions(m_ind, out uint ofs);
                
                if (len == 0)
                {
                    SetFlag(0);
                    m_towrite[m_itowrite++] = inBuffer[m_ind];
                    m_ind++;
                }
                else
                {
                    SetFlag(1);
                    if (len <= 6 && ofs <= 0xff)
                    {
                        SetFlag(0);
                        SetFlagsL((byte)(len - 3), 2);
                        m_towrite[m_itowrite++] = (byte)ofs;
                        m_ind += len;
                    }
                    else
                    {
                        SetFlag(1);
                        ushort u16 = (ushort)ofs;
                        byte hi, lo;
                        if (len <= 9)
                            u16 |= (ushort)((len - 2) << 13);
                        hi = (byte)(u16 >> 8);
                        lo = (byte)(u16 & 0xff);
                        m_towrite[m_itowrite++] = hi;
                        m_towrite[m_itowrite++] = lo;
                        m_ind += len;
                        if (len > 9)
                        {
                            if (len <= 25)
                            {
                                SetFlag(0);
                                SetFlagsL((byte)(len - 10), 4);
                            }
                            else
                            {
                                SetFlag(1);
                                m_towrite[m_itowrite++] = (byte)(len - 0x1a);
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
