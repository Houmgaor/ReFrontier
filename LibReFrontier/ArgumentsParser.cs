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
        /// Simple arguments parser.
        /// </summary>
        /// <param name="args">Input arguments from the CLI</param>
        /// <returns>Dictionary of arguments. Arguments with no value have a null value assigned.</returns>
        public static Dictionary<string, string> ParseArguments(string[] args)
        {
            var arguments = new Dictionary<string, string>();
            foreach (var arg in args)
            {
                string[] parts = arg.Split('=');
                if (parts.Length == 2)
                {
                    arguments[parts[0]] = parts[1];
                }
                else
                {
                    arguments[arg] = null;
                }
            }
            return arguments;
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
        /// Parse input compresion argument.
        /// </summary>
        /// <param name="inputArg">The value entered for compression, format ("type,level").</param>
        /// <returns>Corresponding compression.</returns>
        /// <exception cref="ArgumentException">Input argument is ill-formed.</exception>
        /// <exception cref="InvalidCastException">The compression type is invalid.</exception>
        public static Compression ParseCompression(string inputArg)
        {

            var matches = inputArg.Split(",");
            if (matches.Length != 2)
            {
                throw new ArgumentException(
                    $"Check the input of compress! " +
                    $"Received: {inputArg}. " +
                    "Cannot split as compression [type],[level]. " +
                    "Example: --compress=3,50"
                );
            }
            int compressionType = int.Parse(matches[0]);
            if (compressionType == 1)
            {
                throw new InvalidCastException("Check compression type, cannot be 1!");
            }
            int compressionLevel = int.Parse(matches[1]);
            if (compressionLevel == 0)
            {
                throw new ArgumentException("Cannot set a compression level of 0!");
            }
            Compression compression = new()
            {
                type = Enum.GetValues<CompressionType>()[compressionType],
                level = compressionLevel
            };
            return compression;
        }
    }
}
