using System.Text;

namespace LibReFrontier;

/// <summary>
/// Options for CSV file encoding.
/// By default, CSV files are written in UTF-8 with BOM for easier editing.
/// Game binary files always use Shift-JIS.
/// </summary>
public class CsvEncodingOptions
{
    /// <summary>
    /// If true, output CSV files in Shift-JIS encoding.
    /// If false (default), output in UTF-8 with BOM.
    /// </summary>
    public bool UseShiftJisOutput { get; set; } = false;

    /// <summary>
    /// Get the encoding to use for CSV output based on current settings.
    /// </summary>
    /// <returns>UTF-8 with BOM (default) or Shift-JIS encoding.</returns>
    public Encoding GetOutputEncoding() => UseShiftJisOutput
        ? TextFileConfiguration.ShiftJisEncoding
        : TextFileConfiguration.Utf8WithBomEncoding;

    /// <summary>
    /// Default options using UTF-8 with BOM output.
    /// </summary>
    public static CsvEncodingOptions Default => new();

    /// <summary>
    /// Options for Shift-JIS output (legacy behavior).
    /// </summary>
    public static CsvEncodingOptions ShiftJis => new() { UseShiftJisOutput = true };
}
