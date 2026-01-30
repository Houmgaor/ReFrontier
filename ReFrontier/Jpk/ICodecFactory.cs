using LibReFrontier;

namespace ReFrontier.Jpk
{
    /// <summary>
    /// Factory interface for creating JPK encode/decode instances.
    /// </summary>
    public interface ICodecFactory
    {
        /// <summary>
        /// Create an encoder for the specified compression type.
        /// </summary>
        /// <param name="compressionType">Type of compression.</param>
        /// <returns>Encoder instance.</returns>
        /// <exception cref="System.InvalidOperationException">If compression type is not supported.</exception>
        IJPKEncode CreateEncoder(CompressionType compressionType);

        /// <summary>
        /// Create a decoder for the specified compression type.
        /// </summary>
        /// <param name="compressionType">Type of compression.</param>
        /// <returns>Decoder instance.</returns>
        /// <exception cref="System.InvalidOperationException">If compression type is not supported.</exception>
        IJPKDecode CreateDecoder(CompressionType compressionType);
    }
}
