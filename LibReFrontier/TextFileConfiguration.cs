using System.Globalization;
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
}
