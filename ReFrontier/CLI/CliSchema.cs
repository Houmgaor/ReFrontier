using System;
using System.CommandLine;

using LibReFrontier;

namespace ReFrontier.CLI
{
    /// <summary>
    /// Defines the CLI schema for ReFrontier command-line interface.
    /// <para>The interface has two shapes. The verb form (<c>ReFrontier unpack file.bin</c>)
    /// groups each mode with only the options that apply to it. The legacy flat form
    /// (<c>ReFrontier file.bin --decryptOnly</c>) is still accepted so existing scripts keep
    /// working; the flags that select a mode there are deprecated and warn.</para>
    /// </summary>
    public class CliSchema
    {
        // Legacy root symbols. Hidden from help so the flat flag list no longer competes
        // with the verbs, but still parsed so existing command lines keep working.
        private readonly Argument<string> _fileArgument;
        private readonly Option<string?> _fileOption;
        private readonly Option<bool> _logOption;
        private readonly Option<bool> _noMetaOption;
        private readonly Option<bool> _stageContainerOption;
        private readonly Option<bool> _autoStageOption;
        private readonly Option<bool> _nonRecursiveOption;
        private readonly Option<bool> _decryptOnlyOption;
        private readonly Option<bool> _noDecryptionOption;
        private readonly Option<bool> _ignoreJPKOption;
        private readonly Option<bool> _cleanUpOption;
        private readonly Option<bool> _packOption;
        private readonly Option<string?> _compressTypeOption;
        private readonly Option<int> _compressLevelOption;
        private readonly Option<bool> _encryptOption;
        private readonly Option<bool> _validateOption;
        private readonly Option<string?> _diffOption;
        private readonly Option<bool> _restoreOption;

        // Options shared by every command.
        private readonly Option<int> _parallelismOption;
        private readonly Option<bool> _quietOption;
        private readonly Option<bool> _verboseOption;

        // Verb symbols.
        private readonly Argument<string> _verbInputArgument;
        private readonly Argument<string> _diffLeftArgument;
        private readonly Argument<string> _diffRightArgument;
        private readonly Option<bool> _flatOption;
        private readonly Option<bool> _keepCompressedOption;
        private readonly Option<bool> _keepEncryptedOption;
        private readonly Option<bool> _stageOption;
        private readonly Option<bool> _autoStageVerbOption;
        private readonly Option<bool> _cleanOption;
        private readonly Option<bool> _noMetaVerbOption;
        private readonly Option<string?> _typeOption;
        private readonly Option<int> _compressLevelVerbOption;
        private readonly Option<int> _restoreLevelVerbOption;
        private readonly Option<bool> _encryptVerbOption;

        // Verb commands, compared by reference when dispatching.
        private Command? _unpackCommand;
        private Command? _decryptCommand;
        private Command? _packCommand;
        private Command? _restoreCommand;
        private Command? _compressCommand;
        private Command? _encryptCommand;
        private Command? _validateCommand;
        private Command? _diffCommand;

        /// <summary>
        /// Default compression level used by the <c>compress</c> verb when none is given.
        /// </summary>
        public const int DefaultCompressionLevel = 80;

