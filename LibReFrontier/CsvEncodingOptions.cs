using System.Text;

namespace LibReFrontier;

/// <summary>
/// Output format for data extraction.
/// </summary>
public enum OutputFormat
{
    /// <summary>CSV output (default).</summary>
    Csv,
    /// <summary>JSON output.</summary>
    Json
}

/// <summary>
/// Options for CSV file encoding.
/// By default, CSV files are written in UTF-8 with BOM for easier editing.
/// Game binary files always use CP932 (Windows-31J).
/// </summary>
public class CsvEncodingOptions
{
    /// <summary>
    /// If true, output CSV files in CP932 encoding.
    /// If false (default), output in UTF-8 with BOM.
    /// </summary>
    public bool UseCp932Output { get; set; } = false;

    /// <summary>
    /// Output format (CSV or JSON).
    /// </summary>
    public OutputFormat Format { get; set; } = OutputFormat.Csv;

    /// <summary>
    /// Get the encoding to use for CSV output based on current settings.
    /// </summary>
    /// <returns>UTF-8 with BOM (default) or CP932 encoding.</returns>
    public Encoding GetOutputEncoding() => UseCp932Output
        ? TextFileConfiguration.Cp932Encoding
        : TextFileConfiguration.Utf8WithBomEncoding;

    /// <summary>
    /// Default options using UTF-8 with BOM output.
    /// </summary>
    public static CsvEncodingOptions Default => new();

    /// <summary>
    /// Options for Shift-JIS output (legacy behavior).
    /// </summary>
    public static CsvEncodingOptions Cp932 => new() { UseCp932Output = true };
}
