/*
Stuff related to compression.
*/

using System.Diagnostics.CodeAnalysis;

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
    [SuppressMessage("Naming", "CA1724:Type names should not match namespaces",
        Justification = "Compression is a domain-specific term that doesn't conflict in practice with System.IO.Compression")]
    public readonly struct Compression : System.IEquatable<Compression>
    {
        /// <summary>
        /// Gets the compression type.
        /// </summary>
        public CompressionType Type { get; }

        /// <summary>
        /// Gets the compression level.
        /// </summary>
        public int Level { get; }

        public Compression(CompressionType type, int level)
        {
            Type = type;
            Level = level;
        }

        public override bool Equals(object? obj)
        {
            return obj is Compression other && Equals(other);
        }

        public bool Equals(Compression other)
        {
            return Type == other.Type && Level == other.Level;
        }

        public override int GetHashCode()
        {
            return System.HashCode.Combine(Type, Level);
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
