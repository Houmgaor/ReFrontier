using LibReFrontier;
using LibReFrontier.Exceptions;

namespace ReFrontier.Jpk
{
    /// <summary>
    /// Default implementation of <see cref="ICodecFactory"/> for JPK compression.
    ///
    /// <para><b>Codec Selection:</b></para>
    /// <para>Creates encoder/decoder instances based on <see cref="CompressionType"/>:</para>
    /// <code>
    ///   Type     Encoder           Decoder           Description
    ///   ────     ───────           ───────           ───────────
    ///   RW       JPKEncodeRW       JPKDecodeRW       No compression
    ///   None     (not supported)   JPKDecodeRW       Decode-only marker
    ///   HFIRW    JPKEncodeHFIRW    JPKDecodeHFIRW    Huffman only
    ///   LZ       JPKEncodeLz       JPKDecodeLz       LZ77 only
    ///   HFI      JPKEncodeHFI      JPKDecodeHFI      Huffman + LZ77
    /// </code>
    ///
    /// <para><b>Resource Management:</b></para>
    /// <para>Codec instances are created fresh each time. HFI decoders implement
    /// IDisposable and should be disposed after use.</para>
    /// </summary>
    public class DefaultCodecFactory : ICodecFactory
    {
        /// <inheritdoc />
        public IJPKEncode CreateEncoder(CompressionType compressionType)
        {
            return compressionType switch
            {
                CompressionType.RW => new JPKEncodeRW(),
                CompressionType.HFIRW => new JPKEncodeHFIRW(),
                CompressionType.LZ => new JPKEncodeLz(),
                CompressionType.HFI => new JPKEncodeHFI(),
                _ => throw new CompressionException($"Unsupported/invalid encoder type: {compressionType}")
            };
        }

        /// <inheritdoc />
        public IJPKDecode CreateDecoder(CompressionType compressionType)
        {
            return compressionType switch
            {
                CompressionType.RW => new JPKDecodeRW(),
                CompressionType.None => new JPKDecodeRW(), // None is raw bytes, same as RW
                CompressionType.HFIRW => new JPKDecodeHFIRW(),
                CompressionType.LZ => new JPKDecodeLz(),
                CompressionType.HFI => new JPKDecodeHFI(),
                _ => throw new CompressionException($"Unsupported/invalid decoder type: {compressionType}")
            };
        }
    }
}
