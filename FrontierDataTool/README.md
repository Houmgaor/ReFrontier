# FrontierDataTool

Extract and modify game data structures (armor, weapons, quests, shop prices).

## Features

- Automatically handles encrypted (ECD/EXF) and compressed (JPK) files
- Exports to CSV in UTF-8 with BOM (easy editing in Excel/text editors)
- Auto-detects CSV encoding when importing (supports both UTF-8 and CP932)
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
| `--english-skills` | Write English skill tree names instead of the game's own |

```shell
./FrontierDataTool dump --suffix demo --mhfpac mhfpac.bin --mhfdat mhfdat.bin --mhfinf mhfinf.bin
```

Generates:

- `Armor.csv` - Armor data
- `Melee.csv` - Melee weapon data
- `Ranged.csv` - Ranged weapon data
- `InfQuests.csv` - Quest data
- `mhsx_[type]_demo.txt` - Various data files

#### English skill names

`--english-skills` replaces each skill tree name with an English one where the tool knows
it, which is what a Japanese client needs to be readable:

```shell
./FrontierDataTool dump --suffix jp --mhfpac mhfpac.bin --mhfdat mhfdat.bin --mhfinf mhfinf.bin --english-skills
# なし 受身 運気 運搬 自動防御  ->  None Passive Fate Backpacking Auto-Guard
```

The table covers 205 of the 232 trees the current client defines; an index it does not know
keeps the game's own string, so nothing is lost. It is worth using even on an English
client, which leaves several names in Japanese (`0x13` 広域回復, `0x30` 肉, `0xC1` 採集の極み)
and spells `0x4A` "Deoderant".

Two skill trees can share an English name — `0x01` and `0x5F` are both "Passive", the
English client's rendering of the distinct 受身 and 受け身 — so the second carries its ID:
`Passive` and `Passive (0x5F)`. This keeps names unique, which is what makes an English dump
safe to edit and import back.

`import` accepts English names whether or not the dump used the flag, since nothing in a CSV
records which spelling produced it. A name matching neither the game's nor the English set
is reported rather than silently written as skill 0.

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
| `--offsets <id\|file>` | Offset profile naming where the data sits. Detected from the files when omitted. |
| `--cp932` | Output CSV in CP932 / Windows-31J encoding (default: UTF-8 with BOM) |
| `--json` | Output JSON files instead of CSV |
| `--close` | Return without waiting for a keypress |
| `--help` | Show help |
| `--version` | Show version |

### Deprecated options

The pre-2.3.0 form put every task on one command as a flag. Those flags still work, so
existing scripts keep running, but the ones selecting a task now name the command to use
and will be removed in a future release.

| Option | Use instead |
|--------|-------------|
| `--dump` | `dump` |
| `--modshop` | `modshop <mhfdat.bin>` |
| `--import` | `import <file.csv>` |
| `--csv <file>` | positional argument of `import` |
| `--shift-jis` | `--cp932` |

The renamed options are still accepted under their old spelling by the commands that take
them, so `import --csv Armor.csv` and `modshop --mhfdat mhfdat.bin` work and a script can
move over in two steps.

## Game versions and offset profiles

The data has no self-describing structure: the tool finds armor, skills and quests at
offsets that differ between game versions. Those offsets live in **offset profiles**, JSON
files under `FrontierDataTool/Offsets/Profiles/`, embedded in the executable.

| Profile | Covers |
|---------|--------|
| `zz` | PC, G10 through ZZ — verified against `pc`, `pc-z-jp` and `pc-zz-en` |

With no `--offsets`, the profile is worked out from the files themselves: each one is tried
and judged on whether its pointers land inside the file and its regions end after they
start. A profile from the wrong version fails that at once. The chosen one is named in the
output:

```console
$ ./FrontierDataTool dump --suffix demo --mhfpac mhfpac.bin --mhfdat mhfdat.bin --mhfinf mhfinf.bin
Offset profile: zz (PC, G10 through ZZ (verified against pc, pc-z-jp and pc-zz-en)).
```

A version with no profile is now named as such, instead of failing part-way through with a
stream error:

```console
Error: No known offset profile matches these files, so they are from a game version this
tool cannot read yet. The closest is 'zz', where 16 of 34 pointers resolve.
mhfDat.armor.stringPointers[0] runs from 0x251EA0 back to 0x1249C0.
```

### Adding a version

See [docs/OFFSET_PROFILES.md](../docs/OFFSET_PROFILES.md) for what is known about each PC
client's layout and the two scripts that find the offsets.

Copy `zz.json`, change the offsets, and pass it with `--offsets my-version.json`. Offsets
are written as hex strings (`"0x6BD40"`) because that is how every other tool in this
ecosystem quotes them; plain numbers are accepted too. The file is checked when it loads —
pointer lists must cover every armor slot, offsets cannot be negative, and quest sections
cannot overlap — so a mistake is reported rather than silently producing garbage.

`--offsets zz` also forces a known profile onto files it was not detected for, which is
useful when adding a version: it shows how far the existing offsets get.

## CSV Encoding

By default, CSV files are written in **UTF-8 with BOM** for easier editing in Excel and text editors.

When reading CSV files (for `import`), the encoding is **auto-detected**:

- Files starting with UTF-8 BOM (`EF BB BF`) are read as UTF-8
- Other files are read as CP932 (legacy format)

Use `--cp932` to output CSV files in CP932 for compatibility with older workflows.

> **Why CP932 and not Shift-JIS?** The game files use **CP932** (Windows-31J), Microsoft's
> extension of Shift_JIS. It adds the NEC and IBM rows and maps a few code points
> differently — `0x8160` is FULLWIDTH TILDE in CP932 and WAVE DASH in JIS X 0208.
> The tools have always read and written codepage 932; only the name was imprecise.
> `--shift-jis` is still accepted as a spelling of `--cp932`.
