using System;
using System.IO;

using LibReFrontier;
using LibReFrontier.Exceptions;
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
            string outputPath;
            try
            {
                outputPath = _unpackingService.UnpackSimpleArchive(
                    filePath,
                    reader,
                    4, // Skip 4-byte header
                    args.createLog,
                    args.cleanUp,
                    args.autoStage,
                    args.verbose
                );
            }
            catch (PackingException ex)
            {
                // This handler is the fallback probe: it accepts any file and finds out
                // whether it is a container by trying. Not being one is an ordinary
                // outcome, not an error, and counting it as one made a second run over an
                // already extracted folder report failures that meant nothing.
                if (ex.Message.Contains("--stageContainer", StringComparison.Ordinal))
                    _logger.WriteLine(ex.Message);
                else if (args.verbose)
                    _logger.WriteLine($"{filePath} is not a container. Skipping.");

                return ProcessFileResult.Skipped(ex.Message);
            }

            // Record the container so it can be packed back through its log file.
            // Without a log there is nothing to repack from, so report no layer.
            if (!args.createLog)
                return ProcessFileResult.Success(outputPath);

            return ProcessFileResult.Success(outputPath, new RecipeLayer
            {
                Kind = RecipeLayerKind.Container,
                ContainerType = "SimpleArchive",
                Directory = System.IO.Path.GetFileName(outputPath),
                OriginalSize = reader.BaseStream.Length,
            });
        }
    }
}
