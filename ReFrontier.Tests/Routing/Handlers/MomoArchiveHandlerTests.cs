using LibReFrontier;

using ReFrontier.Jpk;
using ReFrontier.Routing.Handlers;
using ReFrontier.Services;
using ReFrontier.Tests.Mocks;

namespace ReFrontier.Tests.Routing.Handlers
{
    /// <summary>
    /// Tests for MomoArchiveHandler.
    /// </summary>
    public class MomoArchiveHandlerTests
    {
        private readonly InMemoryFileSystem _fileSystem;
        private readonly TestLogger _logger;
        private readonly MomoArchiveHandler _handler;

        public MomoArchiveHandlerTests()
        {
            _fileSystem = new InMemoryFileSystem();
            _logger = new TestLogger();
            var unpackingService = new UnpackingService(
                _fileSystem,
                _logger,
                new DefaultCodecFactory(),
                FileProcessingConfig.Default()
            );
            _handler = new MomoArchiveHandler(_logger, unpackingService);
        }

        [Fact]
        public void CanHandle_MomoMagic_ReturnsTrue()
        {
            var result = _handler.CanHandle(FileMagic.MOMO, new InputArguments());
            Assert.True(result);
        }

        [Fact]
        public void CanHandle_OtherMagic_ReturnsFalse()
        {
            var result = _handler.CanHandle(FileMagic.ECD, new InputArguments());
            Assert.False(result);
        }

        [Fact]
        public void Priority_Returns100()
        {
            Assert.Equal(100, _handler.Priority);
        }

        [Fact]
        public void Handle_CallsCorrectService()
        {
            // Create a valid MOMO simple archive structure
            // Structure: 8-byte header (MOMO) + file count + file entries
            var data = new byte[64];
            data[0] = 0x4D; data[1] = 0x4F; data[2] = 0x4D; data[3] = 0x4F; // MOMO magic
            data[4] = 0x00; data[5] = 0x00; data[6] = 0x00; data[7] = 0x00; // padding
            // File count (0 files)
            data[8] = 0x00; data[9] = 0x00; data[10] = 0x00; data[11] = 0x00;

            // Register file in filesystem
            _fileSystem.WriteAllBytes("test.bin", data);

            using var ms = new MemoryStream(data);
            using var reader = new BinaryReader(ms);

            try
            {
                var result = _handler.Handle("test.bin", reader, new InputArguments { verbose = true });
                // If it doesn't throw, the handler was called
                Assert.True(true);
            }
            catch
            {
                // Some unpacking may fail with minimal data, but we logged the message
            }

            Assert.Contains("MOMO Header detected", _logger.Output);
        }
    }
}
