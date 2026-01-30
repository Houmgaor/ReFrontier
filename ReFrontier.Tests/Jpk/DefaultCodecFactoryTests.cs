using System;
using Xunit;

using LibReFrontier;
using ReFrontier.Jpk;

namespace ReFrontier.Tests.Jpk
{
    /// <summary>
    /// Tests for DefaultCodecFactory.
    /// </summary>
    public class DefaultCodecFactoryTests
    {
        private readonly DefaultCodecFactory _factory;

        public DefaultCodecFactoryTests()
        {
            _factory = new DefaultCodecFactory();
        }

        #region CreateEncoder Tests

        [Theory]
        [InlineData(CompressionType.RW)]
        [InlineData(CompressionType.HFIRW)]
        [InlineData(CompressionType.HFI)]
        [InlineData(CompressionType.LZ)]
        public void CreateEncoder_ValidType_ReturnsEncoder(CompressionType type)
        {
            // Act
            var encoder = _factory.CreateEncoder(type);

            // Assert
            Assert.NotNull(encoder);
            Assert.IsAssignableFrom<IJPKEncode>(encoder);
        }

        [Fact]
        public void CreateEncoder_NoneType_ThrowsInvalidOperationException()
        {
            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() =>
                _factory.CreateEncoder(CompressionType.None));

            Assert.Contains("Unsupported", exception.Message);
            Assert.Contains("None", exception.Message);
        }

        [Fact]
        public void CreateEncoder_HFIRWType_ReturnsEncoder()
        {
            // Act
            var encoder = _factory.CreateEncoder(CompressionType.HFIRW);

            // Assert
            Assert.NotNull(encoder);
            Assert.IsType<JPKEncodeHFIRW>(encoder);
        }

        [Fact]
        public void CreateEncoder_InvalidEnumValue_ThrowsInvalidOperationException()
        {
            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() =>
                _factory.CreateEncoder((CompressionType)999));

            Assert.Contains("Unsupported", exception.Message);
        }

        #endregion

        #region CreateDecoder Tests

        [Theory]
        [InlineData(CompressionType.None)]
        [InlineData(CompressionType.RW)]
        [InlineData(CompressionType.HFIRW)]
        [InlineData(CompressionType.LZ)]
        [InlineData(CompressionType.HFI)]
        public void CreateDecoder_ValidType_ReturnsDecoder(CompressionType type)
        {
            // Act
            var decoder = _factory.CreateDecoder(type);

            // Assert
            Assert.NotNull(decoder);
            Assert.IsAssignableFrom<IJPKDecode>(decoder);
        }

        [Fact]
        public void CreateDecoder_InvalidEnumValue_ThrowsInvalidOperationException()
        {
            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() =>
                _factory.CreateDecoder((CompressionType)999));

            Assert.Contains("Unsupported", exception.Message);
        }

        #endregion

        #region Encoder Type Verification

        [Fact]
        public void CreateEncoder_RW_ReturnsJPKEncodeRW()
        {
            var encoder = _factory.CreateEncoder(CompressionType.RW);
            Assert.IsType<JPKEncodeRW>(encoder);
        }

        [Fact]
        public void CreateEncoder_HFI_ReturnsJPKEncodeHFI()
        {
            var encoder = _factory.CreateEncoder(CompressionType.HFI);
            Assert.IsType<JPKEncodeHFI>(encoder);
        }

        [Fact]
        public void CreateEncoder_LZ_ReturnsJPKEncodeLz()
        {
            var encoder = _factory.CreateEncoder(CompressionType.LZ);
            Assert.IsType<JPKEncodeLz>(encoder);
        }

        #endregion

        #region Decoder Type Verification

        [Fact]
        public void CreateDecoder_RW_ReturnsJPKDecodeRW()
        {
            var decoder = _factory.CreateDecoder(CompressionType.RW);
            Assert.IsType<JPKDecodeRW>(decoder);
        }

        [Fact]
        public void CreateDecoder_None_ReturnsJPKDecodeRW()
        {
            // None type uses RW decoder (raw bytes)
            var decoder = _factory.CreateDecoder(CompressionType.None);
            Assert.IsType<JPKDecodeRW>(decoder);
        }

        [Fact]
        public void CreateDecoder_HFIRW_ReturnsJPKDecodeHFIRW()
        {
            var decoder = _factory.CreateDecoder(CompressionType.HFIRW);
            Assert.IsType<JPKDecodeHFIRW>(decoder);
        }

        [Fact]
        public void CreateDecoder_LZ_ReturnsJPKDecodeLz()
        {
            var decoder = _factory.CreateDecoder(CompressionType.LZ);
            Assert.IsType<JPKDecodeLz>(decoder);
        }

        [Fact]
        public void CreateDecoder_HFI_ReturnsJPKDecodeHFI()
        {
            var decoder = _factory.CreateDecoder(CompressionType.HFI);
            Assert.IsType<JPKDecodeHFI>(decoder);
        }

        #endregion
    }
}
