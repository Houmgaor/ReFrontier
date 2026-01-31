using System.IO;

namespace ReFrontier.Jpk
{
    /// <summary>
    /// Huffman-only encoder for Monster Hunter Frontier JPK files (type 2: HFIRW).
    ///
    /// <para><b>Algorithm Overview:</b></para>
    /// <para>Applies Huffman coding without LZ77 compression. Each input byte is replaced
    /// by a variable-length bit code based on a randomly-generated Huffman tree. Bytes
    /// that appear frequently in typical data get shorter codes.</para>
    ///
    /// <para><b>Output Format:</b></para>
    /// <list type="bullet">
    ///   <item>2 bytes: Huffman table length (0x1FE = 510 entries)</item>
    ///   <item>510 × 2 bytes: Huffman tree table (1020 bytes overhead)</item>
    ///   <item>Variable: Huffman-encoded byte stream</item>
    /// </list>
    ///
    /// <para><b>When to Use:</b></para>
    /// <para>HFIRW is effective when data has many repeated byte values (skewed
    /// distribution) but few repeated multi-byte sequences. For data with repeated
    /// sequences, use LZ or HFI instead.</para>
    ///
    /// <para><b>Inheritance:</b></para>
    /// <para>Inherits from <see cref="JPKEncodeHFI"/> to reuse Huffman tree generation
    /// and bit writing, but overrides <see cref="ProcessOnEncode"/> to skip the LZ77
    /// compression stage.</para>
    ///
    /// <para><b>Note on Tree Generation:</b></para>
    /// <para>The Huffman tree is randomly shuffled at encode time. This means the same
    /// input will produce different (but equally valid) output on each run. Decoding
    /// always works because the tree is stored in the file header.</para>
    /// </summary>
    internal class JPKEncodeHFIRW : JPKEncodeHFI
    {
        /// <summary>
        /// Encode using Huffman table but without LZ compression.
        /// Writes bytes one by one using Huffman encoding.
        /// </summary>
        /// <param name="inBuffer">Input bytes buffer.</param>
        /// <param name="outStream">Stream to write to.</param>
        /// <param name="level">Compression level (unused for HFIRW, kept for interface compatibility).</param>
        public override void ProcessOnEncode(byte[] inBuffer, Stream outStream, int level = 16)
        {
            // Initialize and write the Huffman table
            FillTable();
            BinaryWriter bw = new(outStream);
            bw.Write(m_hfTableLen);
            for (int i = 0; i < m_hfTableLen; i++)
                bw.Write(m_hfTable[i]);

            // Write each byte using Huffman encoding (no LZ compression)
            for (int i = 0; i < inBuffer.Length; i++)
            {
                WriteByte(outStream, inBuffer[i]);
            }

            // Flush any remaining bits
            FlushWrite(outStream);
        }
    }
}
