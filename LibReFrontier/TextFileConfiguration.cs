using System.Globalization;
using System.IO;
using System.Text;

using CsvHelper.Configuration;

namespace LibReFrontier;

/// <summary>
/// Centralized configuration for text file encoding and CSV settings.
/// Used across FrontierTextTool and FrontierDataTool for consistent file handling.
/// </summary>
public static class TextFileConfiguration
{
    /// <summary>
    /// UTF-8 BOM bytes used to identify UTF-8 encoded files.
    /// </summary>
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    /// <summary>
    /// Shift-JIS encoding used for Japanese game text files.
    /// </summary>
    public static Encoding ShiftJisEncoding
    {
        get
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding("shift-jis");
        }
    }

    /// <summary>
    /// UTF-8 encoding with BOM for CSV files.
    /// The BOM helps Excel and other editors detect the encoding automatically.
    /// </summary>
    public static Encoding Utf8WithBomEncoding => new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    /// <summary>
    /// Create a CSV configuration for Japanese tab-separated files.
    /// </summary>
    /// <returns>CsvConfiguration with Japanese culture and tab delimiter.</returns>
    public static CsvConfiguration CreateJapaneseCsvConfig()
    {
        return new CsvConfiguration(CultureInfo.CreateSpecificCulture("jp-JP"))
        {
            Delimiter = "\t",
        };
    }

    /// <summary>
    /// Detect the encoding of a CSV file by checking for UTF-8 BOM.
    /// Falls back to Shift-JIS if no BOM is detected.
    /// </summary>
    /// <param name="filePath">Path to the file to check.</param>
    /// <returns>The detected encoding (UTF-8 with BOM or Shift-JIS).</returns>
    public static Encoding DetectCsvEncoding(string filePath)
    {
        byte[] buffer = new byte[3];
        using var stream = File.OpenRead(filePath);
        int bytesRead = stream.Read(buffer, 0, 3);

        if (bytesRead >= 3 &&
            buffer[0] == Utf8Bom[0] &&
            buffer[1] == Utf8Bom[1] &&
            buffer[2] == Utf8Bom[2])
        {
            return Utf8WithBomEncoding;
        }

        return ShiftJisEncoding;
    }

    /// <summary>
    /// Detect encoding from a stream by checking for UTF-8 BOM.
    /// The stream position is reset after detection.
    /// </summary>
    /// <param name="stream">Stream to check.</param>
    /// <returns>The detected encoding (UTF-8 with BOM or Shift-JIS).</returns>
    public static Encoding DetectCsvEncoding(Stream stream)
    {
        long originalPosition = stream.Position;
        byte[] buffer = new byte[3];
        int bytesRead = stream.Read(buffer, 0, 3);
        stream.Position = originalPosition;

        if (bytesRead >= 3 &&
            buffer[0] == Utf8Bom[0] &&
            buffer[1] == Utf8Bom[1] &&
            buffer[2] == Utf8Bom[2])
        {
            return Utf8WithBomEncoding;
        }

        return ShiftJisEncoding;
    }

    /// <summary>
    /// Validate that a string can be encoded to Shift-JIS without data loss.
    /// This is important when reading UTF-8 CSV files that will be inserted into
    /// game binary files (which require Shift-JIS encoding).
    /// </summary>
    /// <param name="text">The text to validate.</param>
    /// <returns>True if the text can be fully represented in Shift-JIS.</returns>
    public static bool ValidateShiftJisCompatibility(string text)
    {
        if (string.IsNullOrEmpty(text))
            return true;

        try
        {
            var encoding = ShiftJisEncoding;
            byte[] encoded = encoding.GetBytes(text);
            string decoded = encoding.GetString(encoded);
            return text == decoded;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get characters from text that cannot be encoded to Shift-JIS.
    /// Useful for providing detailed error messages.
    /// </summary>
    /// <param name="text">The text to check.</param>
    /// <returns>Array of characters that cannot be encoded to Shift-JIS.</returns>
    public static char[] GetIncompatibleCharacters(string text)
    {
        if (string.IsNullOrEmpty(text))
            return [];

        var encoding = ShiftJisEncoding;
        var incompatible = new System.Collections.Generic.List<char>();

        foreach (char c in text)
        {
            byte[] encoded = encoding.GetBytes(new[] { c });
            string decoded = encoding.GetString(encoded);
            if (decoded.Length != 1 || decoded[0] != c)
            {
                if (!incompatible.Contains(c))
                    incompatible.Add(c);
            }
        }

        return [.. incompatible];
    }
}
