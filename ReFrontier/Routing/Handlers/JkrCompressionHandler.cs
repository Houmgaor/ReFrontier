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
        private readonly IFileSystem _fileSystem;
        private readonly ILogger _logger;
        private readonly UnpackingService _unpackingService;

        /// <summary>
        /// Create a new JkrCompressionHandler.
        /// </summary>
        /// <param name="fileSystem">File system abstraction.</param>
        /// <param name="logger">Logger for output.</param>
        /// <param name="unpackingService">Service for unpacking operations.</param>
        public JkrCompressionHandler(IFileSystem fileSystem, ILogger logger, UnpackingService unpackingService)
        {
            _fileSystem = fileSystem;
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
                outputPath = _unpackingService.UnpackJPK(filePath);
                if (!args.quiet)
                    _logger.WriteLine($"File decompressed to {outputPath}.");

                // Replace input file, deprecated behavior, will be removed in 2.0.0
                if (
                    args.rewriteOldFile && outputPath != filePath &&
                    _fileSystem.GetAttributes(outputPath).HasFlag(FileAttributes.Normal)
                )
                {
                    _fileSystem.Copy(outputPath, filePath);
                }
            }

            return ProcessFileResult.Success(outputPath);
        }
    }
}