        /// <summary>
        /// Creates a new CliSchema instance and initializes all CLI options.
        /// </summary>
        public CliSchema()
        {
            // Arguments
            _fileArgument = new Argument<string>("inputPath")
            {
                Description = "Input file or directory to process",
                Arity = ArgumentArity.ZeroOrOne,
                Hidden = true
            };

            // Deprecated alias for backward compatibility
            _fileOption = new Option<string?>("--file")
            {
                Description = "[Deprecated] Use positional argument instead. Input file or directory to process.",
                Hidden = true
            };

            // Unpacking options
            // Metadata is written by default: without it a file cannot be rebuilt, and
            // the option to produce it was easy to forget until the rebuild failed.
            _logOption = new Option<bool>("--saveMeta")
            {
                Description = "[Deprecated] Metadata is now saved by default. Use --no-meta to disable.",
                Hidden = true
            };

            _noMetaOption = new Option<bool>("--noMeta")
            {
                Description = "Do not write metadata (.meta, .log, .recipe.json). Rebuilding will not be possible.",
                Hidden = true
            };

            _stageContainerOption = new Option<bool>("--stageContainer")
            {
                Description = "Unpack file as stage-specific container",
                Hidden = true
            };

            _autoStageOption = new Option<bool>("--autoStage")
            {
                Description = "Automatically attempt to unpack containers that might be stage-specific",
                Hidden = true
            };

            _nonRecursiveOption = new Option<bool>("--nonRecursive")
            {
                Description = "Do not unpack recursively",
                Hidden = true
            };

            _decryptOnlyOption = new Option<bool>("--decryptOnly")
            {
                Description = "Decrypt ECD files without unpacking",
                Hidden = true
            };

            _noDecryptionOption = new Option<bool>("--noDecryption")
            {
                Description = "Don't decrypt ECD files, no unpacking",
                Hidden = true
            };

            _ignoreJPKOption = new Option<bool>("--ignoreJPK")
            {
                Description = "Do not decompress JPK files",
                Hidden = true
            };

            _cleanUpOption = new Option<bool>("--cleanUp")
            {
                Description = "Delete simple archives after unpacking",
                Hidden = true
            };

            // Packing options
            _packOption = new Option<bool>("--pack")
            {
                Description = "Repack directory (requires log file)",
                Hidden = true
            };

            _compressTypeOption = new Option<string?>("--compress")
            {
                Description = "Compression type: rw, hfirw, lz, hfi (or numeric: 0, 2, 3, 4)",
                Hidden = true
            };

            _compressLevelOption = new Option<int>("--level")
            {
                Description = "Compression level (e.g., 50, 100)",
                DefaultValueFactory = _ => 0,
                Hidden = true
            };

            _encryptOption = new Option<bool>("--encrypt")
            {
                Description = "Encrypt input file with ECD algorithm",
                Hidden = true
            };

            _validateOption = new Option<bool>("--validate")
            {
                Description = "Validate file integrity without extracting (checks CRC32, structure, bounds)",
                Hidden = true
            };

            _diffOption = new Option<string?>("--diff")
            {
                Description = "Compare structurally against another file",
                Hidden = true
            };

            _restoreOption = new Option<bool>("--restore")
            {
                Description = "Rebuild a file using the recipe saved during extraction",
                Hidden = true
            };

            // Options every command accepts. Recursive so they work before or after the verb.
            _parallelismOption = new Option<int>("--parallelism")
            {
                Description = "Number of parallel threads (0 = auto-detect, default: 0)",
                DefaultValueFactory = _ => 0,
                Recursive = true
            };

            _quietOption = new Option<bool>("--quiet")
            {
                Description = "Suppress progress bar during processing",
                Recursive = true
            };

            _verboseOption = new Option<bool>("--verbose")
            {
                Description = "Show per-file processing messages",
                Recursive = true
            };

            // Verb symbols. The camelCase spellings are accepted too, as the hidden legacy
            // options added to each verb below, so a script can adopt the verbs without also
            // having to rename every option on the same line.
            _verbInputArgument = new Argument<string>("inputPath")
            {
                Description = "File or directory to process"
            };

            _diffLeftArgument = new Argument<string>("first")
            {
                Description = "First file to compare"
            };

            _diffRightArgument = new Argument<string>("second")
            {
                Description = "Second file to compare"
            };

            _flatOption = new Option<bool>("--flat")
            {
                Description = "Do not unpack nested containers"
            };

            _keepCompressedOption = new Option<bool>("--keep-compressed")
            {
                Description = "Do not decompress JPK payloads"
            };

            _keepEncryptedOption = new Option<bool>("--keep-encrypted")
            {
                Description = "Do not decrypt ECD files"
            };

            _stageOption = new Option<bool>("--stage")
            {
                Description = "Treat the input as a stage-specific container"
            };

            _autoStageVerbOption = new Option<bool>("--auto-stage")
            {
                Description = "Detect and unpack stage-specific containers automatically"
            };

            _cleanOption = new Option<bool>("--clean")
            {
                Description = "Delete source archives after unpacking"
            };

            _noMetaVerbOption = new Option<bool>("--no-meta")
            {
                Description = "Do not write metadata (.meta, .log, .recipe.json); rebuilding becomes impossible"
            };

            _typeOption = new Option<string?>("--type")
            {
                Description = "Compression algorithm: rw, hfirw, lz, hfi (or numeric: 0, 2, 3, 4)",
                Required = true
            };

            _compressLevelVerbOption = new Option<int>("--level")
            {
                Description = "Compression level 1-100",
                DefaultValueFactory = _ => DefaultCompressionLevel
            };

            // No default factory: an unspecified level reads as 0, meaning "keep the level
            // the recipe recorded" rather than any particular level.
            _restoreLevelVerbOption = new Option<int>("--level")
            {
                Description = "Override the compression level recorded in the recipe"
            };

            _encryptVerbOption = new Option<bool>("--encrypt")
            {
                Description = "Encrypt the compressed output, producing a game-ready file"
            };
        }

