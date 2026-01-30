# FrontierDataTool

FrontierDataTool is a small utility to work on game values.
It has three actions: `--dump`, `--modshop`, and `--import`.

## Automatic File Processing

**New in v1.1.0**: FrontierDataTool now automatically detects and handles encrypted and compressed files. You no longer need to manually decrypt or decompress files before using the tool!

The tool automatically:
- Detects ECD/EXF encryption and decrypts files
- Detects JPK compression and decompresses files
- Creates `.meta` files for re-encryption when needed
- Cleans up temporary files after processing

## Action `--dump`

The `--dump` action dumps all data from the main files to CSV files.
It needs all data files: mhfpac.bin, mhfdat.bin, mhfinf.bin.

**The files can be encrypted and/or compressed** - the tool will automatically process them.

```shell
# Replace "demo" by any file suffix you want
# Works with encrypted/compressed files directly!
./FrontierDataTool --dump --suffix demo --mhfpac mhfpac.bin --mhfdat mhfdat.bin --mhfinf mhfinf.bin
```

The output will look like `mhsx_[data type]_{suffix}.txt` and `Armor.csv`, `Melee.csv`, `Ranged.csv`, `InfQuests.csv`.

## Action `--modshop`

Change various prices from the shop by editing "mhfdat.bin".
The buy prices is divided by 50, and the sell price multiplied by 5.

**The file can be encrypted and/or compressed** - the tool will automatically process it.

```shell
# Works with encrypted/compressed files directly!
./FrontierDataTool --modshop --mhfdat mhfdat.bin
```

## Action `--import`

Import modified armor data from a CSV file back into the game files.

**The game files can be encrypted and/or compressed** - the tool will automatically process them.

```shell
# Import modified armor data
./FrontierDataTool --import --csv Armor.csv --mhfdat mhfdat.bin --mhfpac mhfpac.bin
```

The output will be written to `output/mhfdat.bin`.

## Help

Use `--help` for detailed explanations on all options.

```shell
./FrontierDataTool --help
```
