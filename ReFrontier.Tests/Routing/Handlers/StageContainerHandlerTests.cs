using System.IO;

using LibReFrontier;

using ReFrontier.Jpk;
using ReFrontier.Routing.Handlers;
using ReFrontier.Services;
using ReFrontier.Tests.Mocks;

using Xunit;

namespace ReFrontier.Tests.Routing.Handlers
{
    /// <summary>
    /// Tests for StageContainerHandler.
    /// </summary>
    public class StageContainerHandlerTests
    {
        private readonly InMemoryFileSystem _fileSystem;
        private readonly TestLogger _logger;
        private readonly StageContainerHandler _handler;

        public StageContainerHandlerTests()
        {
            _fileSystem = new InMemoryFileSystem();
            _logger = new TestLogger();
            var unpackingService = new UnpackingService(
                _fileSystem,
                _logger,
                new DefaultCodecFactory(),
                FileProcessingConfig.Default()
            );
            _handler = new StageContainerHandler(_logger, unpackingService);
        }

        [Fact]
        public void CanHandle_StageContainerFlag_ReturnsTrue()
        {
            var args = new InputArguments { stageContainer = true };
            var result = _handler.CanHandle(0, args);
            Assert.True(result);
        }

        [Fact]
        public void CanHandle_NoStageContainerFlag_ReturnsFalse()
        {
            var args = new InputArguments { stageContainer = false };
            var result = _handler.CanHandle(0, args);
            Assert.False(result);
        }

        [Fact]
        public void Priority_Returns1000()
        {
            Assert.Equal(1000, _handler.Priority);
        }
    }
}
