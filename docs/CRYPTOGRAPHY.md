# Monster Hunter Frontier Cryptography

This document describes the encryption systems used in Monster Hunter Frontier Online, based on reverse engineering of the game executable and analysis of game files.

## Overview

MHF uses two encryption formats:

| Format | Magic | Algorithm | Usage |
|--------|-------|-----------|-------|
| ECD | `0x1A646365` | LCG + Nibble Feistel | Primary encryption for all game files |
| EXF | `0x1A667865` | LCG + XOR | Alternative format (rare) |

Both formats use a 16-byte header followed by encrypted payload data.

## Key Index Discovery

**Important Finding:** Analysis of 1,962 encrypted files from MHF revealed that **100% use key index 4**.

This includes:
- Main data files (`mhfdat.bin`, `mhfemd.bin`, `mhfinf.bin`, etc.)
- Stage files (`st*.pac`)
- NPC models and textures
- All other encrypted game assets

This means `.meta` files are technically redundant for standard MHF files, as the key index can be assumed to be 4. The tool retains `.meta` file support for edge cases (development builds, regional variants, older versions).

## ECD Format (Primary Encryption)

### Header Structure (16 bytes)

```
Offset  Size  Type    Description
0x00    4     uint32  Magic: 0x1A646365 ("ecd\x1A" little-endian)
0x04    2     uint16  Key index (selects LCG parameters, 0-5)
0x06    2     -       Unused padding
0x08    4     uint32  Payload size (encrypted data length)
0x0C    4     uint32  CRC32 of decrypted payload
```

### LCG Key Parameters

The encryption uses a Linear Congruential Generator (LCG) with selectable parameters:

| Key | Multiplier | Increment | Notes |
|-----|------------|-----------|-------|
| 0 | 0x4A4B522E (1,246,450,222) | 1 | Unique |
| 1 | 0x00010DCD (69,069) | 1 | Same as 2, 3 |
| 2 | 0x00010DCD (69,069) | 1 | Same as 1, 3 |
| 3 | 0x00010DCD (69,069) | 1 | Same as 1, 2 |
| **4** | **0x0019660D (1,664,525)** | **3** | **All MHF files use this** |
| 5 | 0x7D2B89DD (2,100,005,341) | 1 | Unique |

**Notable:** Key 4's multiplier **1,664,525** is the famous LCG constant from "Numerical Recipes in C" (Press et al., 1992), a widely-used pseudo-random number generator.

### LCG Formula

```
state(n+1) = state(n) * multiplier + increment
```

The LCG state is 32-bit unsigned, with natural overflow.

### Decryption Algorithm

1. **Initialize LCG state** from CRC32:
   ```
   state = (crc32 << 16) | (crc32 >> 16) | 1
   ```
   The bit rotation and OR with 1 ensures an odd starting value.

2. **Generate initial XOR pad:**
   ```
   xorpad = LCG_next(state)
   feedback = xorpad & 0xFF
   ```

3. **For each encrypted byte:**
   ```
   xorpad = LCG_next(state)

   // XOR with previous output (cipher feedback)
   temp = encrypted_byte ^ feedback

   // Split into nibbles
   high_nibble = (temp >> 4) & 0x0F
   low_nibble = temp & 0x0F

   // 8-round Feistel-like transformation
   for j in 0..7:
       mix = xorpad ^ low_nibble
       low_nibble = high_nibble
       high_nibble ^= mix
       high_nibble &= 0xFF
       xorpad >>= 4

   // Recombine nibbles
   decrypted = (high_nibble & 0x0F) | ((low_nibble & 0x0F) << 4)
   feedback = decrypted
   ```

### Encryption Algorithm

Encryption is the inverse operation. The same LCG sequence is generated, and the Feistel transformation is applied in reverse to produce the ciphertext.

### CRC32 Validation

After decryption, the CRC32 of the decrypted payload should match the value stored in the header at offset 0x0C. This allows verification of successful decryption.

## EXF Format (Alternative Encryption)

### Header Structure (16 bytes)

```
Offset  Size  Type    Description
0x00    4     uint32  Magic: 0x1A667865 ("exf\x1A" little-endian)
0x04    2     uint16  Key index (selects LCG parameters, 0-4)
0x06    6     -       Unused
0x0C    4     uint32  Seed value for XOR key generation
```

### Key Generation

EXF generates a 16-byte XOR key from the header:

```
seed = header[0x0C..0x10]  // 4 bytes as uint32
state = seed

for i in 0..3:
    state = state * multiplier + increment  // LCG step
    key[i*4..(i+1)*4] = state ^ seed        // 4 bytes
```

### Decryption Algorithm

For each byte at position `i` (0-indexed from payload start):

```
position = i
encrypted = payload[i]

// Position-based lookup
key_idx1 = position & 0x0F
key_idx2 = ((encrypted ^ position) & 0xF0) >> 4

// Nibble transformation
r4 = encrypted ^ position
r12 = key[key_idx1]
r7 = key[key_idx2]
r9 = (r4 >> 4) ^ r12
r26 = (r7 >> 4) ^ r4

// Recombine
decrypted = (r26 & 0x0F) | ((r9 & 0x0F) << 4)
```

### Encryption (Brute Force)

The EXF transformation is not easily invertible. Encryption uses brute-force search over all 256 possible byte values to find the ciphertext that produces the desired plaintext.

## Implementation Notes

### Variable Naming Convention

The code in `LibReFrontier/Crypto.cs` uses variable names like `r8`, `r10`, `r11`, `r12`, `r26`, `r28`. These correspond to PowerPC register names from the original reverse-engineered assembly code, preserved for easier cross-reference with disassembly.

### Source Locations

The encryption keys were extracted from the game executable:
- ECD keys: Address `0x10292DCC`
- EXF keys: Address `0x1025F4E0`

### Re-encryption Without .meta Files

Since all known MHF files use key index 4, you can encrypt without a `.meta` file:

```csharp
// Using default key index (4)
byte[] encrypted = Crypto.EncodeEcd(plaintext);

// Or explicitly specify key index
byte[] encrypted = Crypto.EncodeEcd(plaintext, keyIndex: 4);
```

The CRC32 and payload size are calculated automatically.

## Security Analysis

The ECD encryption is a symmetric stream cipher with:

- **Key derivation:** CRC32 of plaintext (known-plaintext vulnerability)
- **Cipher:** Nibble-based Feistel network with LCG-generated round keys
- **Feedback:** Output feedback mode (previous decrypted byte affects next)

This is **not cryptographically secure** by modern standards:
- The CRC32-based initialization means identical files produce identical ciphertexts
- The LCG is predictable given the key index and CRC32
- No authentication (integrity only via CRC32)

However, the encryption serves its purpose of obfuscating game data from casual inspection.

## References

- Original reverse engineering by mhvuze and enler
- LCG constants: Press, W. H., et al. "Numerical Recipes in C" (1992)
- Implementation: `LibReFrontier/Crypto.cs`
