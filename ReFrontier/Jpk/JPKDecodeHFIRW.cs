using System.IO;

namespace ReFrontier.Jpk
{
    /// <summary>
    /// Decoding using JpkGetHf.
    /// 
    /// It uses HFI byte reading and RW processing.
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
            for (int index = 0; index < outBuffer.Length; index++)
                outBuffer[index] = base.ReadByte(inStream);
        }
    }
}
