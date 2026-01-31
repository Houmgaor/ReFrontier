using System;
using System.CommandLine;
using System.IO;
using System.Text;

using FrontierDataTool.Services;

using LibReFrontier;

namespace FrontierDataTool
{
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
                new LibReFrontier.Abstractions.RealFileSystem(),
                new LibReFrontier.Abstractions.ConsoleLogger(),
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

            // Action options
            Option<bool> dumpOption = new("--dump")
            {
                Description = "Extract weapon/armor/skill/quest data (requires --suffix, --mhfpac, --mhfdat, --mhfinf)"
            };

            Option<bool> modshopOption = new("--modshop")
            {
                Description = "Modify shop prices (requires --mhfdat)"
            };

            Option<bool> importOption = new("--import")
            {
                Description = "Import modified CSV back into game files. Auto-detects CSV type from filename: Armor.csv (requires --mhfdat, --mhfpac), Melee.csv (requires --mhfdat), Ranged.csv (requires --mhfdat), InfQuests.csv (requires --mhfinf)"
            };

            // Parameter options
            Option<string?> suffixOption = new("--suffix")
            {
                Description = "Output suffix for files"
            };

            Option<string?> mhfpacOption = new("--mhfpac")
            {
                Description = "Path to mhfpac.bin"
            };

            Option<string?> mhfdatOption = new("--mhfdat")
            {
                Description = "Path to mhfdat.bin"
            };

            Option<string?> mhfinfOption = new("--mhfinf")
            {
                Description = "Path to mhfinf.bin"
            };

