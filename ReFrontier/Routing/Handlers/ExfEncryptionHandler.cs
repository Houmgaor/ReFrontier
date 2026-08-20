using System;
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
        private readonly FileProcessingConfig _config;

        /// <summary>
        /// Create a new ExfEncryptionHandler.
        /// </summary>
        /// <param name="logger">Logger for output.</param>
        /// <param name="fileProcessingService">Service for file processing operations.</param>
        /// <param name="config">Configuration settings, defaults are used when omitted.</param>
        public ExfEncryptionHandler(ILogger logger, FileProcessingService fileProcessingService, FileProcessingConfig? config = null)
        {
            _logger = logger;
            _fileProcessingService = fileProcessingService;
            _config = config ?? FileProcessingConfig.Default();
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
            var outputPath = _fileProcessingService.DecryptExfFile(
                filePath, args.createLog, args.cleanUp, args.verbose, out byte[] header
            );
            // Record the encryption so the file can be re-encrypted with its original key.
            return ProcessFileResult.Success(outputPath, new RecipeLayer
            {
                Kind = RecipeLayerKind.Exf,
                MetaFile = args.createLog ? $"{filePath}{_config.MetaSuffix}" : null,
                // Carried in the recipe so it stays usable on its own; the meta file is
                // still written for --encrypt, FrontierTextTool and older versions.
                Header = Convert.ToBase64String(header),
                OriginalSize = reader.BaseStream.Length,
            });
        }
    }
}
