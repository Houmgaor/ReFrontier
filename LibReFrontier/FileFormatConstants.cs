namespace LibReFrontier;

/// <summary>
/// Constants for Monster Hunter Frontier file format structures.
/// Centralizes magic numbers used across the codebase for archive parsing.
/// </summary>
public static class FileFormatConstants
{
    /// <summary>
    /// Header length for ECD and EXF encrypted containers (16 bytes).
    /// </summary>
    public const int EncryptionHeaderLength = 0x10;

    /// <summary>
    /// Entry size in simple archive containers (offset + size = 8 bytes).
    /// </summary>
    public const int SimpleArchiveEntrySize = 0x08;

    /// <summary>
    /// Header size of a MOMO archive (8 bytes): 4-byte magic then the entry count.
    /// </summary>
    public const int MomoHeaderSize = 0x08;

    /// <summary>
    /// Alignment of entry data inside a MOMO archive (64 bytes).
    /// <para>Entry data begins at the entry table end rounded up to this boundary, each
    /// following entry begins at the previous entry's end rounded up to it, and the file
    /// is padded with zeros to it. Verified against all 615 MOMO archives shipped in the
    /// PC client's dat/sound directory.</para>
    /// </summary>
    public const int MomoEntryAlignment = 0x40;

    /// <summary>
    /// Entry metadata size in MHA archives (20 bytes).
    /// Contains: stringOffset, entryOffset, entrySize, paddedSize, fileId.
    /// </summary>
    public const int MhaEntryMetadataSize = 0x14;

    /// <summary>
    /// Header size for stage containers (24 bytes).
    /// Contains: 3 segments of (offset + size) pairs.
    /// </summary>
    public const int StageContainerHeaderSize = 0x18;

    /// <summary>
    /// Rest header size in stage containers (8 bytes).
    /// Contains: restCount, unkHeader.
    /// </summary>
    public const int StageContainerRestHeaderSize = 0x08;

    /// <summary>
    /// Entry size for rest entries in stage containers (12 bytes).
    /// Contains: offset, size, unk.
    /// </summary>
    public const int StageContainerRestEntrySize = 0x0C;

    /// <summary>
    /// Header length for FTXT text files (16 bytes).
    /// Contains: 10 bytes padding/unknown, 2 bytes string count, 4 bytes text block size.
    /// </summary>
    public const int FtxtHeaderLength = 0x10;
}
