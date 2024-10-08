using System;
using System.IO;
using System.Linq;

namespace ReFrontier.jpk
{
    class JPKEncodeHFI : JPKEncodeLz
    {
        private static readonly short m_hfTableLen = 0x1fe;
        private readonly short[] m_hfTable = new short[m_hfTableLen];
        private readonly short[] m_Paths = new short[0x100];
        private readonly short[] m_Lengths = new short[0x100];

        private int m_filled = 0;
        private int m_depth = 0;
        private void GetPaths(int strt, int lev, int pth)
        {
            int maxlev = 30;
            if (lev >= maxlev) return;
            if (lev >= m_depth) m_depth = lev;
            
            if (strt < m_hfTableLen)
            {
                int val = m_hfTable[strt];
                if (val < 0x100)
                {
                    m_Paths[val] = (short)pth;
                    m_Lengths[val] = (short)lev;
                    m_filled++;
                    return;
                }
                strt = val;
            }
            GetPaths(2 * (strt - 0x100), lev + 1, pth << 1);
            GetPaths(2 * (strt - 0x100) + 1, lev + 1, (pth << 1) | 1);
        }
        private void FillTable()
        {
            Array.Clear(m_Paths, 0, m_Paths.Length);
            Array.Clear(m_Lengths, 0, m_Lengths.Length);
            short[] rndseq = new short[0x100];
            for (short i = 0; i < rndseq.Length; i++) rndseq[i] = i;
            Random rnd = new();
            rndseq = [.. rndseq.OrderBy(x => rnd.Next())];
            for (int i = 0; i < 0x100; i++) m_hfTable[i] = rndseq[i];
            for (int i = 0x100; i < m_hfTableLen; i++) m_hfTable[i] = (short)i;
            
            GetPaths(m_hfTableLen, 0, 0);
        }

        public override void ProcessOnEncode(byte[] inBuffer, Stream outStream, int level = 16, ShowProgress progress = null)
        {
            FillTable();
            BinaryWriter br = new(outStream);
            br.Write(m_hfTableLen);
            for (int i = 0; i < m_hfTableLen; i++) br.Write(m_hfTable[i]);
            base.ProcessOnEncode(inBuffer, outStream, level, progress);
            FlushWrite(outStream);
        }

        private byte m_bits = 0;
        private int m_bitcount = 0;
        private void WriteBit(Stream s, byte b)
        {
            if (m_bitcount == 8)
            {
                s.WriteByte(m_bits);
                m_bits = 0;
                m_bitcount = 0;
            }
            m_bits <<= 1;
            m_bits |= b;
            m_bitcount++;
        }
        private void WriteBits(Stream s, short bits, short len)
        {
            while (len > 0)
            {
                len--;
                WriteBit(s, (byte)((bits >> len) & 1));
            }
        }
        private void FlushWrite(Stream s)
        {
            if (m_bitcount > 0)
            {
                m_bits <<= 8 - m_bitcount;
                s.WriteByte(m_bits);
            }
        }
        public override void WriteByte(Stream s, byte b)
        {
            short bits = m_Paths[b];
            short len = m_Lengths[b];
            WriteBits(s, bits, len);
        }
    }
}
