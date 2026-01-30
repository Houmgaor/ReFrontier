using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Reflection;
using System.Text;

using FrontierTextTool.Services;

namespace FrontierTextTool
{
    /// <summary>
    /// Utility program for text data edition.
    /// </summary>
    public class Program
    {
        private readonly TextExtractionService _extractionService;
        private readonly TextInsertionService _insertionService;
        private readonly CsvMergeService _mergeService;

        /// <summary>
        /// Create a new Program instance with default services.
        /// </summary>
        public Program()
            : this(new TextExtractionService(), new TextInsertionService(), new CsvMergeService())
        {
        }

        /// <summary>
        /// Create a new Program instance with injectable services for testing.
        /// </summary>
        public Program(
            TextExtractionService extractionService,
            TextInsertionService insertionService,
            CsvMergeService mergeService)
        {
            _extractionService = extractionService ?? throw new ArgumentNullException(nameof(extractionService));
            _insertionService = insertionService ?? throw new ArgumentNullException(nameof(insertionService));
            _mergeService = mergeService ?? throw new ArgumentNullException(nameof(mergeService));
        }

        /// <summary>
        /// Main CLI for text edition.
        /// </summary>
        /// <param name="args">Arguments passed</param>
        /// <returns>Exit code (0 for success).</returns>
        private static int Main(string[] args)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var fileVersionAttribute = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ?? "unknown";

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var program = new Program();

            // Root command
            var rootCommand = new RootCommand($"FrontierTextTool v{fileVersionAttribute} - Extract and edit text data");

            // Primary file argument
            var fileArgument = new Argument<string>(
                name: "file",
                description: "Input file to process"
            );
            rootCommand.AddArgument(fileArgument);

            // Action options (mutually exclusive actions)
            var fulldumpOption = new Option<bool>(
                name: "--fulldump",
                description: "Dump all data from file"
            );
            rootCommand.AddOption(fulldumpOption);

            var dumpOption = new Option<bool>(
                name: "--dump",
                description: "Dump data range from file (requires --startIndex and --endIndex)"
            );
            rootCommand.AddOption(dumpOption);

            var insertOption = new Option<bool>(
                name: "--insert",
                description: "Add data from CSV to file (requires --csv)"
            );
            rootCommand.AddOption(insertOption);

            var mergeOption = new Option<bool>(
                name: "--merge",
                description: "Merge two CSV files (requires --csv for new CSV)"
            );
            rootCommand.AddOption(mergeOption);

            var cleanTradosOption = new Option<bool>(
                name: "--cleanTrados",
                description: "Clean up ill-encoded characters in file"
            );
            rootCommand.AddOption(cleanTradosOption);

            var insertCatOption = new Option<bool>(
                name: "--insertCAT",
                description: "Insert CAT file to CSV file (requires --csv)"
            );
            rootCommand.AddOption(insertCatOption);

            // Parameter options
            var startIndexOption = new Option<int>(
                name: "--startIndex",
                description: "Start offset for dump",
                getDefaultValue: () => 0
            );
            rootCommand.AddOption(startIndexOption);

            var endIndexOption = new Option<int>(
                name: "--endIndex",
                description: "End offset for dump",
                getDefaultValue: () => 0
            );
            rootCommand.AddOption(endIndexOption);

            var csvOption = new Option<string>(
                name: "--csv",
                description: "Secondary CSV file for insert, merge, or insertCAT operations"
            );
            rootCommand.AddOption(csvOption);

            // Global options
            var verboseOption = new Option<bool>(
                name: "--verbose",
                description: "More verbosity"
            );
            rootCommand.AddOption(verboseOption);

            var trueOffsetsOption = new Option<bool>(
                name: "--trueOffsets",
                description: "Correct the value of string offsets"
            );
            rootCommand.AddOption(trueOffsetsOption);

            var nullStringsOption = new Option<bool>(
                name: "--nullStrings",
                description: "Check if strings are valid before outputting them"
            );
            rootCommand.AddOption(nullStringsOption);

            var closeOption = new Option<bool>(
                name: "--close",
                description: "Close terminal after command"
            );
            rootCommand.AddOption(closeOption);

