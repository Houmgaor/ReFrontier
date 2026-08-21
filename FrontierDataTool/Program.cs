using System;
using System.IO;
using System.Text;

using FrontierDataTool.CLI;
using FrontierDataTool.Services;

using LibReFrontier;
using LibReFrontier.Abstractions;

namespace FrontierDataTool
{
    /// <summary>
    /// Utility program for game data extraction and edition.
    /// </summary>
    public class Program
    {
        private DataExtractionService _extractionService;
        private readonly DataImportService _importService;

        /// <summary>
        /// Create a new Program instance with default services.
        /// </summary>
        public Program()
            : this(new DataExtractionService(), new DataImportService())
        {
        }

        /// <summary>
        /// Create a new Program instance with injectable services for testing.
        /// </summary>
        public Program(DataExtractionService extractionService, DataImportService importService)
        {
            _extractionService = extractionService ?? throw new ArgumentNullException(nameof(extractionService));
            _importService = importService ?? throw new ArgumentNullException(nameof(importService));
        }

        /// <summary>
        /// Update extraction service with encoding options.
        /// </summary>
        private void UpdateExtractionServiceWithEncoding(CsvEncodingOptions encodingOptions)
        {
            _extractionService = new DataExtractionService(
                new RealFileSystem(),
                new ConsoleLogger(),
                encodingOptions);
            // ImportService only reads CSVs, auto-detects encoding, doesn't need options
        }

        /// <summary>
        /// Get weapon and armor data from game files.
        /// </summary>
        /// <param name="args">Input argument from console.</param>
        /// <returns>Exit code (0 for success).</returns>
        private static int Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var program = new Program();
            var schema = new CliSchema();
            var rootCommand = schema.CreateRootCommand();

            // The same action serves the root (legacy flat form) and every verb;
            // CliSchema decides which shape it was given.
            Func<System.CommandLine.ParseResult, int> handler = parseResult =>
            {
                CliArguments arguments;
                try
                {
                    arguments = schema.ExtractArguments(parseResult);
                }
                catch (InvalidOperationException ex)
                {
                    Console.Error.WriteLine(ex.Message);
                    FinishCommand(schema.WantsClose(parseResult));
                    return 1;
                }

                return program.Run(arguments);
            };

            rootCommand.SetAction(handler);
            foreach (var subcommand in rootCommand.Subcommands)
            {
                subcommand.SetAction(handler);
            }

            return rootCommand.Parse(args).Invoke();
        }

        /// <summary>
        /// Run the task the command line selected.
        /// </summary>
        /// <param name="arguments">Parsed command line.</param>
        /// <returns>Exit code (0 for success).</returns>
        internal int Run(CliArguments arguments)
        {
            var encodingOptions = arguments.Cp932 ? CsvEncodingOptions.Cp932 : CsvEncodingOptions.Default;
            if (arguments.Json)
                encodingOptions.Format = OutputFormat.Json;
            UpdateExtractionServiceWithEncoding(encodingOptions);

            try
            {
                return arguments.Action switch
                {
                    DataToolAction.Dump => RunDump(arguments),
                    DataToolAction.ModShop => RunModShop(arguments),
                    DataToolAction.Import => RunImport(arguments),
                    _ => Fail($"Error: Unknown command '{arguments.Action}'.")
                };
            }
            catch (Exception ex)
            {
                return Fail($"Error: {ex.Message}");
            }
            finally
            {
                FinishCommand(arguments.Close);
            }
        }

        private int RunDump(CliArguments arguments)
        {
            // Rengoku-only dump (doesn't require mhfpac/mhfdat/mhfinf)
            if (!string.IsNullOrEmpty(arguments.Rengoku))
            {
                if (!File.Exists(arguments.Rengoku))
                    return Fail($"Error: File '{arguments.Rengoku}' does not exist.");

                _extractionService.DumpRengokuData(arguments.Rengoku);

                // If no other files specified, we're done
                if (string.IsNullOrEmpty(arguments.MhfPac)
                    && string.IsNullOrEmpty(arguments.MhfDat)
                    && string.IsNullOrEmpty(arguments.MhfInf))
                {
                    return 0;
                }
            }

            if (string.IsNullOrEmpty(arguments.Suffix))
                return Fail("Error: 'dump' requires --suffix.");
            if (string.IsNullOrEmpty(arguments.MhfPac))
                return Fail("Error: 'dump' requires --mhfpac.");
            if (string.IsNullOrEmpty(arguments.MhfDat))
                return Fail("Error: 'dump' requires --mhfdat.");
            if (string.IsNullOrEmpty(arguments.MhfInf))
                return Fail("Error: 'dump' requires --mhfinf.");

            if (!File.Exists(arguments.MhfPac))
                return Fail($"Error: File '{arguments.MhfPac}' does not exist.");
            if (!File.Exists(arguments.MhfDat))
                return Fail($"Error: File '{arguments.MhfDat}' does not exist.");
            if (!File.Exists(arguments.MhfInf))
                return Fail($"Error: File '{arguments.MhfInf}' does not exist.");

            _extractionService.DumpData(arguments.Suffix, arguments.MhfPac, arguments.MhfDat, arguments.MhfInf);
            return 0;
        }

