# Monster Hunter Frontier Archive Formats

This document describes the binary structure of archive formats used in Monster Hunter Frontier Online.

## Magic Number Reference

| Format | Magic (Hex) | Magic (ASCII) | Description |
|--------|-------------|---------------|-------------|
| MOMO   | `0x4F4D4F4D` | "MOMO" | Simple archive container |
| MHA    | `0x0161686D` | "mha\x01" | Named archive with metadata |
| ECD    | `0x1A646365` | "\x1Aecd" | Encrypted (LCG cipher) |
| EXF    | `0x1A667865` | "\x1Aexf" | Encrypted (XOR cipher) |
| JKR    | `0x1A524B4A` | "\x1AJKR" | JPK compressed |
| FTXT   | `0x000B0000` | - | MHF text file |

## Simple Archive (MOMO)

Simple archives are unnamed containers with sequential file entries.

### Header Structure

```text
Offset  Size  Description
0x00    4     Entry count (uint32)
0x04    8*N   Entry table (N entries)
```

### Entry Table Structure (8 bytes per entry)

```text
Offset  Size  Description
0x00    4     File data offset (uint32)
0x04    4     File data size (uint32)
```

### Notes

- Files with MOMO magic (`0x4F4D4F4D`) have an 8-byte header before the entry table
- Generic simple containers (txb, bin, pac, gab) have a 4-byte header (just the count)

## MHA Archive (Named Archive)

MHA archives contain named files with extended metadata.

### Header Structure (24 bytes)

```text
Offset  Size  Description
0x00    4     Magic: 0x0161686D
0x04    4     Pointer to entry metadata block (uint32)
0x08    4     Entry count (uint32)
0x0C    4     Pointer to entry names block (uint32)
0x10    4     Entry names block length (uint32)
0x14    2     Unknown 1 (int16)
0x16    2     Unknown 2 (int16)
```

### Layout

```text
[Header 24 bytes]
[File data for all entries]
[Entry names block - null-terminated UTF-8 strings]
[Entry metadata block]
```

### Entry Metadata Structure (20 bytes per entry)

```text
Offset  Size  Description
0x00    4     String offset in names block (uint32)
0x04    4     File data offset (uint32)
0x08    4     File data size (uint32)
0x0C    4     Padded size (uint32)
0x10    4     File ID (uint32)
```

## ECD Encrypted Container

ECD files use a Linear Congruential Generator (LCG) with a nibble-based Feistel cipher.

**See [CRYPTOGRAPHY.md](./CRYPTOGRAPHY.md) for full algorithm details.**

### Header Structure (16 bytes)

```text
Offset  Size  Description
0x00    4     Magic: 0x1A646365
0x04    2     Key index (0-5, selects LCG parameters)
0x06    2     Unused padding
0x08    4     Payload size (uint32)
0x0C    4     CRC32 of decrypted data (uint32)
```

### Key Index Discovery

**All known MHF files use key index 4.** Analysis of 1,962 encrypted files confirmed this.

Key 4 uses LCG parameters: multiplier=0x0019660D (1,664,525), increment=3.

### Re-encryption

Files can be re-encrypted without a `.meta` file using the default key index:

```csharp
byte[] encrypted = Crypto.EncodeEcd(plaintext);  // Uses key index 4
```

## EXF Encrypted Container

EXF files use LCG-derived XOR keys with position-dependent nibble transformation.

**See [CRYPTOGRAPHY.md](./CRYPTOGRAPHY.md) for full algorithm details.**

### Header Structure (16 bytes)

```text
Offset  Size  Description
0x00    4     Magic: 0x1A667865
0x04    2     Key index (0-4, selects LCG parameters)
0x06    6     Unused
0x0C    4     Seed value for XOR key generation
```

## JKR Compressed Container (JPK)

JKR files contain JPK-compressed data supporting multiple compression algorithms.

### Header Structure (16 bytes)

```text
Offset  Size  Description
0x00    4     Magic: 0x1A524B4A
0x04    2     Version/Flags: 0x0108
0x06    2     Compression type (uint16)
0x08    4     Data start offset (uint32, typically 0x10)
0x0C    4     Decompressed data size (uint32)
```

### Compression Types

| Type | Name  | Description |
|------|-------|-------------|
| 0    | RW    | Run-length with window |
| 1    | None  | No compression (raw data) |
| 2    | HFIRW | Huffman + RW |
| 3    | LZ    | LZ77-based compression |
| 4    | HFI   | Huffman indexed |

### Notes

- Type 3 (LZ) is most commonly used for game data
- Compression level affects window size and search depth

## Stage Container

Stage containers have no magic number and use a fixed structure for stage-specific data.

### Header Structure (24 bytes fixed section)

```text
Offset  Size  Description
0x00    8*3   Fixed segment table (3 entries)
0x18    4     Rest segment count (uint32)
0x1C    4     Unknown header value (uint32)
0x20    12*N  Rest segment table (N entries)
```

### Fixed Segment Entry (8 bytes)

```text
Offset  Size  Description
0x00    4     File data offset (uint32)
0x04    4     File data size (uint32)
```

### Rest Segment Entry (12 bytes)

```text
Offset  Size  Description
0x00    4     File data offset (uint32)
0x04    4     File data size (uint32)
0x08    4     Unknown value (uint32)
```

### Notes

- First 3 segments are fixed (model, collision, etc.)
- Remaining segments are variable count
- Null entries have offset=0 and size=0

## File Detection

Files can be identified by reading the first 4 bytes and comparing against known magic numbers:

```csharp
uint magic = reader.ReadUInt32();
if (FileMagic.IsEncrypted(magic))
    // Handle ECD or EXF
else if (FileMagic.IsJpkCompressed(magic))
    // Handle JKR
else if (magic == FileMagic.MOMO)
    // Handle simple archive with 8-byte header
// etc.
```

See `LibReFrontier/FileMagic.cs` for the complete magic number definitions.
