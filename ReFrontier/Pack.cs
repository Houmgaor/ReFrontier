using System;
using System.IO;

using LibReFrontier;
using LibReFrontier.Abstractions;

using ReFrontier.Jpk;
using ReFrontier.Services;

namespace ReFrontier
{
    /// <summary>
    /// Files packing and compression.
    ///
    /// Files packing takes multiple files and make them one.
    /// </summary>
    public class Pack
    {
        private readonly PackingService _packingService;

        /// <summary>
        /// Create a new Pack instance with default dependencies.
        /// </summary>
        public Pack() : this(new PackingService())
        {
        }

        /// <summary>
        /// Create a new Pack instance with injectable dependencies.
        /// </summary>
        /// <param name="fileSystem">File system abstraction.</param>
        /// <param name="logger">Logger abstraction.</param>
        /// <param name="codecFactory">Codec factory.</param>
        /// <param name="config">Configuration settings.</param>
        public Pack(IFileSystem fileSystem, ILogger logger, ICodecFactory codecFactory, FileProcessingConfig config)
            : this(new PackingService(fileSystem, logger, codecFactory, config))
        {
        }

        /// <summary>
        /// Create a new Pack instance with a packing service.
        /// </summary>
        /// <param name="packingService">The packing service to use.</param>
        public Pack(PackingService packingService)
        {
            _packingService = packingService ?? throw new ArgumentNullException(nameof(packingService));
        }

        /// <summary>
        /// Standard packing of an input directory.
        ///
        /// It needs a log file to work.
        /// </summary>
        /// <param name="inputDir">Input directory path.</param>
        /// <exception cref="FileNotFoundException">The log file does not exist.</exception>
        /// <exception cref="InvalidOperationException">The packing format does not exist.</exception>
        public void ProcessPackInput(string inputDir)
        {
            _packingService.ProcessPackInput(inputDir);
        }

        /// <summary>
        /// Compress a JPK file to a JKR type.
        /// </summary>
        /// <param name="compression">Compression to use.</param>
        /// <param name="inPath">Input file path.</param>
        /// <param name="otPath">Output file path.</param>
        public void JPKEncode(Compression compression, string inPath, string otPath)
        {
            _packingService.JPKEncode(compression, inPath, otPath);
        }
    }
}
