using System;
using System.CommandLine;
using System.IO;
using System.Reflection;
using System.Text;

using FrontierTextTool.Services;

using LibReFrontier;

namespace FrontierTextTool
{
    /// <summary>
    /// Utility program for text data edition.
    /// </summary>
    public class Program
    {
        private TextExtractionService _extractionService;
        private TextInsertionService _insertionService;
        private CsvMergeService _mergeService;

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
        /// Update services with encoding options.
        /// </summary>
        private void UpdateServicesWithEncoding(CsvEncodingOptions encodingOptions)
        {
            _extractionService = new TextExtractionService(
                new LibReFrontier.Abstractions.RealFileSystem(),
                new LibReFrontier.Abstractions.ConsoleLogger(),
                encodingOptions);
            _mergeService = new CsvMergeService(
                new LibReFrontier.Abstractions.RealFileSystem(),
                new LibReFrontier.Abstractions.ConsoleLogger(),
                encodingOptions);
            // InsertionService only reads CSVs, auto-detects encoding, doesn't need options
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

            // Primary file argument
            Argument<string> fileArgument = new("file")
            {
                Description = "Input file to process"
            };

            // Action options (mutually exclusive actions)
            Option<bool> fulldumpOption = new("--fulldump")
            {
                Description = "Dump all data from file"
            };

            Option<bool> dumpOption = new("--dump")
            {
                Description = "Dump data range from file (requires --startIndex and --endIndex)"
            };

            Option<bool> insertOption = new("--insert")
            {
                Description = "Add data from CSV to file (requires --csv)"
            };

            Option<bool> mergeOption = new("--merge")
            {
                Description = "Merge two CSV files (requires --csv for new CSV)"
            };

            Option<bool> cleanTradosOption = new("--cleanTrados")
            {
                Description = "Clean up ill-encoded characters in file"
            };

            Option<bool> insertCatOption = new("--insertCAT")
            {
                Description = "Insert CAT file to CSV file (requires --csv)"
            };

            // Parameter options
            Option<int> startIndexOption = new("--startIndex")
            {
                Description = "Start offset for dump",
                DefaultValueFactory = _ => 0
            };

            Option<int> endIndexOption = new("--endIndex")
            {
                Description = "End offset for dump",
                DefaultValueFactory = _ => 0
            };

            Option<string?> csvOption = new("--csv")
            {
                Description = "Secondary CSV file for insert, merge, or insertCAT operations"
            };

            // Global options
            Option<bool> verboseOption = new("--verbose")
            {
                Description = "More verbosity"
            };

            Option<bool> trueOffsetsOption = new("--trueOffsets")
            {
                Description = "Correct the value of string offsets"
            };

            Option<bool> nullStringsOption = new("--nullStrings")
            {
                Description = "Check if strings are valid before outputting them"
            };

            Option<bool> closeOption = new("--close")
            {
                Description = "Close terminal after command"
            };

            Option<bool> shiftJisOption = new("--shift-jis")
            {
                Description = "Output CSV files in Shift-JIS encoding (default: UTF-8 with BOM)"
            };

            // Root command
            RootCommand rootCommand = new($"FrontierTextTool v{fileVersionAttribute} - Extract and edit text data")
            {
                fileArgument,
                fulldumpOption,
                dumpOption,
                insertOption,
                mergeOption,
                cleanTradosOption,
                insertCatOption,
                startIndexOption,
                endIndexOption,
                csvOption,
                verboseOption,
                trueOffsetsOption,
                nullStringsOption,
                closeOption,
                shiftJisOption
            };

            // Set handler
            rootCommand.SetAction(parseResult =>
            {
                var file = parseResult.GetValue(fileArgument)!;
                var fulldump = parseResult.GetValue(fulldumpOption);
                var dump = parseResult.GetValue(dumpOption);
                var insert = parseResult.GetValue(insertOption);
                var merge = parseResult.GetValue(mergeOption);
                var cleanTrados = parseResult.GetValue(cleanTradosOption);
                var insertCat = parseResult.GetValue(insertCatOption);
                var startIndex = parseResult.GetValue(startIndexOption);
                var endIndex = parseResult.GetValue(endIndexOption);
                var csv = parseResult.GetValue(csvOption);
                var verbose = parseResult.GetValue(verboseOption);
                var trueOffsets = parseResult.GetValue(trueOffsetsOption);
                var nullStrings = parseResult.GetValue(nullStringsOption);
                var close = parseResult.GetValue(closeOption);
                var shiftJis = parseResult.GetValue(shiftJisOption);

                // Configure encoding options
                var encodingOptions = shiftJis ? CsvEncodingOptions.ShiftJis : CsvEncodingOptions.Default;
                program.UpdateServicesWithEncoding(encodingOptions);

                // Count how many actions are specified
                int actionCount = (fulldump ? 1 : 0) + (dump ? 1 : 0) + (insert ? 1 : 0) +
                                  (merge ? 1 : 0) + (cleanTrados ? 1 : 0) + (insertCat ? 1 : 0);

                if (actionCount == 0)
                {
                    Console.Error.WriteLine("Error: No action specified. Use --fulldump, --dump, --insert, --merge, --cleanTrados, or --insertCAT.");
                    FinishCommand(close);
                    return 1;
                }

                if (actionCount > 1)
                {
                    Console.Error.WriteLine("Error: Only one action can be specified at a time.");
                    FinishCommand(close);
                    return 1;
                }

                // Validate file exists
                if (!File.Exists(file))
                {
                    Console.Error.WriteLine($"Error: File '{file}' does not exist.");
                    FinishCommand(close);
                    return 1;
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
                            FinishCommand(close);
                            return 1;
                        }
                        program._insertionService.InsertStrings(file, csv, verbose, trueOffsets);
                    }
                    else if (merge)
                    {
                        if (string.IsNullOrEmpty(csv))
                        {
                            Console.Error.WriteLine("Error: --merge requires --csv <newCsvFile>.");
                            FinishCommand(close);
                            return 1;
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
                            FinishCommand(close);
                            return 1;
                        }
                        program._mergeService.InsertCatFile(file, csv);
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error: {ex.Message}");
                    FinishCommand(close);
                    return 1;
                }

                FinishCommand(close);
                return 0;
            });

            return rootCommand.Parse(args).Invoke();
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
