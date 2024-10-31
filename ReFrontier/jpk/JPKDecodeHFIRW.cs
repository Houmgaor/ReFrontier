﻿using System.IO;

namespace ReFrontier.jpk
{
    /// <summary>
    /// Decoding using JpkGetHf.
    /// 
    /// It uses HFI byte reading and RW processing.
    /// </summary>
    class JPKDecodeHFIRW : JPKDecodeHFI
    {
        /// <summary>
        /// Decompress byte by byte on the whole file with JpkGetHf.
        /// </summary>
        /// <param name="inStream">Stream to read from.</param>
        /// <param name="outBuffer">Output buffer.</param>
        public override void ProcessOnDecode(Stream inStream, byte[] outBuffer)
        {
            int index = 0;
            while (index < outBuffer.Length)
                outBuffer[index++] = base.ReadByte(inStream);
        }
    }
}
