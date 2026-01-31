using System.IO;

using LibReFrontier.Abstractions;

using ReFrontier.Services;

namespace ReFrontier.Routing.Handlers
{
    /// <summary>
    /// Handler for stage-specific container files (no magic header).
    /// </summary>
    public class StageContainerHandler : IFileTypeHandler
    {
        private readonly ILogger _logger;
        private readonly UnpackingService _unpackingService;

        /// <summary>
        /// Create a new StageContainerHandler.
        /// </summary>
        /// <param name="logger">Logger for output.</param>
        /// <param name="unpackingService">Service for unpacking operations.</param>
        public StageContainerHandler(ILogger logger, UnpackingService unpackingService)
        {
            _logger = logger;
            _unpackingService = unpackingService;
        }

        /// <inheritdoc/>
        public bool CanHandle(uint fileMagic, InputArguments args)
        {
            // Stage containers have no specific magic - only handle if flag is set
            return args.stageContainer;
        }

        /// <inheritdoc/>
        public int Priority => 1000; // Highest priority - must be checked first

        /// <inheritdoc/>
        public ProcessFileResult Handle(string filePath, BinaryReader reader, InputArguments args)
        {
            var outputPath = _unpackingService.UnpackStageContainer(
                filePath,
                reader,
                args.createLog,
                args.cleanUp,
                args.quiet
            );
            return ProcessFileResult.Success(outputPath);
        }
    }
}
