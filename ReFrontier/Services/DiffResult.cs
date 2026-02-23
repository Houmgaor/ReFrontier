using System.Collections.Generic;

namespace ReFrontier.Services
{
    /// <summary>
    /// Result of structurally comparing two MHF game files.
    /// </summary>
    public class DiffResult
    {
        /// <summary>
        /// Path or name of the first file.
        /// </summary>
        public string File1 { get; set; } = "";

        /// <summary>
        /// Path or name of the second file.
        /// </summary>
        public string File2 { get; set; } = "";

        /// <summary>
        /// Whether both files are structurally identical.
        /// </summary>
        public bool AreIdentical => Differences.Count == 0;

        /// <summary>
        /// List of structural differences found between the two files.
        /// </summary>
        public List<DiffEntry> Differences { get; set; } = new();

        /// <summary>
        /// Format chain detected in file 1 (e.g. "ECD > JPK > SimpleArchive").
        /// </summary>
        public string FormatChain1 { get; set; } = "";

        /// <summary>
        /// Format chain detected in file 2.
        /// </summary>
        public string FormatChain2 { get; set; } = "";
    }

    /// <summary>
    /// A single structural difference between two files at a specific layer.
    /// </summary>
    public class DiffEntry
    {
        /// <summary>
        /// Format layer where the difference was found (e.g. "ECD", "JPK", "SimpleArchive").
        /// </summary>
        public string Layer { get; set; } = "";

        /// <summary>
        /// Property that differs (e.g. "CRC32", "EntryCount", "Entry[3].Size").
        /// </summary>
        public string Property { get; set; } = "";

        /// <summary>
        /// Value from file 1 (null if property only exists in file 2).
        /// </summary>
        public string? Value1 { get; set; }

        /// <summary>
        /// Value from file 2 (null if property only exists in file 1).
        /// </summary>
        public string? Value2 { get; set; }
    }
}
