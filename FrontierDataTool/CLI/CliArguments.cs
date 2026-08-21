namespace FrontierDataTool.CLI
{
    /// <summary>
    /// The task a single invocation performs.
    /// </summary>
    public enum DataToolAction
    {
        /// <summary>Extract weapon, armor, skill and quest data to CSV or JSON.</summary>
        Dump,

        /// <summary>Rewrite shop prices in mhfdat.bin.</summary>
        ModShop,

        /// <summary>Write an edited CSV back into the game files.</summary>
        Import
    }

    /// <summary>
    /// Immutable DTO containing the parsed command line, whichever shape it was given in.
    /// </summary>
    public readonly struct CliArguments
    {
        /// <summary>
        /// Task selected by the verb, or by the legacy mode flag.
        /// </summary>
        public DataToolAction Action { get; init; }

        /// <summary>
        /// Suffix appended to the names of the files a dump writes.
        /// </summary>
        public string? Suffix { get; init; }

        /// <summary>
        /// Path to mhfpac.bin.
        /// </summary>
        public string? MhfPac { get; init; }

        /// <summary>
        /// Path to mhfdat.bin.
        /// </summary>
        public string? MhfDat { get; init; }

        /// <summary>
        /// Path to mhfinf.bin.
        /// </summary>
        public string? MhfInf { get; init; }

        /// <summary>
        /// Path to rengoku_data.bin (Hunting Road data).
        /// </summary>
        public string? Rengoku { get; init; }

        /// <summary>
        /// CSV to import. Its name selects which importer runs.
        /// </summary>
        public string? CsvPath { get; init; }

        /// <summary>
        /// Whether to return without waiting for a keypress.
        /// </summary>
        public bool Close { get; init; }

        /// <summary>
        /// Whether to write CSV files in CP932 (Windows-31J) instead of UTF-8 with BOM.
        /// </summary>
        public bool Cp932 { get; init; }

        /// <summary>
        /// Whether to write JSON instead of CSV.
        /// </summary>
        public bool Json { get; init; }

        /// <summary>
        /// Write English skill tree names in place of the game's own, where one is known.
        /// </summary>
        public bool EnglishSkills { get; init; }

        /// <summary>
        /// Offset profile to read the files with: a built-in id, or the path to a JSON
        /// profile. Null detects it from the files.
        /// </summary>
        public string? Offsets { get; init; }
    }
}
