namespace FrontierTextTool.CLI
{
    /// <summary>
    /// The task a single invocation performs.
    /// </summary>
    public enum TextToolAction
    {
        /// <summary>Extract strings from a game file to CSV.</summary>
        Dump,

        /// <summary>Write the strings of a CSV back into a game file.</summary>
        Insert,

        /// <summary>Merge an older CSV with a newer one.</summary>
        Merge,

        /// <summary>Strip the spacing a CAT tool inserted around Japanese punctuation.</summary>
        CleanTrados,

        /// <summary>Fold a CAT tool export back into a CSV.</summary>
        InsertCat
    }

    /// <summary>
    /// Immutable DTO containing the parsed command line, whichever shape it was given in.
    /// </summary>
    public readonly struct CliArguments
    {
        /// <summary>
        /// Task selected by the verb, or by the legacy mode flag.
        /// </summary>
        public TextToolAction Action { get; init; }

        /// <summary>
        /// File the task reads: a game file for <see cref="TextToolAction.Dump"/> and
        /// <see cref="TextToolAction.Insert"/>, a CSV for <see cref="TextToolAction.Merge"/>
        /// and <see cref="TextToolAction.CleanTrados"/>, a CAT export for
        /// <see cref="TextToolAction.InsertCat"/>.
        /// </summary>
        public string InputPath { get; init; }

        /// <summary>
        /// Second file, for the tasks that take one. Null otherwise.
        /// </summary>
        public string? CsvPath { get; init; }

        /// <summary>
        /// First byte of the range to dump. Zero means the whole file.
        /// </summary>
        public int StartIndex { get; init; }

        /// <summary>
        /// Last byte of the range to dump. Zero means the whole file.
        /// </summary>
        public int EndIndex { get; init; }

        /// <summary>
        /// Whether to correct the value of string offsets.
        /// </summary>
        public bool TrueOffsets { get; init; }

        /// <summary>
        /// Whether to check that strings are valid before outputting them.
        /// </summary>
        public bool NullStrings { get; init; }

        /// <summary>
        /// Whether to show per-string messages.
        /// </summary>
        public bool Verbose { get; init; }

        /// <summary>
        /// Whether to return without waiting for a keypress.
        /// </summary>
        public bool Close { get; init; }

        /// <summary>
        /// Whether to write CSV files in CP932 (Windows-31J) instead of UTF-8 with BOM.
        /// </summary>
        public bool Cp932 { get; init; }
    }
}
