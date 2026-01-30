# FrontierTextTool

FrontierTextTool let's you read, edit and write game texts using a custom CSV format.
Both command generate a "CSV" separated by tabulation (`\t`), and encoded in shift-jis.

> [!WARNING]
> By Houmgaor: if you want to work on text data, I recommend using [FrontierTextHandler](https://github.com/Houmgaor/FrontierTextHandler). It is a simple Python tool that is more reliable than this project with less bloating.

## Usage

1. Extract data with `./FrontierTextTool mhfdat.bin --fulldump --trueOffsets --nullStrings --close`
2. Edit the generated CSV, for instance `mhfdat.csv`
3. Change the original file with `./FrontierTextTool mhfdat.bin --insert --csv mhfdat.csv --verbose --close`

This file includes a `--help` command with detailed explanations on each option.

## Extract data

Use `--dump` with `--startIndex` and `--endIndex` if you know which portion of the file to extract.
Otherwise use `--fulldump --trueOffsets --nullStrings`.
This second command will find all text, but will output many unreadable text as a side effect.

```shell
# Dump specific range
./FrontierTextTool mhfdat.bin --dump --startIndex 3040 --endIndex 3328506

# Dump all text
./FrontierTextTool mhfdat.bin --fulldump --trueOffsets --nullStrings
```

The result will be a CSV with name pattern `<file>.csv`.

Some recommended offsets:

- mhfdat.bin: 3040 3328506 or 3072 3328538
- mhfpac.bin: 4416 1278736 (conservative) 4416 1278872 (complete)

## Insert back

Use `--insert` with `--csv` to specify the CSV file.
You can add verbosity with `--verbose`.

```shell
./FrontierTextTool mhfdat.bin --insert --csv mhfdat.csv --verbose
```

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
