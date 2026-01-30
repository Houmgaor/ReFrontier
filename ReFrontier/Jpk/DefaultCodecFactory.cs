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
                CompressionType.HFIRW => new JPKDecodeHFIRW(),
                CompressionType.LZ => new JPKDecodeLz(),
                CompressionType.HFI => new JPKDecodeHFI(),
                _ => throw new NotImplementedException($"JPK type {compressionType} is not supported.")
            };
        }
    }
}
