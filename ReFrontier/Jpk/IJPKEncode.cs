using System.IO;

namespace ReFrontier.Jpk
{
  /// <summary>
  /// Show progress on a task.
  /// </summary>
  /// <param name="perc">Percentage of completion, from 0 to 100.</param>
  public delegate void ShowProgress(long perc);

  /// <summary>
  /// Define the encoding scheme to JPK compression.
  /// </summary>
  internal interface IJPKEncode
  {
    /// <summary>
    /// Write byte to the stream.
    /// </summary>
    /// <param name="s">Stream to write to.</param>
    /// <param name="b">Byte to write.</param>
    void WriteByte(Stream s, byte b);

    /// <summary>
    /// Encode file.
    /// </summary>
    /// <param name="inBuffer">Input buffer.</param>
    /// <param name="outStream">Output stream</param>
    /// <param name="level">Encoding level between 0 and 10000.</param>
    /// <param name="progress"></param>
    void ProcessOnEncode(byte[] inBuffer, Stream outStream, int level=16, ShowProgress progress = null);
  }
}
