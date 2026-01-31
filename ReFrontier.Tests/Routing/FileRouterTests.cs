using LibReFrontier;

using ReFrontier.Routing;
using ReFrontier.Tests.Mocks;

namespace ReFrontier.Tests.Routing
{
    /// <summary>
    /// Tests for FileRouter class.
    /// </summary>
    public class FileRouterTests
    {
        private readonly TestLogger _logger;
        private readonly FileRouter _router;

        public FileRouterTests()
        {
            _logger = new TestLogger();
            _router = new FileRouter(_logger);
        }

        [Fact]
        public void Route_NoHandlers_ReturnsSkipped()
        {
            var data = new byte[] { 0x4D, 0x4F, 0x4D, 0x4F }; // MOMO magic
            using var ms = new MemoryStream(data);
            using var reader = new BinaryReader(ms);

            var result = _router.Route("test.bin", FileMagic.MOMO, reader, new InputArguments());

            Assert.False(result.WasProcessed);
            Assert.NotNull(result.SkipReason);
        }

        [Fact]
        public void Route_WithMatchingHandler_CallsHandler()
        {
            var handler = new TestHandler(FileMagic.MOMO, 100);
            _router.RegisterHandler(handler);

            var data = new byte[] { 0x4D, 0x4F, 0x4D, 0x4F };
            using var ms = new MemoryStream(data);
            using var reader = new BinaryReader(ms);

            var result = _router.Route("test.bin", FileMagic.MOMO, reader, new InputArguments());

            Assert.True(result.WasProcessed);
            Assert.True(handler.WasCalled);
        }

        [Fact]
        public void Route_MultipleHandlers_UsesHighestPriority()
        {
            var lowPriorityHandler = new TestHandler(FileMagic.MOMO, 50);
            var highPriorityHandler = new TestHandler(FileMagic.MOMO, 100);

            _router.RegisterHandler(lowPriorityHandler);
            _router.RegisterHandler(highPriorityHandler);

            var data = new byte[] { 0x4D, 0x4F, 0x4D, 0x4F };
            using var ms = new MemoryStream(data);
            using var reader = new BinaryReader(ms);

            var result = _router.Route("test.bin", FileMagic.MOMO, reader, new InputArguments());

            Assert.True(highPriorityHandler.WasCalled);
            Assert.False(lowPriorityHandler.WasCalled);
        }

        // Test handler for routing tests
        private class TestHandler : IFileTypeHandler
        {
            private readonly uint _magic;
            private readonly int _priority;

            public bool WasCalled { get; private set; }

            public TestHandler(uint magic, int priority)
            {
                _magic = magic;
                _priority = priority;
            }

            public bool CanHandle(uint fileMagic, InputArguments args) => fileMagic == _magic;

            public int Priority => _priority;

            public ProcessFileResult Handle(string filePath, BinaryReader reader, InputArguments args)
            {
                WasCalled = true;
                return ProcessFileResult.Success("output.bin");
            }
        }
    }
}
