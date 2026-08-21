# FrontierDataTool

Extract and modify game data structures (armor, weapons, quests, shop prices).

## Features

- Automatically handles encrypted (ECD/EXF) and compressed (JPK) files
- Exports to CSV in UTF-8 with BOM (easy editing in Excel/text editors)
- Auto-detects CSV encoding when importing (supports both UTF-8 and Shift-JIS)
- Supports importing modified data back into game files

## Command Reference

```shell
./FrontierDataTool <command> [options]
```

Every task is a command. `--help` on a command lists only the options that apply to it,
and `./FrontierDataTool --help` lists the commands.

| Command | Description |
|---------|-------------|
| `dump` | Extract weapon, armor, skill and quest data |
| `modshop <mhfdat.bin>` | Rewrite shop prices in `mhfdat.bin` |
| `import <file.csv>` | Write an edited CSV back into the game files |

### `dump`

| Option | Description |
|--------|-------------|
| `--suffix <name>` | Suffix for the names of the files written. Required. |
| `--mhfpac <file>` | Path to `mhfpac.bin`. Required. |
| `--mhfdat <file>` | Path to `mhfdat.bin`. Required. |
| `--mhfinf <file>` | Path to `mhfinf.bin`. Required. |
| `--rengoku <file>` | Path to `rengoku_data.bin` (Hunting Road data) |

```shell
./FrontierDataTool dump --suffix demo --mhfpac mhfpac.bin --mhfdat mhfdat.bin --mhfinf mhfinf.bin
```

Generates:

- `Armor.csv` - Armor data
- `Melee.csv` - Melee weapon data
- `Ranged.csv` - Ranged weapon data
- `InfQuests.csv` - Quest data
- `mhsx_[type]_demo.txt` - Various data files

`--rengoku` dumps the Hunting Road data and needs none of the other files, so it can be
given on its own:

```shell
./FrontierDataTool dump --rengoku rengoku_data.bin
```

### `import`

| Option | Description |
|--------|-------------|
| `--mhfdat <file>` | Path to `mhfdat.bin` |
| `--mhfpac <file>` | Path to `mhfpac.bin` |
| `--mhfinf <file>` | Path to `mhfinf.bin` |
| `--rengoku <file>` | Path to `rengoku_data.bin` |

The importer is selected by the CSV's **filename**, and each one needs different files:

| CSV file | Required options |
|----------|------------------|
| `Armor.csv` | `--mhfdat`, `--mhfpac` |
| `Melee.csv` | `--mhfdat` |
| `Ranged.csv` | `--mhfdat` |
| `InfQuests.csv` | `--mhfinf` |
| `RengokuFloors.csv`, `RengokuSpawns.csv` | `--rengoku` |

```shell
./FrontierDataTool import Armor.csv --mhfdat mhfdat.bin --mhfpac mhfpac.bin
./FrontierDataTool import Melee.csv --mhfdat mhfdat.bin
./FrontierDataTool import Ranged.csv --mhfdat mhfdat.bin
./FrontierDataTool import InfQuests.csv --mhfinf mhfinf.bin
```

> **Note**: Quest text fields (Title, TextMain, TextSubA, TextSubB) are **read-only** and cannot be modified through CSV import.

Output is written to the `output/` directory.

### `modshop`

Adjusts shop prices in `mhfdat.bin` (buy price / 50, sell price * 5):

```shell
./FrontierDataTool modshop mhfdat.bin
```

### Options for every command

| Option | Description |
|--------|-------------|
| `--shift-jis` | Output CSV in Shift-JIS encoding (default: UTF-8 with BOM) |
| `--json` | Output JSON files instead of CSV |
| `--close` | Return without waiting for a keypress |
| `--help` | Show help |
| `--version` | Show version |

### Deprecated options

The pre-2.4.0 form put every task on one command as a flag. Those flags still work, so
existing scripts keep running, but the ones selecting a task now name the command to use
and will be removed in a future release.

| Option | Use instead |
|--------|-------------|
| `--dump` | `dump` |
| `--modshop` | `modshop <mhfdat.bin>` |
| `--import` | `import <file.csv>` |
| `--csv <file>` | positional argument of `import` |

The renamed options are still accepted under their old spelling by the commands that take
them, so `import --csv Armor.csv` and `modshop --mhfdat mhfdat.bin` work and a script can
move over in two steps.

## CSV Encoding

By default, CSV files are written in **UTF-8 with BOM** for easier editing in Excel and text editors.

When reading CSV files (for `import`), the encoding is **auto-detected**:

- Files starting with UTF-8 BOM (`EF BB BF`) are read as UTF-8
- Other files are read as Shift-JIS (legacy format)

Use `--shift-jis` to output CSV files in Shift-JIS encoding for compatibility with older workflows.
