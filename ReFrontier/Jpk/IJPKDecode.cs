﻿using System.IO;

namespace ReFrontier.Jpk
{
    /// <summary>
    /// Base interface for JPK decoding.
    /// </summary>
    internal interface IJPKDecode
    {
        /// <summary>
        /// Read byte from stream.
        /// </summary>
        /// <param name="stream">Input stream.</param>
        /// <returns>Read byte.</returns>
        byte ReadByte(Stream stream);
        /// <summary>
        /// File processing.
        /// </summary>
        /// <param name="inStream">Input stream.</param>
        /// <param name="outBuffer">Output buffer.</param>
        void ProcessOnDecode(Stream inStream, byte[] outBuffer);
    }
}
