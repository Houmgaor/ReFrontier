# FrontierTextTool

FrontierTextTool let's you read, edit and write game texts using a custom CSV format.
Both command generate a "CSV" separated by tabulation (`\t`), and encoded in shift-jis.

> [!WARNING]
> By Houmgaor: if you want to work on text data, I recommend using [FrontierTextHandler](https://github.com/Houmgaor/FrontierTextHandler). It is a simple Python tool that is more reliable than this project with less bloating.

## Automatic File Processing

**New in v1.1.0**: FrontierTextTool now automatically detects and handles encrypted and compressed files. You no longer need to manually decrypt or decompress files before using the tool!

The tool automatically:
- Detects ECD/EXF encryption and decrypts files
- Detects JPK compression and decompresses files
- Creates `.meta` files for re-encryption when needed
- Cleans up temporary files after processing

## Usage

1. Extract data with `./FrontierTextTool mhfdat.bin --fulldump --trueOffsets --nullStrings --close`
2. Edit the generated CSV, for instance `mhfdat.csv`
3. Change the original file with `./FrontierTextTool mhfdat.bin --insert --csv mhfdat.csv --verbose --close`

**Files can be encrypted and/or compressed** - the tool will automatically process them!

This file includes a `--help` command with detailed explanations on each option.

## Extract data

Use `--dump` with `--startIndex` and `--endIndex` if you know which portion of the file to extract.
Otherwise use `--fulldump --trueOffsets --nullStrings`.
This second command will find all text, but will output many unreadable text as a side effect.

**Files can be encrypted and/or compressed** - the tool will automatically process them!

```shell
# Dump specific range (works with encrypted/compressed files!)
./FrontierTextTool mhfdat.bin --dump --startIndex 3040 --endIndex 3328506

# Dump all text (works with encrypted/compressed files!)
./FrontierTextTool mhfdat.bin --fulldump --trueOffsets --nullStrings
```

The result will be a CSV with name pattern `<file>.csv`.

Some recommended offsets:

- mhfdat.bin: 3040 3328506 or 3072 3328538
- mhfpac.bin: 4416 1278736 (conservative) 4416 1278872 (complete)

## Insert back

Use `--insert` with `--csv` to specify the CSV file.
You can add verbosity with `--verbose`.

**Important**: When using `--insert`, make sure you have a `.meta` file for the original encrypted file (e.g., `mhfdat.bin.meta`). This file is automatically created when you extract data from an encrypted file, or you can create it by decrypting the original file with ReFrontier using the `--log` option.

```shell
# Works with encrypted/compressed files directly!
./FrontierTextTool mhfdat.bin --insert --csv mhfdat.csv --verbose
```

The output will be compressed and encrypted automatically to `output/mhfdat.bin`.

## Merge two CSV

Use `--merge` with `--csv` to merge an old CSV with a new one.

```shell
./FrontierTextTool old.csv --merge --csv new.csv
```

## Clean Trados

Use `--cleanTrados` to clean up ill-encoded characters from CAT tools.

```shell
./FrontierTextTool file.txt --cleanTrados
```

## Insert CAT file

Use `--insertCAT` with `--csv` to insert a CAT file into a CSV.

```shell
./FrontierTextTool catfile.txt --insertCAT --csv target.csv
```
