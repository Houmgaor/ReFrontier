using System.IO;

using LibReFrontier;
using LibReFrontier.Abstractions;

using ReFrontier.Services;

namespace ReFrontier.Routing.Handlers
{
    /// <summary>
    /// Handler for EXF encrypted files.
    /// </summary>
    public class ExfEncryptionHandler : IFileTypeHandler
    {
        private readonly ILogger _logger;
        private readonly FileProcessingService _fileProcessingService;

        /// <summary>
        /// Create a new ExfEncryptionHandler.
        /// </summary>
        /// <param name="logger">Logger for output.</param>
        /// <param name="fileProcessingService">Service for file processing operations.</param>
        public ExfEncryptionHandler(ILogger logger, FileProcessingService fileProcessingService)
        {
            _logger = logger;
            _fileProcessingService = fileProcessingService;
        }

        /// <inheritdoc/>
        public bool CanHandle(uint fileMagic, InputArguments args)
        {
            return fileMagic == FileMagic.EXF;
        }

        /// <inheritdoc/>
        public int Priority => 100;

        /// <inheritdoc/>
        public ProcessFileResult Handle(string filePath, BinaryReader reader, InputArguments args)
        {
            if (args.verbose)
                _logger.WriteLine("EXF Header detected.");
            var outputPath = _fileProcessingService.DecryptExfFile(filePath, args.createLog, args.cleanUp, args.verbose);
            return ProcessFileResult.Success(outputPath);
        }
    }
}
