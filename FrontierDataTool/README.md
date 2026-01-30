# FrontierDataTool

FrontierDataTool is a small utility to work on game values.
It has two actions: `--dump` and `--modshop`.

## Action `--dump`

The `--dump` action dumps all data from the main files to CSV files.
It needs all data files: mhfpac.bin, mhfdat.bin, mhfinf.bin.
These files need to be decrypted and decompressed.

```shell
# Replace "demo" by any file suffix you want
./FrontierDataTool --dump --suffix demo --mhfpac mhfpac.bin --mhfdat mhfdat.bin --mhfinf mhfinf.bin
```

The output will look like `mhsx_[data type]_{suffix}.txt`

## Action `--modshop`

Change various prices from the shop by editing "mhfdat.bin".
The buy prices is divided by 50, and the sell price multiplied by 5.

```shell
./FrontierDataTool --modshop --mhfdat mhfdat.bin
```

## Help

Use `--help` for detailed explanations on all options.

```shell
./FrontierDataTool --help
```