            Option<string?> csvOption = new("--csv")
            {
                Description = "Path to the CSV file to import (e.g., Armor.csv)"
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
            RootCommand rootCommand = new("FrontierDataTool - Extract and edit Monster Hunter Frontier game data")
            {
                dumpOption,
                modshopOption,
                importOption,
                suffixOption,
                mhfpacOption,
                mhfdatOption,
                mhfinfOption,
                csvOption,
                closeOption,
                shiftJisOption
            };

            // Set handler
            rootCommand.SetAction(parseResult =>
            {
                var dump = parseResult.GetValue(dumpOption);
                var modshop = parseResult.GetValue(modshopOption);
                var import = parseResult.GetValue(importOption);
                var suffix = parseResult.GetValue(suffixOption);
                var mhfpac = parseResult.GetValue(mhfpacOption);
                var mhfdat = parseResult.GetValue(mhfdatOption);
                var mhfinf = parseResult.GetValue(mhfinfOption);
                var csv = parseResult.GetValue(csvOption);
                var close = parseResult.GetValue(closeOption);
                var shiftJis = parseResult.GetValue(shiftJisOption);

                // Configure encoding options
                var encodingOptions = shiftJis ? CsvEncodingOptions.ShiftJis : CsvEncodingOptions.Default;
                program.UpdateExtractionServiceWithEncoding(encodingOptions);

                // Count how many actions are specified
                int actionCount = (dump ? 1 : 0) + (modshop ? 1 : 0) + (import ? 1 : 0);

                if (actionCount == 0)
                {
                    Console.Error.WriteLine("Error: No action specified. Use --dump, --modshop, or --import.");
                    FinishCommand(close);
                    return 1;
                }

                if (actionCount > 1)
                {
                    Console.Error.WriteLine("Error: Only one action can be specified at a time.");
                    FinishCommand(close);
                    return 1;
                }

                try
                {
                    if (dump)
                    {
                        // Validate required parameters
                        if (string.IsNullOrEmpty(suffix))
                        {
                            Console.Error.WriteLine("Error: --dump requires --suffix.");
                            FinishCommand(close);
                            return 1;
                        }
                        if (string.IsNullOrEmpty(mhfpac))
                        {
                            Console.Error.WriteLine("Error: --dump requires --mhfpac.");
                            FinishCommand(close);
                            return 1;
                        }
                        if (string.IsNullOrEmpty(mhfdat))
                        {
                            Console.Error.WriteLine("Error: --dump requires --mhfdat.");
                            FinishCommand(close);
                            return 1;
                        }
                        if (string.IsNullOrEmpty(mhfinf))
                        {
                            Console.Error.WriteLine("Error: --dump requires --mhfinf.");
                            FinishCommand(close);
                            return 1;
                        }

                        // Validate files exist
                        if (!File.Exists(mhfpac))
                        {
                            Console.Error.WriteLine($"Error: File '{mhfpac}' does not exist.");
                            FinishCommand(close);
                            return 1;
                        }
                        if (!File.Exists(mhfdat))
                        {
                            Console.Error.WriteLine($"Error: File '{mhfdat}' does not exist.");
                            FinishCommand(close);
                            return 1;
                        }
                        if (!File.Exists(mhfinf))
                        {
                            Console.Error.WriteLine($"Error: File '{mhfinf}' does not exist.");
                            FinishCommand(close);
                            return 1;
                        }

                        program._extractionService.DumpData(suffix, mhfpac, mhfdat, mhfinf);
                    }
                    else if (modshop)
                    {
                        if (string.IsNullOrEmpty(mhfdat))
                        {
                            Console.Error.WriteLine("Error: --modshop requires --mhfdat.");
                            FinishCommand(close);
                            return 1;
                        }

                        if (!File.Exists(mhfdat))
                        {
                            Console.Error.WriteLine($"Error: File '{mhfdat}' does not exist.");
                            FinishCommand(close);
                            return 1;
                        }

                        program._importService.ModShop(mhfdat);
                    }
                    else if (import)
                    {
                        if (string.IsNullOrEmpty(csv))
                        {
                            Console.Error.WriteLine("Error: --import requires --csv.");
                            FinishCommand(close);
                            return 1;
                        }

                        if (!File.Exists(csv))
                        {
                            Console.Error.WriteLine($"Error: File '{csv}' does not exist.");
                            FinishCommand(close);
                            return 1;
                        }

                        // Auto-detect CSV type from filename
                        string csvFilename = Path.GetFileName(csv).ToLowerInvariant();

                        if (csvFilename.StartsWith("armor"))
                        {
                            // Armor import requires mhfdat and mhfpac
                            if (string.IsNullOrEmpty(mhfdat))
                            {
                                Console.Error.WriteLine("Error: Armor.csv import requires --mhfdat.");
                                FinishCommand(close);
                                return 1;
                            }
                            if (string.IsNullOrEmpty(mhfpac))
                            {
                                Console.Error.WriteLine("Error: Armor.csv import requires --mhfpac.");
                                FinishCommand(close);
                                return 1;
                            }
                            if (!File.Exists(mhfdat))
                            {
                                Console.Error.WriteLine($"Error: File '{mhfdat}' does not exist.");
                                FinishCommand(close);
                                return 1;
                            }
                            if (!File.Exists(mhfpac))
                            {
                                Console.Error.WriteLine($"Error: File '{mhfpac}' does not exist.");
                                FinishCommand(close);
                                return 1;
                            }

                            program._importService.ImportArmorData(mhfdat, csv, mhfpac);
                        }
                        else if (csvFilename.StartsWith("melee"))
                        {
                            // Melee import requires mhfdat
                            if (string.IsNullOrEmpty(mhfdat))
                            {
                                Console.Error.WriteLine("Error: Melee.csv import requires --mhfdat.");
                                FinishCommand(close);
                                return 1;
                            }
                            if (!File.Exists(mhfdat))
                            {
                                Console.Error.WriteLine($"Error: File '{mhfdat}' does not exist.");
                                FinishCommand(close);
                                return 1;
                            }

                            program._importService.ImportMeleeData(mhfdat, csv);
                        }
                        else if (csvFilename.StartsWith("ranged"))
                        {
                            // Ranged import requires mhfdat
                            if (string.IsNullOrEmpty(mhfdat))
                            {
                                Console.Error.WriteLine("Error: Ranged.csv import requires --mhfdat.");
                                FinishCommand(close);
                                return 1;
                            }
                            if (!File.Exists(mhfdat))
                            {
                                Console.Error.WriteLine($"Error: File '{mhfdat}' does not exist.");
                                FinishCommand(close);
                                return 1;
                            }

                            program._importService.ImportRangedData(mhfdat, csv);
                        }
                        else if (csvFilename.StartsWith("infquest"))
                        {
                            // Quest import requires mhfinf
                            if (string.IsNullOrEmpty(mhfinf))
                            {
                                Console.Error.WriteLine("Error: InfQuests.csv import requires --mhfinf.");
                                FinishCommand(close);
                                return 1;
                            }
                            if (!File.Exists(mhfinf))
                            {
                                Console.Error.WriteLine($"Error: File '{mhfinf}' does not exist.");
                                FinishCommand(close);
                                return 1;
                            }

                            program._importService.ImportQuestData(mhfinf, csv);
                        }
                        else
                        {
                            Console.Error.WriteLine($"Error: Unknown CSV type '{csvFilename}'. Expected Armor.csv, Melee.csv, Ranged.csv, or InfQuests.csv.");
                            FinishCommand(close);
                            return 1;
                        }
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
