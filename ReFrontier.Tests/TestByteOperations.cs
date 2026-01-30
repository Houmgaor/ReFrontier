using LibReFrontier;

namespace ReFrontier.Tests
{
    /// <summary>
    /// Tests for LibReFrontier.ByteOperations class.
    /// </summary>
    public class TestByteOperations
    {
        #region GetOffsetOfArray Tests

        [Fact]
        public void GetOffsetOfArray_EmptyNeedle_ReturnsZero()
        {
            byte[] haystack = [1, 2, 3, 4, 5];
            byte[] needle = [];

            int result = ByteOperations.GetOffsetOfArray(haystack, needle);

            Assert.Equal(0, result);
        }

        [Fact]
        public void GetOffsetOfArray_NeedleNotFound_ReturnsMinusOne()
        {
            byte[] haystack = [1, 2, 3, 4, 5];
            byte[] needle = [6, 7];

            int result = ByteOperations.GetOffsetOfArray(haystack, needle);

            Assert.Equal(-1, result);
        }

        [Fact]
        public void GetOffsetOfArray_NeedleAtStart_ReturnsZero()
        {
            byte[] haystack = [1, 2, 3, 4, 5];
            byte[] needle = [1, 2];

            int result = ByteOperations.GetOffsetOfArray(haystack, needle);

            Assert.Equal(0, result);
        }

        [Fact]
        public void GetOffsetOfArray_NeedleInMiddle_ReturnsCorrectOffset()
        {
            byte[] haystack = [1, 2, 3, 4, 5];
            byte[] needle = [3, 4];

            int result = ByteOperations.GetOffsetOfArray(haystack, needle);

            Assert.Equal(2, result);
        }

        [Fact]
        public void GetOffsetOfArray_NeedleAtEnd_ReturnsCorrectOffset()
        {
            byte[] haystack = [1, 2, 3, 4, 5];
            byte[] needle = [4, 5];

            int result = ByteOperations.GetOffsetOfArray(haystack, needle);

            Assert.Equal(3, result);
        }

        [Fact]
        public void GetOffsetOfArray_HaystackSmallerThanNeedle_ReturnsMinusOne()
        {
            byte[] haystack = [1, 2];
            byte[] needle = [1, 2, 3, 4, 5];

            int result = ByteOperations.GetOffsetOfArray(haystack, needle);

            Assert.Equal(-1, result);
        }

        [Fact]
        public void GetOffsetOfArray_ExactMatch_ReturnsZero()
        {
            byte[] haystack = [1, 2, 3];
            byte[] needle = [1, 2, 3];

            int result = ByteOperations.GetOffsetOfArray(haystack, needle);

            Assert.Equal(0, result);
        }

        [Fact]
        public void GetOffsetOfArray_SingleByteNeedle_FindsFirst()
        {
            byte[] haystack = [5, 1, 2, 1, 3];
            byte[] needle = [1];

            int result = ByteOperations.GetOffsetOfArray(haystack, needle);

            Assert.Equal(1, result);
        }

        [Fact]
        public void GetOffsetOfArray_PartialMatchThenFullMatch_ReturnsCorrectOffset()
        {
            // Haystack has partial match at index 0, full match at index 3
            byte[] haystack = [1, 2, 5, 1, 2, 3];
            byte[] needle = [1, 2, 3];

            int result = ByteOperations.GetOffsetOfArray(haystack, needle);

            Assert.Equal(3, result);
        }

        [Fact]
        public void GetOffsetOfArray_EmptyHaystack_ReturnsMinusOne()
        {
            byte[] haystack = [];
            byte[] needle = [1];

            int result = ByteOperations.GetOffsetOfArray(haystack, needle);

            Assert.Equal(-1, result);
        }

        [Fact]
        public void GetOffsetOfArray_BothEmpty_ReturnsZero()
        {
            byte[] haystack = [];
            byte[] needle = [];

            int result = ByteOperations.GetOffsetOfArray(haystack, needle);

            Assert.Equal(0, result);
        }

        #endregion

        #region CheckForMagic Tests

        [Fact]
        public void CheckForMagic_FmodDetection_ValidSize_ReturnsFmod()
        {
            // headerInt=1 and data[8..12] contains the data length
            byte[] data = new byte[100];
            // Set the size at offset 8 to match data.Length
            byte[] sizeBytes = BitConverter.GetBytes(100);
            Array.Copy(sizeBytes, 0, data, 8, 4);

            string? result = ByteOperations.CheckForMagic(1, data);

            Assert.Equal("fmod", result);
        }

        [Fact]
        public void CheckForMagic_FmodDetection_InvalidSize_ReturnsNull()
        {
            // headerInt=1 but size at offset 8 doesn't match
            byte[] data = new byte[100];
            byte[] sizeBytes = BitConverter.GetBytes(50); // Wrong size
            Array.Copy(sizeBytes, 0, data, 8, 4);

            string? result = ByteOperations.CheckForMagic(1, data);

            Assert.Null(result);
        }

        [Fact]
        public void CheckForMagic_FsklDetection_ValidSize_ReturnsFskl()
        {
            // headerInt=0xC0000000 and data[8..12] contains the data length
            byte[] data = new byte[200];
            byte[] sizeBytes = BitConverter.GetBytes(200);
            Array.Copy(sizeBytes, 0, data, 8, 4);

            string? result = ByteOperations.CheckForMagic(0xC0000000, data);

            Assert.Equal("fskl", result);
        }

        [Fact]
        public void CheckForMagic_FsklDetection_InvalidSize_ReturnsNull()
        {
            // headerInt=0xC0000000 but size at offset 8 doesn't match
            byte[] data = new byte[200];
            byte[] sizeBytes = BitConverter.GetBytes(100); // Wrong size
            Array.Copy(sizeBytes, 0, data, 8, 4);

            string? result = ByteOperations.CheckForMagic(0xC0000000, data);

            Assert.Null(result);
        }

        [Fact]
        public void CheckForMagic_UnknownHeaderInt_ReturnsNull()
        {
            byte[] data = new byte[100];

            string? result = ByteOperations.CheckForMagic(0x12345678, data);

            Assert.Null(result);
        }

        [Fact]
        public void CheckForMagic_ZeroHeaderInt_ReturnsNull()
        {
            byte[] data = new byte[100];

            string? result = ByteOperations.CheckForMagic(0, data);

            Assert.Null(result);
        }

        #endregion
    }
}
