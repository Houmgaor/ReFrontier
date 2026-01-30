using LibReFrontier;
using LibReFrontier.Exceptions;

namespace ReFrontier.Jpk
{
    /// <summary>
    /// Factory interface for creating JPK encode/decode codec instances.
    ///
    /// <para><b>Supported Compression Types:</b></para>
    /// <list type="bullet">
    ///   <item><b>RW (0)</b> - Raw bytes, no compression</item>
    ///   <item><b>None (1)</b> - Decoder only, treated as raw bytes</item>
    ///   <item><b>HFIRW (2)</b> - Huffman coding only</item>
    ///   <item><b>LZ (3)</b> - LZ77 compression</item>
    ///   <item><b>HFI (4)</b> - Huffman + LZ77 compression</item>
    /// </list>
    /// </summary>
    public interface ICodecFactory
    {
        /// <summary>
        /// Create an encoder for the specified compression type.
        /// </summary>
        /// <param name="compressionType">Type of compression (RW, HFIRW, LZ, or HFI).</param>
        /// <returns>Encoder instance.</returns>
        /// <exception cref="CompressionException">If compression type is not supported for encoding (e.g., None).</exception>
        IJPKEncode CreateEncoder(CompressionType compressionType);

        /// <summary>
        /// Create a decoder for the specified compression type.
        /// </summary>
        /// <param name="compressionType">Type of compression.</param>
        /// <returns>Decoder instance.</returns>
        /// <exception cref="CompressionException">If compression type is not supported.</exception>
        IJPKDecode CreateDecoder(CompressionType compressionType);
    }
}
