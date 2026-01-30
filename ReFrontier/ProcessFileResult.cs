namespace ReFrontier;

/// <summary>
/// Result of processing a file, indicating success or skip with reason.
/// </summary>
public readonly struct ProcessFileResult
{
    /// <summary>
    /// Output path if the file was processed, null otherwise.
    /// </summary>
    public string? OutputPath { get; }

    /// <summary>
    /// True if the file was actually processed.
    /// </summary>
    public bool WasProcessed { get; }

    /// <summary>
    /// Reason why the file was skipped, null if processed.
    /// </summary>
    public string? SkipReason { get; }

    private ProcessFileResult(string? outputPath, bool wasProcessed, string? skipReason)
    {
        OutputPath = outputPath;
        WasProcessed = wasProcessed;
        SkipReason = skipReason;
    }

    /// <summary>
    /// Create a successful result with the output path.
    /// </summary>
    /// <param name="outputPath">Path to the processed output file or directory.</param>
    /// <returns>A success result.</returns>
    public static ProcessFileResult Success(string outputPath) => new(outputPath, true, null);

    /// <summary>
    /// Create a skipped result with the reason.
    /// </summary>
    /// <param name="reason">Why the file was skipped.</param>
    /// <returns>A skipped result.</returns>
    public static ProcessFileResult Skipped(string reason) => new(null, false, reason);
}
