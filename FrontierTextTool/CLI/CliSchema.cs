using System;
using System.CommandLine;

using LibReFrontier.CLI;

namespace FrontierTextTool.CLI
{
    /// <summary>
    /// Defines the CLI schema for FrontierTextTool.
    /// <para>The interface has two shapes. The verb form
    /// (<c>FrontierTextTool dump mhfdat.bin</c>) groups each task with only the options
    /// that apply to it. The legacy flat form (<c>FrontierTextTool mhfdat.bin --fulldump</c>)
    /// is still accepted so existing scripts keep working; the flags that select a task
    /// there are deprecated and warn.</para>
    /// </summary>
    public class CliSchema
    {
        // Legacy root symbols. Hidden from help so the flat flag list no longer competes
        // with the verbs, but still parsed so existing command lines keep working.
        private readonly Argument<string> _fileArgument;
        private readonly Option<bool> _fulldumpOption;
        private readonly Option<bool> _dumpOption;
        private readonly Option<bool> _insertOption;
        private readonly Option<bool> _mergeOption;
        private readonly Option<bool> _cleanTradosOption;
        private readonly Option<bool> _insertCatOption;

        // Modifiers under their old camelCase spelling. Accepted on the verbs too, so a
        // script can adopt a verb without renaming every option on the same line.
        private readonly Option<int> _startIndexOption;
        private readonly Option<int> _endIndexOption;
        private readonly Option<bool> _trueOffsetsOption;
        private readonly Option<bool> _nullStringsOption;
        private readonly Option<string?> _csvOption;

        // Options shared by every command.
        private readonly Option<bool> _verboseOption;
        private readonly Option<bool> _closeOption;
        private readonly Option<bool> _shiftJisOption;

        // Verb symbols. Each verb names its files after what they hold, so that
        // 'merge <old-csv> <new-csv>' says which way round the two go.
        private readonly Argument<string> _dumpInputArgument;
        private readonly Argument<string> _insertInputArgument;
        private readonly Argument<string> _insertCsvArgument;
        private readonly Argument<string> _mergeOldArgument;
        private readonly Argument<string> _mergeNewArgument;
        private readonly Argument<string> _cleanInputArgument;
        private readonly Argument<string> _catInputArgument;
        private readonly Argument<string> _catCsvArgument;
        private readonly Option<int> _startOption;
        private readonly Option<int> _endOption;
        private readonly Option<bool> _trueOffsetsVerbOption;
        private readonly Option<bool> _nullStringsVerbOption;

        // Verb commands, compared by reference when dispatching.
        private Command? _dumpCommand;
        private Command? _insertCommand;
        private Command? _mergeCommand;
        private Command? _cleanTradosCommand;
        private Command? _insertCatCommand;

        /// <summary>
        /// Every verb the tool accepts, used to spot a path spelled like a command.
        /// </summary>
        public static readonly string[] VerbNames = [
            "dump",
            "insert",
            "merge",
            "clean-trados",
            "insert-cat"
        ];

