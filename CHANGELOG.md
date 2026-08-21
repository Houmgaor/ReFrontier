# Changelog

All notable changes to ReFrontier will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

- **Build**: The assembly version lives in `Directory.Build.props` alone. The four shipped
  projects each carried their own `<Version>`/`<FileVersion>` pair, eight lines that had to
  be edited in lockstep at every release; they now inherit one definition. `<Nullable>` was
  likewise declared in the props *and* re-declared in all five projects, and is now set once.
- **Build**: Implicit usings are off everywhere, tests included. `ReFrontier.Tests` was the
  one project that enabled them, so a file meant different things depending on which side of
  the solution it sat on; 49 test files gained the usings they had been getting for free.
- **Build**: XML documentation is still generated, for the IDE and for IDE0005, but no longer
  published. Nothing reads it at runtime and it was ~280 KB of every release zip
  (`ReFrontier.xml` alone was 133 KB).
- **FrontierTextTool, FrontierDataTool**: The CSV output flag is `--cp932`. The tools read
  and write **CP932** (Windows-31J), Microsoft's extension of Shift_JIS: it adds the NEC and
  IBM rows and maps some code points differently, notably `0x8160`, which is FULLWIDTH TILDE
  in CP932 and WAVE DASH in JIS X 0208. .NET resolves `"shift-jis"` to codepage 932, so the
  behaviour was always CP932 and only the name claimed otherwise. `--shift-jis` is still
  accepted, hidden from help and silent, since it selects the same encoding it always did.
- **LibReFrontier**: `TextFileConfiguration.ShiftJisEncoding` is now `Cp932Encoding` and
  pins codepage 932 by number rather than resolving an alias string. `UseShiftJisOutput`,
  `CsvEncodingOptions.ShiftJis` and `ValidateShiftJisCompatibility` are renamed to match.
  Verified as a pure rename: a full text dump and a game-file write-back are byte-identical
  to the previous build under both flag spellings.

### Fixed

- **FrontierDataTool**: Quest data reads correctly. Every offset in
  `MhfDataOffsets.MhfInf.QuestSections` was `0x20` too high, which is not a multiple of the
  `0x160` entry size, so every read began in the middle of an entry: the four string
  pointers at `entry + 0x140` came out as unrelated small integers, and the first one that
  happened to fall outside the file ended the dump with `Unable to read beyond the end of
  the stream`. `dump` had never produced usable quest data on the current PC client — it
  wrote garbage until it crashed. Each section moved down by `0x20`; all 1092 quests now
  read with their titles, goal types, map IDs and distinct quest IDs, and dump → import →
  dump returns identical titles.
- **FrontierDataTool**: A string pointer outside the file now says so, naming the pointer,
  where it was read and the file size, instead of surfacing as a stream error from the read
  that followed the seek. A pointer landing exactly at the end still reads as the empty
  string, as it always did.

## [2.3.0] - 2026-08-21

### Added

- **ReFrontier**: Every task is now a command: `unpack`, `decrypt`, `pack`, `restore`,
  `compress`, `encrypt`, `validate` and `diff`. The 20 options were a flat list in which
  seven selected a task and the rest modified one particular task, with nothing to say
  which was which, so `--help` presented twenty equal peers and combinations like
  `--validate --stageContainer` parsed happily and ignored half of what was asked. Each
  command now carries only the options that apply to it: `ReFrontier --help` lists eight
  commands, and `ReFrontier unpack --help` lists six options instead of twenty.
  `ReFrontier <file>` with no command still unpacks.
- **ReFrontier**: Options are named in kebab-case: `--flat`, `--keep-compressed`,
  `--keep-encrypted`, `--stage`, `--auto-stage`, `--clean`, `--no-meta`. The four previous
  spellings for "don't" (`--nonRecursive`, `--noDecryption`, `--noMeta`, `--ignoreJPK`)
  were hard to guess, and `--decryptOnly` versus `--noDecryption` differed in a way that
  could not be read off the names. The old spellings are still accepted, hidden from help,
  so a script can adopt the commands without renaming every option on the same line.
