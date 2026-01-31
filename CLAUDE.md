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

**Application Entry Point:**

**ReFrontier/Program.cs**: Main entry point reduced to ~40 lines. Contains `InputArguments` struct. Main method delegates to CLI layer and orchestrator. Program class manages parallel processing (configurable via `--parallelism` CLI option) with `ConcurrentQueue` for recursive unpacking. Supports dependency injection via constructor for testability.

**CLI Layer** (`ReFrontier/CLI/`):
- `CliSchema` - Defines all CLI options and creates System.CommandLine RootCommand
- `CliArguments` - Immutable DTO containing parsed CLI arguments
- Separates CLI infrastructure from business logic

**Orchestration** (`ReFrontier/Orchestration/`):
- `ApplicationOrchestrator` - Coordinates high-level application flow (file/directory validation, routing to processing methods)
- Uses two-constructor DI pattern for testability

**File Routing** (`ReFrontier/Routing/`):
- `IFileTypeHandler` - Interface for file type handlers with `CanHandle()`, `Handle()`, and `Priority`
- `FileRouter` - Registry-based router that selects handler by magic number and priority
- `Handlers/` - One handler per file type (stage containers, ECD/EXF encryption, JPK compression, MOMO/MHA archives, FTXT text, simple archives)
- ProcessFile method reduced from ~100 lines to ~30 lines by delegating to router

**Services** (`ReFrontier/Services/`):
- `FileProcessingService` - Encryption/decryption operations
- `PackingService` - JPK encoding and archive packing
- `UnpackingService` - JPK decoding and archive unpacking
- `FileProcessingConfig` - Configurable paths and suffixes

**Compression** (`ReFrontier/Jpk/`):
- Codec implementations following `IJPKEncode`/`IJPKDecode` interfaces for different algorithms (RW, HFI, HFIRW, LZ)
- `ICodecFactory` for testable codec creation

**Abstractions** (`LibReFrontier/Abstractions/`):
- `IFileSystem` / `RealFileSystem` - File system operations
- `ILogger` / `ConsoleLogger` - Console output

**Core Libraries:**

**LibReFrontier/Crypto.cs**: ECD encryption/decryption and EXF decoding with CRC32 validation.

**LibReFrontier/Compression.cs**: `CompressionType` enum (RW, None, HFIRW, LZ, HFI) and `Compression` struct.

**LibReFrontier/FileMagic.cs**: Magic number constants for all file formats.

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
# Basic unpacking with metadata (required for repacking/re-encryption)
./ReFrontier mhfdat.bin --saveMeta

# Auto-detect optimal parallelism (default)
./ReFrontier mhfdat.bin --saveMeta

# Use 8 parallel threads
./ReFrontier directory/ --parallelism 8

# Single-threaded processing
./ReFrontier file.bin --parallelism 1

# Suppress progress output for faster processing
./ReFrontier directory/ --quiet

# Decrypt only
./ReFrontier file.bin --decryptOnly

# Compress and encrypt
./ReFrontier file.bin --compress=3,50 --encrypt

# Repack directory
./ReFrontier file.bin --pack
```

Key options:
- `--parallelism` - Number of parallel threads (0=auto-detect using CPU cores, default: 0)
- `--quiet` - Suppress progress output during processing (reduces logging overhead for better parallelism)
- `--saveMeta` - Save metadata files (required for repacking/re-encryption)
- `--recursive`/`--nonRecursive` - Control recursive unpacking
- `--compress=[type],[level]` - Compression settings
- `--encrypt` - Encrypt output
- `--pack` - Repack directory
- `--cleanUp` - Delete source files after processing

## Testing

Tests are in `ReFrontier.Tests/` using xUnit. The main project uses `InternalsVisibleTo` to expose internals to the test project.

### Test Organization

- `ReFrontier.Tests/Mocks/` - Test doubles (`InMemoryFileSystem`, `TestLogger`)
- `ReFrontier.Tests/CLI/` - CLI schema and argument parsing tests
- `ReFrontier.Tests/Orchestration/` - Application orchestrator tests
- `ReFrontier.Tests/Routing/` - File router tests
- `ReFrontier.Tests/Routing/Handlers/` - Individual handler tests
- `ReFrontier.Tests/Services/` - Service-level unit tests
- `ReFrontier.Tests/Integration/` - Integration tests (roundtrip, text tool)
- `ReFrontier.Tests/Jpk/` - Codec factory tests
- `ReFrontier.Tests/FrontierTextTool/` - Text extraction/insertion tests
- `ReFrontier.Tests/FrontierDataTool/` - Data extraction/import tests
- Root test files (`TestCrypto.cs`, `TestJpk*.cs`, etc.) - Component tests

### Testing Pattern

Services and components use the two-constructor DI pattern:
- Default parameterless constructor creates real dependencies (for production use)
- Injection constructor accepts interfaces for testing (IFileSystem, ILogger, etc.)

This allows both backward-compatible instantiation and fully testable code with mock dependencies

## Dependencies

- Spectre.Console 0.49.1 (CLI interface)
- CsvHelper 33.0.1 (FrontierTextTool, FrontierDataTool)
- System.Text.Encoding.CodePages 9.0.0 (Shift-JIS support)
- System.IO.Hashing 9.0.0 (CRC32)
