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
    /// CP932 (Windows-31J) encoding used for Japanese game text files.
    /// <para>Codepage 932 is Microsoft's extension of Shift_JIS: it adds the NEC and IBM
    /// rows and maps a handful of code points differently, notably <c>0x8160</c>, which is
    /// FULLWIDTH TILDE here and WAVE DASH in JIS X 0208. The game files use the extended
    /// set, so the codepage is named by number rather than through an alias: .NET resolves
    /// "shift-jis", "shift_jis" and "sjis" to 932 as well, but those names claim a narrower
    /// encoding than what is actually read and written.</para>
    /// </summary>
    public static Encoding Cp932Encoding
    {
        get
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(932);
        }
    }

    /// <summary>
    /// UTF-8 encoding with BOM for CSV files.
    /// The BOM helps Excel and other editors detect the encoding automatically.
    /// </summary>
    public static Encoding Utf8WithBomEncoding => new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    /// <summary>
    /// Create a CSV configuration for Japanese CSV files.
    /// Uses RFC 4180 standard which automatically quotes fields containing
    /// commas, quotes, or newlines.
    /// </summary>
    /// <returns>CsvConfiguration with Japanese culture and comma delimiter.</returns>
    public static CsvConfiguration CreateJapaneseCsvConfig()
    {
        return new CsvConfiguration(CultureInfo.CreateSpecificCulture("jp-JP"))
        {
            Delimiter = ",",
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

        return Cp932Encoding;
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

        return Cp932Encoding;
    }

    /// <summary>
    /// Validate that a string can be encoded to Shift-JIS without data loss.
    /// This is important when reading UTF-8 CSV files that will be inserted into
    /// game binary files (which require Shift-JIS encoding).
    /// </summary>
    /// <param name="text">The text to validate.</param>
    /// <returns>True if the text can be fully represented in Shift-JIS.</returns>
    public static bool ValidateCp932Compatibility(string text)
    {
        if (string.IsNullOrEmpty(text))
            return true;

        try
        {
            var encoding = Cp932Encoding;
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

        var encoding = Cp932Encoding;
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