- **ReFrontier**: `compress` defaults to level 80 rather than rejecting a missing `--level`.
- **ReFrontier**: Extraction recipes carry the encryption header themselves (`Header`, Base64,
  recipe version 2). A recipe is now self-contained: it can be moved or renamed without its
  `.meta` file and still rebuild the original byte for byte, including the ECD header fields
  that the default key cannot reproduce.
- **ReFrontier**: `FileProcessingService` gained overloads that take the encryption header as
  bytes rather than a file path (`EncryptEcdFile`, `EncryptExfFile`), and that report the
  header when decrypting (`DecryptEcdFile`, `DecryptExfFile`).


- **ReFrontier**: Extraction now records how a file was taken apart in a
  `<original file>.recipe.json` next to it, written alongside the `.meta` file when
  extracting with `--saveMeta`. The recipe lists each encryption and compression layer
  that was undone, outermost first.
- **ReFrontier**: Container archives are recorded too. A file that unpacks into a directory
  (simple archive, MOMO, MHA, stage container) gets a `Container` layer naming that
  directory, and `--restore` rebuilds every entry that was itself unpacked, packs the
  directory back through its log file, and applies whatever compression and encryption sat
  above it. Nesting is followed to the bottom and rebuilt depth first, so a model file that
  is an encrypted archive of archives of compressed streams comes back in one command.
  `--restore` accepts the original file name or the unpacked directory.
- **ReFrontier**: New `--restore` option rebuilds a file by reversing its recipe, so
  repacking no longer requires re-specifying `--compress`, `--level` and `--encrypt`.
  Previously the JPK compression type was read from the JKR header during extraction and
  then discarded, leaving the user to guess it when repacking; guessing wrong produced a
  file the game rejects, with no error. `--restore` accepts either the original file name
  or the extracted one, and refuses to run on a file that is still packed.
- **FrontierTextTool**: Every task is now a command: `dump`, `insert`, `merge`,
  `clean-trados` and `insert-cat`. The tool took one file and six boolean flags, of which
  exactly one had to be set and any second one was an error found at run time, and the
  meaning of that file changed with the flag chosen. Each command now names its files
  after what they hold, so `merge <old-csv> <new-csv>` says which way round the two go,
  and `--help` on a command lists only the options that apply to it. `--fulldump` and
  `--dump` became one `dump`, with the range optional.
- **FrontierDataTool**: Every task is now a command: `dump`, `modshop` and `import`. The
  file each one acts on is its argument, so `modshop mhfdat.bin` and `import Armor.csv`
  replace `--modshop --mhfdat mhfdat.bin` and `--import --csv Armor.csv`. The root help
  listed nine options that only meant something in combination; it now lists three
  commands.
- **FrontierTextTool**: Options are named in kebab-case: `--start-index`, `--end-index`,
  `--true-offsets`, `--null-strings`, matching `--shift-jis` which already was.

### Changed

- The task-selecting flags are deprecated in favour of the commands and warn with the
  command to use: `--decryptOnly`, `--pack`, `--restore`, `--validate`, `--diff`,
  `--compress` and `--encrypt`. They still work, so existing scripts keep running, and
  will be removed in a future major release. The bare form `ReFrontier <file>` is not
  deprecated.
- `.meta` files are still written and still read. `--encrypt`, FrontierTextTool and older
  versions of ReFrontier all use them, and a version 2 recipe read by an older build falls
  back to the `.meta` file and rebuilds correctly. Version 1 recipes are read unchanged.
- Metadata is written by default. `.meta`, `.log` and `.recipe.json` were only
  produced with `--saveMeta`, so the common case of extracting a file and then trying to
  rebuild it failed, and on a bare JPK file the original was deleted with nothing recording
  how it had been compressed. `--saveMeta` is still accepted and now warns; `--noMeta`
  disables the metadata for anyone who wants a clean directory.
