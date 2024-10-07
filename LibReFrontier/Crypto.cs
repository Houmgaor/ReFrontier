using Force.Crc32;
using System;

namespace LibReFrontier
{
    // With major help from enler
    public class Crypto
    {
        static uint LoadUInt32BE(byte[] buffer, int offset)
        {
            uint value = (uint)((buffer[offset] << 24) | (buffer[offset + 1] << 16) | (buffer[offset + 2] << 8) | buffer[offset + 3]);
            return value;
        }

        // from addr 0x10292DCC
        static readonly byte[] rndBufEcd = [ 0x4A, 0x4B, 0x52, 0x2E, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x0D,
            0xCD, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x0D, 0xCD, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x0D, 0xCD,
            0x00, 0x00, 0x00, 0x01, 0x00, 0x19, 0x66, 0x0D, 0x00, 0x00, 0x00, 0x03, 0x7D, 0x2B, 0x89, 0xDD, 0x00,
            0x00, 0x00, 0x01 ];

        // from addr 0x1025F4E0
        static readonly byte[] rndBufExf = [ 0x4A, 0x4B, 0x52, 0x2E, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x0D,
            0xCD, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x0D, 0xCD, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x0D, 0xCD,
            0x00, 0x00, 0x00, 0x01, 0x02, 0xE9, 0x0E, 0xDD, 0x00, 0x00, 0x00, 0x03 ];

        static uint getRndEcd(int index, ref uint rnd)
        {
            rnd = rnd * LoadUInt32BE(rndBufEcd, 8 * index) + LoadUInt32BE(rndBufEcd, 8 * index + 4);
            return rnd;
        }

        static byte[] CreateXorkeyExf(byte[] header)
        {
            byte[] keyBuffer = new byte[16];
            int index = BitConverter.ToUInt16(header, 4);
            uint tempVal = BitConverter.ToUInt32(header, 0xc);
            uint value = BitConverter.ToUInt32(header, 0xc);
            for (int i = 0; i < 4; i++)
            {
                tempVal = tempVal * LoadUInt32BE(rndBufExf, index * 8) + LoadUInt32BE(rndBufExf, index * 8 + 4);
                uint key = tempVal ^ value;
                byte[] tempKey = BitConverter.GetBytes(key);
                Array.Copy(tempKey, 0, keyBuffer, i * 4, 4);
            }
            return keyBuffer;
        }

        public static void decEcd(byte[] buffer)
        {
            uint fsize = BitConverter.ToUInt32(buffer, 8);
            uint crc32 = BitConverter.ToUInt32(buffer, 12);
            int index = BitConverter.ToUInt16(buffer, 4);
            uint rnd = (crc32 << 16) | (crc32 >> 16) | 1;

            uint xorpad = getRndEcd(index, ref rnd);

            byte r8 = (byte)xorpad;

            for (int i = 0; i < fsize; i++)
            {
                xorpad = getRndEcd(index, ref rnd);

                byte data = buffer[0x10 + i];
                uint r11 = (uint)(data ^ r8);
                uint r12 = (r11 >> 4) & 0xFF;
                for (int j = 0; j < 8; j++)
                {
                    uint r10 = xorpad ^ r11;
                    r11 = r12;
                    r12 = r12 ^ r10;
                    r12 = r12 & 0xFF;
                    xorpad = xorpad >> 4;
                }

                r8 = (byte)((r12 & 0xF) | ((r11 & 0xF) << 4));
                buffer[0x10 + i] = r8;
            }
        }

        public static byte[] encEcd(byte[] buffer, byte[] bufferMeta)
        {
            // Update meta data
            int fsize = buffer.Length;
            uint crc32w = Crc32Algorithm.Compute(buffer);
            int index = BitConverter.ToUInt16(bufferMeta, 4);

            // Write meta data
            byte[] buf = new byte[16 + fsize];
            byte[] bufnum;
            Array.Copy(bufferMeta, buf, bufferMeta.Length);
            bufnum = BitConverter.GetBytes(fsize);
            Array.Copy(bufnum, 0, buf, 8, 4);
            bufnum = BitConverter.GetBytes(crc32w);
            Array.Copy(bufnum, 0, buf, 12, 4);

            // Fill data with nullspace
            int i;
            for (i = 16 + fsize; i < buf.Length; i++) buf[i] = 0;

            // Encrypt data
            uint rnd = (crc32w << 16) | (crc32w >> 16) | 1;
            uint xorpad = getRndEcd(index, ref rnd);
            byte r8 = (byte)xorpad;

            for (i = 0; i < fsize; i++)
            {
                xorpad = getRndEcd(index, ref rnd);
                byte data = buffer[i];
                uint r11 = 0;
                uint r12 = 0;
                for (int j = 0; j < 8; j++)
                {
                    uint r10 = xorpad ^ r11;
                    r11 = r12;
                    r12 ^= r10;
                    r12 = r12 & 0xFF;
                    xorpad = xorpad >> 4;
                }

                uint dig2 = data;
                uint dig1 = (dig2 >> 4) & 0xFF;
                dig1 ^= r11;
                dig2 ^= r12;
                dig1 ^= dig2;

                byte rr = (byte)((dig2 & 0xF) | ((dig1 & 0xF) << 4));
                rr = (byte)(rr ^ r8);
                buf[16 + i] = rr;
                r8 = data;
            }
            return buf;
        }

        public static void decExf(byte[] buffer)
        {
            byte[] header = new byte[16];
            Array.Copy(buffer, header, header.Length);
            if (BitConverter.ToUInt32(header, 0) == 0x1a667865)
            {
                byte[] keybuf = CreateXorkeyExf(header);
                for (int i = 16; i < buffer.Length - header.Length; i++)
                {
                    uint r28 = (uint)(i - 0x10);
                    byte r8 = buffer[i];
                    int index = (int)(r28 & 0xf);
                    uint r4 = r8 ^ r28;
                    uint r12 = keybuf[index];
                    uint r0 = (r4 & 0xf0) >> 4;
                    uint r7 = keybuf[r0];
                    uint r9 = r4 >> 4;
                    uint r5 = r7 >> 4;
                    r9 = r9 ^ r12;
                    uint r26 = r5 ^ r4;
                    r26 = (uint)(r26 & ~0xf0) | ((r9 & 0xf) << 4);
                    buffer[i] = (byte)r26;
                }
            }
        }
    }
}
