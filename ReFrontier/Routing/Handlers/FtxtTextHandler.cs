using System.IO;

using LibReFrontier;
using LibReFrontier.Abstractions;

using ReFrontier.Services;

namespace ReFrontier.Routing.Handlers
{
    /// <summary>
    /// Handler for MHF text files (FTXT format).
    /// </summary>
    public class FtxtTextHandler : IFileTypeHandler
    {
        private readonly ILogger _logger;
        private readonly UnpackingService _unpackingService;

        /// <summary>
        /// Create a new FtxtTextHandler.
        /// </summary>
        /// <param name="logger">Logger for output.</param>
        /// <param name="unpackingService">Service for unpacking operations.</param>
        public FtxtTextHandler(ILogger logger, UnpackingService unpackingService)
        {
            _logger = logger;
            _unpackingService = unpackingService;
        }

        /// <inheritdoc/>
        public bool CanHandle(uint fileMagic, InputArguments args)
        {
            return fileMagic == FileMagic.FTXT;
        }

        /// <inheritdoc/>
        public int Priority => 100;

        /// <inheritdoc/>
        public ProcessFileResult Handle(string filePath, BinaryReader reader, InputArguments args)
        {
            if (!args.quiet)
                _logger.WriteLine("MHF Text file detected.");
            var outputPath = _unpackingService.PrintFTXT(filePath, reader);
            return ProcessFileResult.Success(outputPath);
        }
    }
}
