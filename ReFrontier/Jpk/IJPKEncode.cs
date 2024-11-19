using System.IO;

namespace ReFrontier.Jpk
{
  public delegate void ShowProgress(long perc);

    internal interface IJPKEncode
  {
    void WriteByte(Stream s, byte b);

    /// <summary>
    /// Encode file.
    /// </summary>
    /// <param name="inBuffer">Input buffer.</param>
    /// <param name="outStream">Output stream</param>
    /// <param name="level">Encoding level between 0 and 100.</param>
    /// <param name="progress"></param>
    void ProcessOnEncode(byte[] inBuffer, Stream outStream, int level=16, ShowProgress progress = null);
  }
}
