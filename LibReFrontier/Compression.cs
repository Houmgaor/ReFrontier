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
    public readonly struct Compression : System.IEquatable<Compression>
    {
        public readonly CompressionType type;
        public readonly int level;

        public Compression(CompressionType type, int level)
        {
            this.type = type;
            this.level = level;
        }

        public override bool Equals(object? obj)
        {
            return obj is Compression other && Equals(other);
        }

        public bool Equals(Compression other)
        {
            return type == other.type && level == other.level;
        }

        public override int GetHashCode()
        {
            return System.HashCode.Combine(type, level);
        }

        public static bool operator ==(Compression left, Compression right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Compression left, Compression right)
        {
            return !left.Equals(right);
        }
    }
}
