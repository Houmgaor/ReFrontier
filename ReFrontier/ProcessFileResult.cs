using LibReFrontier;

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

    /// <summary>
    /// The transformation this handler undone, if it is one that has to be
    /// reversed to rebuild the file. Null when the handler did nothing reversible.
    /// </summary>
    public RecipeLayer? Layer { get; }

    private ProcessFileResult(string? outputPath, bool wasProcessed, string? skipReason, RecipeLayer? layer)
    {
        OutputPath = outputPath;
        WasProcessed = wasProcessed;
        SkipReason = skipReason;
        Layer = layer;
    }

    /// <summary>
    /// Create a successful result with the output path.
    /// </summary>
    /// <param name="outputPath">Path to the processed output file or directory.</param>
    /// <returns>A success result.</returns>
    public static ProcessFileResult Success(string outputPath) => new(outputPath, true, null, null);

    /// <summary>
    /// Create a successful result that also records how to reverse the transformation.
    /// </summary>
    /// <param name="outputPath">Path to the processed output file or directory.</param>
    /// <param name="layer">Recipe layer describing the transformation that was undone.</param>
    /// <returns>A success result.</returns>
    public static ProcessFileResult Success(string outputPath, RecipeLayer? layer) => new(outputPath, true, null, layer);

    /// <summary>
    /// Create a skipped result with the reason.
    /// </summary>
    /// <param name="reason">Why the file was skipped.</param>
    /// <returns>A skipped result.</returns>
    public static ProcessFileResult Skipped(string reason) => new(null, false, reason, null);
}
