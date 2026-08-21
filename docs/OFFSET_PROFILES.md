# Offset profiles

FrontierDataTool finds armour, weapons, items, skills and quests at offsets that differ
between game versions. Those offsets are data: JSON files under
`FrontierDataTool/Offsets/Profiles/`, embedded in the executable, selected with
`--offsets <id|file.json>` or detected from the files.

This page records what is known about the layouts, and how it was found, so that adding a
version is a matter of running two scripts rather than starting over.

## Shipped profiles

| Profile | Covers | Verified against |
|---------|--------|------------------|
| `zz` | PC, G10 through ZZ | `pc`, `pc-z-jp`, `pc-zz-en` |

## Survey of the PC clients

Every client in `mhfrontier/client/`, with `mhfdat.bin`, `mhfpac.bin` and `mhfinf.bin`
decrypted and decompressed first (`./ReFrontier unpack <file> --flat`).

| Client | Version byte | Armour chain | Skill-tree pointer | Quest entry | Quest sections found |
|--------|:------------:|--------------|:------------------:|:-----------:|---------------------|
| `pc`, `pc-z-jp`, `pc-zz-en` | `0x59` | descending | `0xA20` | `0x160` | 38 sections, 2822 quests |
| `pc-gg` | `0x59` | ascending | `0x994` | `0xC0` | 31 sections, 634 quests |
| `pc-g2` | `0x58` | ascending | `0x930` | `0xC0` | 26 sections, 582 quests |
| `pc-f4` | `0x55` | ascending | `0x8E8` | `0xA8` | 15 sections, 325 quests |
| `pc-f5` | `0x55` | ascending | `0x8EC` | `0xA8` | 16 sections, 336 quests |
| `pc-s6` | `0x48` | descending, split | `0x7B8` | unknown | none at any stride tried |

### The version byte is not enough to tell versions apart

`mhfdat.bin` opens with the magic `mhf\x1A` and a version byte at `0x04`. It is tempting
to detect the layout from it, but `pc` and `pc-gg` both carry `0x59` and their armour
pointers run in opposite directions. Detection therefore scores each profile against the
file -- pointers must land inside it, regions must end after they start -- rather than
trusting the byte.

### Armour pointers run in opposite directions

In G10-ZZ the five armour slots descend: the pointer at `0x50` holds the highest offset
and each following slot a lower one, so a slot ends where the previous one begins, and
head is closed by a separate pointer at `0xE8`.

```
zz    0x50:0x833F40  0x54:0x7474C0  0x58:0x65AD00  0x5C:0x569D40  0x60:0x47C420   (descending)
gg    0x50:0x2BC220  0x54:0x332D20  0x58:0x39E620  0x5C:0x409D60  0x60:0x477B20   (ascending)
```

For the ascending versions each slot is bounded by the next pointer, which makes the whole
run regular:

```json
"dataPointers":   [ {"start":"0x50","end":"0x54"}, ... {"start":"0x60","end":"0x64"} ],
"stringPointers": [ {"start":"0x64","end":"0x68"}, ... {"start":"0x74","end":"0x78"} ]
```

Confirmed by reading the strings each pointer names: `0x64` gives ヘルム, `0x68` メイル,
`0x6C` アーム, `0x70` ベルト, `0x74` ジャージー -- head, body, arms, waist, legs.

`pc-s6` descends like ZZ but its armour data and its name tables are not adjacent, so the
end pointers are elsewhere in the header and have not been identified.

## Quests need code, not just offsets

The quest entry is a different size in each era: `0x160` in G10-ZZ, `0xC0` in GG and G2,
`0xA8` in Forward.4 and 5. Only the exact stride finds any sections at all, which makes
this a property of the file rather than a guess:

```
gg  stride 0xC0 -> 31 sections, 634 quests      f4  stride 0xA8 -> 15 sections, 325 quests
    stride 0xC4 ->  0 sections                      stride 0xAC ->  0 sections
    stride 0xC8 ->  0 sections                      stride 0xB0 ->  0 sections
```

A different entry size means different fields, and `BinaryReaderService.ReadQuestEntry`
models the G10-ZZ entry in code. So a profile alone cannot make an older version's quests
readable: that needs a reader per layout. Armour, weapons, items and skills are all
expressible as offsets and are not blocked by this.

## Adding a version

Decrypt and decompress first; everything below reads the plain file.

```shell
./ReFrontier unpack mhfdat.bin --flat
python3 docs/tools/scan_string_tables.py mhfdat.bin.decd.bin
```

`scan_string_tables.py` lists every header pointer that begins a table of string-pointers
and prints the first strings of each, so a pointer is identified by what it names:

```
start 0x064  end 0x068    7595 strings  ['装備無し', 'レザーライトヘルム', 'チェーンヘルム']
start 0x084  end 0x100    3228 strings  ['装備無し', 'クロスボウガン', 'クロスボウガン改']
start 0x088  end 0x084   11999 strings  ['装備無し', 'アイアンソード', 'アイアンソード改']
start 0x108  end 0x134    9601 strings  ['－－－－－－', '調合書①入門編', '調合書②初級編']
```

The `end` column is a hint, not an answer: several header pointers can hold the same
value, and for the first armour slot a nearer unrelated pointer often wins. Settle it from
the chain -- whether the run ascends or descends -- rather than from that column.

For quests:

```shell
./ReFrontier unpack mhfinf.bin --flat
python3 docs/tools/scan_quest_sections.py mhfinf.bin.decd.bin --guess-stride
python3 docs/tools/scan_quest_sections.py mhfinf.bin.decd.bin --stride 0xC0
```

`--guess-stride` reports the commonest spacing between entry-like positions, which
*underestimates* the entry size: an entry is detected again at `base+4`, `+8` and `+12`,
so ZZ's `0x160` entries report as `0x150`. Treat it as a starting point and confirm by
running the scan at candidate strides -- only the right one finds runs.

Both scripts are validated against the `zz` profile: the first recovers its armour, weapon
and item pointers exactly, and the second its quest sections, including ones the profile
does not yet list (issue #20).

Once the offsets are known, copy `zz.json`, change them, and check the result reads:

```shell
./FrontierDataTool dump --suffix test --mhfpac mhfpac.bin --mhfdat mhfdat.bin \
    --mhfinf mhfinf.bin --offsets my-version.json
```

The names in `Armor.csv` and `Melee.csv` are the test: if the offsets are wrong they are
mojibake or empty rather than equipment names.
