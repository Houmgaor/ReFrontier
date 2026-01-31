using System.Collections.Generic;
using System.IO;
using System.Linq;

using LibReFrontier.Abstractions;

namespace ReFrontier.Routing
{
    /// <summary>
    /// Routes files to appropriate handlers based on file magic numbers.
    /// </summary>
    public class FileRouter
    {
        private readonly List<IFileTypeHandler> _handlers;
        private readonly ILogger _logger;

        /// <summary>
        /// Create a new FileRouter with an empty handler registry.
        /// </summary>
        /// <param name="logger">Logger for diagnostics.</param>
        public FileRouter(ILogger logger)
        {
            _logger = logger;
            _handlers = new List<IFileTypeHandler>();
        }

        /// <summary>
        /// Register a handler with the router.
        /// </summary>
        /// <param name="handler">The handler to register.</param>
        public void RegisterHandler(IFileTypeHandler handler)
        {
            _handlers.Add(handler);
        }

        /// <summary>
        /// Route a file to the appropriate handler.
        /// </summary>
        /// <param name="filePath">Path to the file.</param>
        /// <param name="fileMagic">Magic number from the file header.</param>
        /// <param name="reader">Binary reader positioned at the start of the file.</param>
        /// <param name="args">Processing arguments.</param>
        /// <returns>Result of processing the file.</returns>
        public ProcessFileResult Route(string filePath, uint fileMagic, BinaryReader reader, InputArguments args)
        {
            // Find handlers that can process this file, ordered by priority (descending)
            var handler = _handlers
                .Where(h => h.CanHandle(fileMagic, args))
                .OrderByDescending(h => h.Priority)
                .FirstOrDefault();

            if (handler != null)
            {
                return handler.Handle(filePath, reader, args);
            }

            // No handler found
            _logger.WriteLine($"No handler found for magic: 0x{fileMagic:X8}");
            return ProcessFileResult.Skipped($"No handler for magic 0x{fileMagic:X8}");
        }
    }
}
