# FrontierDataTool

Extract and modify game data structures (armor, weapons, quests, shop prices).

## Features

- Automatically handles encrypted (ECD/EXF) and compressed (JPK) files
- Exports to tab-separated CSV (Shift-JIS encoding)
- Supports importing modified data back into game files

## Commands

### Dump Data (`--dump`)

Export all game data to CSV files:

```shell
./FrontierDataTool --dump --suffix demo --mhfpac mhfpac.bin --mhfdat mhfdat.bin --mhfinf mhfinf.bin
```

Generates:
- `Armor.csv` - Armor data
- `Melee.csv` - Melee weapon data
- `Ranged.csv` - Ranged weapon data
- `InfQuests.csv` - Quest data
- `mhsx_[type]_demo.txt` - Various data files

### Import Data (`--import`)

Import modified CSV data back into game files. The CSV type is **auto-detected from the filename**.

#### Import Armor

```shell
./FrontierDataTool --import --csv Armor.csv --mhfdat mhfdat.bin --mhfpac mhfpac.bin
```

#### Import Melee Weapons

```shell
./FrontierDataTool --import --csv Melee.csv --mhfdat mhfdat.bin
```

#### Import Ranged Weapons

```shell
./FrontierDataTool --import --csv Ranged.csv --mhfdat mhfdat.bin
```

#### Import Quest Data

```shell
./FrontierDataTool --import --csv InfQuests.csv --mhfinf mhfinf.bin
```

> **Note**: Quest text fields (Title, TextMain, TextSubA, TextSubB) are **read-only** and cannot be modified through CSV import.

Output is written to the `output/` directory.

### Modify Shop Prices (`--modshop`)

Adjust shop prices in `mhfdat.bin` (buy price / 50, sell price * 5):

```shell
./FrontierDataTool --modshop --mhfdat mhfdat.bin
```

## Import Requirements Summary

| CSV File | Required Files |
|----------|----------------|
| `Armor.csv` | `--mhfdat`, `--mhfpac` |
| `Melee.csv` | `--mhfdat` |
| `Ranged.csv` | `--mhfdat` |
| `InfQuests.csv` | `--mhfinf` |

## Options

| Option | Description |
|--------|-------------|
| `--dump` | Export game data to CSV |
| `--import` | Import CSV data back into game files |
| `--modshop` | Modify shop prices |
| `--suffix <name>` | Suffix for output files (required for `--dump`) |
| `--csv <file>` | CSV file path (required for `--import`) |
| `--mhfdat <file>` | Path to mhfdat.bin |
| `--mhfpac <file>` | Path to mhfpac.bin |
| `--mhfinf <file>` | Path to mhfinf.bin |
| `--help` | Show help |
