using System.CommandLine;
using LibReFrontier;

namespace ReFrontier.CLI
{
    /// <summary>
    /// Defines the CLI schema for ReFrontier command-line interface.
    /// </summary>
    public class CliSchema
    {
        private readonly Argument<string> _fileArgument;
        private readonly Option<bool> _logOption;
        private readonly Option<bool> _stageContainerOption;
        private readonly Option<bool> _autoStageOption;
        private readonly Option<bool> _nonRecursiveOption;
        private readonly Option<bool> _decryptOnlyOption;
        private readonly Option<bool> _noDecryptionOption;
        private readonly Option<bool> _ignoreJPKOption;
        private readonly Option<bool> _noFileRewriteOption;
        private readonly Option<bool> _cleanUpOption;
        private readonly Option<bool> _packOption;
        private readonly Option<string?> _compressTypeOption;
        private readonly Option<int> _compressLevelOption;
        private readonly Option<bool> _encryptOption;
        private readonly Option<bool> _closeOption;

        /// <summary>
        /// Creates a new CliSchema instance and initializes all CLI options.
        /// </summary>
        public CliSchema()
        {
            // Arguments
            _fileArgument = new Argument<string>("file")
            {
                Description = "Input file or directory to process"
            };

            // Unpacking options
            _logOption = new Option<bool>("--log")
            {
                Description = "Write log file (required for re-encryption)"
            };

            _stageContainerOption = new Option<bool>("--stageContainer")
            {
                Description = "Unpack file as stage-specific container"
            };

            _autoStageOption = new Option<bool>("--autoStage")
            {
                Description = "Automatically attempt to unpack containers that might be stage-specific"
            };

            _nonRecursiveOption = new Option<bool>("--nonRecursive")
            {
                Description = "Do not unpack recursively"
            };

            _decryptOnlyOption = new Option<bool>("--decryptOnly")
            {
                Description = "Decrypt ECD files without unpacking"
            };

            _noDecryptionOption = new Option<bool>("--noDecryption")
            {
                Description = "Don't decrypt ECD files, no unpacking"
            };

            _ignoreJPKOption = new Option<bool>("--ignoreJPK")
            {
                Description = "Do not decompress JPK files"
            };

            _noFileRewriteOption = new Option<bool>("--noFileRewrite")
            {
                Description = "Avoid rewriting original files"
            };

            _cleanUpOption = new Option<bool>("--cleanUp")
            {
                Description = "Delete simple archives after unpacking"
            };

            // Packing options
            _packOption = new Option<bool>("--pack")
            {
                Description = "Repack directory (requires log file)"
            };

            _compressTypeOption = new Option<string?>("--compress")
            {
                Description = "Compression type: rw, hfirw, lz, hfi (or numeric: 0, 2, 3, 4)"
            };

            _compressLevelOption = new Option<int>("--level")
            {
                Description = "Compression level (e.g., 50, 100)",
                DefaultValueFactory = _ => 0
            };

            _encryptOption = new Option<bool>("--encrypt")
            {
                Description = "Encrypt input file with ECD algorithm"
            };

            // General options
            _closeOption = new Option<bool>("--close")
            {
                Description = "Close window after finishing process"
            };
        }

        /// <summary>
        /// Creates a RootCommand with all CLI options configured.
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
                _logOption,
                _stageContainerOption,
                _autoStageOption,
                _nonRecursiveOption,
                _decryptOnlyOption,
                _noDecryptionOption,
                _ignoreJPKOption,
                _noFileRewriteOption,
                _cleanUpOption,
                _packOption,
                _compressTypeOption,
                _compressLevelOption,
                _encryptOption,
                _closeOption
            };

            return rootCommand;
        }

        /// <summary>
        /// Extracts parsed arguments from a ParseResult into a CliArguments DTO.
        /// </summary>
        /// <param name="parseResult">The parsed command-line result.</param>
        /// <returns>CliArguments containing all parsed values.</returns>
        public CliArguments ExtractArguments(ParseResult parseResult)
        {
            var file = parseResult.GetValue(_fileArgument)!;
            var log = parseResult.GetValue(_logOption);
            var stageContainer = parseResult.GetValue(_stageContainerOption);
            var autoStage = parseResult.GetValue(_autoStageOption);
            var nonRecursive = parseResult.GetValue(_nonRecursiveOption);
            var decryptOnly = parseResult.GetValue(_decryptOnlyOption);
            var noDecryption = parseResult.GetValue(_noDecryptionOption);
            var ignoreJPK = parseResult.GetValue(_ignoreJPKOption);
            var noFileRewrite = parseResult.GetValue(_noFileRewriteOption);
            var cleanUp = parseResult.GetValue(_cleanUpOption);
            var pack = parseResult.GetValue(_packOption);
            var compressType = parseResult.GetValue(_compressTypeOption);
            var compressLevel = parseResult.GetValue(_compressLevelOption);
            var encrypt = parseResult.GetValue(_encryptOption);
            var close = parseResult.GetValue(_closeOption);

            // Parse compression if specified
            Compression compression = new();
            if (!string.IsNullOrEmpty(compressType))
            {
                if (compressLevel <= 0)
                {
                    throw new System.InvalidOperationException(
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
                rewriteOldFile = !noFileRewrite,
                compression = compression
            };

            return new CliArguments
            {
                FilePath = file,
                ProcessingArgs = processingArgs,
                CloseAfterCompletion = close
            };
        }
    }
}
