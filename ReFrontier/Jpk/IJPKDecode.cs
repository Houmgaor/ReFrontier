using System.IO;

namespace ReFrontier.Jpk
{
    /// <summary>
    /// Interface for JPK decompression decoders.
    ///
    /// <para><b>Architecture Overview:</b></para>
    /// <para>JPK decoders reconstruct original data from compressed byte streams. The decoder
    /// hierarchy mirrors the encoder structure:</para>
    /// <code>
    ///   IJPKDecode
    ///       ├── JPKDecodeRW      (raw bytes, no decompression)
    ///       └── JPKDecodeLz      (LZ77 decompression)
    ///               └── JPKDecodeHFI   (Huffman decoding + LZ77)
    ///                       └── JPKDecodeHFIRW  (Huffman only, no LZ77)
    /// </code>
    ///
    /// <para><b>Usage Pattern:</b></para>
    /// <para>Decoders are created via <see cref="ICodecFactory"/>. The <see cref="ReadByte"/>
    /// method is overridden by derived classes to decode bytes before use (e.g., traverse
    /// Huffman tree to get the actual byte value).</para>
    ///
    /// <para><b>Resource Management:</b></para>
    /// <para>Some decoders (e.g., JPKDecodeHFI) implement IDisposable. Use <c>using</c>
    /// statements or dispose explicitly after processing.</para>
    ///
    /// <para><b>Thread Safety:</b></para>
    /// <para>Decoder instances are NOT thread-safe. Create one decoder per thread or
    /// synchronize access externally.</para>
    /// </summary>
    public interface IJPKDecode
    {
        /// <summary>
        /// Read and decode a single byte from the stream.
        /// <para>This method may be overridden by derived classes to apply decoding
        /// (e.g., Huffman tree traversal) before returning the byte.</para>
        /// </summary>
        /// <param name="stream">Input stream.</param>
        /// <returns>Decoded byte value.</returns>
        byte ReadByte(Stream stream);

        /// <summary>
        /// Decompress the input stream into the output buffer.
        /// </summary>
        /// <param name="inStream">Stream containing compressed data.</param>
        /// <param name="outBuffer">Pre-allocated buffer to receive decompressed data.</param>
        /// <param name="outSize">Expected decompressed size. May be less than buffer length
        /// when using pooled buffers. Decompression stops when this many bytes are written.</param>
        void ProcessOnDecode(Stream inStream, byte[] outBuffer, int outSize);
    }
}