        private int RunModShop(CliArguments arguments)
        {
            if (string.IsNullOrEmpty(arguments.MhfDat))
                return Fail("Error: 'modshop' requires mhfdat.bin. Usage: FrontierDataTool modshop <mhfdat.bin>");
            if (!File.Exists(arguments.MhfDat))
                return Fail($"Error: File '{arguments.MhfDat}' does not exist.");

            _importService.ModShop(arguments.MhfDat);
            return 0;
        }

        private int RunImport(CliArguments arguments)
        {
            if (string.IsNullOrEmpty(arguments.CsvPath))
                return Fail("Error: 'import' requires a CSV file. Usage: FrontierDataTool import <file.csv>");
            if (!File.Exists(arguments.CsvPath))
                return Fail($"Error: File '{arguments.CsvPath}' does not exist.");

            // Auto-detect CSV type from filename
            string csvFilename = Path.GetFileName(arguments.CsvPath).ToLowerInvariant();

            if (csvFilename.StartsWith("armor", StringComparison.Ordinal))
            {
                // Armor import requires mhfdat and mhfpac
                if (string.IsNullOrEmpty(arguments.MhfDat))
                    return Fail("Error: Armor.csv import requires --mhfdat.");
                if (string.IsNullOrEmpty(arguments.MhfPac))
                    return Fail("Error: Armor.csv import requires --mhfpac.");
                if (!File.Exists(arguments.MhfDat))
                    return Fail($"Error: File '{arguments.MhfDat}' does not exist.");
                if (!File.Exists(arguments.MhfPac))
                    return Fail($"Error: File '{arguments.MhfPac}' does not exist.");

                _importService.ImportArmorData(arguments.MhfDat, arguments.CsvPath, arguments.MhfPac);
                return 0;
            }

            if (csvFilename.StartsWith("melee", StringComparison.Ordinal))
            {
                if (string.IsNullOrEmpty(arguments.MhfDat))
                    return Fail("Error: Melee.csv import requires --mhfdat.");
                if (!File.Exists(arguments.MhfDat))
                    return Fail($"Error: File '{arguments.MhfDat}' does not exist.");

                _importService.ImportMeleeData(arguments.MhfDat, arguments.CsvPath);
                return 0;
            }

            if (csvFilename.StartsWith("ranged", StringComparison.Ordinal))
            {
                if (string.IsNullOrEmpty(arguments.MhfDat))
                    return Fail("Error: Ranged.csv import requires --mhfdat.");
                if (!File.Exists(arguments.MhfDat))
                    return Fail($"Error: File '{arguments.MhfDat}' does not exist.");

                _importService.ImportRangedData(arguments.MhfDat, arguments.CsvPath);
                return 0;
            }

            if (csvFilename.StartsWith("infquest", StringComparison.Ordinal))
            {
                if (string.IsNullOrEmpty(arguments.MhfInf))
                    return Fail("Error: InfQuests.csv import requires --mhfinf.");
                if (!File.Exists(arguments.MhfInf))
                    return Fail($"Error: File '{arguments.MhfInf}' does not exist.");

                _importService.ImportQuestData(arguments.MhfInf, arguments.CsvPath);
                return 0;
            }

            if (csvFilename.StartsWith("rengoku", StringComparison.Ordinal))
            {
                if (string.IsNullOrEmpty(arguments.Rengoku))
                    return Fail("Error: Rengoku CSV import requires --rengoku.");
                if (!File.Exists(arguments.Rengoku))
                    return Fail($"Error: File '{arguments.Rengoku}' does not exist.");

                _importService.ImportRengokuData(arguments.Rengoku, arguments.CsvPath);
                return 0;
            }

            return Fail(
                $"Error: Unknown CSV type '{csvFilename}'. Expected Armor.csv, Melee.csv, Ranged.csv, InfQuests.csv, RengokuFloors.csv, or RengokuSpawns.csv.");
        }

        /// <summary>
        /// Report an error and give the exit code that goes with it. The keypress wait
        /// happens in Run's finally block, so every path reaches it.
        /// </summary>
        /// <param name="message">Message to print on standard error.</param>
        /// <returns>Always 1.</returns>
        private static int Fail(string message)
        {
            Console.Error.WriteLine(message);
            return 1;
        }

        /// <summary>
        /// Finish command execution with optional wait and message.
        /// </summary>
        /// <param name="close">If true, don't wait for user input.</param>
        private static void FinishCommand(bool close)
        {
            if (!close)
            {
                Console.WriteLine("Done");
                Console.Read();
            }
        }

        /// <summary>
        /// Get weapon model ID data string from numeric ID.
        /// Kept for backward compatibility with existing tests.
        /// </summary>
        /// <param name="id">Numeric model ID.</param>
        /// <returns>Model ID string (e.g., "we001", "wf002").</returns>
        public static string GetModelIdData(int id)
        {
            return BinaryReaderService.GetModelIdData(id);
        }
    }
}
