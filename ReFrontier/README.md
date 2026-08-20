# ReFrontier CLI

The main command-line tool for decompressing and processing Monster Hunter Frontier game files.

For installation and quick start, see the [main README](../README.md).

## Command Reference

```shell
./ReFrontier <inputPath> [options]
```

Where `<inputPath>` is a file or directory to process.

### Decryption Options

| Option | Description |
|--------|-------------|
| `--saveMeta` | Save metadata files (required for repacking/re-encryption) |
| `--decryptOnly` | Decrypt without decompressing |
| `--noDecryption` | Skip decryption entirely |

### Compression Options

| Option | Description |
|--------|-------------|
| `--compress <type>` | Compression type: `rw`, `hfirw`, `lz`, `hfi` (or `0`, `2`, `3`, `4`) |
| `--level <n>` | Compression level (1-100). Only affects `lz` and `hfi`. Diminishing returns above ~80. |

See [ARCHIVE_FORMATS.md](../docs/ARCHIVE_FORMATS.md#compression-types) for algorithm details.

### Encryption Options

| Option | Description |
|--------|-------------|
| `--encrypt` | Encrypt with ECD algorithm (uses `.meta` file if available, otherwise default key) |

### Rebuilding Options

| Option | Description |
|--------|-------------|
| `--restore` | Rebuild a file from the recipe saved during extraction, reversing every layer it recorded |

`--restore` replaces having to remember `--compress`, `--level` and `--encrypt` for a file
you extracted earlier. It needs a `<original file>.recipe.json`, written when you extract
with `--saveMeta`. Pass `--level` alongside it to override the compression level;
everything else comes from the recipe.

It accepts a file or an unpacked directory. For a container archive it rebuilds the nested
entries, packs the directory through its log, and applies the layers above it, following
nesting to the bottom. See the [main README](../README.md#rebuilding).

### Unpacking Options

| Option | Description |
|--------|-------------|
| `--stageContainer` | Treat file as stage-specific container |
| `--autoStage` | Auto-detect stage-specific containers |
| `--nonRecursive` | Don't unpack nested archives |
| `--ignoreJPK` | Skip JPK decompression |
| `--pack` | Repack a directory (requires log file) |

### General Options

| Option | Description |
|--------|-------------|
| `--parallelism <n>` | Number of parallel threads (0 = auto-detect, default: 0) |
| `--quiet` | Suppress progress bar during processing |
| `--verbose` | Show per-file processing messages |
| `--cleanUp` | Delete intermediate/original files |
| `--help` | Show help message |
| `--version` | Show version |

### Deprecated Options

| Option | Description |
|--------|-------------|
| `--file <path>` | [Deprecated] Use positional argument `<inputPath>` instead |

## Examples

### Decrypt and decompress a file

```shell
./ReFrontier mhfdat.bin --saveMeta
```

### Decrypt only (preserve compression)

```shell
./ReFrontier mhfdat.bin --saveMeta --decryptOnly
```

### Compress with LZ at level 50

```shell
./ReFrontier mhfdat.bin --compress lz --level 50
```

### Compress and encrypt in one step

```shell
./ReFrontier mhfdat.bin --compress hfi --level 80 --encrypt
```

### Rebuild a file you extracted earlier

```shell
./ReFrontier mhfdat.bin --saveMeta            # writes mhfdat.bin.recipe.json
# edit mhfdat.bin.decd.bin
./ReFrontier mhfdat.bin.decd.bin --restore    # writes output/mhfdat.bin
```

### Rebuild at a different compression level

```shell
./ReFrontier mhfdat.bin.decd.bin --restore --level 100
```

### Unpack a folder recursively

```shell
./ReFrontier dat_folder/ --saveMeta
```

### Repack a directory

```shell
./ReFrontier unpacked_folder/ --pack
```

`--pack` rebuilds the archive from its `.log` file, which names each entry as it was
inside the container. Unpacking is recursive by default, so a nested `entry.jkr` is
replaced by its decompressed `entry.jkr.bin` and the log no longer matches what is on
disk. Either extract with `--nonRecursive` so entries stay packed:

```shell
./ReFrontier em001_b.pac --saveMeta --nonRecursive   # entries stay packed
# edit the entries in em001_b.pac.decd.unpacked/
./ReFrontier em001_b.pac.decd.unpacked --pack        # writes output/em001_b.pac.decd
```

or use `--restore`, which rebuilds the unpacked entries itself and applies the encryption
and compression around the container in the same pass:

```shell
./ReFrontier em001_b.pac --saveMeta                  # entries unpacked as usual
./ReFrontier em001_b.pac --restore                   # writes output/em001_b.pac
```

If entries are missing, `--pack` reports all of them and what each became, and writes
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
