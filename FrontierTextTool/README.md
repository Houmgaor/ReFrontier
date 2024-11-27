# FrontierTextTool

FrontierTextTool let's you read, edit and write game texts using a custom CSV format.
Both command generate a "CSV" separated by tabulation (`\t`), and encoded in shift-jis.

> [!WARNING]
> By Houmgaor: if you want to work on text data, I recommend using [FrontierTextHandler](https://github.com/Houmgaor/FrontierTextHandler). It is a simple Python tool that is more reliable than this project with less bloating.

## Usage

1. Extract data with ``./FrontierTextTool.exe fulldump mhfdat.bin --trueOffsets --nullStrings --close``
2. Edit the generated CSV, for instance ``mhfdat.bin.csv``
3. Change the original file with ``./FrontierTextTool.exe insert mhfdat.bin mhfdat.bin.csv --verbose --close``.

This file includes a "--help" command with detailed explanations on each command.

## Extract data

Use ``dump <file> [start offset] [end offset]`` if you know which portion of the file to extract.
Otherwise use ``fulldump <file> --trueOffsets --nullStrings``.
This second command will find all text, but will output many unreadable text as a side effect.

The result of both command will be a CSV with name pattern "\<file\>.csv".

Some recommended offsets:

- mhfdat.bin: 3040 3328506 or 3072 3328538
- mhfpac.bin: 4416 1278736 (conservative) 4416 1278872 (complete)

## Insert back

Use ``insert <file> <csvFile>``.
You can add some verbosity with ``--verbose``.

## Merge two CSV

Use ``merge <old CSV> <new CSV>``.
