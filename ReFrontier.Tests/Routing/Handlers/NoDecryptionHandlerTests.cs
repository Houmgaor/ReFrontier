using LibReFrontier;

using ReFrontier.Routing.Handlers;
using ReFrontier.Tests.Mocks;

namespace ReFrontier.Tests.Routing.Handlers
{
    /// <summary>
    /// Tests for NoDecryptionHandler.
    /// </summary>
    public class NoDecryptionHandlerTests
    {
        private readonly TestLogger _logger;
        private readonly NoDecryptionHandler _handler;

        public NoDecryptionHandlerTests()
        {
            _logger = new TestLogger();
            _handler = new NoDecryptionHandler(_logger);
        }

        [Fact]
        public void CanHandle_EcdWithNoDecryption_ReturnsTrue()
        {
            var args = new InputArguments { noDecryption = true };
            var result = _handler.CanHandle(FileMagic.ECD, args);
            Assert.True(result);
        }

        [Fact]
        public void CanHandle_EcdWithoutNoDecryption_ReturnsFalse()
        {
            var args = new InputArguments { noDecryption = false };
            var result = _handler.CanHandle(FileMagic.ECD, args);
            Assert.False(result);
        }

        [Fact]
        public void CanHandle_OtherMagic_ReturnsFalse()
        {
            var args = new InputArguments { noDecryption = true };
            var result = _handler.CanHandle(FileMagic.MOMO, args);
            Assert.False(result);
        }

        [Fact]
        public void Priority_Returns200()
        {
            Assert.Equal(200, _handler.Priority);
        }

        [Fact]
        public void Handle_ReturnsSkipped()
        {
            var data = new byte[] { 0x65, 0x63, 0x64, 0x1A }; // ECD magic
            using var ms = new MemoryStream(data);
            using var reader = new BinaryReader(ms);

            var result = _handler.Handle("test.bin", reader, new InputArguments { verbose = true });

            Assert.False(result.WasProcessed);
            Assert.Equal("Decryption disabled", result.SkipReason);
            Assert.Contains("Not decrypting due to flag", _logger.Output);
        }
    }
}
