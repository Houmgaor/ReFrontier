using System;
using System.CommandLine;

using LibReFrontier.CLI;

namespace FrontierDataTool.CLI
{
    /// <summary>
    /// Defines the CLI schema for FrontierDataTool.
    /// <para>The interface has two shapes. The verb form
    /// (<c>FrontierDataTool import Armor.csv --mhfdat mhfdat.bin</c>) groups each task with
    /// only the options that apply to it. The legacy flat form
    /// (<c>FrontierDataTool --import --csv Armor.csv</c>) is still accepted so existing
    /// scripts keep working; the flags that select a task there are deprecated and warn.</para>
    /// </summary>
    public class CliSchema
    {
        // Legacy task flags. Hidden from help so the flat flag list no longer competes
        // with the verbs, but still parsed so existing command lines keep working.
        private readonly Option<bool> _dumpOption;
        private readonly Option<bool> _modshopOption;
        private readonly Option<bool> _importOption;

        // The same file options under their root spelling. Separate instances so they can
        // stay hidden on the root, where they only exist to keep old command lines parsing,
        // while the verbs list theirs in help.
        private readonly Option<string?> _suffixLegacyOption;
        private readonly Option<string?> _mhfpacLegacyOption;
        private readonly Option<string?> _mhfdatLegacyOption;
        private readonly Option<string?> _mhfinfLegacyOption;
        private readonly Option<string?> _rengokuLegacyOption;
        private readonly Option<string?> _csvOption;

        // File options the verbs offer.
        private readonly Option<string?> _suffixOption;
        private readonly Option<string?> _mhfpacOption;
        private readonly Option<string?> _mhfdatOption;
        private readonly Option<string?> _mhfinfOption;
        private readonly Option<string?> _rengokuOption;

        // Options shared by every command.
        private readonly Option<bool> _closeOption;
        private readonly Option<bool> _cp932Option;
        private readonly Option<bool> _shiftJisOption;
        private readonly Option<bool> _jsonOption;
        private readonly Option<bool> _englishSkillsOption;

        // Verb symbols.
        private readonly Argument<string> _csvArgument;
        private readonly Argument<string> _mhfdatArgument;

        // Verb commands, compared by reference when dispatching.
        private Command? _dumpCommand;
        private Command? _modshopCommand;
        private Command? _importCommand;

        private const string SuffixDescription = "Output suffix for files";
        private const string MhfPacDescription = "Path to mhfpac.bin";
        private const string MhfDatDescription = "Path to mhfdat.bin";
        private const string MhfInfDescription = "Path to mhfinf.bin";
        private const string RengokuDescription = "Path to rengoku_data.bin (Hunting Road data)";

        /// <summary>
        /// Creates a new CliSchema instance and initializes all CLI options.
        /// </summary>
        public CliSchema()
        {
            _dumpOption = new Option<bool>("--dump")
            {
                Description = "Extract weapon/armor/skill/quest data",
                Hidden = true
            };

            _modshopOption = new Option<bool>("--modshop")
            {
                Description = "Modify shop prices",
                Hidden = true
            };

            _importOption = new Option<bool>("--import")
            {
                Description = "Import modified CSV back into game files",
                Hidden = true
            };

            _suffixLegacyOption = new Option<string?>("--suffix") { Description = SuffixDescription, Hidden = true };
            _mhfpacLegacyOption = new Option<string?>("--mhfpac") { Description = MhfPacDescription, Hidden = true };
            _mhfdatLegacyOption = new Option<string?>("--mhfdat") { Description = MhfDatDescription, Hidden = true };
            _mhfinfLegacyOption = new Option<string?>("--mhfinf") { Description = MhfInfDescription, Hidden = true };
            _rengokuLegacyOption = new Option<string?>("--rengoku") { Description = RengokuDescription, Hidden = true };

            _suffixOption = new Option<string?>("--suffix") { Description = SuffixDescription };
            _mhfpacOption = new Option<string?>("--mhfpac") { Description = MhfPacDescription };
            _mhfdatOption = new Option<string?>("--mhfdat") { Description = MhfDatDescription };
            _mhfinfOption = new Option<string?>("--mhfinf") { Description = MhfInfDescription };
            _rengokuOption = new Option<string?>("--rengoku") { Description = RengokuDescription };

            _csvOption = new Option<string?>("--csv")
            {
                Description = "Path to the CSV file to import (e.g., Armor.csv)",
                Hidden = true
            };

            _closeOption = new Option<bool>("--close")
            {
                Description = "Close terminal after command",
                Recursive = true
            };

            _cp932Option = new Option<bool>("--cp932")
            {
                Description = "Output CSV files in CP932 (Windows-31J) encoding (default: UTF-8 with BOM)",
                Recursive = true
            };

            // The flag was called --shift-jis, which named a narrower encoding than the one
            // it selected. Still accepted, hidden from help, and silent: it is a renamed
            // option rather than a task, so nothing about a command line changes meaning.
            _shiftJisOption = new Option<bool>("--shift-jis")
            {
                Description = "Output CSV files in CP932 (Windows-31J) encoding (default: UTF-8 with BOM)",
                Recursive = true,
                Hidden = true
            };

            _jsonOption = new Option<bool>("--json")
            {
                Description = "Output JSON files instead of CSV",
                Recursive = true
            };

            _englishSkillsOption = new Option<bool>("--english-skills")
            {
                Description = "Write English skill tree names instead of the game's own"
            };

            _csvArgument = new Argument<string>("csv")
            {
                Description = "CSV to import; its name selects the importer (Armor, Melee, Ranged, InfQuests, Rengoku)",
                Arity = ArgumentArity.ZeroOrOne
            };

            _mhfdatArgument = new Argument<string>("mhfdat")
            {
                Description = "Path to mhfdat.bin",
                Arity = ArgumentArity.ZeroOrOne
            };
        }

