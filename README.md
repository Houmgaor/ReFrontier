<p align="center">
  <img src="docs/banner.png" alt="ReFrontier Banner" width="800">
</p>

# ReFrontier

[![CI](https://github.com/Houmgaor/ReFrontier/actions/workflows/ci.yml/badge.svg)](https://github.com/Houmgaor/ReFrontier/actions/workflows/ci.yml)
[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-blue)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Platform](https://img.shields.io/badge/platform-windows%20%7C%20linux%20%7C%20macos-lightgrey)](https://github.com/Houmgaor/ReFrontier/releases)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

ReFrontier is a command-line toolset for modding Monster Hunter Frontier Online. It handles the full round-trip workflow: unpacking, decrypting, and decompressing game files for editing, then compressing, encrypting, and repacking them for use in-game.

## Features

Originally based on [mhvuze/ReFrontier](https://github.com/mhvuze/ReFrontier), ReFrontier has been extensively rewritten. Version 2.0 introduced breaking changes—see [MIGRATION_2.0.md](./MIGRATION_2.0.md) for upgrade instructions. You can use any release pre-2.0 for a version fully compatible with the original.

Key features:

- **Cross-platform**: Works on Windows, Linux, and macOS
- **Performance**: 4x faster single-threaded, with multithreaded unpacking support
- **Round-trip editing**: Full support for ECD/EXF encryption and FTXT text repacking
- **Single command**: Compress and encrypt files in one step
- **Reliability**: Fixed duplicate filename issues
- **Security**: Removed memory-unsafe code and outdated libraries
- **Text tools**: Improved CSV parsing and cleaner fulldump output
- **Stateful round-trip**: Extraction records how a file was packed, so rebuilding it is one flag (`--restore`)
- **Validation**: Non-destructive integrity checking of game files (`--validate`)
- **Diff**: Structural comparison of two game files through encryption/compression layers (`--diff`)

## Installation

Download the [latest release](https://github.com/Houmgaor/ReFrontier/releases) for your operating system.
Unzip the archive to find `ReFrontier.exe` (or `ReFrontier` on Linux/macOS).

To get the latest features, you can [build from source](#build).

## Usage

You can drag-and-drop files or folders onto the executable, or use the command line.

### Quick Start

1. Copy `mhfdat.bin` (or any file or folder) from the MHFrontier `dat/` folder to the same directory as the executable.

2. Decrypt and decompress the file:
    ```shell
    ./ReFrontier mhfdat.bin --saveMeta
    ```

3. Edit the extracted data (see [tools](#see-also) and [included utilities](#data-editing)).

4. Rebuild the game file:
    ```shell
    ./ReFrontier mhfdat.bin.decd.bin --restore
    ```

    The compression algorithm and encryption key come from the recipe written in step 2,
    so there is nothing to remember. See [Rebuilding](#rebuilding) for the details.

5. Replace the original `mhfdat.bin` with `output/mhfdat.bin`.

For detailed command reference, see [ReFrontier/README.md](./ReFrontier/README.md) or run:

```shell
./ReFrontier --help
```

### Common Options

| Option | Description |
|--------|-------------|
| `--help` | Display CLI help |
| `--saveMeta` | Save metadata files (required for repacking/re-encryption) |
| `--restore` | Rebuild a file using the recipe saved during extraction |
| `--cleanUp` | Delete intermediate files |
| `--validate` | Check file integrity without writing output |
| `--diff <file>` | Structural comparison with another file |

### Decryption

ReFrontier decrypts (ECD → JPK) and decompresses files by default.

To preserve metadata for later re-encryption, use `--saveMeta`:
```shell
./ReFrontier mhfdat.bin --saveMeta --decryptOnly
```

### Decompression

Decompression *replaces* the original file. Always backup important data first.

Compressed files are identified by their `JKR` header (first bytes of the file).

```shell
./ReFrontier mhfdat.bin  # Decompress if already decrypted
```

### Data Editing

Once files are decrypted and decompressed, you can edit them using:

- [FrontierTextTool](./FrontierTextTool/README.md) - Extract and modify game text
- [FrontierDataTool](./FrontierDataTool/README.md) - Extract and modify game data structures
- External tools listed in [See Also](#see-also)

### Rebuilding

Extracting with `--saveMeta` writes an *extraction recipe* next to the original file,
recording every transformation that was undone:

```json
{
  "Version": 1,
  "SourceFile": "mhfdat.bin",
  "ExtractedFile": "mhfdat.bin.decd.bin",
  "Layers": [
    { "Kind": "Ecd", "MetaFile": "mhfdat.bin.meta", "OriginalSize": 7383160 },
    { "Kind": "Jpk", "Algorithm": "HFI", "OriginalSize": 7383144 }
  ]
}
```

`--restore` reads it and reverses those layers, so you do not have to know that
`mhfdat.bin` happens to be ECD-encrypted and HFI-compressed:

```shell
./ReFrontier mhfdat.bin.decd.bin --restore
```

The rebuilt file is written to `output/` under its original name. You can point
`--restore` at either the edited file or the original name; ReFrontier finds the
recipe either way, and refuses to run if you point it at a file that is still packed.

Recipes are plain JSON and safe to edit by hand, for instance to force a different
algorithm. Two notes on what they can and cannot capture:

- **Compression level is not recorded.** It is an encoder-side parameter that the JKR
  header does not store, so it cannot be recovered from a game file. Restoring defaults
  to level 80; override it with `--level`. This affects output size only, not
  correctness — the game reads any valid level.
- **Container archives are out of scope.** Files that unpack into a directory (MOMO,
  MHA) are repacked through their `.log` file with `--pack`, as before.

If you prefer to drive it manually, or have no recipe, the explicit route still works:

```shell
./ReFrontier mhfdat.bin.decd.bin --compress hfi --level 80 --encrypt
```

### Compression

Compress files using `--compress <type> --level <level>`:

| Type | Alias | Algorithm | Ratio |
|------|-------|-----------|-------|
| `rw` | `0` | No compression (raw) | 1:1 |
| `hfirw` | `2` | Huffman coding only | ~60-90% |
| `lz` | `3` | LZ77 sliding window | ~30-70% |
| `hfi` | `4` | Huffman + LZ77 (best) | ~20-50% |

The `--level` parameter (1-100) controls compression aggressiveness for `lz` and `hfi` only (ignored by `rw` and `hfirw`). Diminishing returns above ~80.

```shell
./ReFrontier mhfdat.bin --compress hfi --level 80
```

Output is written to the `output/` directory.

For technical details on compression algorithms, see [docs/ARCHIVE_FORMATS.md](./docs/ARCHIVE_FORMATS.md#compression-types).

### Encryption

Encrypt a compressed file with `--encrypt`:

```shell
./ReFrontier mhfdat.bin --encrypt
```

If a `.meta` file exists (e.g., `mhfdat.bin.meta` created during [decryption](#decryption)), it will be used.
Otherwise, the default ECD key index (4) is used automatically. This works for all known MHF files, but may not match other game versions or regions.

You can compress and encrypt in a single command:

```shell
./ReFrontier mhfdat.bin --compress hfi --level 80 --encrypt
```

Both ECD and EXF encryption formats are fully supported for round-trip editing.

### Text File Editing (FTXT)

FTXT text files can be extracted and repacked:

```shell
# Extract text with metadata
./ReFrontier text.ftxt --saveMeta

# Edit the generated .txt file, then repack
./ReFrontier text.ftxt.txt --pack
```

### Validation

Check the structural integrity of a game file without writing any output:

```shell
./ReFrontier mhfdat.bin --validate
```

Recursively validates encryption, compression, and archive layers (ECD, EXF, JPK, MOMO, MHA, FTXT) with CRC32 verification and bounds checking.

### Diff

Compare two game files structurally, peeling through encryption and compression layers:

```shell
./ReFrontier original.bin --diff modified.bin
```

Useful for verifying round-trip correctness (unpack, edit, repack), comparing game versions, or catching regressions. Exit code 0 means identical, 1 means differences found.

## Build

Requires [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```shell
git clone https://github.com/Houmgaor/ReFrontier.git
cd ReFrontier
dotnet build --configuration Release
```

The executable will be at `./ReFrontier/bin/Release/net8.0/ReFrontier.exe`.

## See Also

Related tools and projects:

| Project | Description |
|---------|-------------|
| [Monster-Hunter-Frontier-Patterns](https://github.com/var-username/Monster-Hunter-Frontier-Patterns) | Binary file format templates |
| [FrontierTextHandler](https://github.com/Houmgaor/FrontierTextHandler) | Python tool for text editing |
| [MHFrontier-Blender-Addon](https://github.com/Houmgaor/MHFrontier-Blender-Addon) | Import 3D models |
| [Erupe](https://github.com/Houmgaor/Erupe) | MHFrontier private server |

## Credits

- Based on [mhvuze/ReFrontier](https://github.com/mhvuze/ReFrontier)
- With additional features from [chakratos/ReFrontier](https://github.com/chakratos/ReFrontier)
- Special thanks to enler for their help!

## License

Edits in this project are licensed under the [MIT License](LICENSE).

See [ReFrontier#2](https://github.com/mhvuze/ReFrontier/issues/2) for license information on the original code.
