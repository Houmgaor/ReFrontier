/*
Stuff related to compression.
*/

namespace LibReFrontier
{
    /// <summary>
    /// Supported compression types.
    /// </summary>
    public enum CompressionType
    {
        RW = 0,
        /// <summary>
        /// Special type for "no compression".
        /// </summary>
        None = 1,
        HFIRW = 2,
        LZ = 3,
        HFI = 4,
    }

    /// <summary>
    /// A JPK compression format defintion.
    /// </summary>
    public struct Compression
    {
        public CompressionType type;
        public int level;

        public override bool Equals(object obj)
        {
            throw new System.NotImplementedException();
        }

        public override int GetHashCode()
        {
            throw new System.NotImplementedException();
        }

        public static bool operator ==(Compression left, Compression right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Compression left, Compression right)
        {
            return !(left == right);
        }
    }
}
