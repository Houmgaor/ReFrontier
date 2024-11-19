# FrontierDataTool

FrontierDataTool is a small utility to work on game values.
It has two commands: `dump` and `modshop`.

## Command `dump`

The `dump` command dumps all data from the main files to a single CSV.
It needs all data files: mhfpac.bin, mhfdat.bin, mhfinf.bin.
These files need to be decrypted and decompressed.

```shell
# Replace "demo" by any file suffix you want
./FrontierDataTool.exe dump demo mhfpac.bin mhfdat.bin mhinf.bin
```

The output will look like "mhsx_[data type]_{suffix}.txt"

## Command `modshop`

Change various prices from the shop by editing "mhfdat.bin".
The buy prices is divided by 50, and the sell price multiplied by 5.

```shell
./FrontierDataTool.exe modshop mhfdat.bin
```
