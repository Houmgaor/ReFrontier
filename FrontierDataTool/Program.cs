using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Text;

using FrontierDataTool.Services;

namespace FrontierDataTool
{
    public class Program
    {
        private readonly DataExtractionService _extractionService;
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
        /// Get weapon and armor data from game files.
        /// </summary>
        /// <param name="args">Input argument from console.</param>
        /// <returns>Exit code (0 for success).</returns>
        private static int Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var program = new Program();

            // Root command
            var rootCommand = new RootCommand("FrontierDataTool - Extract and edit Monster Hunter Frontier game data");

            // Action options
            var dumpOption = new Option<bool>(
                name: "--dump",
                description: "Extract weapon/armor/skill/quest data (requires --suffix, --mhfpac, --mhfdat, --mhfinf)"
            );
            rootCommand.AddOption(dumpOption);

            var modshopOption = new Option<bool>(
                name: "--modshop",
                description: "Modify shop prices (requires --mhfdat)"
            );
            rootCommand.AddOption(modshopOption);

            var importOption = new Option<bool>(
                name: "--import",
                description: "Import modified CSV back into game files (requires --csv, --mhfdat, --mhfpac)"
            );
            rootCommand.AddOption(importOption);

            // Parameter options
            var suffixOption = new Option<string>(
                name: "--suffix",
                description: "Output suffix for files"
            );
            rootCommand.AddOption(suffixOption);

            var mhfpacOption = new Option<string>(
                name: "--mhfpac",
                description: "Path to mhfpac.bin"
            );
            rootCommand.AddOption(mhfpacOption);

            var mhfdatOption = new Option<string>(
                name: "--mhfdat",
                description: "Path to mhfdat.bin"
            );
            rootCommand.AddOption(mhfdatOption);

            var mhfinfOption = new Option<string>(
                name: "--mhfinf",
                description: "Path to mhfinf.bin"
            );
            rootCommand.AddOption(mhfinfOption);

            var csvOption = new Option<string>(
                name: "--csv",
                description: "Path to the CSV file to import (e.g., Armor.csv)"
            );
            rootCommand.AddOption(csvOption);

            var closeOption = new Option<bool>(
                name: "--close",
                description: "Close terminal after command"
            );
            rootCommand.AddOption(closeOption);

            // Set handler
            rootCommand.SetHandler((InvocationContext context) =>
            {
                var dump = context.ParseResult.GetValueForOption(dumpOption);
                var modshop = context.ParseResult.GetValueForOption(modshopOption);
                var import = context.ParseResult.GetValueForOption(importOption);
                var suffix = context.ParseResult.GetValueForOption(suffixOption);
                var mhfpac = context.ParseResult.GetValueForOption(mhfpacOption);
                var mhfdat = context.ParseResult.GetValueForOption(mhfdatOption);
                var mhfinf = context.ParseResult.GetValueForOption(mhfinfOption);
                var csv = context.ParseResult.GetValueForOption(csvOption);
                var close = context.ParseResult.GetValueForOption(closeOption);

                // Count how many actions are specified
                int actionCount = (dump ? 1 : 0) + (modshop ? 1 : 0) + (import ? 1 : 0);

                if (actionCount == 0)
                {
                    Console.Error.WriteLine("Error: No action specified. Use --dump, --modshop, or --import.");
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

                try
                {
                    if (dump)
                    {
                        // Validate required parameters
                        if (string.IsNullOrEmpty(suffix))
                        {
                            Console.Error.WriteLine("Error: --dump requires --suffix.");
                            context.ExitCode = 1;
                            FinishCommand(close);
                            return;
                        }
                        if (string.IsNullOrEmpty(mhfpac))
                        {
                            Console.Error.WriteLine("Error: --dump requires --mhfpac.");
                            context.ExitCode = 1;
                            FinishCommand(close);
                            return;
                        }
                        if (string.IsNullOrEmpty(mhfdat))
                        {
                            Console.Error.WriteLine("Error: --dump requires --mhfdat.");
                            context.ExitCode = 1;
                            FinishCommand(close);
                            return;
                        }
                        if (string.IsNullOrEmpty(mhfinf))
                        {
                            Console.Error.WriteLine("Error: --dump requires --mhfinf.");
                            context.ExitCode = 1;
                            FinishCommand(close);
                            return;
                        }

                        // Validate files exist
                        if (!File.Exists(mhfpac))
                        {
                            Console.Error.WriteLine($"Error: File '{mhfpac}' does not exist.");
                            context.ExitCode = 1;
                            FinishCommand(close);
                            return;
                        }
                        if (!File.Exists(mhfdat))
                        {
                            Console.Error.WriteLine($"Error: File '{mhfdat}' does not exist.");
                            context.ExitCode = 1;
                            FinishCommand(close);
                            return;
                        }
                        if (!File.Exists(mhfinf))
                        {
                            Console.Error.WriteLine($"Error: File '{mhfinf}' does not exist.");
                            context.ExitCode = 1;
                            FinishCommand(close);
                            return;
                        }

                        program._extractionService.DumpData(suffix, mhfpac, mhfdat, mhfinf);
                    }
                    else if (modshop)
                    {
                        if (string.IsNullOrEmpty(mhfdat))
                        {
                            Console.Error.WriteLine("Error: --modshop requires --mhfdat.");
                            context.ExitCode = 1;
                            FinishCommand(close);
                            return;
                        }

                        if (!File.Exists(mhfdat))
                        {
                            Console.Error.WriteLine($"Error: File '{mhfdat}' does not exist.");
                            context.ExitCode = 1;
                            FinishCommand(close);
                            return;
                        }

                        program._importService.ModShop(mhfdat);
                    }
                    else if (import)
                    {
                        if (string.IsNullOrEmpty(csv))
                        {
                            Console.Error.WriteLine("Error: --import requires --csv.");
                            context.ExitCode = 1;
                            FinishCommand(close);
                            return;
                        }
                        if (string.IsNullOrEmpty(mhfdat))
                        {
                            Console.Error.WriteLine("Error: --import requires --mhfdat.");
                            context.ExitCode = 1;
                            FinishCommand(close);
                            return;
                        }
                        if (string.IsNullOrEmpty(mhfpac))
                        {
                            Console.Error.WriteLine("Error: --import requires --mhfpac.");
                            context.ExitCode = 1;
                            FinishCommand(close);
                            return;
                        }

                        if (!File.Exists(csv))
                        {
                            Console.Error.WriteLine($"Error: File '{csv}' does not exist.");
                            context.ExitCode = 1;
                            FinishCommand(close);
                            return;
                        }
                        if (!File.Exists(mhfdat))
                        {
                            Console.Error.WriteLine($"Error: File '{mhfdat}' does not exist.");
                            context.ExitCode = 1;
                            FinishCommand(close);
                            return;
                        }
                        if (!File.Exists(mhfpac))
                        {
                            Console.Error.WriteLine($"Error: File '{mhfpac}' does not exist.");
                            context.ExitCode = 1;
                            FinishCommand(close);
                            return;
                        }

                        program._importService.ImportArmorData(mhfdat, csv, mhfpac);
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
