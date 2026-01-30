# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

ReFrontier is a .NET 8.0 toolset for unpacking, decrypting, decompressing, and editing Monster Hunter Frontier Online game files. It's a fork of mhvuze/ReFrontier with improvements for cross-platform compatibility and performance.

## Build Commands

```bash
# Build (debug)
dotnet build

# Build (release)
dotnet build --configuration Release

# Run tests
dotnet test

# Run tests with verbose output
dotnet test --verbosity normal

# Run a single test by name
dotnet test --filter "FullyQualifiedName~TestClassName.TestMethodName"
# Example: dotnet test --filter "FullyQualifiedName~TestCrypto.TestEcdEncryption"

# Run tests with code coverage
dotnet test --collect:"XPlat Code Coverage"

# Publish self-contained release
dotnet publish -c Release -o publish
```

## Solution Structure

The solution contains 5 projects:

- **ReFrontier** - Main CLI application for file processing (entry point: `ReFrontier/Program.cs`)
- **LibReFrontier** - Shared library with cryptography, compression, and file utilities
- **FrontierTextTool** - Text extraction/editing tool using CSV format
- **FrontierDataTool** - Data structure extraction/editing tool
- **ReFrontier.Tests** - xUnit test project

## Architecture

### Core Processing Flow

```
Input File → Decrypt (ECD/EXF) → Decompress (JPK) → Unpack (containers) → Output
```

Packing reverses this flow: Pack → Compress → Encrypt.

### Key Components

**ReFrontier/Program.cs**: Main entry point containing `InputArguments` struct and file processing logic. Uses parallel processing (4 concurrent threads) with `ConcurrentQueue` for recursive unpacking. Supports dependency injection via constructor for testability.

**ReFrontier/Pack.cs**: Handles repacking directories and JPK compression (`JPKEncode`). Delegates to `PackingService`.

**ReFrontier/Unpack.cs**: Handles archive extraction and JPK decompression. Delegates to `UnpackingService`.

**ReFrontier/Services/**: Service layer providing testable implementations:
- `FileProcessingService` - Encryption/decryption operations
- `PackingService` - JPK encoding and archive packing
- `UnpackingService` - JPK decoding and archive unpacking
- `FileProcessingConfig` - Configurable paths and suffixes

**ReFrontier/Jpk/**: Compression codec implementations following `IJPKEncode`/`IJPKDecode` interfaces for different algorithms (RW, HFI, HFIRW, LZ). Uses `ICodecFactory` for testable codec creation.

**LibReFrontier/Abstractions/**: Dependency injection abstractions:
- `IFileSystem` / `RealFileSystem` - File system operations
- `ILogger` / `ConsoleLogger` - Console output

**LibReFrontier/Crypto.cs**: ECD encryption/decryption and EXF decoding with CRC32 validation.

**LibReFrontier/Compression.cs**: `CompressionType` enum (RW, None, HFIRW, LZ, HFI) and `Compression` struct.

### Supported File Formats

Files are identified by magic headers:
- `0x4F4D4F4D` (MOMO) - Simple archive
- `0x1A646365` (ECD) - Encrypted container
- `0x1A667865` (EXF) - Alternative encrypted format
- `0x1A524B4A` (JKR) - Compressed JPK format
- `0x0161686D` (MHA) - MHA container

### Output Conventions

- Unpacked files go to `output/` directory
- `.meta` files store encryption metadata (required for re-encryption)
- `.decd` suffix for decrypted files
- `.unpacked/` suffix for unpacked directories

## CLI Usage

```bash
# Basic unpacking with log (required for re-encryption)
./ReFrontier mhfdat.bin --log

# Decrypt only
./ReFrontier file.bin --decryptOnly

# Compress and encrypt
./ReFrontier file.bin --compress=3,50 --encrypt

# Repack directory
./ReFrontier file.bin --pack
```

Key options: `--log`, `--recursive`/`--nonRecursive`, `--compress=[type],[level]`, `--encrypt`, `--pack`, `--cleanUp`

## Testing

Tests are in `ReFrontier.Tests/` using xUnit. The main project uses `InternalsVisibleTo` to expose internals to the test project.

### Test Organization

- `ReFrontier.Tests/Mocks/` - Test doubles (`InMemoryFileSystem`, `TestLogger`)
- `ReFrontier.Tests/Services/` - Service-level unit tests
- `ReFrontier.Tests/Integration/` - Integration tests (roundtrip, text tool)
- `ReFrontier.Tests/Jpk/` - Codec factory tests
- `ReFrontier.Tests/FrontierTextTool/` - Text extraction/insertion tests
- `ReFrontier.Tests/FrontierDataTool/` - Data extraction/import tests
- Root test files (`TestCrypto.cs`, `TestJpk*.cs`, etc.) - Component tests

### Testing Pattern

Services use constructor injection for testability. Static methods delegate to instance methods internally, allowing both backward-compatible static API and testable instance-based code

## Dependencies

- Spectre.Console 0.49.1 (CLI interface)
- CsvHelper 33.0.1 (FrontierTextTool, FrontierDataTool)
- System.Text.Encoding.CodePages 9.0.0 (Shift-JIS support)
- System.IO.Hashing 9.0.0 (CRC32)
