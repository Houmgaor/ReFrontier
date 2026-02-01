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

| Type | Name  | Algorithm | Compression Ratio | Speed |
|------|-------|-----------|-------------------|-------|
| 0    | RW    | Raw Writing (no compression) | 1:1 | Fastest |
| 1    | None  | No compression (decode-only marker) | 1:1 | Fastest |
| 2    | HFIRW | Huffman coding only | ~60-90% | Fast |
| 3    | LZ    | LZ77 sliding window | ~30-70% | Moderate |
| 4    | HFI   | Huffman + LZ77 | ~20-50% | Slowest |

### Algorithm Details

**RW (Raw Writing)**: Data is stored as-is with no transformation. Use for pre-compressed data or when decompression speed is critical.

**HFIRW (Huffman only)**: Builds a Huffman tree where frequently-occurring byte values get shorter bit codes. Effective when data has a skewed byte distribution but few repeated sequences.

**LZ (LZ77)**: Sliding window compression that replaces repeated byte sequences with back-references (offset, length). Uses a flag-bit system where each group of 8 items is preceded by a flag byte. Window size up to 8191 bytes, match length 3-280 bytes.

**HFI (Huffman + LZ77)**: First applies LZ77 to find repeated sequences, then encodes the resulting byte stream using Huffman coding. Achieves best compression at the cost of processing time.

### Compression Level

The `--level` parameter (1-100) controls compression aggressiveness for LZ-based algorithms:

| Parameter | Range | Effect |
|-----------|-------|--------|
| Match length | 6-280 bytes | Maximum length of repeated sequences to encode |
| Window size | 50-8191 bytes | How far back to search for matches |

**Important notes:**
- Level only affects **LZ** and **HFI** compression. It is ignored by RW and HFIRW.
- Higher level = better compression ratio but slower encoding.
- Diminishing returns above level ~80.
- Recommended: `--compress hfi --level 80` for a good balance.

### Encoding Notes

- HFI (type 4) is recommended for maximum compression
- The Huffman tree is randomly shuffled at encode time, so the same input produces different (but equally valid) output on each run
- Decoding always works because the tree is stored in the file header (510 entries × 2 bytes = 1020 bytes overhead)

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
