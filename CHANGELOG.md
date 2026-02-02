# Changelog

All notable changes to ReFrontier will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

- **FrontierDataTool**: Identified QuestData fields from mhfinf.bin (based on ImHex patterns):
  - `Unk11` → `MaxPlayers` (max player count)
  - `Unk12` → `MapId` (map/location ID)
  - `Unk13-16` → `QuestStringPtr` (pointer to quest text)
  - `Unk17-18` → `QuestRestrictions` (quest restriction flags)
  - `Unk19-20` → `QuestId` (quest identifier)
- **FrontierDataTool**: Added missing QuestTypes enum values: `SlayAll`, `SlayTotal`, `BreakPart`, `SlayOrDamage`, `EsotericAction`
- **FrontierDataTool**: Fixed typos in QuestTypes enum: `Kill` → `Slay`, `Damging` → `Damaging`
- **FrontierDataTool**: Added documentation for weapon unknown fields (based on Wii U symbol analysis):
  - MeleeWeaponEntry: `Unk11-14` likely weapon-specific (HH notes, GL shells), `Unk16` evolution-related
  - RangedWeaponEntry: `Unk10` bullet level data, `Unk23` gun type/level, various ammo configuration blocks
- **FrontierDataTool**: Renamed armor fields based on Wii U symbol analysis:
  - `Unk10` → `EqType` (equipment type: General/SP/Gou/Evolution/HC/Ravi)
  - `Unk40` → `ArmorType` (armor tier: zenith/prayer/g-rank/exotic/gou)
- **FrontierDataTool**: Added documentation for remaining armor unknown fields

## [2.0.0] - 2026-02-01

### Added

- **UTF-8 CSV encoding**: CSV files are now written in UTF-8 with BOM by default for easier editing in Excel and text editors
- **CSV encoding auto-detection**: When reading CSV files, encoding is automatically detected (UTF-8 BOM or Shift-JIS)
- **Shift-JIS validation**: Warns when inserting text containing characters that cannot be encoded to Shift-JIS
- `--shift-jis` option for FrontierTextTool and FrontierDataTool to output CSV files in Shift-JIS encoding
- **EXF encryption**: Full round-trip support for EXF encrypted files (decrypt with `--saveMeta`, re-encrypt with `--encrypt`)
- **FTXT repacking**: Pack extracted text files back to FTXT binary format (extract with `--saveMeta`, repack with `--pack`)
- **Auto decrypt/decompress**: FrontierTextTool and FrontierDataTool now automatically detect and process encrypted (ECD/EXF) and compressed (JPK) files
- `FilePreprocessor` class for automatic file preprocessing with cleanup support
- Unit tests for Crypto, JPK compression codecs, ArgumentsParser, ByteOperations, and FileOperations
- GitHub Actions CI workflow for build and test with code coverage
- Dependency injection support for testability (`IFileSystem`, `ILogger`, `ICodecFactory`)
- Service layer (`FileProcessingService`, `PackingService`, `UnpackingService`)
- Support for `None` decoder in JPK decompression

### Changed

- **BREAKING**: Replaced custom argument parser with System.CommandLine library
- **BREAKING**: Removed static methods and Lazy singletons in favor of instance methods
- **BREAKING**: Removed `--noFileRewrite` CLI option and `rewriteOldFile` parameter - decrypted/decompressed files are now always written to new files (e.g., `.decd` suffix) instead of overwriting originals
- FrontierTextTool and FrontierDataTool no longer require manual decryption/decompression of input files
- Refactored `InputArguments` to a struct for easier debugging
- Made `Program` class and methods public for testing

### Fixed

- EXF decryption now processes all bytes (was stopping 16 bytes early)
- Removed dead nullspace fill loop in Crypto.cs
- Improved exception types throughout the codebase
- Added proper `using` statements for streams and readers to prevent resource leaks
- Upgraded CsvHelper in FrontierTextTool to match FrontierDataTool version

### Notes

- See [MIGRATION_2.0.md](MIGRATION_2.0.md) for detailed upgrade instructions

## [1.2.0] - 2024-11-27

### Added

- Parallel processing for depacking with configurable thread count
- Option to avoid rewriting encrypted files
- Option to delete intermediary files on encryption (`--cleanUp`)
- CRC32 hasher now uses .NET standard library (`System.IO.Hashing`)

### Changed

- Removed unused batch files and progress bar code
- Improved code structure for packing/depacking operations
- Better method naming and documentation

### Fixed

- Files with same name now unpack correctly (#5)
- File not entirely depacked in some cases
- Wrong path name for file recompression
- Crash on bad file containers
- Thread-safe methods for concurrent operations
- Replaced removed hashing library dependency
- Rewrite old file by default for backward compatibility

## [1.1.0] - 2024-11-20

### Added

- Version display in CLI (`--version`)
- Compress and encrypt in one command
- Contributions from Chakratos on FrontierTextTool
- Updated CsvHelper to 33.0.1 for better CSV parsing
- Standard CLI for FrontierTextTool

### Changed

- Renamed namespaces to start with uppercase
- Split `Helpers.cs` into smaller focused files (`FileOperations.cs`, etc.)
- Split `FDataTool/Structs.cs` into smaller files
- More strict compression format handling
- Merged Chakratos fork with improved text handling

### Fixed

- Compression level was wrong in FrontierTextTool
- CSV header `eString` renamed to `EString`
- Removed garbage from fulldump output
- Better handling of CSV files writing
- Decompression issues

## [1.0.1] - 2024-11-17

### Fixed

- JPKLZ decompression bugs
- State length limits in compression
- Shift-JIS encoding support (now properly enabled)

## [1.0.0] - 2024-11-05

This is the first release of the Houmgaor fork, modernizing the original mhvuze/ReFrontier.

### Added

- Cross-platform support (Linux, macOS, Windows)
- GitHub Actions workflow for automated releases
- Comprehensive documentation

### Changed

- Upgraded to .NET 8.0
- Standardized CLI arguments
- Removed SSH/FTP dependency code
- Removed memory-unsafe code
- Complete code cleanup and linting

### Fixed

- Compilation in release mode
- Bug in compression data
- Various code quality issues

## Pre-fork History (mhvuze/ReFrontier)

### 2022

- Added batch processing to create MHFUP info
- Added `-trueoffsets` option for string dump via pointer table
- Improved handling of empty entries in pointer tables
- File boundary checking

### 2021

- Added repacking support for stage containers
- Seek for null entries in stage containers

### 2020

- Added MHA repacking and `-noDecryption` flag
- Added support for stage-specific containers
- Added `-nonRecursive` option
- Added JPK type 3+4 compression support

### 2019

- Initial release
- ECD/EXF encryption/decryption
- JPK compression/decompression
- Archive unpacking (MOMO, MHA, JKR formats)
- FrontierTextTool for text extraction
- FrontierDataTool for data structure extraction

[2.0.0]: https://github.com/Houmgaor/ReFrontier/compare/v1.2.0...v2.0.0
[1.2.0]: https://github.com/Houmgaor/ReFrontier/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/Houmgaor/ReFrontier/compare/v1.0.1...v1.1.0
[1.0.1]: https://github.com/Houmgaor/ReFrontier/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/Houmgaor/ReFrontier/releases/tag/v1.0.0
