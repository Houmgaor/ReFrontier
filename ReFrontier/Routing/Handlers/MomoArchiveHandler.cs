using System.IO;

using LibReFrontier;
using LibReFrontier.Abstractions;

using ReFrontier.Services;

namespace ReFrontier.Routing.Handlers
{
    /// <summary>
    /// Handler for MOMO archive files (snp, snd).
    /// </summary>
    public class MomoArchiveHandler : IFileTypeHandler
    {
        private readonly ILogger _logger;
        private readonly UnpackingService _unpackingService;

        /// <summary>
        /// Create a new MomoArchiveHandler.
        /// </summary>
        /// <param name="logger">Logger for output.</param>
        /// <param name="unpackingService">Service for unpacking operations.</param>
        public MomoArchiveHandler(ILogger logger, UnpackingService unpackingService)
        {
            _logger = logger;
            _unpackingService = unpackingService;
        }

        /// <inheritdoc/>
        public bool CanHandle(uint fileMagic, InputArguments args)
        {
            return fileMagic == FileMagic.MOMO;
        }

        /// <inheritdoc/>
        public int Priority => 100;

        /// <inheritdoc/>
        public ProcessFileResult Handle(string filePath, BinaryReader reader, InputArguments args)
        {
            if (!args.quiet)
                _logger.WriteLine("MOMO Header detected.");
            var outputPath = _unpackingService.UnpackSimpleArchive(
                filePath,
                reader,
                8, // Skip 8-byte MOMO header
                args.createLog,
                args.cleanUp,
                args.autoStage,
                args.quiet
            );
            return ProcessFileResult.Success(outputPath);
        }
    }
}
