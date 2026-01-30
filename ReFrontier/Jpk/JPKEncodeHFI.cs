using System;
using System.IO;
using System.Linq;

namespace ReFrontier.Jpk
{
    /// <summary>
    /// Huffman + LZ77 compression encoder for Monster Hunter Frontier JPK files (type 4: HFI).
    ///
    /// <para><b>Algorithm Overview:</b></para>
    /// <para>Combines Huffman coding with LZ77 compression. First applies LZ77 to find
    /// repeated sequences, then encodes the output bytes using a Huffman tree for
    /// additional compression of frequently-occurring byte values.</para>
    ///
    /// <para><b>Output Format:</b></para>
    /// <list type="bullet">
    ///   <item>2 bytes: Huffman table length (0x1FE = 510 entries)</item>
    ///   <item>510 × 2 bytes: Huffman tree table (1020 bytes)</item>
    ///   <item>Variable: Huffman-encoded LZ77 data stream</item>
    /// </list>
    ///
    /// <para><b>Huffman Tree Structure:</b></para>
    /// <para>The tree is stored as an array where:</para>
    /// <list type="bullet">
    ///   <item>Indices 0-255: Leaf nodes (actual byte values, randomly shuffled)</item>
    ///   <item>Indices 256-509: Internal nodes pointing to children at (value-256)*2 and (value-256)*2+1</item>
    /// </list>
    ///
    /// <para><b>Encoding Process:</b></para>
    /// <para>Each byte from the LZ77 stage is replaced by its Huffman code (variable-length
    /// bit sequence determined by traversing the tree from root to leaf).</para>
    /// </summary>
    internal class JPKEncodeHFI : JPKEncodeLz
    {
        /// <summary>
        /// Number of possible byte values (0-255).
        /// </summary>
        private const int m_headerLength = 0x100;

        /// <summary>
        /// Huffman table length: 256 leaves + 254 internal nodes = 510 entries.
        /// </summary>
        protected static readonly short m_hfTableLen = 0x1fe;

        /// <summary>
        /// Huffman tree table. Values 0-255 are leaves, 256+ are internal nodes.
        /// </summary>
        protected readonly short[] m_hfTable = new short[m_hfTableLen];

        /// <summary>
        /// Huffman code paths for each byte value (0-255). The path is the bit sequence
        /// to reach that leaf from the root.
        /// </summary>
        private readonly short[] m_paths = new short[m_headerLength];

        /// <summary>
        /// Bit lengths for each Huffman code (how many bits in m_paths are valid).
        /// </summary>
        private readonly short[] m_lengths = new short[m_headerLength];

        private int m_depth = 0;
        private byte m_bits = 0;
        private int m_bitcount = 0;

        private void GetPaths(int strt, int level, int pth)
        {
            int maxlev = 30;
            if (level >= maxlev) return;
            if (level >= m_depth) m_depth = level;

            if (strt < m_hfTableLen)
            {
                int val = m_hfTable[strt];
                if (val < m_headerLength)
                {
                    m_paths[val] = (short)pth;
                    m_lengths[val] = (short)level;
                    return;
                }
                strt = val;
            }
            GetPaths(2 * (strt - m_headerLength), level + 1, pth << 1);
            GetPaths(2 * (strt - m_headerLength) + 1, level + 1, (pth << 1) | 1);
        }

        protected void FillTable()
        {
            Array.Clear(m_paths, 0, m_paths.Length);
            Array.Clear(m_lengths, 0, m_lengths.Length);
            short[] rndseq = new short[m_headerLength];
            for (short i = 0; i < rndseq.Length; i++) rndseq[i] = i;
            Random rnd = new();
            rndseq = [.. rndseq.OrderBy(x => rnd.Next())];
            for (int i = 0; i < m_headerLength; i++)
                m_hfTable[i] = rndseq[i];
            for (int i = m_headerLength; i < m_hfTableLen; i++)
                m_hfTable[i] = (short)i;

            GetPaths(m_hfTableLen, 0, 0);
        }


        /// <summary>
        /// Compress the file based on the LZ compression.
        /// </summary>
        /// <param name="inBuffer">Input bytes buffer.</param>
        /// <param name="outStream">Stream to write to.</param>
        /// <param name="level">Compression level. Level will be truncated between 6 and 8191.</param>
        public override void ProcessOnEncode(byte[] inBuffer, Stream outStream, int level = 16)
        {
            FillTable();
            BinaryWriter br = new(outStream);
            br.Write(m_hfTableLen);
            for (int i = 0; i < m_hfTableLen; i++)
                br.Write(m_hfTable[i]);
            base.ProcessOnEncode(inBuffer, outStream, level);
            FlushWrite(outStream);
        }

        private void WriteBit(Stream outStream, byte inByte)
        {
            if (m_bitcount == 8)
            {
                outStream.WriteByte(m_bits);
                m_bits = 0;
                m_bitcount = 0;
            }
            m_bits <<= 1;
            m_bits |= inByte;
            m_bitcount++;
        }

        private void WriteBits(Stream outStream, short bits, short length)
        {
            while (length > 0)
            {
                length--;
                WriteBit(outStream, (byte)((bits >> length) & 1));
            }
        }
        protected void FlushWrite(Stream outStream)
        {
            if (m_bitcount > 0)
            {
                m_bits <<= 8 - m_bitcount;
                outStream.WriteByte(m_bits);
            }
        }

        /// <summary>
        /// Write a single byte from <paramref name="inByte"/> to <paramref name="outStream"/>.
        /// </summary>
        /// <param name="outStream">Stream to write to</param>
        /// <param name="inByte">byte to write</param>
        public override void WriteByte(Stream outStream, byte inByte)
        {
            short bits = m_paths[inByte];
            short len = m_lengths[inByte];
            WriteBits(outStream, bits, len);
        }
    }
}
