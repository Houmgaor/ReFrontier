using System.IO;

namespace ReFrontier.Jpk
{
    /// <summary>
    /// Interface for JPK compression encoders.
    ///
    /// <para><b>Architecture Overview:</b></para>
    /// <para>JPK encoders transform input data into a compressed byte stream. The encoder
    /// hierarchy uses inheritance to combine algorithms:</para>
    /// <code>
    ///   IJPKEncode
    ///       ├── JPKEncodeRW      (raw bytes, no compression)
    ///       └── JPKEncodeLz      (LZ77 compression)
    ///               └── JPKEncodeHFI   (adds Huffman on top of LZ77)
    ///                       └── JPKEncodeHFIRW  (Huffman only, skips LZ77)
    /// </code>
    ///
    /// <para><b>Usage Pattern:</b></para>
    /// <para>Encoders are created via <see cref="ICodecFactory"/> and process data in a
    /// single pass. The <see cref="WriteByte"/> method is overridden by derived classes
    /// to intercept output bytes for additional encoding (e.g., Huffman).</para>
    ///
    /// <para><b>Thread Safety:</b></para>
    /// <para>Encoder instances are NOT thread-safe. Create one encoder per thread or
    /// synchronize access externally.</para>
    /// </summary>
    public interface IJPKEncode
    {
        /// <summary>
        /// Write a single byte to the output stream.
        /// <para>This method may be overridden by derived classes to apply additional
        /// encoding (e.g., Huffman bit codes) before writing to the stream.</para>
        /// </summary>
        /// <param name="outStream">Stream to write to.</param>
        /// <param name="inByte">Byte to write.</param>
        void WriteByte(Stream outStream, byte inByte);

        /// <summary>
        /// Compress the input buffer and write to the output stream.
        /// </summary>
        /// <param name="inBuffer">Input data to compress.</param>
        /// <param name="outStream">Stream to write compressed data to.</param>
        /// <param name="level">Compression level (6-8191). Higher values increase compression
        /// ratio at the cost of speed. Controls both max match length and search window size
        /// for LZ-based codecs. Ignored by RW and HFIRW codecs.</param>
        void ProcessOnEncode(byte[] inBuffer, Stream outStream, int level = 16);
    }
}
