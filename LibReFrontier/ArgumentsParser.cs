using System;
using System.Collections.Generic;

using LibReFrontier.Abstractions;

namespace LibReFrontier
{
    /// <summary>
    /// Useful methods for Command Line Interface.
    /// </summary>
    public class ArgumentsParser
    {
        private static readonly ILogger DefaultLogger = new ConsoleLogger();

        private readonly ILogger _logger;

        /// <summary>
        /// Mapping of named compression types to their enum values.
        /// </summary>
        private static readonly Dictionary<string, CompressionType> NamedCompressionTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            { "rw", CompressionType.RW },
            { "hfirw", CompressionType.HFIRW },
            { "lz", CompressionType.LZ },
            { "hfi", CompressionType.HFI }
        };

        /// <summary>
        /// Create a new ArgumentsParser instance with default dependencies.
        /// </summary>
        public ArgumentsParser() : this(DefaultLogger)
        {
        }

        /// <summary>
        /// Create a new ArgumentsParser instance with injectable logger.
        /// </summary>
        /// <param name="logger">Logger abstraction.</param>
        public ArgumentsParser(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }


        /// <summary>
        /// Print to console with seperator.
        /// Static version for backward compatibility.
        /// </summary>
        /// <param name="input">Value to print</param>
        /// <param name="printBefore">Set to true to display input before the separator.</param>
        public static void Print(string input, bool printBefore)
        {
            DefaultLogger.PrintWithSeparator(input, printBefore);
        }

        /// <summary>
        /// Print to console with separator.
        /// Instance method for testability.
        /// </summary>
        /// <param name="input">Value to print</param>
        /// <param name="printBefore">Set to true to display input before the separator.</param>
        public void PrintInstance(string input, bool printBefore)
        {
            _logger.PrintWithSeparator(input, printBefore);
        }

        /// <summary>
        /// Parse input compression argument.
        /// Supports both named types (rw, hfirw, lz, hfi) and numeric types (0, 2, 3, 4).
        /// </summary>
        /// <param name="compressionType">The compression type, either named (rw, hfirw, lz, hfi) or numeric (0, 2, 3, 4).</param>
        /// <param name="compressionLevel">The compression level (must be greater than 0).</param>
        /// <returns>Corresponding compression.</returns>
        /// <exception cref="ArgumentException">Compression level is invalid.</exception>
        /// <exception cref="InvalidCastException">The compression type is invalid.</exception>
        public static Compression ParseCompression(string compressionType, int compressionLevel)
        {
            if (compressionLevel <= 0)
            {
                throw new ArgumentException("Cannot set a compression level of 0 or less!");
            }

            CompressionType type;

            // Try to parse as named type first
            if (NamedCompressionTypes.TryGetValue(compressionType, out type))
            {
                // Named type found
            }
            else if (int.TryParse(compressionType, out int numericType))
            {
                // Numeric type
                if (numericType == 1)
                {
                    throw new InvalidCastException("Check compression type, cannot be 1 (None)!");
                }
                if (numericType < 0 || numericType > 4)
                {
                    throw new InvalidCastException($"Invalid compression type: {numericType}. Valid types are 0 (RW), 2 (HFIRW), 3 (LZ), 4 (HFI).");
                }
                type = Enum.GetValues<CompressionType>()[numericType];
            }
            else
            {
                throw new InvalidCastException(
                    $"Invalid compression type: '{compressionType}'. " +
                    "Valid named types are: rw, hfirw, lz, hfi. " +
                    "Valid numeric types are: 0 (RW), 2 (HFIRW), 3 (LZ), 4 (HFI)."
                );
            }

            return new Compression
            {
                type = type,
                level = compressionLevel
            };
        }

        /// <summary>
        /// Parse input compression argument from legacy format.
        /// </summary>
        /// <param name="inputArg">The value entered for compression, format ("type,level").</param>
        /// <returns>Corresponding compression.</returns>
        /// <exception cref="ArgumentException">Input argument is ill-formed.</exception>
        /// <exception cref="InvalidCastException">The compression type is invalid.</exception>
        [Obsolete("Use ParseCompression(string compressionType, int compressionLevel) instead.")]
        public static Compression ParseCompression(string inputArg)
        {
            var matches = inputArg.Split(",");
            if (matches.Length != 2)
            {
                throw new ArgumentException(
                    $"Check the input of compress! " +
                    $"Received: {inputArg}. " +
                    "Cannot split as compression [type],[level]. " +
                    "Example: --compress 3 50"
                );
            }

            if (!int.TryParse(matches[1], out int compressionLevel))
            {
                throw new FormatException($"Invalid compression level: '{matches[1]}'. Must be a number.");
            }

            return ParseCompression(matches[0], compressionLevel);
        }
    }
}