        /// <summary>
        /// Creates a RootCommand with all verbs and options configured.
        /// </summary>
        /// <param name="version">Application version string.</param>
        /// <param name="productName">Product name.</param>
        /// <param name="description">Application description.</param>
        /// <returns>Configured RootCommand.</returns>
        public RootCommand CreateRootCommand(string version, string productName, string description)
        {
            var rootCommand = new RootCommand($"{productName} - {description}, by MHVuze, additions by Houmgaor")
            {
                _fileArgument,
                _fileOption,
                _logOption,
                _noMetaOption,
                _stageContainerOption,
                _autoStageOption,
                _nonRecursiveOption,
                _decryptOnlyOption,
                _noDecryptionOption,
                _ignoreJPKOption,
                _cleanUpOption,
                _packOption,
                _compressTypeOption,
                _compressLevelOption,
                _encryptOption,
                _parallelismOption,
                _quietOption,
                _verboseOption,
                _validateOption,
                _diffOption,
                _restoreOption
            };

            _unpackCommand = new Command("unpack", "Decrypt, decompress and unpack a file or directory")
            {
                _verbInputArgument,
                _flatOption,
                _keepCompressedOption,
                _keepEncryptedOption,
                _stageOption,
                _autoStageVerbOption,
                _cleanOption,
                _noMetaVerbOption,
                // Hidden legacy spellings, accepted but absent from help.
                _nonRecursiveOption,
                _ignoreJPKOption,
                _noDecryptionOption,
                _stageContainerOption,
                _autoStageOption,
                _cleanUpOption,
                _noMetaOption
            };

            _decryptCommand = new Command("decrypt", "Decrypt an ECD or EXF file without unpacking it")
            {
                _verbInputArgument,
                _cleanOption,
                _noMetaVerbOption,
                _cleanUpOption,
                _noMetaOption
            };

            _packCommand = new Command("pack", "Repack a directory that was produced by unpack")
            {
                _verbInputArgument,
                _noMetaVerbOption,
                _noMetaOption
            };

            _restoreCommand = new Command("restore", "Rebuild a file from the recipe written during extraction")
            {
                _verbInputArgument,
                _restoreLevelVerbOption
            };

            _compressCommand = new Command("compress", "Compress a file with a JPK algorithm")
            {
                _verbInputArgument,
                _typeOption,
                _compressLevelVerbOption,
                _encryptVerbOption
            };

            _encryptCommand = new Command("encrypt", "Encrypt a file with the ECD algorithm")
            {
                _verbInputArgument
            };

            _validateCommand = new Command("validate", "Check file integrity without writing any output")
            {
                _verbInputArgument
            };

            _diffCommand = new Command("diff", "Compare two files structurally")
            {
                _diffLeftArgument,
                _diffRightArgument
            };

            rootCommand.Add(_unpackCommand);
            rootCommand.Add(_decryptCommand);
            rootCommand.Add(_packCommand);
            rootCommand.Add(_restoreCommand);
            rootCommand.Add(_compressCommand);
            rootCommand.Add(_encryptCommand);
            rootCommand.Add(_validateCommand);
            rootCommand.Add(_diffCommand);

            // Adding subcommands makes System.CommandLine demand one, which would reject the
            // bare form 'ReFrontier file.bin'. An action on the root makes it legal again;
            // Main replaces this placeholder with the real handler.
            rootCommand.SetAction(_ => 0);

            return rootCommand;
        }

