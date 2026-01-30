using LibReFrontier;

using ReFrontier.Jpk;
using ReFrontier.Services;
using ReFrontier.Tests.Mocks;

namespace ReFrontier.Tests
{
    /// <summary>
    /// Tests for constructor null argument validation across services.
    /// </summary>
    public class ConstructorValidationTests
    {
        private readonly InMemoryFileSystem _fileSystem = new();
        private readonly TestLogger _logger = new();
        private readonly FileProcessingConfig _config = FileProcessingConfig.Default();
        private readonly ICodecFactory _codecFactory = new DefaultCodecFactory();

        #region PackingService Constructor Tests

        [Fact]
        public void PackingService_NullFileSystem_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new PackingService(null!, _logger, _codecFactory, _config));
        }

        [Fact]
        public void PackingService_NullLogger_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new PackingService(_fileSystem, null!, _codecFactory, _config));
        }

        [Fact]
        public void PackingService_NullCodecFactory_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new PackingService(_fileSystem, _logger, null!, _config));
        }

        [Fact]
        public void PackingService_NullConfig_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new PackingService(_fileSystem, _logger, _codecFactory, null!));
        }

        #endregion

        #region UnpackingService Constructor Tests

        [Fact]
        public void UnpackingService_NullFileSystem_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new UnpackingService(null!, _logger, _codecFactory, _config));
        }

        [Fact]
        public void UnpackingService_NullLogger_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new UnpackingService(_fileSystem, null!, _codecFactory, _config));
        }

        [Fact]
        public void UnpackingService_NullCodecFactory_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new UnpackingService(_fileSystem, _logger, null!, _config));
        }

        [Fact]
        public void UnpackingService_NullConfig_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new UnpackingService(_fileSystem, _logger, _codecFactory, null!));
        }

        #endregion

        #region FileProcessingService Constructor Tests

        [Fact]
        public void FileProcessingService_NullFileSystem_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new FileProcessingService(null!, _logger, _config));
        }

        [Fact]
        public void FileProcessingService_NullLogger_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new FileProcessingService(_fileSystem, null!, _config));
        }

        [Fact]
        public void FileProcessingService_NullConfig_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new FileProcessingService(_fileSystem, _logger, null!));
        }

        #endregion

        #region Pack Constructor Tests

        [Fact]
        public void Pack_NullPackingService_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new Pack((PackingService)null!));
        }

        #endregion

        #region Unpack Constructor Tests

        [Fact]
        public void Unpack_NullUnpackingService_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new Unpack((UnpackingService)null!));
        }

        #endregion

        #region Program Constructor Tests

        [Fact]
        public void Program_NullFileSystem_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new Program(null!, _logger, _codecFactory, _config));
        }

        [Fact]
        public void Program_NullLogger_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new Program(_fileSystem, null!, _codecFactory, _config));
        }

        [Fact]
        public void Program_NullConfig_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new Program(_fileSystem, _logger, _codecFactory, null!));
        }

        #endregion

        #region FileOperations Constructor Tests

        [Fact]
        public void FileOperations_NullFileSystem_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new FileOperations(null!, _logger));
        }

        [Fact]
        public void FileOperations_NullLogger_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new FileOperations(_fileSystem, null!));
        }

        #endregion

        #region ArgumentsParser Constructor Tests

        [Fact]
        public void ArgumentsParser_NullLogger_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new ArgumentsParser(null!));
        }

        #endregion
    }
}
