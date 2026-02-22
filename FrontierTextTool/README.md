# FrontierTextTool

Extract, edit, and reinsert game text using CSV format.

> **Note**: For text editing, consider using [FrontierTextHandler](https://github.com/Houmgaor/FrontierTextHandler), a simpler Python alternative.

## Features

- Automatically handles encrypted (ECD/EXF) and compressed (JPK) files
- Exports to CSV in UTF-8 with BOM (easy editing in Excel/text editors)
- Auto-detects CSV encoding when reading (supports both UTF-8 and Shift-JIS)
- Validates Shift-JIS compatibility when inserting text into game files
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

**Full dump** (finds all text, automatically filters garbage from binary data):
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

Fix encoding issues from CAT (Computer-Assisted Translation) tools like SDL Trados:
```shell
./FrontierTextTool file.csv --cleanTrados
```

### Insert CAT File

Import translations from a CAT tool export file into your CSV:
```shell
./FrontierTextTool catfile.txt --insertCAT --csv target.csv
```

## About CAT Tools and Trados

**CAT** (Computer-Assisted Translation) tools help professional translators work more efficiently by providing translation memory, terminology databases, and text segmentation. **SDL Trados Studio** is one of the most widely used CAT tools in the industry.

When translating Monster Hunter Frontier text, the typical workflow is:
1. Extract game text to CSV using `--fulldump`
2. Import the CSV into a CAT tool for translation
3. Export the translated text from the CAT tool
4. Use `--insertCAT` to merge translations back into the CSV
5. Use `--insert` to inject the translated CSV into the game file

**Why `--cleanTrados`?** CAT tools often insert extra spaces after punctuation when segmenting text. This breaks Japanese text formatting since Japanese doesn't use spaces between words. The `--cleanTrados` command removes erroneous spaces after Japanese punctuation marks like `。！？：．」「）（`.

## Command Reference

```shell
./FrontierTextTool <file> [options]
```

The `<file>` argument type depends on the action:
- **Binary file** for `--fulldump`, `--dump`, `--insert` (e.g., `mhfdat.bin`)
- **CSV file** for `--merge`, `--cleanTrados` (e.g., `old.csv`)
- **CAT export file** for `--insertCAT` (e.g., `translations.txt`)

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
| `--shift-jis` | Output CSV in Shift-JIS encoding (default: UTF-8 with BOM) |
| `--help` | Show help |

## CSV Encoding

By default, CSV files are written in **UTF-8 with BOM** for easier editing in Excel and text editors.

When reading CSV files (for `--insert`, `--merge`, `--insertCAT`), the encoding is **auto-detected**:
- Files starting with UTF-8 BOM (`EF BB BF`) are read as UTF-8
- Other files are read as Shift-JIS (legacy format)

Use `--shift-jis` to output CSV files in Shift-JIS encoding for compatibility with older workflows.

> **Note**: When inserting text into game files, strings must be compatible with Shift-JIS encoding. The tool will warn about any characters that cannot be encoded.
