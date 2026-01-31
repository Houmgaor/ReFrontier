using System.IO;

using LibReFrontier;
using LibReFrontier.Abstractions;

namespace ReFrontier.Routing.Handlers
{
    /// <summary>
    /// Handler that skips ECD decryption when noDecryption flag is set.
    /// </summary>
    public class NoDecryptionHandler : IFileTypeHandler
    {
        private readonly ILogger _logger;

        /// <summary>
        /// Create a new NoDecryptionHandler.
        /// </summary>
        /// <param name="logger">Logger for output.</param>
        public NoDecryptionHandler(ILogger logger)
        {
            _logger = logger;
        }

        /// <inheritdoc/>
        public bool CanHandle(uint fileMagic, InputArguments args)
        {
            return fileMagic == FileMagic.ECD && args.noDecryption;
        }

        /// <inheritdoc/>
        public int Priority => 200; // Higher than normal ECD handler

        /// <inheritdoc/>
        public ProcessFileResult Handle(string filePath, BinaryReader reader, InputArguments args)
        {
            _logger.WriteLine("ECD Header detected.");
            _logger.PrintWithSeparator("Not decrypting due to flag.", false);
            return ProcessFileResult.Skipped("Decryption disabled");
        }
    }
}
