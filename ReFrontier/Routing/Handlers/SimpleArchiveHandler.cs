using System.IO;

using LibReFrontier.Abstractions;

using ReFrontier.Services;

namespace ReFrontier.Routing.Handlers
{
    /// <summary>
    /// Fallback handler for simple archive containers (txb, bin, pac, gab).
    /// Handles files with no recognized magic header.
    /// </summary>
    public class SimpleArchiveHandler : IFileTypeHandler
    {
        private readonly ILogger _logger;
        private readonly UnpackingService _unpackingService;

        /// <summary>
        /// Create a new SimpleArchiveHandler.
        /// </summary>
        /// <param name="logger">Logger for output.</param>
        /// <param name="unpackingService">Service for unpacking operations.</param>
        public SimpleArchiveHandler(ILogger logger, UnpackingService unpackingService)
        {
            _logger = logger;
            _unpackingService = unpackingService;
        }

        /// <inheritdoc/>
        public bool CanHandle(uint fileMagic, InputArguments args)
        {
            // This is a fallback handler - it accepts any file
            // But only if not stage container (which has higher priority)
            return !args.stageContainer;
        }

        /// <inheritdoc/>
        public int Priority => 0; // Lowest priority - fallback handler

        /// <inheritdoc/>
        public ProcessFileResult Handle(string filePath, BinaryReader reader, InputArguments args)
        {
            // Try to unpack as simple container: i.e. txb, bin, pac, gab
            reader.BaseStream.Seek(0, SeekOrigin.Begin);
            var outputPath = _unpackingService.UnpackSimpleArchive(
                filePath,
                reader,
                4, // Skip 4-byte header
                args.createLog,
                args.cleanUp,
                args.autoStage
            );
            return ProcessFileResult.Success(outputPath);
        }
    }
}
