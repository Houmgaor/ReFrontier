using System;
using System.IO;

using LibReFrontier.Abstractions;
using ReFrontier.Jpk;
using ReFrontier.Services;

namespace ReFrontier
{
    /// <summary>
    /// File unpacking: multiple file from one.
    /// </summary>
    public class Unpack
    {
        private readonly UnpackingService _unpackingService;

        /// <summary>
        /// Create a new Unpack instance with default dependencies.
        /// </summary>
        public Unpack() : this(new UnpackingService())
        {
        }

        /// <summary>
        /// Create a new Unpack instance with injectable dependencies.
        /// </summary>
        /// <param name="fileSystem">File system abstraction.</param>
        /// <param name="logger">Logger abstraction.</param>
        /// <param name="codecFactory">Codec factory.</param>
        /// <param name="config">Configuration settings.</param>
        public Unpack(IFileSystem fileSystem, ILogger logger, ICodecFactory codecFactory, FileProcessingConfig config)
            : this(new UnpackingService(fileSystem, logger, codecFactory, config))
        {
        }

        /// <summary>
        /// Create a new Unpack instance with an unpacking service.
        /// </summary>
        /// <param name="unpackingService">The unpacking service to use.</param>
        public Unpack(UnpackingService unpackingService)
        {
            _unpackingService = unpackingService ?? throw new ArgumentNullException(nameof(unpackingService));
        }

        /// <summary>
        /// Unpack a simple archive file container.
        /// </summary>
        /// <param name="input">Input file name to read from.</param>
        /// <param name="brInput">Binary reader to the input file.</param>
        /// <param name="magicSize">File magic size, depends on file type.</param>
        /// <param name="createLog">true is a log file should be created.</param>
        /// <param name="cleanUp">Remove the initial input file.</param>
        /// <param name="autoStage">Unpack stage container if true.</param>
        /// <returns>Output folder path.</returns>
        public string UnpackSimpleArchive(
            string input, BinaryReader brInput, int magicSize, bool createLog,
            bool cleanUp, bool autoStage
        )
        {
            return _unpackingService.UnpackSimpleArchive(input, brInput, magicSize, createLog, cleanUp, autoStage);
        }

        /// <summary>
        /// Unpack a MHA file container.
        /// </summary>
        /// <param name="input">Input file name to read from.</param>
        /// <param name="brInput">Binary reader to the input file.</param>
        /// <param name="createLog">true is a log file should be created.</param>
        /// <returns>Output folder path.</returns>
        public string UnpackMHA(string input, BinaryReader brInput, bool createLog)
        {
            return _unpackingService.UnpackMHA(input, brInput, createLog);
        }

        /// <summary>
        /// Unpack, decompress, a JPK file.
        /// </summary>
        /// <param name="input">Input file path.</param>
        /// <returns>Output folder path.</returns>
        public string UnpackJPK(string input)
        {
            return _unpackingService.UnpackJPK(input);
        }

        /// <summary>
        /// Unpack a stage file container.
        /// </summary>
        /// <param name="input">Input file name to read from.</param>
        /// <param name="brInput">Binary reader to the input file.</param>
        /// <param name="createLog">true is a log file should be created.</param>
        /// <param name="cleanUp">Remove the initial input file.</param>
        /// <returns>Output folder path.</returns>
        public string UnpackStageContainer(string input, BinaryReader brInput, bool createLog, bool cleanUp)
        {
            return _unpackingService.UnpackStageContainer(input, brInput, createLog, cleanUp);
        }

        /// <summary>
        /// Write output to txt file.
        /// </summary>
        /// <param name="input">Input ftxt file, usually has MHF header.</param>
        /// <param name="brInput">Binary reader to the file.</param>
        /// <returns>Output file path.</returns>
        public string PrintFTXT(string input, BinaryReader brInput)
        {
            return _unpackingService.PrintFTXT(input, brInput);
        }
    }
}
