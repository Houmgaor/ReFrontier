# ReFrontier

[![CI](https://github.com/Houmgaor/ReFrontier/actions/workflows/ci.yml/badge.svg)](https://github.com/Houmgaor/ReFrontier/actions/workflows/ci.yml)
[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-blue)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Platform](https://img.shields.io/badge/platform-windows%20%7C%20linux%20%7C%20macos-lightgrey)](https://github.com/Houmgaor/ReFrontier/releases)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

Tools for unpacking, decrypting, and editing Monster Hunter Frontier Online game files.

Special thanks to enler for their help!

## Features

This fork is fully compatible with [mhvuze/ReFrontier](https://github.com/mhvuze/ReFrontier) and [Chakratos/ReFrontier](https://github.com/Chakratos/ReFrontier), with the following improvements:

- **Cross-platform**: Works on Windows, Linux, and macOS
- **Performance**: 4x faster single-threaded, with multithreaded unpacking support
- **Round-trip editing**: Full support for ECD/EXF encryption and FTXT text repacking
- **Single command**: Compress and encrypt files in one step
- **Reliability**: Fixed duplicate filename issues ([#5](https://github.com/Houmgaor/ReFrontier/issues/5))
- **Security**: Removed memory-unsafe code and outdated libraries
- **Text tools**: Improved CSV parsing and cleaner fulldump output

## Installation

Download the [latest release](https://github.com/Houmgaor/ReFrontier/releases) for your operating system.
Unzip the archive to find `ReFrontier.exe` (or `ReFrontier` on Linux/macOS).

To get the latest features, you can [build from source](#build).

## Usage

You can drag-and-drop files or folders onto the executable, or use the command line.

### Quick Start

1. Copy `mhfdat.bin` (or any file) from the MHFrontier `dat/` folder to the same directory as the executable.

2. Decrypt and decompress the file:
    ```shell
    ./ReFrontier mhfdat.bin --log
    ```

3. Edit the extracted data (see [tools](#see-also) and [included utilities](#data-editing)).

4. Compress and encrypt:
    ```shell
    ./ReFrontier mhfdat.bin --compress hfi --level 80 --encrypt
    ```

5. Replace the original `mhfdat.bin` with the new file.

For detailed command reference, see [ReFrontier/README.md](./ReFrontier/README.md) or run:

```shell
./ReFrontier --help
```

### Common Options

| Option | Description |
|--------|-------------|
| `--help` | Display CLI help |
| `--close` | Close terminal after execution |
| `--log` | Generate `.meta` file (required for re-encryption) |
| `--cleanUp` | Delete intermediate files |

### Decryption

ReFrontier decrypts (ECD → JPK) and decompresses files by default.

To preserve metadata for later re-encryption, use `--log`:
```shell
./ReFrontier mhfdat.bin --log --decryptOnly
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

### Compression

Compress files using `--compress <type> --level <level>`:

| Type | Alias | Description |
|------|-------|-------------|
| `rw` | `0` | RW compression |
| `hfirw` | `2` | HFIRW compression (decode only) |
| `lz` | `3` | LZ compression |
| `hfi` | `4` | HFI Huffman compression |

```shell
./ReFrontier mhfdat.bin --compress hfi --level 80
```

Output is written to the `output/` directory.

### Encryption

Encrypt a compressed file with `--encrypt`:

```shell
./ReFrontier mhfdat.bin --encrypt
```

This requires a `.meta` file (e.g., `mhfdat.bin.meta`) created during [decryption](#decryption).

You can compress and encrypt in a single command:

```shell
./ReFrontier mhfdat.bin --compress hfi --level 80 --encrypt
```

Both ECD and EXF encryption formats are fully supported for round-trip editing.

### Text File Editing (FTXT)

FTXT text files can be extracted and repacked:

```shell
# Extract text with metadata
./ReFrontier text.ftxt --log

# Edit the generated .txt file, then repack
./ReFrontier text.ftxt.txt --pack
```

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

## License

Licensed under the [MIT License](LICENSE).

This fork is published with the agreement of the original author ([mhvuze](https://github.com/mhvuze)).
