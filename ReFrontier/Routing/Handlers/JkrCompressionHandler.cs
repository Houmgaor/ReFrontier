using System.IO;

using LibReFrontier;
using LibReFrontier.Abstractions;

using ReFrontier.Services;

namespace ReFrontier.Routing.Handlers
{
    /// <summary>
    /// Handler for JKR (JPK compressed) files.
    /// </summary>
    public class JkrCompressionHandler : IFileTypeHandler
    {
        private readonly ILogger _logger;
        private readonly UnpackingService _unpackingService;

        /// <summary>
        /// Create a new JkrCompressionHandler.
        /// </summary>
        /// <param name="logger">Logger for output.</param>
        /// <param name="unpackingService">Service for unpacking operations.</param>
        public JkrCompressionHandler(ILogger logger, UnpackingService unpackingService)
        {
            _logger = logger;
            _unpackingService = unpackingService;
        }

        /// <inheritdoc/>
        public bool CanHandle(uint fileMagic, InputArguments args)
        {
            return fileMagic == FileMagic.JKR;
        }

        /// <inheritdoc/>
        public int Priority => 100;

        /// <inheritdoc/>
        public ProcessFileResult Handle(string filePath, BinaryReader reader, InputArguments args)
        {
            if (!args.quiet)
                _logger.WriteLine("JKR Header detected.");
            string outputPath = filePath;

            if (!args.ignoreJPK)
            {
                outputPath = _unpackingService.UnpackJPK(filePath, args.quiet);
                if (!args.quiet)
                    _logger.WriteLine($"File decompressed to {outputPath}.");
            }

            return ProcessFileResult.Success(outputPath);
        }
    }
}
