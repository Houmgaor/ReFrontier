# FrontierTextTool

Extract, edit, and reinsert game text using CSV format.

> **Note**: For text editing, consider using [FrontierTextHandler](https://github.com/Houmgaor/FrontierTextHandler), a simpler Python alternative.

## Features

- Automatically handles encrypted (ECD/EXF) and compressed (JPK) files
- Exports to tab-separated CSV in Shift-JIS encoding
- Preserves metadata for re-encryption

## Quick Start

1. **Extract** text to CSV:
   ```shell
   ./FrontierTextTool mhfdat.bin --fulldump --trueOffsets --nullStrings
   ```

2. **Edit** the generated `mhfdat.csv` file.

3. **Reinsert** modified text:
   ```shell
   ./FrontierTextTool mhfdat.bin --insert --csv mhfdat.csv
   ```

Output is automatically compressed and encrypted to `output/mhfdat.bin`.

## Commands

### Extract Text

**Full dump** (finds all text, may include some garbage):
```shell
./FrontierTextTool mhfdat.bin --fulldump --trueOffsets --nullStrings
```

**Range dump** (if you know the byte offsets):
```shell
./FrontierTextTool mhfdat.bin --dump --startIndex 3040 --endIndex 3328506
```

Recommended offsets:
- `mhfdat.bin`: 3040 to 3328506
- `mhfpac.bin`: 4416 to 1278736

### Insert Text

```shell
./FrontierTextTool mhfdat.bin --insert --csv mhfdat.csv --verbose
```

Requires a `.meta` file from the original extraction (created automatically).

### Merge CSV Files

Combine an old CSV with a new one:
```shell
./FrontierTextTool old.csv --merge --csv new.csv
```

### Clean Trados Artifacts

Fix encoding issues from CAT tools:
```shell
./FrontierTextTool file.txt --cleanTrados
```

### Insert CAT File

```shell
./FrontierTextTool catfile.txt --insertCAT --csv target.csv
```

## Options

| Option | Description |
|--------|-------------|
| `--fulldump` | Extract all text from file |
| `--dump` | Extract text in specified range |
| `--startIndex` | Start byte offset for `--dump` |
| `--endIndex` | End byte offset for `--dump` |
| `--trueOffsets` | Use pointer table offsets |
| `--nullStrings` | Include empty strings |
| `--insert` | Insert CSV back into file |
| `--csv <file>` | Specify CSV file path |
| `--verbose` | Show detailed output |
| `--merge` | Merge two CSV files |
| `--cleanTrados` | Fix CAT tool encoding |
| `--insertCAT` | Insert CAT file into CSV |
| `--help` | Show help |