        /// <summary>
        /// Creates a new CliSchema instance and initializes all CLI options.
        /// </summary>
        public CliSchema()
        {
            _fileArgument = new Argument<string>("file")
            {
                Description = "Input file: binary file for dump/insert, CSV for merge/cleanTrados, CAT file for insertCAT",
                Arity = ArgumentArity.ZeroOrOne,
                Hidden = true
            };

            _fulldumpOption = new Option<bool>("--fulldump")
            {
                Description = "Dump all data from file",
                Hidden = true
            };

            _dumpOption = new Option<bool>("--dump")
            {
                Description = "Dump data range from file",
                Hidden = true
            };

            _insertOption = new Option<bool>("--insert")
            {
                Description = "Add data from CSV to file",
                Hidden = true
            };

            _mergeOption = new Option<bool>("--merge")
            {
                Description = "Merge two CSV files",
                Hidden = true
            };

            _cleanTradosOption = new Option<bool>("--cleanTrados")
            {
                Description = "Clean up ill-encoded characters in file",
                Hidden = true
            };

            _insertCatOption = new Option<bool>("--insertCAT")
            {
                Description = "Insert CAT file to CSV file",
                Hidden = true
            };

            _startIndexOption = new Option<int>("--startIndex")
            {
                Description = "Start offset for dump",
                Hidden = true
            };

            _endIndexOption = new Option<int>("--endIndex")
            {
                Description = "End offset for dump",
                Hidden = true
            };

            _trueOffsetsOption = new Option<bool>("--trueOffsets")
            {
                Description = "Correct the value of string offsets",
                Hidden = true
            };

            _nullStringsOption = new Option<bool>("--nullStrings")
            {
                Description = "Check if strings are valid before outputting them",
                Hidden = true
            };

            _csvOption = new Option<string?>("--csv")
            {
                Description = "Secondary CSV file for insert, merge, or insertCAT operations",
                Hidden = true
            };

            _verboseOption = new Option<bool>("--verbose")
            {
                Description = "More verbosity",
                Recursive = true
            };

            _closeOption = new Option<bool>("--close")
            {
                Description = "Close terminal after command",
                Recursive = true
            };

            _shiftJisOption = new Option<bool>("--shift-jis")
            {
                Description = "Output CSV files in Shift-JIS encoding (default: UTF-8 with BOM)",
                Recursive = true
            };

            _dumpInputArgument = new Argument<string>("file")
            {
                Description = "Game file to read strings from"
            };

            _insertInputArgument = new Argument<string>("file")
            {
                Description = "Game file to write the strings into"
            };

            _insertCsvArgument = new Argument<string>("csv")
            {
                Description = "CSV holding the strings to write",
                Arity = ArgumentArity.ZeroOrOne
            };

            _mergeOldArgument = new Argument<string>("old-csv")
            {
                Description = "CSV to merge into"
            };

            _mergeNewArgument = new Argument<string>("new-csv")
            {
                Description = "CSV to take the newer strings from",
                Arity = ArgumentArity.ZeroOrOne
            };

            _cleanInputArgument = new Argument<string>("csv")
            {
                Description = "CSV to clean"
            };

            _catInputArgument = new Argument<string>("cat-file")
            {
                Description = "CAT tool export to read"
            };

            _catCsvArgument = new Argument<string>("csv")
            {
                Description = "CSV to fold the export into",
                Arity = ArgumentArity.ZeroOrOne
            };

            _startOption = new Option<int>("--start-index")
            {
                Description = "First byte to dump (omit to dump the whole file)"
            };

            _endOption = new Option<int>("--end-index")
            {
                Description = "Last byte to dump (omit to dump the whole file)"
            };

            _trueOffsetsVerbOption = new Option<bool>("--true-offsets")
            {
                Description = "Correct the value of string offsets"
            };

            _nullStringsVerbOption = new Option<bool>("--null-strings")
            {
                Description = "Check if strings are valid before outputting them"
            };
        }

