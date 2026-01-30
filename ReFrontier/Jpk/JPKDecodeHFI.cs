using System.IO;

namespace ReFrontier.Jpk
{
    /// <summary>
    /// Huffman + LZ77 decompression decoder for Monster Hunter Frontier JPK files (type 4: HFI).
    ///
    /// <para><b>Algorithm Overview:</b></para>
    /// <para>Decodes data compressed with Huffman + LZ77 encoding. First reads the Huffman
    /// tree from the file header, then decodes each byte by traversing the tree based on
    /// input bits, finally applying LZ77 decompression to reconstruct the original data.</para>
    ///
    /// <para><b>Input Format:</b></para>
    /// <list type="bullet">
    ///   <item>2 bytes: Huffman table length (typically 0x1FE = 510)</item>
    ///   <item>510 × 2 bytes: Huffman tree table</item>
    ///   <item>Variable: Huffman-encoded LZ77 data stream</item>
    /// </list>
    ///
    /// <para><b>Decoding Process:</b></para>
    /// <para>For each byte needed by the LZ77 decoder:</para>
    /// <list type="number">
    ///   <item>Start at tree root (index = table length)</item>
    ///   <item>Read one bit from the data stream</item>
    ///   <item>Navigate to left child (bit=0) or right child (bit=1)</item>
    ///   <item>Repeat until reaching a leaf node (value &lt; 256)</item>
    ///   <item>Output the leaf value as the decoded byte</item>
    /// </list>
    ///
    /// <para><b>Stream Layout:</b></para>
    /// <para>The file has two interleaved streams: the Huffman tree (sequential) and
    /// the bit stream (at m_hfDataOffset). ReadByte alternates between seeking to
    /// read tree nodes and seeking to read data bits.</para>
    /// </summary>
    internal class JPKDecodeHFI : JPKDecodeLz
    {
        /// <summary>
        /// Current byte from the Huffman-encoded data stream.
        /// </summary>
        private byte m_flagHF = 0;

        /// <summary>
        /// Current bit position within m_flagHF (7 = MSB, -1 = need new byte).
        /// </summary>
        private int m_flagShift = 0;

        /// <summary>
        /// File offset where the Huffman tree table begins.
        /// </summary>
        private int m_hfTableOffset = 0;

        /// <summary>
        /// File offset where the Huffman-encoded data stream begins (after the tree).
        /// </summary>
        private int m_hfDataOffset = 0;

        /// <summary>
        /// Number of entries in the Huffman table (typically 510).
        /// </summary>
        private int m_hfTableLen = 0;

        /// <summary>
        /// Initialize the Huffman table parameters from the stream.
        /// Must be called before ReadByte() is used.
        /// </summary>
        /// <param name="inStream">Stream to read table info from.</param>
        protected void InitializeTable(Stream inStream)
        {
            BinaryReader br = new(inStream);
            m_hfTableLen = br.ReadInt16();
            m_hfTableOffset = (int)inStream.Position;
            m_hfDataOffset = m_hfTableOffset + m_hfTableLen * 4 - 0x3fc;
        }

        /// <summary>
        /// Decompress data the same as JPKDecodeLz, but sets input properties.
        /// </summary>
        /// <param name="inStream">Stream to read from.</param>
        /// <param name="outBuffer">Outpute buffer.</param>
        public override void ProcessOnDecode(Stream inStream, byte[] outBuffer)
        {
            InitializeTable(inStream);
            base.ProcessOnDecode(inStream, outBuffer);
        }

        /// <summary>
        /// Read a byte implementing JpkGetHf
        /// </summary>
        /// <param name="s">Stream to read from</param>
        /// <returns>Read byte.</returns>
        public override byte ReadByte(Stream s)
        {
            int data = m_hfTableLen;
            BinaryReader br = new(s);

            while (data >= 0x100)
            {
                m_flagShift--;
                if (m_flagShift < 0)
                {
                    m_flagShift = 7;
                    s.Seek(m_hfDataOffset++, SeekOrigin.Begin);
                    m_flagHF = br.ReadByte();
                }
                byte bit = (byte)((m_flagHF >> m_flagShift) & 0x1);
                s.Seek((data * 2 - 0x200 + bit) * 2 + m_hfTableOffset, SeekOrigin.Begin);
                data = br.ReadInt16();
            }
            return (byte)data;
        }
    }
}
