# FrontierTextTool

FrontierTextTool let's you read, edit and write game texts using a custom CSV format.
Both command generate a "CSV" separated by tabulation (`\t`), and encoded in shift-jis.

>[!WARNING] By Houmgaor: if you want to work on text data, I recommend using [FrontierTextHandler](https://github.com/Houmgaor/FrontierTextHandler). It is a simple Python tool that is more reliable than this project with less bloating.

## Usage

1. Extract data with ``./FrontierTextTool.exe fulldump mhfdat.bin --trueOffsets --nullStrings --close``
2. Edit the generated CSV, for instance ``mhfdat.bin.csv``
3. Change the original file with ``./FrontierTextTool.exe insert mhfdat.bin mhfdat.bin.csv --verbose --close``.

This file includes a "--help" command with detailed explanations on each command.
