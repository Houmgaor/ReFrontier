using System.IO;

namespace ReFrontier.Jpk
{
    /// <summary>
    /// Raw Writing (RW) encoder - passes data through without compression.
    ///
    /// <para><b>Algorithm:</b></para>
    /// <para>No transformation is applied. Input bytes are copied directly to the output
    /// stream in sequence. This is useful for:</para>
    /// <list type="bullet">
    ///   <item>Data that is already compressed (e.g., embedded images)</item>
    ///   <item>Small files where compression overhead exceeds savings</item>
    ///   <item>Debugging or when maximum decode speed is required</item>
    /// </list>
    ///
    /// <para><b>Performance:</b></para>
    /// <para>O(n) time, O(1) space. No buffering or lookback required.</para>
    /// </summary>
    internal class JPKEncodeRW : IJPKEncode
    {
        /// <summary>
        /// Copy bytes from <paramref name="inBuffer"/> to <paramref name="outStream"/>.
        /// </summary>
        /// <param name="inBuffer">Input bytes buffer.</param>
        /// <param name="outStream">Stream to write to.</param>
        /// <param name="level">Compression level. Level will be truncated between 6 and 8191.</param>
        public void ProcessOnEncode(byte[] inBuffer, Stream outStream, int level = 16)
        {
            for (int i = 0; i < inBuffer.Length; i++)
            {
                WriteByte(outStream, inBuffer[i]);
            }
        }

        /// <summary>
        /// Write a single byte directly from <paramref name="inByte"/> to <paramref name="outStream"/>.
        /// </summary>
        /// <param name="outStream">Stream to write to.</param>
        /// <param name="inByte">Byte to write.</param>
        public void WriteByte(Stream outStream, byte inByte)
        {
            outStream.WriteByte(inByte);
        }
    }
}
