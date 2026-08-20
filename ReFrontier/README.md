# ReFrontier CLI

The main command-line tool for decompressing and processing Monster Hunter Frontier game files.

For installation and quick start, see the [main README](../README.md).

## Command Reference

```shell
./ReFrontier <command> <inputPath> [options]
```

Every task is a command. `--help` on a command lists only the options that apply to it,
and `./ReFrontier --help` lists the commands.

| Command | Description |
|---------|-------------|
| `unpack <path>` | Decrypt, decompress and unpack a file or directory |
| `decrypt <file>` | Decrypt an ECD or EXF file without unpacking it |
| `pack <dir>` | Repack a directory produced by `unpack` |
| `restore <path>` | Rebuild a file from the recipe saved during extraction |
| `compress <file> --type <type>` | Compress a file with a JPK algorithm |
| `encrypt <file>` | Encrypt a file with the ECD algorithm |
| `validate <path>` | Check file integrity without writing output |
| `diff <a> <b>` | Compare two files structurally |

Running `./ReFrontier <inputPath>` with no command unpacks, as earlier versions did.

### `unpack`

| Option | Description |
|--------|-------------|
| `--flat` | Don't unpack nested archives |
| `--keep-compressed` | Skip JPK decompression |
| `--keep-encrypted` | Skip decryption entirely |
| `--stage` | Treat file as stage-specific container |
| `--auto-stage` | Auto-detect stage-specific containers |
| `--clean` | Delete source archives after unpacking |
| `--no-meta` | Do not write `.meta`, `.log` or `.recipe.json`. Rebuilding will not be possible. |

### `decrypt`

| Option | Description |
|--------|-------------|
| `--clean` | Delete the source file after decrypting |
| `--no-meta` | Do not write metadata. Rebuilding will not be possible. |

### `pack`

| Option | Description |
|--------|-------------|
| `--no-meta` | Do not write metadata |

### `compress`

| Option | Description |
|--------|-------------|
| `--type <type>` | Compression type: `rw`, `hfirw`, `lz`, `hfi` (or `0`, `2`, `3`, `4`). Required. |
| `--level <n>` | Compression level (1-100), default 80. Only affects `lz` and `hfi`. Diminishing returns above ~80. |
| `--encrypt` | Encrypt the compressed output, producing a game-ready file |

