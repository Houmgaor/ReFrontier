using System.Collections.Generic;
using System.Linq;

namespace ReFrontier.Services
{
    /// <summary>
    /// Result of validating a file's structural integrity.
    /// </summary>
    public class ValidationResult
    {
        /// <summary>
        /// Path to the validated file.
        /// </summary>
        public string FilePath { get; set; } = "";

        /// <summary>
        /// Whether all checks passed.
        /// </summary>
        public bool IsValid => Checks.Count > 0 && Checks.All(c => c.Passed);

        /// <summary>
        /// Whether the file format was recognized.
        /// </summary>
        public bool IsRecognized => Checks.Count > 0 && !Checks.Any(c => c.Layer == "Unknown");

        /// <summary>
        /// Individual validation checks performed.
        /// </summary>
        public List<ValidationCheck> Checks { get; set; } = new();

        /// <summary>
        /// Summary of format layers detected (e.g. "ECD > JPK > SimpleArchive").
        /// </summary>
        public string FormatChain
        {
            get
            {
                var layers = Checks
                    .Select(c => c.Layer)
                    .Distinct()
                    .ToList();
                return string.Join(" > ", layers);
            }
        }

        /// <summary>
        /// First failing check, or null if all passed.
        /// </summary>
        public ValidationCheck? FirstFailure => Checks.FirstOrDefault(c => !c.Passed);
    }

    /// <summary>
    /// A single validation check within a layer.
    /// </summary>
    public class ValidationCheck
    {
        /// <summary>
        /// Format layer this check applies to (e.g. "ECD", "JPK", "MOMO").
        /// </summary>
        public string Layer { get; set; } = "";

        /// <summary>
        /// Name of the check (e.g. "CRC32", "DecompressedSize", "EntryBounds").
        /// </summary>
        public string CheckName { get; set; } = "";

        /// <summary>
        /// Whether this check passed.
        /// </summary>
        public bool Passed { get; set; }

        /// <summary>
        /// Detail message (e.g. "Expected CRC32 0xABCD1234, got 0xABCD1234").
        /// </summary>
        public string? Detail { get; set; }
    }
}
