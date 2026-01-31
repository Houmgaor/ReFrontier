/*
JPK Compression Types for Monster Hunter Frontier Online.

JPK is the container format used for compressed data in MHF files. The format
supports multiple compression algorithms that trade off speed vs compression ratio.

File structure:
  - Magic: 0x1A524B4A ("JKR\x1A")
  - 4 bytes: Compression type (see CompressionType enum)
  - 4 bytes: Decompressed size
  - Variable: Compressed data (format depends on type)

Algorithm Hierarchy:
  RW (Raw) ──► No compression, direct byte copy
  LZ77 ──────► Sliding window with back-references (good compression, moderate speed)
  Huffman ───► Variable-length bit codes for frequent bytes

  Combinations:
    HFIRW = Huffman only (compresses repeated byte values)
    HFI   = Huffman + LZ77 (best compression, slowest)
*/

using System.Diagnostics.CodeAnalysis;

namespace LibReFrontier
{
    /// <summary>
    /// JPK compression algorithms used in Monster Hunter Frontier files.
    ///
    /// <para><b>Algorithm Selection Guide:</b></para>
    /// <list type="table">
    ///   <listheader>
    ///     <term>Type</term>
    ///     <description>Best For</description>
    ///   </listheader>
    ///   <item>
    ///     <term>RW</term>
    ///     <description>Already-compressed data or when speed is critical</description>
    ///   </item>
    ///   <item>
    ///     <term>HFIRW</term>
    ///     <description>Data with skewed byte distribution (many repeated values)</description>
    ///   </item>
    ///   <item>
    ///     <term>LZ</term>
    ///     <description>General-purpose compression with good speed/ratio balance</description>
    ///   </item>
    ///   <item>
    ///     <term>HFI</term>
    ///     <description>Maximum compression when processing time is not critical</description>
    ///   </item>
    /// </list>
    /// </summary>
    public enum CompressionType
    {
        /// <summary>
        /// Raw Writing - No compression applied.
        /// <para>Data is stored as-is with no transformation. Use for pre-compressed
        /// data or when decompression speed is critical.</para>
        /// <para><b>Compression ratio:</b> 1:1 (no reduction)</para>
        /// </summary>
        RW = 0,

        /// <summary>
        /// Special marker indicating uncompressed data (decode-only).
        /// <para>Treated identically to RW during decompression. Cannot be used
        /// for encoding—use RW instead.</para>
        /// </summary>
        None = 1,

        /// <summary>
        /// Huffman coding without LZ77 compression.
        /// <para>Builds a Huffman tree (510 entries) where frequently-occurring byte
        /// values get shorter bit codes. Effective when data has a skewed byte
        /// distribution but few repeated sequences.</para>
        /// <para><b>Compression ratio:</b> ~60-90% of original (varies with data)</para>
        /// <para><b>Speed:</b> Fast encode/decode</para>
        /// </summary>
        HFIRW = 2,

        /// <summary>
        /// LZ77 sliding window compression.
        /// <para>Replaces repeated byte sequences with back-references (offset, length).
        /// Uses a flag-bit system where each group of 8 items is preceded by a flag
        /// byte indicating literals vs back-references.</para>
        /// <para><b>Compression ratio:</b> ~30-70% of original</para>
        /// <para><b>Speed:</b> Moderate (faster than HFI)</para>
        /// <para><b>Window size:</b> Up to 8191 bytes back</para>
        /// <para><b>Match length:</b> 3-280 bytes</para>
        /// </summary>
        LZ = 3,

        /// <summary>
        /// Huffman + LZ77 combined compression.
        /// <para>First applies LZ77 to find repeated sequences, then encodes the
        /// resulting byte stream using Huffman coding. Achieves best compression
        /// at the cost of processing time.</para>
        /// <para><b>Compression ratio:</b> ~20-50% of original (best)</para>
        /// <para><b>Speed:</b> Slowest (due to two-stage processing)</para>
        /// </summary>
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