        /// <summary>
        /// Creates the root command with every verb, plus the legacy flat form.
        /// </summary>
        /// <param name="version">Version to show in the description.</param>
        /// <returns>The configured root command.</returns>
        public RootCommand CreateRootCommand(string version)
        {
            _dumpCommand = new Command("dump", "Extract strings from a game file to CSV")
            {
                _dumpInputArgument,
                _startOption,
                _endOption,
                _trueOffsetsVerbOption,
                _nullStringsVerbOption,
                // Hidden legacy spellings, accepted but absent from help.
                _startIndexOption,
                _endIndexOption,
                _trueOffsetsOption,
                _nullStringsOption
            };

            _insertCommand = new Command("insert", "Write the strings of a CSV back into a game file")
            {
                _insertInputArgument,
                _insertCsvArgument,
                _trueOffsetsVerbOption,
                _csvOption,
                _trueOffsetsOption
            };

            _mergeCommand = new Command("merge", "Merge an older CSV with a newer one")
            {
                _mergeOldArgument,
                _mergeNewArgument,
                _csvOption
            };

            _cleanTradosCommand = new Command("clean-trados", "Strip the spacing a CAT tool inserted around Japanese punctuation")
            {
                _cleanInputArgument
            };

            _insertCatCommand = new Command("insert-cat", "Fold a CAT tool export back into a CSV")
            {
                _catInputArgument,
                _catCsvArgument,
                _csvOption
            };

            RootCommand rootCommand = new($"FrontierTextTool v{version} - Extract and edit text data")
            {
                _fileArgument,
                _fulldumpOption,
                _dumpOption,
                _insertOption,
                _mergeOption,
                _cleanTradosOption,
                _insertCatOption,
                _startIndexOption,
                _endIndexOption,
                _trueOffsetsOption,
                _nullStringsOption,
                _csvOption,
                _verboseOption,
                _closeOption,
                _shiftJisOption,
                _dumpCommand,
                _insertCommand,
                _mergeCommand,
                _cleanTradosCommand,
                _insertCatCommand
            };

            // Adding subcommands makes System.CommandLine demand one, which would reject
            // the legacy form 'FrontierTextTool file.bin --fulldump'. An action on the root
            // makes it legal again; Main replaces this placeholder with the real handler.
            rootCommand.SetAction(_ => 0);

            return rootCommand;
        }

        /// <summary>
        /// A verb name wins over a path spelled the same way; say so rather than let the
        /// resulting error stand on its own.
        /// </summary>
        /// <param name="args">Raw command line arguments.</param>
        /// <param name="fileSystem">File system used to check whether the path exists.</param>
        /// <returns>A message to show, or null when there is no ambiguity.</returns>
        public static string? DescribeVerbPathCollision(string[] args, LibReFrontier.Abstractions.IFileSystem fileSystem)
        {
            return CliDeprecation.DescribeVerbPathCollision("FrontierTextTool <command>", VerbNames, args, fileSystem);
        }

        /// <summary>
        /// Whether the command line asked not to wait for a keypress. Readable before
        /// <see cref="ExtractArguments"/>, which may throw before the DTO exists.
        /// </summary>
        /// <param name="parseResult">The parsed command-line result.</param>
        /// <returns>True when --close was given.</returns>
        public bool WantsClose(ParseResult parseResult)
        {
            ArgumentNullException.ThrowIfNull(parseResult);
            return parseResult.GetValue(_closeOption);
        }

        /// <summary>
        /// Extracts parsed arguments from a ParseResult into a CliArguments DTO.
        /// </summary>
        /// <param name="parseResult">The parsed command-line result.</param>
        /// <returns>CliArguments describing the task to run.</returns>
        /// <exception cref="InvalidOperationException">The command line names no task,
        /// names more than one, or leaves out a file the task needs.</exception>
        public CliArguments ExtractArguments(ParseResult parseResult)
        {
            ArgumentNullException.ThrowIfNull(parseResult);

            var command = parseResult.CommandResult.Command;

            if (ReferenceEquals(command, _dumpCommand))
                return ExtractDump(parseResult);
            if (ReferenceEquals(command, _insertCommand))
                return ExtractTwoFileVerb(parseResult, TextToolAction.Insert, _insertInputArgument, _insertCsvArgument);
            if (ReferenceEquals(command, _mergeCommand))
                return ExtractTwoFileVerb(parseResult, TextToolAction.Merge, _mergeOldArgument, _mergeNewArgument);
            if (ReferenceEquals(command, _cleanTradosCommand))
                return BaseArguments(parseResult, TextToolAction.CleanTrados, parseResult.GetValue(_cleanInputArgument)!, null);
            if (ReferenceEquals(command, _insertCatCommand))
                return ExtractTwoFileVerb(parseResult, TextToolAction.InsertCat, _catInputArgument, _catCsvArgument);

            return ExtractLegacy(parseResult);
        }

