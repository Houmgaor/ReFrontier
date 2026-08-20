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

# Run directly (without building separately)
dotnet run --project ReFrontier -- mhfdat.bin

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
- `RestoreService` - Rebuilds a file from its `ExtractionRecipe` (finds the recipe, reverses its layers, recurses through container entries)
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

**LibReFrontier/ExtractionRecipe.cs**: `ExtractionRecipe` and `RecipeLayer`, the JSON record of how a file was unpacked. Handlers report a `RecipeLayer` through `ProcessFileResult.Layer`; `Program.ProcessFile` accumulates them across its recursion and writes the recipe.

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
- `.recipe.json` files record the encryption/compression/container layers undone during extraction, consumed by `--restore`. From recipe version 2 they embed the encryption header, so they work without the `.meta` file; `.meta` is still written for `--encrypt`, FrontierTextTool and older versions
- `.decd` suffix for decrypted files
- `.unpacked/` suffix for unpacked directories

## CLI Usage

Each task is a command; `--help` on a command lists only its options.

```bash
# Basic unpacking; metadata for repacking is written automatically
./ReFrontier unpack mhfdat.bin

# A bare path still unpacks, as in earlier versions
./ReFrontier mhfdat.bin

# Use 8 parallel threads
./ReFrontier unpack directory/ --parallelism 8

# Single-threaded processing
./ReFrontier unpack file.bin --parallelism 1

# Suppress progress bar (for scripts or logs)
./ReFrontier unpack directory/ --quiet

# Show per-file processing messages
./ReFrontier unpack directory/ --verbose

# Decrypt only
./ReFrontier decrypt file.bin

# Compress and encrypt
./ReFrontier compress file.bin --type hfi --level 80 --encrypt

# Repack directory
./ReFrontier pack directory.unpacked/

# Rebuild a file from the recipe written during extraction
./ReFrontier restore mhfdat.bin.decd.bin

# Check integrity, or compare two files
./ReFrontier validate mhfdat.bin
./ReFrontier diff original.bin modified.bin
```

Commands:
- `unpack <path>` - Decrypt, decompress and unpack a file or directory
- `decrypt <file>` - Decrypt without decompressing
- `pack <dir>` - Repack a directory produced by `unpack`
- `restore <path>` - Rebuild a file or unpacked directory from its `.recipe.json` (reverses recorded encryption, compression and container layers, recursing into nested containers)
- `compress <file> --type <type>` - Compression type: `rw`, `hfirw`, `lz`, `hfi` (or `0`, `2`, `3`, `4`)
- `encrypt <file>` - Encrypt output (uses `.meta` file if available, otherwise default key index 4)
- `validate <path>` - Check integrity without writing output
- `diff <a> <b>` - Structural comparison of two files

Options for every command:
- `--parallelism` - Number of parallel threads (0=auto-detect using CPU cores, default: 0)
- `--quiet` - Suppress the progress bar during processing
- `--verbose` - Show per-file processing messages (off by default for cleaner output)

Options for `unpack` (and, where they apply, `decrypt` and `pack`):
- `--no-meta` - Skip metadata (`.meta`, `.log`, `.recipe.json`); rebuilding becomes impossible. Metadata is written by default.
- `--flat` - Disable recursive unpacking (recursive is the default)
- `--keep-encrypted` - Skip decryption entirely
- `--keep-compressed` - Skip JPK decompression
- `--stage` / `--auto-stage` - Treat file as, or detect, a stage-specific container
- `--clean` - Delete source files after processing

Options for `compress` and `restore`:
- `--level <n>` - Compression level (1-100, diminishing returns above ~80). `compress` defaults to 80; on `restore` it overrides the level in the recipe.
- `compress --encrypt` - Encrypt the compressed output in the same pass

The pre-2.3.0 flat flags (`--decryptOnly`, `--pack`, `--restore`, `--validate`, `--diff`,
`--compress`, `--encrypt`, and the camelCase modifier spellings) are still accepted. The
task-selecting ones warn and name the command to use. `CliSchema.ExtractArguments` routes
both shapes into the same `CliArguments` DTO, so nothing downstream knows the difference.

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
