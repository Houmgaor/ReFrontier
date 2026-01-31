# ReFrontier CLI

The main command-line tool for decompressing and processing Monster Hunter Frontier game files.

For installation and quick start, see the [main README](../README.md).

## Command Reference

```shell
./ReFrontier <file|folder> [options]
```

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
| `--level <n>` | Compression level (1-100). Higher = better ratio but slower. Diminishing returns above ~80. |

### Encryption Options

| Option | Description |
|--------|-------------|
| `--encrypt` | Encrypt with ECD algorithm (requires `.meta` file) |

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
| `--cleanUp` | Delete intermediate/original files |
| `--help` | Show help message |
| `--version` | Show version |

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

### Unpack a folder recursively

```shell
./ReFrontier dat_folder/ --saveMeta
```

### Repack a directory

```shell
./ReFrontier unpacked_folder/ --pack
```

## Compression Performance

Compression efficiency varies by level. Testing on vanilla `mhfdat.bin`:

| Level | Size (bytes) | Savings | Time |
|-------|--------------|---------|------|
| 1 | 9,453,891 | 64.3% | ~4s |
| 50 | ~7,000,000 | ~73% | ~30s |
| 100 | 6,045,761 | 77.2% | ~2m |
| Original (COG) | 5,363,764 | 79.7% | - |

Levels above 80 offer diminishing returns for significantly longer compression times.