        /// <summary>
        /// Names of the verbs, used to spot a path that looks like a command.
        /// </summary>
        private static readonly string[] VerbNames = [
            "unpack",
            "decrypt",
            "pack",
            "restore",
            "compress",
            "encrypt",
            "validate",
            "diff"
        ];

        /// <summary>
        /// A verb name wins over a path that happens to be spelled the same way. When the
        /// only argument is such a path, say so rather than let the missing-argument error
        /// stand on its own.
        /// </summary>
        /// <param name="args">Raw command line arguments.</param>
        /// <param name="fileSystem">File system used to check whether the path exists.</param>
        /// <returns>A message to show, or null when there is no ambiguity.</returns>
        public static string? DescribeVerbPathCollision(string[] args, LibReFrontier.Abstractions.IFileSystem fileSystem)
        {
            ArgumentNullException.ThrowIfNull(args);
            ArgumentNullException.ThrowIfNull(fileSystem);

            if (args.Length != 1)
                return null;

            string candidate = args[0];
            if (Array.IndexOf(VerbNames, candidate) < 0)
                return null;
            if (!fileSystem.FileExists(candidate) && !fileSystem.DirectoryExists(candidate))
                return null;

            return $"Note: '{candidate}' is both a command and a path that exists here; the command wins.\n"
                 + $"  To act on the path, name it explicitly: ReFrontier unpack .{System.IO.Path.DirectorySeparatorChar}{candidate}";
        }

