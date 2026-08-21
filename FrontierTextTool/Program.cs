using System;
using System.IO;
using System.Reflection;
using System.Text;

using FrontierTextTool.CLI;
using FrontierTextTool.Services;

using LibReFrontier;
using LibReFrontier.Abstractions;

namespace FrontierTextTool
{
    /// <summary>
    /// Utility program for text data edition.
    /// </summary>
    public class Program
    {
        private TextExtractionService _extractionService;
        private readonly TextInsertionService _insertionService;
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
                new RealFileSystem(),
                new ConsoleLogger(),
                encodingOptions);
            _mergeService = new CsvMergeService(
                new RealFileSystem(),
                new ConsoleLogger(),
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
            var schema = new CliSchema();
            var rootCommand = schema.CreateRootCommand(fileVersionAttribute);

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

            string? collision = CliSchema.DescribeVerbPathCollision(args, new RealFileSystem());
            if (collision != null)
            {
                Console.Error.WriteLine(collision);
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
            UpdateServicesWithEncoding(arguments.ShiftJis ? CsvEncodingOptions.ShiftJis : CsvEncodingOptions.Default);

            if (!File.Exists(arguments.InputPath))
            {
                Console.Error.WriteLine($"Error: File '{arguments.InputPath}' does not exist.");
                FinishCommand(arguments.Close);
                return 1;
            }

            try
            {
                switch (arguments.Action)
                {
                    case TextToolAction.Dump:
                        _extractionService.DumpAndHash(
                            arguments.InputPath, arguments.StartIndex, arguments.EndIndex,
                            arguments.TrueOffsets, arguments.NullStrings);
                        break;
                    case TextToolAction.Insert:
                        _insertionService.InsertStrings(
                            arguments.InputPath, arguments.CsvPath!, arguments.Verbose, arguments.TrueOffsets);
                        break;
                    case TextToolAction.Merge:
                        _mergeService.Merge(arguments.InputPath, arguments.CsvPath!);
                        break;
                    case TextToolAction.CleanTrados:
                        _mergeService.CleanTrados(arguments.InputPath);
                        break;
                    case TextToolAction.InsertCat:
                        _mergeService.InsertCatFile(arguments.InputPath, arguments.CsvPath!);
                        break;
                    default:
                        Console.Error.WriteLine($"Error: Unknown command '{arguments.Action}'.");
                        FinishCommand(arguments.Close);
                        return 1;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                FinishCommand(arguments.Close);
                return 1;
            }

            FinishCommand(arguments.Close);
            return 0;
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
