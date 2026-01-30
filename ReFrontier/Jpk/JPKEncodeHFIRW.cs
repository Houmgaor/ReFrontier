using System.IO;

namespace ReFrontier.Jpk
{
    /// <summary>
    /// HFIRW encoding: Huffman table encoding with raw byte writing (no LZ compression).
    ///
    /// This is the inverse of JPKDecodeHFIRW which reads Huffman-encoded bytes
    /// without LZ decompression.
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