- Decompressing a file no longer deletes it. `UnpackJPK` removed its input
  unconditionally, which destroyed the user's own file when they decompressed one directly.
  Files supplied by the user are now kept unless `--cleanUp` is passed; intermediates
  ReFrontier itself produces are still removed, so the extracted layout is unchanged.
- A file that turns out not to be a container is reported as skipped rather than as an
  error. The fallback handler accepts any file and finds out by trying, so failing is an
  ordinary outcome; counting it as an error made a second run over an already extracted
  folder report failures that meant nothing.
- Directory scans skip payloads a previous run extracted, so re-running over the same
  folder no longer retries them.
- **FrontierTextTool, FrontierDataTool**: The old flat flags are still accepted, so
  existing scripts keep running. The ones that select a task warn once and name the
  command to use; the renamed options are accepted silently and hidden from help, so a
  script can move over in two steps.
- **LibReFrontier**: `LibReFrontier.CLI.CliDeprecation` holds the deprecation notice and
  the verb/path ambiguity notice the three tools share, so they word them the same way.

### Fixed

- **ReFrontier**: HFI and HFIRW compression is reproducible. The Huffman leaf permutation
  was shuffled with an unseeded `Random` on every call, so compressing the same file twice
  produced two different files and output could not be compared, cached or checksummed.
  The seed is now fixed. Compression ratio is unaffected — the permutation is arbitrary,
  since every code is 8 bits long whatever the order — and the table is pinned by a test so
  the multi-OS CI catches any platform difference in seeded `Random` or in `OrderBy`.
  See the issue on HFI's Huffman stage not actually compressing.
- **ReFrontier**: Compressing and encrypting in one pass works. `JPKEncode` writes to the
  output directory, but the encryption step then looked for its input beside the input
  file, so the documented one-step form failed with `Could not find file` every time and
  only the compressed intermediate was produced. It now encrypts what compression just
  wrote. The result validates through both layers and round-trips to the original payload.
- **ReFrontier**: Encrypting on its own reports which file it acts on. The path is derived
  by dropping the last extension, so `encrypt mhfjmp.bin.decd` encrypts `mhfjmp.bin` — the
  original, already encrypted file — and reported nothing but `Done.`. The derivation is
  unchanged, but it is no longer silent. Prefer `compress --encrypt` or `restore`, neither
  of which depends on that layout.
- **Documentation**: The README described repacking FTXT text with
  `./ReFrontier text.ftxt.txt --pack`, which never worked: `--pack` takes a directory and
  ReFrontier has no FTXT writer at all. Writing text back is FrontierTextTool's job, and
  the README now says so.
- **ReFrontier**: MHA archives are repacked with their entry padding intact. `PackMHA` laid
  entries out consecutively and wrote the entry size as the padded size, so a repacked
  archive was smaller than the original and every entry offset moved. Entry padding is a
  multiple of 512 and always strictly greater than the entry, but the exact amount is not
  derivable from the entry size: 1,140 of the 26,048 entries in the client's archives
  reserve more than the next boundary. Unpacking now records each entry's padded size in the
  log as a third column, and packing reuses it, falling back to the next boundary past the
  data for entries that grew or for logs written before the column existed. All 81 MHA
  archives shipped with the PC client now unpack and repack byte for byte.

- **ReFrontier**: MOMO archives can be unpacked. `UnpackSimpleArchive` read the entry count
  at the stream's position, which for MOMO is the magic, so every one of the 615 MOMO
  archives in the PC client's `dat/sound` directory failed with "Not a valid simple
  container (invalid size or entry count)". The count sits immediately before the entry
  table in both archive shapes: at offset 0 for a headerless archive, after the magic for
  MOMO.
- **ReFrontier**: MOMO archives are repacked as MOMO rather than as headerless archives.
  Unpacking records `MOMO` as the container type, and packing writes the magic, the count
  and 64-byte aligned entry data. All 615 shipped archives unpack and repack byte for byte.
  The `Unpack` facade takes the container type as an optional argument for the same reason;
  callers that omit it keep unpacking headerless archives as before.
