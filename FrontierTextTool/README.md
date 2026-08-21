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
   ./FrontierTextTool dump mhfdat.bin --true-offsets --null-strings
   ```

2. **Edit** the generated `mhfdat.csv` file.

3. **Reinsert** modified text:

   ```shell
   ./FrontierTextTool insert mhfdat.bin mhfdat.csv
   ```

Output is automatically compressed and encrypted to `output/mhfdat.bin`.

## Command Reference

```shell
./FrontierTextTool <command> <file> [options]
```

Every task is a command. `--help` on a command lists only the options that apply to it,
and `./FrontierTextTool --help` lists the commands.

| Command | Description |
|---------|-------------|
| `dump <file>` | Extract strings from a game file to CSV |
| `insert <file> <csv>` | Write the strings of a CSV back into a game file |
| `merge <old-csv> <new-csv>` | Merge an older CSV with a newer one |
| `clean-trados <csv>` | Strip the spacing a CAT tool inserted around Japanese punctuation |
| `insert-cat <cat-file> <csv>` | Fold a CAT tool export back into a CSV |

### `dump`

| Option | Description |
|--------|-------------|
| `--start-index <n>` | First byte to dump. Omit to dump the whole file. |
| `--end-index <n>` | Last byte to dump. Omit to dump the whole file. |
| `--true-offsets` | Correct the value of string offsets |
| `--null-strings` | Check that strings are valid before outputting them |

With no range, `dump` finds all text and filters the garbage that binary data decodes to:

```shell
./FrontierTextTool dump mhfdat.bin --true-offsets --null-strings
```

With a range, it reads only those bytes:

```shell
./FrontierTextTool dump mhfdat.bin --start-index 3040 --end-index 3328506
```

Known good ranges:

- `mhfdat.bin`: 3040 to 3328506
- `mhfpac.bin`: 4416 to 1278736

### `insert`

| Option | Description |
|--------|-------------|
| `--true-offsets` | Correct the value of string offsets |

```shell
./FrontierTextTool insert mhfdat.bin mhfdat.csv --verbose
```

Requires a `.meta` file from the original extraction, which `dump` writes automatically.

### `merge`

Combines an old CSV with a new one, keeping the translations already in the old file:

```shell
./FrontierTextTool merge old.csv new.csv
```

### `clean-trados`

Fixes encoding issues from CAT (Computer-Assisted Translation) tools like SDL Trados:

```shell
./FrontierTextTool clean-trados file.csv
```

### `insert-cat`

Imports translations from a CAT tool export file into your CSV:

```shell
./FrontierTextTool insert-cat catfile.txt target.csv
```

### Options for every command

| Option | Description |
|--------|-------------|
| `--verbose` | Show detailed output |
| `--shift-jis` | Output CSV in Shift-JIS encoding (default: UTF-8 with BOM) |
| `--close` | Return without waiting for a keypress |
| `--help` | Show help |
| `--version` | Show version |

### Deprecated options

The pre-2.3.0 form put every task on one command as a flag. Those flags still work, so
existing scripts keep running, but the ones selecting a task now name the command to use
and will be removed in a future release.

| Option | Use instead |
|--------|-------------|
| `--fulldump` | `dump` |
| `--dump` | `dump --start-index <n> --end-index <n>` |
| `--insert` | `insert` |
| `--merge` | `merge` |
| `--cleanTrados` | `clean-trados` |
| `--insertCAT` | `insert-cat` |
| `--csv <file>` | second positional argument |
| `--startIndex <n>` | `--start-index <n>` |
| `--endIndex <n>` | `--end-index <n>` |
| `--trueOffsets` | `--true-offsets` |
| `--nullStrings` | `--null-strings` |

The renamed options are still accepted under their old spelling by the commands that take
them, so `insert mhfdat.bin --csv mhfdat.csv` works and a script can move over in two steps.

## About CAT Tools and Trados

**CAT** (Computer-Assisted Translation) tools help professional translators work more efficiently by providing translation memory, terminology databases, and text segmentation. **SDL Trados Studio** is one of the most widely used CAT tools in the industry.

When translating Monster Hunter Frontier text, the typical workflow is:

1. Extract game text to CSV using `dump`
2. Import the CSV into a CAT tool for translation
3. Export the translated text from the CAT tool
4. Use `insert-cat` to merge translations back into the CSV
5. Use `insert` to inject the translated CSV into the game file

**Why `clean-trados`?** CAT tools often insert extra spaces after punctuation when segmenting text. This breaks Japanese text formatting since Japanese doesn't use spaces between words. The `clean-trados` command removes erroneous spaces after Japanese punctuation marks like `。！？：．」「）（`.

## CSV Encoding

By default, CSV files are written in **UTF-8 with BOM** for easier editing in Excel and text editors.

When reading CSV files (for `insert`, `merge`, `insert-cat`), the encoding is **auto-detected**:

- Files starting with UTF-8 BOM (`EF BB BF`) are read as UTF-8
- Other files are read as Shift-JIS (legacy format)

Use `--shift-jis` to output CSV files in Shift-JIS encoding for compatibility with older workflows.

> **Note**: When inserting text into game files, strings must be compatible with Shift-JIS encoding. The tool will warn about any characters that cannot be encoded.