        /// <summary>
        /// True when either the kebab-case option or its hidden legacy spelling was given.
        /// </summary>
        private static bool Either(ParseResult parseResult, Option<bool> preferred, Option<bool> legacy)
        {
            return parseResult.GetValue(preferred) || parseResult.GetValue(legacy);
        }

        /// <summary>
        /// Value of the kebab-case option, falling back to its hidden legacy spelling.
        /// </summary>
        private static int Either(ParseResult parseResult, Option<int> preferred, Option<int> legacy)
        {
            int value = parseResult.GetValue(preferred);
            return value != 0 ? value : parseResult.GetValue(legacy);
        }

        /// <summary>
        /// Build the parts of the DTO that every command shares.
        /// </summary>
        private CliArguments BaseArguments(ParseResult parseResult, TextToolAction action, string input, string? csv)
        {
            return new CliArguments
            {
                Action = action,
                InputPath = input,
                CsvPath = csv,
                Verbose = parseResult.GetValue(_verboseOption),
                Close = parseResult.GetValue(_closeOption),
                ShiftJis = parseResult.GetValue(_shiftJisOption)
            };
        }

        private CliArguments ExtractDump(ParseResult parseResult)
        {
            var arguments = BaseArguments(parseResult, TextToolAction.Dump, parseResult.GetValue(_dumpInputArgument)!, null);
            return arguments with
            {
                StartIndex = Either(parseResult, _startOption, _startIndexOption),
                EndIndex = Either(parseResult, _endOption, _endIndexOption),
                TrueOffsets = Either(parseResult, _trueOffsetsVerbOption, _trueOffsetsOption),
                NullStrings = Either(parseResult, _nullStringsVerbOption, _nullStringsOption)
            };
        }

        private CliArguments ExtractTwoFileVerb(
            ParseResult parseResult,
            TextToolAction action,
            Argument<string> inputArgument,
            Argument<string> csvArgument)
        {
            // The second file may be positional or given as --csv, so that a script keeps
            // working when only the verb is adopted.
            string? csv = parseResult.GetValue(csvArgument);
            if (string.IsNullOrEmpty(csv))
                csv = parseResult.GetValue(_csvOption);
            RequireCsv(action, csv);

            var arguments = BaseArguments(parseResult, action, parseResult.GetValue(inputArgument)!, csv);
            return action == TextToolAction.Insert
                ? arguments with { TrueOffsets = Either(parseResult, _trueOffsetsVerbOption, _trueOffsetsOption) }
                : arguments;
        }

