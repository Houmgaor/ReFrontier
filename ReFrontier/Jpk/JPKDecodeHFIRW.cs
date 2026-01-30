using System.IO;

namespace ReFrontier.Jpk
{
    /// <summary>
    /// Huffman-only decompression decoder for Monster Hunter Frontier JPK files (type 2: HFIRW).
    ///
    /// <para><b>Algorithm Overview:</b></para>
    /// <para>Decodes data compressed with Huffman coding only (no LZ77). Each byte is
    /// decoded by traversing the Huffman tree, but unlike HFI, there are no back-references
    /// or LZ77 compression—just straight Huffman decoding of each byte.</para>
    ///
    /// <para><b>Inheritance:</b></para>
    /// <para>Inherits from JPKDecodeHFI to reuse the Huffman tree reading (ReadByte),
    /// but overrides ProcessOnDecode to use simple sequential decoding (like RW)
    /// instead of LZ77 decompression.</para>
    ///
    /// <para><b>Use Case:</b></para>
    /// <para>HFIRW is useful when data has many repeated byte values but few repeated
    /// sequences. The Huffman tree compresses frequent bytes to shorter bit codes.</para>
    /// </summary>
    internal class JPKDecodeHFIRW : JPKDecodeHFI
    {
        /// <summary>
        /// Decompress byte by byte on the whole file with JpkGetHf.
        /// </summary>
        /// <param name="inStream">Stream to read from.</param>
        /// <param name="outBuffer">Output buffer.</param>
        public override void ProcessOnDecode(Stream inStream, byte[] outBuffer)
        {
            InitializeTable(inStream);
            for (int index = 0; index < outBuffer.Length; index++)
                outBuffer[index] = base.ReadByte(inStream);
        }
    }
}
