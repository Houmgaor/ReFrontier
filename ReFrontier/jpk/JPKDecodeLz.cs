using System;
using System.IO;

namespace ReFrontier.jpk
{
    class JPKDecodeLz : IJPKDecode
    {
        private int m_shiftIndex = 0;
        private byte m_flag = 0;

        private static void Jpkcpy_lz(byte[] outBuffer, int offset, int length, ref int index)
        {
            for (int i = 0; i < length; i++, index++)
            {
                outBuffer[index] = outBuffer[index - offset - 1];
            }
        }

        private byte Jpkbit_lz(Stream s)
        {
            m_shiftIndex--;
            if (m_shiftIndex < 0)
            {
                m_shiftIndex = 7;
                m_flag = ReadByte(s);
            }
            return (byte)((m_flag >> m_shiftIndex) & 1);
        }

        public virtual void ProcessOnDecode(Stream inStream, byte[] outBuffer)//implements jpkdec_lz
        {
            int outIndex = 0;
            while (inStream.Position < inStream.Length && outIndex < outBuffer.Length)
            {
                if (Jpkbit_lz(inStream) == 0)
                {
                    outBuffer[outIndex++] = ReadByte(inStream);
                    continue;
                }
                
                if (Jpkbit_lz(inStream) == 0)
                {
                    // Case 0
                    byte length = (byte)((Jpkbit_lz(inStream) << 1) | Jpkbit_lz(inStream));
                    byte offset = ReadByte(inStream);
                    Jpkcpy_lz(outBuffer, offset, length + 3, ref outIndex);
                    continue;
                }

                byte hi = ReadByte(inStream);
                byte lo = ReadByte(inStream);
                int len = (hi & 0xE0) >> 5;
                int off = ((hi & 0x1F) << 8) | lo;
                if (len != 0)
                {
                    // Case 1
                    Jpkcpy_lz(outBuffer, off, len + 2, ref outIndex);
                    continue;
                }

                if (Jpkbit_lz(inStream) == 0)
                {
                    // Case 2
                    len = (byte)((Jpkbit_lz(inStream) << 3) | (Jpkbit_lz(inStream) << 2) | (Jpkbit_lz(inStream) << 1) | Jpkbit_lz(inStream));
                    Jpkcpy_lz(outBuffer, off, len + 2 + 8, ref outIndex);
                    continue;
                }

                byte temp = ReadByte(inStream);
                if (temp == 0xFF)
                {
                    // Case 3
                    for (int i = 0; i < off + 0x1B; i++)
                        outBuffer[outIndex++] = ReadByte(inStream);
                    continue;
                }
                // Case 4
                Jpkcpy_lz(outBuffer, off, temp + 0x1a, ref outIndex);
            }
        }

        public virtual byte ReadByte(Stream s)
        {
            int value = s.ReadByte();
            if (value < 0)
                throw new NotImplementedException();
            return (byte)value;
        }
    }
}