        /// <summary>
        /// Extracts parsed arguments from a ParseResult into a CliArguments DTO.
        /// </summary>
        /// <param name="parseResult">The parsed command-line result.</param>
        /// <returns>CliArguments containing all parsed values.</returns>
        public CliArguments ExtractArguments(ParseResult parseResult)
        {
            ArgumentNullException.ThrowIfNull(parseResult);

            var command = parseResult.CommandResult.Command;

            if (ReferenceEquals(command, _unpackCommand))
                return ExtractUnpack(parseResult);
            if (ReferenceEquals(command, _decryptCommand))
                return ExtractDecrypt(parseResult);
            if (ReferenceEquals(command, _packCommand))
                return ExtractPack(parseResult);
            if (ReferenceEquals(command, _restoreCommand))
                return ExtractRestore(parseResult);
            if (ReferenceEquals(command, _compressCommand))
                return ExtractCompress(parseResult);
            if (ReferenceEquals(command, _encryptCommand))
                return ExtractEncrypt(parseResult);
            if (ReferenceEquals(command, _validateCommand))
                return ExtractValidate(parseResult);
            if (ReferenceEquals(command, _diffCommand))
                return ExtractDiff(parseResult);

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
        /// True when metadata was switched off, under either spelling.
        /// </summary>
        private bool NoMeta(ParseResult parseResult)
        {
            return Either(parseResult, _noMetaVerbOption, _noMetaOption);
        }

        /// <summary>
        /// Build the parts of the DTO that every command shares.
        /// </summary>
        private CliArguments BaseArguments(ParseResult parseResult, string filePath, InputArguments processingArgs)
        {
            return new CliArguments
            {
                FilePath = filePath,
                ProcessingArgs = processingArgs,
                Parallelism = parseResult.GetValue(_parallelismOption),
                Quiet = parseResult.GetValue(_quietOption),
                Verbose = parseResult.GetValue(_verboseOption)
            };
        }

        private CliArguments ExtractUnpack(ParseResult parseResult)
        {
            var processingArgs = new InputArguments
            {
                createLog = !NoMeta(parseResult),
                recursive = !Either(parseResult, _flatOption, _nonRecursiveOption),
                ignoreJPK = Either(parseResult, _keepCompressedOption, _ignoreJPKOption),
                noDecryption = Either(parseResult, _keepEncryptedOption, _noDecryptionOption),
                stageContainer = Either(parseResult, _stageOption, _stageContainerOption),
                autoStage = Either(parseResult, _autoStageVerbOption, _autoStageOption),
                cleanUp = Either(parseResult, _cleanOption, _cleanUpOption),
                compression = new Compression()
            };
            return BaseArguments(parseResult, parseResult.GetValue(_verbInputArgument)!, processingArgs);
        }

        private CliArguments ExtractDecrypt(ParseResult parseResult)
        {
            var processingArgs = new InputArguments
            {
                createLog = !NoMeta(parseResult),
                recursive = true,
                decryptOnly = true,
                cleanUp = Either(parseResult, _cleanOption, _cleanUpOption),
                compression = new Compression()
            };
            return BaseArguments(parseResult, parseResult.GetValue(_verbInputArgument)!, processingArgs);
        }

        private CliArguments ExtractPack(ParseResult parseResult)
        {
            var processingArgs = new InputArguments
            {
                createLog = !NoMeta(parseResult),
                recursive = true,
                repack = true,
                compression = new Compression()
            };
            return BaseArguments(parseResult, parseResult.GetValue(_verbInputArgument)!, processingArgs);
        }

        private CliArguments ExtractRestore(ParseResult parseResult)
        {
            var processingArgs = new InputArguments
            {
                createLog = true,
                recursive = true,
                compression = new Compression()
            };
            int level = parseResult.GetValue(_restoreLevelVerbOption);
            var args = BaseArguments(parseResult, parseResult.GetValue(_verbInputArgument)!, processingArgs);
            return args with { Restore = true, CompressionLevel = level > 0 ? level : null };
        }

        private CliArguments ExtractCompress(ParseResult parseResult)
        {
            int level = parseResult.GetValue(_compressLevelVerbOption);
            if (level == 0)
                level = DefaultCompressionLevel;

            var type = parseResult.GetValue(_typeOption);
            var processingArgs = new InputArguments
            {
                createLog = true,
                recursive = true,
                encrypt = parseResult.GetValue(_encryptVerbOption),
                compression = ArgumentsParser.ParseCompression(type!, level)
            };
            var args = BaseArguments(parseResult, parseResult.GetValue(_verbInputArgument)!, processingArgs);
            return args with { CompressionLevel = level };
        }

        private CliArguments ExtractEncrypt(ParseResult parseResult)
        {
            var processingArgs = new InputArguments
            {
                createLog = true,
                recursive = true,
                encrypt = true,
                compression = new Compression()
            };
            return BaseArguments(parseResult, parseResult.GetValue(_verbInputArgument)!, processingArgs);
        }

        private CliArguments ExtractValidate(ParseResult parseResult)
        {
            var processingArgs = new InputArguments
            {
                createLog = true,
                recursive = true,
                compression = new Compression()
            };
            var args = BaseArguments(parseResult, parseResult.GetValue(_verbInputArgument)!, processingArgs);
            return args with { Validate = true };
        }

        private CliArguments ExtractDiff(ParseResult parseResult)
        {
            var processingArgs = new InputArguments
            {
                createLog = true,
                recursive = true,
                compression = new Compression()
            };
            var args = BaseArguments(parseResult, parseResult.GetValue(_diffLeftArgument)!, processingArgs);
            return args with { DiffPath = parseResult.GetValue(_diffRightArgument) };
        }

        /// <summary>
        /// Extract arguments from the legacy flat form, warning about the flags that
        /// select a mode now that a verb does the same job.
        /// </summary>
        private CliArguments ExtractLegacy(ParseResult parseResult)
        {
            var fileArg = parseResult.GetValue(_fileArgument);
            var fileOpt = parseResult.GetValue(_fileOption);

            // --file option takes precedence for backward compatibility
            string? file;
            if (!string.IsNullOrEmpty(fileOpt))
            {
                Console.Error.WriteLine("Warning: --file is deprecated. Use positional argument instead: ReFrontier <inputPath>");
                file = fileOpt;
            }
            else
            {
                file = fileArg;
            }

            if (string.IsNullOrEmpty(file))
            {
                throw new InvalidOperationException(
                    "Error: No input file or directory specified. Usage: ReFrontier <command> <inputPath>\n" +
                    "Run 'ReFrontier --help' to see the available commands."
                );
            }

            var noMeta = parseResult.GetValue(_noMetaOption);
            var log = !noMeta;
            if (parseResult.GetValue(_logOption))
            {
                Console.Error.WriteLine(
                    "Warning: --saveMeta is deprecated. Metadata is saved by default; use --no-meta to disable it."
                );
            }
            var stageContainer = parseResult.GetValue(_stageContainerOption);
            var autoStage = parseResult.GetValue(_autoStageOption);
            var nonRecursive = parseResult.GetValue(_nonRecursiveOption);
            var decryptOnly = parseResult.GetValue(_decryptOnlyOption);
            var noDecryption = parseResult.GetValue(_noDecryptionOption);
            var ignoreJPK = parseResult.GetValue(_ignoreJPKOption);
            var cleanUp = parseResult.GetValue(_cleanUpOption);
            var pack = parseResult.GetValue(_packOption);
            var compressType = parseResult.GetValue(_compressTypeOption);
            var compressLevel = parseResult.GetValue(_compressLevelOption);
            var encrypt = parseResult.GetValue(_encryptOption);
            var parallelism = parseResult.GetValue(_parallelismOption);
            var quiet = parseResult.GetValue(_quietOption);
            var verbose = parseResult.GetValue(_verboseOption);
            var validate = parseResult.GetValue(_validateOption);
            var diffPath = parseResult.GetValue(_diffOption);
            var restore = parseResult.GetValue(_restoreOption);

            WarnDeprecatedModeFlags(file, decryptOnly, pack, restore, validate, diffPath, compressType, compressLevel, encrypt);

            // Parse compression if specified
            Compression compression = new();
            if (!string.IsNullOrEmpty(compressType))
            {
                if (compressLevel <= 0)
                {
                    throw new InvalidOperationException(
                        "Error: --level is required when using --compress. Example: --compress lz --level 100"
                    );
                }
                compression = ArgumentsParser.ParseCompression(compressType, compressLevel);
            }

            // Build input arguments
            var processingArgs = new InputArguments
            {
                createLog = log,
                recursive = !nonRecursive,
                repack = pack,
                decryptOnly = decryptOnly,
                noDecryption = noDecryption,
                encrypt = encrypt,
                cleanUp = cleanUp,
                ignoreJPK = ignoreJPK,
                stageContainer = stageContainer,
                autoStage = autoStage,
                compression = compression
            };

            return new CliArguments
            {
                FilePath = file,
                ProcessingArgs = processingArgs,
                Parallelism = parallelism,
                Quiet = quiet,
                Verbose = verbose,
                Validate = validate,
                DiffPath = diffPath,
                Restore = restore,
                CompressionLevel = compressLevel > 0 ? compressLevel : null
            };
        }

        /// <summary>
        /// Point the mode-selecting flags at their verb. The bare form
        /// (<c>ReFrontier file.bin</c>) is not deprecated and stays silent.
        /// </summary>
        private static void WarnDeprecatedModeFlags(
            string file,
            bool decryptOnly,
            bool pack,
            bool restore,
            bool validate,
            string? diffPath,
            string? compressType,
            int compressLevel,
            bool encrypt)
        {
            if (decryptOnly)
                WarnFlag("--decryptOnly", $"ReFrontier decrypt {file}");
            if (pack)
                WarnFlag("--pack", $"ReFrontier pack {file}");
            if (restore)
                WarnFlag("--restore", $"ReFrontier restore {file}");
            if (validate)
                WarnFlag("--validate", $"ReFrontier validate {file}");
            if (!string.IsNullOrEmpty(diffPath))
                WarnFlag("--diff", $"ReFrontier diff {file} {diffPath}");
            if (!string.IsNullOrEmpty(compressType))
            {
                string level = compressLevel > 0 ? $" --level {compressLevel}" : "";
                string encryptSuffix = encrypt ? " --encrypt" : "";
                WarnFlag("--compress", $"ReFrontier compress {file} --type {compressType}{level}{encryptSuffix}");
            }
            else if (encrypt)
            {
                WarnFlag("--encrypt", $"ReFrontier encrypt {file}");
            }
        }

        private static void WarnFlag(string flag, string replacement)
        {
            Console.Error.WriteLine($"Warning: {flag} is deprecated and will be removed in a future release.");
            Console.Error.WriteLine($"  Use: {replacement}");
        }
    }
}