            // Set handler
            rootCommand.SetHandler((InvocationContext context) =>
            {
                var file = context.ParseResult.GetValueForArgument(fileArgument);
                var fulldump = context.ParseResult.GetValueForOption(fulldumpOption);
                var dump = context.ParseResult.GetValueForOption(dumpOption);
                var insert = context.ParseResult.GetValueForOption(insertOption);
                var merge = context.ParseResult.GetValueForOption(mergeOption);
                var cleanTrados = context.ParseResult.GetValueForOption(cleanTradosOption);
                var insertCat = context.ParseResult.GetValueForOption(insertCatOption);
                var startIndex = context.ParseResult.GetValueForOption(startIndexOption);
                var endIndex = context.ParseResult.GetValueForOption(endIndexOption);
                var csv = context.ParseResult.GetValueForOption(csvOption);
                var verbose = context.ParseResult.GetValueForOption(verboseOption);
                var trueOffsets = context.ParseResult.GetValueForOption(trueOffsetsOption);
                var nullStrings = context.ParseResult.GetValueForOption(nullStringsOption);
                var close = context.ParseResult.GetValueForOption(closeOption);

                // Count how many actions are specified
                int actionCount = (fulldump ? 1 : 0) + (dump ? 1 : 0) + (insert ? 1 : 0) +
                                  (merge ? 1 : 0) + (cleanTrados ? 1 : 0) + (insertCat ? 1 : 0);

                if (actionCount == 0)
                {
                    Console.Error.WriteLine("Error: No action specified. Use --fulldump, --dump, --insert, --merge, --cleanTrados, or --insertCAT.");
                    context.ExitCode = 1;
                    FinishCommand(close);
                    return;
                }

                if (actionCount > 1)
                {
                    Console.Error.WriteLine("Error: Only one action can be specified at a time.");
                    context.ExitCode = 1;
                    FinishCommand(close);
                    return;
                }

                // Validate file exists
                if (!File.Exists(file))
                {
                    Console.Error.WriteLine($"Error: File '{file}' does not exist.");
                    context.ExitCode = 1;
                    FinishCommand(close);
                    return;
                }

                try
                {
                    if (fulldump)
                    {
                        program._extractionService.DumpAndHash(file, 0, 0, trueOffsets, nullStrings);
                    }
                    else if (dump)
                    {
                        program._extractionService.DumpAndHash(file, startIndex, endIndex, trueOffsets, nullStrings);
                    }
                    else if (insert)
                    {
                        if (string.IsNullOrEmpty(csv))
                        {
                            Console.Error.WriteLine("Error: --insert requires --csv <csvFile>.");
                            context.ExitCode = 1;
                            FinishCommand(close);
                            return;
                        }
                        program._insertionService.InsertStrings(file, csv, verbose, trueOffsets);
                    }
                    else if (merge)
                    {
                        if (string.IsNullOrEmpty(csv))
                        {
                            Console.Error.WriteLine("Error: --merge requires --csv <newCsvFile>.");
                            context.ExitCode = 1;
                            FinishCommand(close);
                            return;
                        }
                        program._mergeService.Merge(file, csv);
                    }
                    else if (cleanTrados)
                    {
                        program._mergeService.CleanTrados(file);
                    }
                    else if (insertCat)
                    {
                        if (string.IsNullOrEmpty(csv))
                        {
                            Console.Error.WriteLine("Error: --insertCAT requires --csv <csvFile>.");
                            context.ExitCode = 1;
                            FinishCommand(close);
                            return;
                        }
                        program._mergeService.InsertCatFile(file, csv);
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error: {ex.Message}");
                    context.ExitCode = 1;
                }

                FinishCommand(close);
            });

            return rootCommand.Invoke(args);
        }

        /// <summary>
        /// Finish command execution with optional wait and message.
        /// </summary>
        /// <param name="autoClose">If true, don't wait for user input.</param>
        private static void FinishCommand(bool autoClose)
        {
            if (!autoClose)
            {
                Console.WriteLine("Done");
                Console.Read();
            }
        }

        /// <summary>
        /// Get byte length of string (avoids issues with special spacing characters).
        /// Kept for backward compatibility with existing tests.
        /// </summary>
        /// <param name="input">Input string to get length</param>
        /// <returns>Length of string in SHIFT-JIS</returns>
        public static int GetNullterminatedStringLength(string input)
        {
            return TextInsertionService.GetNullterminatedStringLength(input);
        }

        /// <summary>
        /// Clean pollution caused by Trados or other CAT from text.
        /// Kept for backward compatibility with existing tests.
        /// </summary>
        /// <param name="text">Input text to clean.</param>
        /// <returns>Cleaned text.</returns>
        public static string CleanTradosText(string text)
        {
            return CsvMergeService.CleanTradosText(text);
        }
    }
}