        /// <summary>
        /// Creates the root command with every verb, plus the legacy flat form.
        /// </summary>
        /// <returns>The configured root command.</returns>
        public RootCommand CreateRootCommand()
        {
            _dumpCommand = new Command("dump", "Extract weapon, armor, skill and quest data")
            {
                _suffixOption,
                _mhfpacOption,
                _mhfdatOption,
                _mhfinfOption,
                _rengokuOption,
                _englishSkillsOption
            };

            _modshopCommand = new Command("modshop", "Rewrite shop prices in mhfdat.bin")
            {
                _mhfdatArgument,
                // Hidden legacy spelling, accepted but absent from help.
                _mhfdatLegacyOption
            };

            _importCommand = new Command("import", "Write an edited CSV back into the game files")
            {
                _csvArgument,
                _mhfdatOption,
                _mhfpacOption,
                _mhfinfOption,
                _rengokuOption,
                _csvOption
            };

            RootCommand rootCommand = new("FrontierDataTool - Extract and edit Monster Hunter Frontier game data")
            {
                _dumpOption,
                _modshopOption,
                _importOption,
                _suffixLegacyOption,
                _mhfpacLegacyOption,
                _mhfdatLegacyOption,
                _mhfinfLegacyOption,
                _rengokuLegacyOption,
                _csvOption,
                _closeOption,
                _cp932Option,
                _shiftJisOption,
                _jsonOption,
                _dumpCommand,
                _modshopCommand,
                _importCommand
            };

            // Adding subcommands makes System.CommandLine demand one, which would reject
            // the legacy form 'FrontierDataTool --dump ...'. An action on the root makes it
            // legal again; Main replaces this placeholder with the real handler.
            rootCommand.SetAction(_ => 0);

            return rootCommand;
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
        /// <exception cref="InvalidOperationException">The command line names no task or
        /// names more than one.</exception>
        public CliArguments ExtractArguments(ParseResult parseResult)
        {
            ArgumentNullException.ThrowIfNull(parseResult);

            var command = parseResult.CommandResult.Command;

            if (ReferenceEquals(command, _dumpCommand))
                return Common(parseResult, DataToolAction.Dump);
            if (ReferenceEquals(command, _modshopCommand))
                return ExtractModShop(parseResult);
            if (ReferenceEquals(command, _importCommand))
                return ExtractImport(parseResult);

            return ExtractLegacy(parseResult);
        }

        /// <summary>
        /// Build the parts of the DTO a verb shares with every other verb.
        /// </summary>
        private CliArguments Common(ParseResult parseResult, DataToolAction action)
        {
            return Globals(parseResult, action) with
            {
                Suffix = parseResult.GetValue(_suffixOption),
                MhfPac = parseResult.GetValue(_mhfpacOption),
                MhfDat = parseResult.GetValue(_mhfdatOption),
                MhfInf = parseResult.GetValue(_mhfinfOption),
                Rengoku = parseResult.GetValue(_rengokuOption),
                EnglishSkills = parseResult.GetValue(_englishSkillsOption)
            };
        }

        /// <summary>
        /// Same, reading the root's hidden copies of the file options.
        /// </summary>
        private CliArguments CommonLegacy(ParseResult parseResult, DataToolAction action)
        {
            return Globals(parseResult, action) with
            {
                Suffix = parseResult.GetValue(_suffixLegacyOption),
                MhfPac = parseResult.GetValue(_mhfpacLegacyOption),
                MhfDat = parseResult.GetValue(_mhfdatLegacyOption),
                MhfInf = parseResult.GetValue(_mhfinfLegacyOption),
                Rengoku = parseResult.GetValue(_rengokuLegacyOption)
            };
        }

        /// <summary>
        /// The options every command shares, whichever shape it was given in.
        /// </summary>
        private CliArguments Globals(ParseResult parseResult, DataToolAction action)
        {
            return new CliArguments
            {
                Action = action,
                Close = parseResult.GetValue(_closeOption),
                Cp932 = parseResult.GetValue(_cp932Option) || parseResult.GetValue(_shiftJisOption),
                Json = parseResult.GetValue(_jsonOption)
            };
        }

        private CliArguments ExtractModShop(ParseResult parseResult)
        {
            // The file may be positional or given as --mhfdat, so that a script keeps
            // working when only the verb is adopted.
            string? mhfdat = parseResult.GetValue(_mhfdatArgument);
            if (string.IsNullOrEmpty(mhfdat))
                mhfdat = parseResult.GetValue(_mhfdatLegacyOption);

            return Globals(parseResult, DataToolAction.ModShop) with { MhfDat = mhfdat };
        }

        private CliArguments ExtractImport(ParseResult parseResult)
        {
            string? csv = parseResult.GetValue(_csvArgument);
            if (string.IsNullOrEmpty(csv))
                csv = parseResult.GetValue(_csvOption);

            var arguments = Common(parseResult, DataToolAction.Import);
            return arguments with { CsvPath = csv };
        }

        /// <summary>
        /// Extract arguments from the legacy flat form, warning about the flags that
        /// select a task now that a verb does the same job.
        /// </summary>
        private CliArguments ExtractLegacy(ParseResult parseResult)
        {
            bool dump = parseResult.GetValue(_dumpOption);
            bool modshop = parseResult.GetValue(_modshopOption);
            bool import = parseResult.GetValue(_importOption);

            int actionCount = (dump ? 1 : 0) + (modshop ? 1 : 0) + (import ? 1 : 0);
            if (actionCount == 0)
            {
                throw new InvalidOperationException(
                    "Error: No command given. Usage: FrontierDataTool <command> [options]\n" +
                    "Run 'FrontierDataTool --help' to see the available commands.");
            }
            if (actionCount > 1)
            {
                throw new InvalidOperationException("Error: Only one command can be given at a time.");
            }

            DataToolAction action = dump
                ? DataToolAction.Dump
                : modshop ? DataToolAction.ModShop : DataToolAction.Import;

            var arguments = CommonLegacy(parseResult, action) with { CsvPath = parseResult.GetValue(_csvOption) };
            WarnDeprecatedModeFlags(arguments);
            return arguments;
        }

        /// <summary>
        /// Point the task-selecting flags at their verb.
        /// </summary>
        private static void WarnDeprecatedModeFlags(CliArguments arguments)
        {
            switch (arguments.Action)
            {
                case DataToolAction.Dump:
                    CliDeprecation.WarnFlag("--dump", $"FrontierDataTool dump{DumpOptions(arguments)}");
                    break;
                case DataToolAction.ModShop:
                    CliDeprecation.WarnFlag(
                        "--modshop",
                        $"FrontierDataTool modshop {arguments.MhfDat ?? "<mhfdat.bin>"}");
                    break;
                case DataToolAction.Import:
                    CliDeprecation.WarnFlag(
                        "--import",
                        $"FrontierDataTool import {arguments.CsvPath ?? "<file.csv>"}{ImportOptions(arguments)}");
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Echo back only the file options the command line actually gave.
        /// </summary>
        private static string DumpOptions(CliArguments arguments)
        {
            return Option("--suffix", arguments.Suffix)
                 + Option("--mhfpac", arguments.MhfPac)
                 + Option("--mhfdat", arguments.MhfDat)
                 + Option("--mhfinf", arguments.MhfInf)
                 + Option("--rengoku", arguments.Rengoku);
        }

        private static string ImportOptions(CliArguments arguments)
        {
            return Option("--mhfdat", arguments.MhfDat)
                 + Option("--mhfpac", arguments.MhfPac)
                 + Option("--mhfinf", arguments.MhfInf)
                 + Option("--rengoku", arguments.Rengoku);
        }

        private static string Option(string name, string? value)
        {
            return string.IsNullOrEmpty(value) ? "" : $" {name} {value}";
        }
    }
}
