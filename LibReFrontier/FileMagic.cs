namespace LibReFrontier;

/// <summary>
/// Magic number constants for Monster Hunter Frontier file formats.
/// </summary>
public static class FileMagic
{
    /// <summary>
    /// MOMO - Simple archive container (snp, snd files).
    /// </summary>
    public const uint MOMO = 0x4F4D4F4D;

    /// <summary>
    /// MHA - Named archive container with file metadata.
    /// </summary>
    public const uint MHA = 0x0161686D;

    /// <summary>
    /// ECD - Encrypted container using LCG cipher.
    /// </summary>
    public const uint ECD = 0x1A646365;

    /// <summary>
    /// EXF - Encrypted container using XOR cipher.
    /// </summary>
    public const uint EXF = 0x1A667865;

    /// <summary>
    /// JKR - JPK compressed container.
    /// </summary>
    public const uint JKR = 0x1A524B4A;

    /// <summary>
    /// FTXT - MHF text file format.
    /// </summary>
    public const uint FTXT = 0x000B0000;

    /// <summary>
    /// Check if a magic number indicates an encrypted file (ECD or EXF).
    /// </summary>
    /// <param name="magic">The magic number to check.</param>
    /// <returns>True if the file is encrypted.</returns>
    public static bool IsEncrypted(uint magic) => magic == ECD || magic == EXF;

    /// <summary>
    /// Check if a magic number indicates a JPK compressed file.
    /// </summary>
    /// <param name="magic">The magic number to check.</param>
    /// <returns>True if the file is JPK compressed.</returns>
    public static bool IsJpkCompressed(uint magic) => magic == JKR;
}