        /// <summary>
        /// Extract arguments from the legacy flat form, warning about the flags that
        /// select a task now that a verb does the same job.
        /// </summary>
        private CliArguments ExtractLegacy(ParseResult parseResult)
        {
            string? file = parseResult.GetValue(_fileArgument);
            bool fulldump = parseResult.GetValue(_fulldumpOption);
            bool dump = parseResult.GetValue(_dumpOption);
            bool insert = parseResult.GetValue(_insertOption);
            bool merge = parseResult.GetValue(_mergeOption);
            bool cleanTrados = parseResult.GetValue(_cleanTradosOption);
            bool insertCat = parseResult.GetValue(_insertCatOption);

            int actionCount = (fulldump ? 1 : 0) + (dump ? 1 : 0) + (insert ? 1 : 0)
                            + (merge ? 1 : 0) + (cleanTrados ? 1 : 0) + (insertCat ? 1 : 0);

            if (actionCount == 0)
            {
                throw new InvalidOperationException(
                    "Error: No command given. Usage: FrontierTextTool <command> <file>\n" +
                    "Run 'FrontierTextTool --help' to see the available commands.");
            }
            if (actionCount > 1)
            {
                throw new InvalidOperationException("Error: Only one command can be given at a time.");
            }
            if (string.IsNullOrEmpty(file))
            {
                throw new InvalidOperationException(
                    "Error: No input file specified. Usage: FrontierTextTool <command> <file>");
            }

            int startIndex = parseResult.GetValue(_startIndexOption);
            int endIndex = parseResult.GetValue(_endIndexOption);
            string? csv = parseResult.GetValue(_csvOption);

            TextToolAction action;
            if (fulldump)
            {
                action = TextToolAction.Dump;
                // --fulldump ignored any range it was given; the verb reads a missing range
                // the same way, so drop it here rather than change what the file sees.
                startIndex = 0;
                endIndex = 0;
            }
            else if (dump)
            {
                action = TextToolAction.Dump;
            }
            else if (insert)
            {
                action = TextToolAction.Insert;
            }
            else if (merge)
            {
                action = TextToolAction.Merge;
            }
            else if (cleanTrados)
            {
                action = TextToolAction.CleanTrados;
            }
            else
            {
                action = TextToolAction.InsertCat;
            }

            WarnDeprecatedModeFlags(action, fulldump, file, csv, startIndex, endIndex);
            RequireCsv(action, csv);

            return new CliArguments
            {
                Action = action,
                InputPath = file,
                CsvPath = csv,
                StartIndex = startIndex,
                EndIndex = endIndex,
                TrueOffsets = parseResult.GetValue(_trueOffsetsOption),
                NullStrings = parseResult.GetValue(_nullStringsOption),
                Verbose = parseResult.GetValue(_verboseOption),
                Close = parseResult.GetValue(_closeOption),
                ShiftJis = parseResult.GetValue(_shiftJisOption)
            };
        }

        /// <summary>
        /// The tasks that read a second file cannot run without it.
        /// </summary>
        private static void RequireCsv(TextToolAction action, string? csv)
        {
            if (!string.IsNullOrEmpty(csv))
                return;

            (string verb, string usage)? shape = action switch
            {
                TextToolAction.Insert => ("insert", "FrontierTextTool insert <file> <csv>"),
                TextToolAction.Merge => ("merge", "FrontierTextTool merge <old-csv> <new-csv>"),
                TextToolAction.InsertCat => ("insert-cat", "FrontierTextTool insert-cat <cat-file> <csv>"),
                _ => null
            };
            if (shape != null)
            {
                throw new InvalidOperationException(
                    $"Error: '{shape.Value.verb}' needs a CSV file. Usage: {shape.Value.usage}");
            }
        }

        /// <summary>
        /// Point the task-selecting flags at their verb.
        /// </summary>
        private static void WarnDeprecatedModeFlags(
            TextToolAction action, bool fulldump, string file, string? csv, int startIndex, int endIndex)
        {
            string second = string.IsNullOrEmpty(csv) ? "<csv>" : csv;
            switch (action)
            {
                case TextToolAction.Dump:
                    string range = startIndex != 0 || endIndex != 0
                        ? $" --start-index {startIndex} --end-index {endIndex}"
                        : "";
                    CliDeprecation.WarnFlag(
                        fulldump ? "--fulldump" : "--dump",
                        $"FrontierTextTool dump {file}{range}");
                    break;
                case TextToolAction.Insert:
                    CliDeprecation.WarnFlag("--insert", $"FrontierTextTool insert {file} {second}");
                    break;
                case TextToolAction.Merge:
                    CliDeprecation.WarnFlag("--merge", $"FrontierTextTool merge {file} {second}");
                    break;
                case TextToolAction.CleanTrados:
                    CliDeprecation.WarnFlag("--cleanTrados", $"FrontierTextTool clean-trados {file}");
                    break;
                case TextToolAction.InsertCat:
                    CliDeprecation.WarnFlag("--insertCAT", $"FrontierTextTool insert-cat {file} {second}");
                    break;
                default:
                    break;
            }
        }
    }
}
