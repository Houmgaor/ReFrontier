using System;
using System.IO;

using LibReFrontier.Exceptions;

namespace ReFrontier.Jpk
{
    /// <summary>
    /// LZ77-based decompression decoder for Monster Hunter Frontier JPK files.
    ///
    /// <para><b>Algorithm Overview:</b></para>
    /// <para>Decodes data compressed with the LZ77 variant used by JPKEncodeLz. Reads a stream
    /// of flag bits and data bytes, reconstructing the original data by either copying literal
    /// bytes or reproducing back-references from already-decoded data.</para>
    ///
    /// <para><b>Decoding Cases (determined by flag bits):</b></para>
    /// <list type="bullet">
    ///   <item><b>Case: Literal (flag=0)</b> - Read and output one byte directly</item>
    ///   <item><b>Case 0 (flag=10xx)</b> - Short back-ref: 2-bit length (3-6), 1-byte offset (0-255)</item>
    ///   <item><b>Case 1 (flag=11, len!=0)</b> - Medium back-ref: 3-bit length in header (3-9), 13-bit offset</item>
    ///   <item><b>Case 2 (flag=11, len=0, then 0xxxx)</b> - Long back-ref A: 4-bit length (10-25), 13-bit offset</item>
    ///   <item><b>Case 3 (flag=11, len=0, then 1, byte=0xFF)</b> - Raw bytes: read (offset+0x1B) literal bytes</item>
    ///   <item><b>Case 4 (flag=11, len=0, then 1, byte!=0xFF)</b> - Long back-ref B: length from byte (26+), 13-bit offset</item>
    /// </list>
    ///
    /// <para><b>Back-Reference Format:</b></para>
    /// <para>Back-references copy <c>length</c> bytes from position <c>(current - offset - 1)</c>.
    /// This allows overlapping copies for run-length encoding of repeated patterns.</para>
    /// </summary>
    internal class JPKDecodeLz : IJPKDecode
    {
        /// <summary>
        /// Current bit position within m_flag (7 = MSB, 0 = LSB, then reload).
        /// </summary>
        private int m_shiftIndex = 0;

        /// <summary>
        /// Current flag byte. Each bit indicates literal (0) or back-reference (1).
        /// </summary>
        private byte m_flag = 0;

        /// <summary>
        /// Copy length bytes to buffer at position index.
        /// Bytes are copied from position index - offset - 1.
        /// </summary>
        /// <param name="buffer">Buffer to rewrite</param>
        /// <param name="offset">Offset position to the left.</param>
        /// <param name="length">Number of bytes to write.</param>
        /// <param name="index">Initial position to start copying bytes.</param>
        private static int JpkCopyLz(byte[] buffer, int offset, int length, int index)
        {
            int noOverlapSpan = Math.Min(length, offset);
            // Copy in block
            Buffer.BlockCopy(buffer, index - offset - 1, buffer, index, noOverlapSpan);
            // Add repeated elements
            for (int i = index + noOverlapSpan; i < length + index; i++)
            {
                buffer[i] = buffer[i - offset - 1];
            }
            return length;
        }

        /// <summary>
        /// Return the value of the next byte from stream.
        /// </summary>
        /// <param name="s">Input stream</param>
        /// <returns>If byte is true or not</returns>
        private bool JpkBitLz(Stream s)
        {
            if (m_shiftIndex <= 0)
            {
                m_shiftIndex = 7;
                m_flag = ReadByte(s);
            }
            else
            {
                m_shiftIndex--;
            }
            return ((m_flag >> m_shiftIndex) & 1) == 1;
        }

        /// <summary>
        /// JPK decompression, implements JpkDecLz
        /// </summary>
        /// <param name="inStream">Stream to read from.</param>
        /// <param name="outBuffer">Buffer of decompressed data to write to.</param>
        /// <param name="outSize">Actual output size.</param>
        public virtual void ProcessOnDecode(Stream inStream, byte[] outBuffer, int outSize)
        {
            int outIndex = 0;
            try
            {
                DecodeLoop(inStream, outBuffer, outSize, ref outIndex);
            }
            catch (Exception ex) when (ex is CompressionException || ex is EndOfStreamException)
            {
                throw TruncatedStream(inStream, outSize, outIndex, ex);
            }

            // The loop above also stops when the input is exhausted at an operation
            // boundary. Without this check the tail of outBuffer would silently stay
            // zero-filled and the caller would treat the result as a complete file.
            if (outIndex != outSize)
                throw TruncatedStream(inStream, outSize, outIndex, null);
        }

