using System.IO;

namespace ReFrontier.Jpk
{
  /// <summary>
  /// Define the encoding scheme to JPK compression.
  /// </summary>
  internal interface IJPKEncode
  {
    /// <summary>
    /// Write byte to the stream.
    /// </summary>
    /// <param name="outStream">Stream to write to.</param>
    /// <param name="inByte">Byte to write.</param>
    void WriteByte(Stream outStream, byte inByte);

    /// <summary>
    /// Encode file.
    /// </summary>
    /// <param name="inBuffer">Input buffer.</param>
    /// <param name="outStream">Output stream</param>
    /// <param name="level">Encoding level between 0 and 10000.</param>
    void ProcessOnEncode(byte[] inBuffer, Stream outStream, int level = 16);
  }
}
