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
    }
}
