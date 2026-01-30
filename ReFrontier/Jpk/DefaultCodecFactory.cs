using System;
using LibReFrontier;

namespace ReFrontier.Jpk
{
    /// <summary>
    /// Default implementation of ICodecFactory.
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
                _ => throw new InvalidOperationException($"Unsupported/invalid encoder type: {compressionType}")
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
                _ => throw new InvalidOperationException($"Unsupported/invalid decoder type: {compressionType}")
            };
        }
    }
}