- **ReFrontier**: `--validate` read the entry count and entry table one field too far along
  for both archive shapes, reporting every MOMO archive as invalid and headerless archives
  as an unrecognised format. In the PC client's `dat` directory this moves 615 files from
  invalid to valid and 30 from unrecognised to valid.
- **ReFrontier**: `--validate` accepts an empty MHA archive. Six files in `dat/extend` are
  well-formed archives whose 24-byte header is the whole file, with a zero entry count and
  both pointers at its end; they were reported as invalid.

- **ReFrontier**: `--pack` no longer leaves a truncated archive at the output path when it
  cannot complete. Entries named by the log are now all checked before anything is written,
  and packing goes through a temporary file that is only promoted once it has fully
  succeeded, so a failed pack leaves any earlier output untouched.
- **ReFrontier**: `--pack` now explains why an entry is missing instead of reporting a bare
  "Could not find file". Unpacking is recursive by default, which replaces a nested
  `entry.jkr` with `entry.jkr.bin` while the log keeps naming the original; the error now
  lists every missing entry at once, says what each one became, and names the two ways out
  (rebuild the entry, or extract with `--nonRecursive`).
- **ReFrontier**: `--pack` reports a truncated or corrupt log file rather than failing with
  an index error.

### Notes

- Compression level is still not recoverable from a game file (it is an encoder-side
  parameter absent from the JKR header). `--restore` defaults to level 80 and accepts
  `--level` as an override; this affects output size only, not correctness.
- `--pack` remains available for repacking a directory on its own; it requires the entries
  to still be in the form the log names, which means extracting with `--nonRecursive`.
  `--restore` is the equivalent for a normal recursive extraction.
- Restoring a container rebuilds its entries in place, under the names the log uses, so the
  unpacked directory is modified. Rebuilding is idempotent.

## [2.2.0] - 2026-02-23

### Added

- **ReFrontier**: New `--validate` option checks a file's integrity without writing any
  output, walking every encryption, compression and archive layer (ECD, EXF, JPK, MOMO,
  MHA, FTXT) and verifying CRC32, declared sizes and bounds.
- **ReFrontier**: New `--diff` option compares two game files structurally, through their
  encryption and compression layers, rather than as opaque bytes.
- **FrontierDataTool**: Quest text can be reimported, and extracted data can be written as
  JSON in addition to CSV.

### Notes

- This section was reconstructed after the fact: the 2.2.0 release bumped the project
  version but did not move its entries out of `[Unreleased]`, so these changes shipped
  undocumented.

## [2.1.0] - 2026-02-22

### Added

- **ReFrontier**: Wait for keypress before closing when launched via drag-and-drop on Windows (allows viewing output before window closes)
- **ReFrontier**: Added deprecated `--file` option for backward compatibility with scripts using the old argument style

### Fixed

- **FrontierTextTool**: `--fulldump` now filters out garbage strings (empty strings, binary data decoded as control characters or private-use Unicode), reducing output by ~89% on typical game files

### Changed

- **ReFrontier**: Renamed positional argument from `file` to `inputPath` for clarity (accepts both files and directories)
- **FrontierTextTool**: Improved file argument description to clarify context-dependent behavior (binary file for dump/insert, CSV for merge/cleanTrados, CAT file for insertCAT)

### Documentation

- **FrontierTextTool**: Added "About CAT Tools and Trados" section explaining Computer-Assisted Translation tools and the translation workflow
- **ReFrontier**: Updated command reference to use `<inputPath>` and added deprecated options section
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

[2.1.0]: https://github.com/Houmgaor/ReFrontier/compare/v2.0.0...v2.1.0
[2.0.0]: https://github.com/Houmgaor/ReFrontier/compare/v1.2.0...v2.0.0
[1.2.0]: https://github.com/Houmgaor/ReFrontier/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/Houmgaor/ReFrontier/compare/v1.0.1...v1.1.0
[1.0.1]: https://github.com/Houmgaor/ReFrontier/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/Houmgaor/ReFrontier/releases/tag/v1.0.0
