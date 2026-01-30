using System.IO;

namespace ReFrontier.Jpk
{
    /// <summary>
    /// "Raw Writing" format, do not apply any compression. 
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
        /// <param name="outStream"></param>
        /// <param name="inByte"></param>
        /// <param name="stream">Stream to write to</param>
        /// <param name="inputByte">byte to write</param>
        public void WriteByte(Stream outStream, byte inByte)
        {
            outStream.WriteByte(inByte);
        }
    }
}