See [ARCHIVE_FORMATS.md](../docs/ARCHIVE_FORMATS.md#compression-types) for algorithm details.

### `encrypt`

Encrypts with the ECD algorithm, using the `.meta` file if one is available and the
default key otherwise.

On its own `encrypt` derives both its input and its output by dropping one extension, and
writes beside the input rather than into `output/`:

```text
encrypt mhfdat.bin.decd.bin   reads mhfdat.bin.decd   writes mhfdat.bin
```

It prints which file it picked. Two consequences worth knowing:

- **The output overwrites that name in place**, which is usually your copy of the original
  game file. Keep a spare copy, or work in a scratch directory.
- **Passing `<name>.decd` instead shifts everything by one level**, so `encrypt
  mhfjmp.bin.decd` encrypts `mhfjmp.bin` — the original, already encrypted file — and
  writes a doubly encrypted `mhfjmp`.

`compress --encrypt` and `restore` depend on none of this and write to `output/`; prefer
them unless you specifically need to encrypt an already compressed file.

### `restore`

| Option | Description |
|--------|-------------|
| `--level <n>` | Override the compression level recorded in the recipe |

`restore` replaces having to remember `compress`, `--level` and `--encrypt` for a file
you extracted earlier. It needs a `<original file>.recipe.json`, which extraction writes
unless you pass `--no-meta`. Everything else comes from the recipe.

It accepts a file or an unpacked directory. For a container archive it rebuilds the nested
entries, packs the directory through its log, and applies the layers above it, following
nesting to the bottom. See the [main README](../README.md#rebuilding).

### Options for every command

| Option | Description |
|--------|-------------|
| `--parallelism <n>` | Number of parallel threads (0 = auto-detect, default: 0) |
| `--quiet` | Suppress progress bar during processing |
| `--verbose` | Show per-file processing messages |
| `--help` | Show help message |
| `--version` | Show version |

### Deprecated options

The pre-2.3.0 form put every task on one command as a flag. Those flags still work, so
existing scripts keep running, but the ones selecting a task now name the command to use
and will be removed in a future release.

| Option | Use instead |
|--------|-------------|
| `--decryptOnly` | `decrypt` |
| `--pack` | `pack` |
| `--restore` | `restore` |
| `--validate` | `validate` |
| `--diff <file>` | `diff <a> <b>` |
| `--compress <type>` | `compress --type <type>` |
| `--encrypt` | `encrypt` |
| `--nonRecursive` | `unpack --flat` |
| `--ignoreJPK` | `unpack --keep-compressed` |
| `--noDecryption` | `unpack --keep-encrypted` |
| `--stageContainer` | `unpack --stage` |
| `--autoStage` | `unpack --auto-stage` |
| `--cleanUp` | `--clean` |
| `--noMeta` | `--no-meta` |
| `--file <path>` | positional `<inputPath>` |
| `--saveMeta` | nothing; metadata is written by default |

The renamed options are still accepted under their old spelling by the commands that take
them, so `unpack file.bin --ignoreJPK` works and a script can move over in two steps.

## Examples

### Decrypt and decompress a file

```shell
./ReFrontier unpack mhfdat.bin
```

### Decrypt only (preserve compression)

```shell
./ReFrontier decrypt mhfdat.bin
```

### Compress with LZ at level 50

```shell
./ReFrontier compress mhfdat.bin --type lz --level 50
```

### Compress and encrypt in one step

```shell
./ReFrontier compress mhfdat.bin --type hfi --level 80 --encrypt
```

### Rebuild a file you extracted earlier

```shell
./ReFrontier unpack mhfdat.bin                 # writes mhfdat.bin.recipe.json
# edit mhfdat.bin.decd.bin
./ReFrontier restore mhfdat.bin.decd.bin       # writes output/mhfdat.bin
```

### Rebuild at a different compression level

```shell
./ReFrontier restore mhfdat.bin.decd.bin --level 100
```

### Unpack a folder recursively

```shell
./ReFrontier unpack dat_folder/
```

### Repack a directory

```shell
./ReFrontier pack unpacked_folder/
```

`pack` rebuilds the archive from its `.log` file, which names each entry as it was
inside the container. Unpacking is recursive by default, so a nested `entry.jkr` is
replaced by its decompressed `entry.jkr.bin` and the log no longer matches what is on
disk. Either extract with `--flat` so entries stay packed:

```shell
./ReFrontier unpack em001_b.pac --flat                # entries stay packed
# edit the entries in em001_b.pac.decd.unpacked/
./ReFrontier pack em001_b.pac.decd.unpacked           # writes output/em001_b.pac.decd
```

or use `restore`, which rebuilds the unpacked entries itself and applies the encryption
and compression around the container in the same pass:

```shell
./ReFrontier unpack em001_b.pac                       # entries unpacked as usual
./ReFrontier restore em001_b.pac                      # writes output/em001_b.pac
```

If entries are missing, `pack` reports all of them and what each became, and writes
nothing rather than leaving a partial archive.

## Compression Performance

Compression efficiency varies by level. Testing HFI compression on vanilla `mhfdat.bin` (26.5 MB decompressed):

| Level | Size (bytes) | Savings | Time |
|-------|--------------|---------|------|
| 1 | 9,453,891 | 64.3% | ~4s |
| 50 | ~7,000,000 | ~73% | ~30s |
| 100 | 6,045,761 | 77.2% | ~2m |
| Original (COG) | 5,363,764 | 79.7% | - |

Levels above 80 offer diminishing returns for significantly longer compression times.

Note: Level only affects LZ-based compression (`lz`, `hfi`). For `rw` and `hfirw`, level is ignored.
