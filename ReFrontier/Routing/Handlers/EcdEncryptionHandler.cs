using System.IO;

using LibReFrontier;
using LibReFrontier.Abstractions;

using ReFrontier.Services;

namespace ReFrontier.Routing.Handlers
{
    /// <summary>
    /// Handler for ECD encrypted files.
    /// Note: Recursive decryption is handled by ProcessFile, not this handler.
    /// </summary>
    public class EcdEncryptionHandler : IFileTypeHandler
    {
        private readonly ILogger _logger;
        private readonly FileProcessingService _fileProcessingService;

        /// <summary>
        /// Create a new EcdEncryptionHandler.
        /// </summary>
        /// <param name="logger">Logger for output.</param>
        /// <param name="fileProcessingService">Service for file processing operations.</param>
        public EcdEncryptionHandler(ILogger logger, FileProcessingService fileProcessingService)
        {
            _logger = logger;
            _fileProcessingService = fileProcessingService;
        }

        /// <inheritdoc/>
        public bool CanHandle(uint fileMagic, InputArguments args)
        {
            return fileMagic == FileMagic.ECD && !args.noDecryption;
        }

        /// <inheritdoc/>
        public int Priority => 100;

        /// <inheritdoc/>
        public ProcessFileResult Handle(string filePath, BinaryReader reader, InputArguments args)
        {
            if (!args.quiet)
                _logger.WriteLine("ECD Header detected.");

            var outputPath = _fileProcessingService.DecryptEcdFile(
                filePath,
                args.createLog,
                args.cleanUp,
                args.quiet
            );

            return ProcessFileResult.Success(outputPath);
        }
    }
}