        /// <summary>
        /// Build the exception describing an incomplete decode.
        /// </summary>
        /// <param name="inStream">Stream being read from.</param>
        /// <param name="outSize">Decompressed size declared by the JPK header.</param>
        /// <param name="outIndex">Number of bytes actually decoded.</param>
        /// <param name="inner">Underlying end-of-stream exception, if any.</param>
        /// <returns>Exception to throw.</returns>
        private static CompressionException TruncatedStream(
            Stream inStream, int outSize, int outIndex, Exception? inner
        )
        {
            string message =
                "Unexpected end of stream. " +
                $"Decoded {outIndex} of {outSize} declared bytes " +
                $"({(outSize > 0 ? 100.0 * outIndex / outSize : 0):F2}%) " +
                $"before running out of input at offset {inStream.Position} of {inStream.Length}. " +
                "The compressed stream is truncated, or the decompressed size in the JPK header " +
                "does not match the data.";
            return inner == null
                ? new CompressionException(message)
                : new CompressionException(message, inner);
        }

        /// <summary>
        /// Run the LZ decoding loop, reporting progress through <paramref name="outIndex"/>.
        /// </summary>
        /// <param name="inStream">Stream to read from.</param>
        /// <param name="outBuffer">Buffer of decompressed data to write to.</param>
        /// <param name="outSize">Actual output size.</param>
        /// <param name="outIndex">Number of bytes written to <paramref name="outBuffer"/> so far.</param>
        private void DecodeLoop(Stream inStream, byte[] outBuffer, int outSize, ref int outIndex)
        {
            while (inStream.Position < inStream.Length && outIndex < outSize)
            {
                if (!JpkBitLz(inStream))
                {
                    outBuffer[outIndex++] = ReadByte(inStream);
                    continue;
                }

                int length, offset;

                if (!JpkBitLz(inStream))
                {
                    // Case 0
                    length = (JpkBitLz(inStream) ? 2 : 0) + (JpkBitLz(inStream) ? 1 : 0);
                    offset = ReadByte(inStream);
                    outIndex += JpkCopyLz(outBuffer, offset, length + 3, outIndex);
                    continue;
                }

                byte hi = ReadByte(inStream);
                byte lo = ReadByte(inStream);
                length = (hi & 0xE0) >> 5;
                offset = ((hi & 0x1F) << 8) | lo;
                if (length != 0)
                {
                    // Case 1, use length directly 
                    outIndex += JpkCopyLz(outBuffer, offset, length + 2, outIndex);
                    continue;
                }

                if (!JpkBitLz(inStream))
                {
                    // Case 2, compute bytes to copy length
                    length = 0;
                    for (int i = 3; i > -1; i--)
                        length += JpkBitLz(inStream) ? 1 << i : 0;
                    outIndex += JpkCopyLz(outBuffer, offset, length + 2 + 8, outIndex);
                    continue;
                }

                byte temp = ReadByte(inStream);
                if (temp == 0xFF)
                {
                    // Case 3
                    for (int i = 0; i < offset + 0x1B; i++)
                        outBuffer[outIndex++] = ReadByte(inStream);
                    continue;
                }
                // Case 4
                outIndex += JpkCopyLz(outBuffer, offset, temp + 0x1a, outIndex);
            }
        }

        /// <summary>
        /// Read a single byte from the stream at the current position.
        /// </summary>
        /// <param name="stream">Stream to read from.</param>
        /// <returns>Read byte.</returns>
        /// <exception cref="CompressionException">Exception when end of file is reached unexpectedly.</exception>
        public virtual byte ReadByte(Stream stream)
        {
            int value = stream.ReadByte();
            if (value < 0)
                throw new CompressionException("Unexpected end of stream.");
            return (byte)value;
        }
    }
}
