# Migration Guide: ReFrontier 1.x to 2.0

This guide covers breaking changes when upgrading from ReFrontier 1.x to 2.0.

## Summary of Breaking Changes

1. **Architecture**: Static methods replaced with dependency injection
2. **CLI**: Custom argument parser replaced with System.CommandLine
3. **Auto-preprocessing**: FrontierTextTool and FrontierDataTool now auto-detect encrypted/compressed files
4. **File output behavior**: Removed `--noFileRewrite` option - files are no longer overwritten in place

## Architecture Changes

### Before (1.x): Static Methods with Lazy Singletons

```csharp
// Old static API
Crypto.DecryptEcd(buffer, key);
Pack.JPKEncode(inputPath, compressionType, level);
Unpack.JPKDecode(inputPath);
```

### After (2.0): Instance Methods with Dependency Injection

```csharp
// New instance-based API with default dependencies
var service = new FileProcessingService();
service.EncryptEcdFile(inputFile, metaFile, cleanUp);

var packingService = new PackingService();
packingService.JPKEncode(inputPath, outputPath, compressionType, level);

var unpackingService = new UnpackingService();
unpackingService.JPKDecode(inputPath, outputPath);
```

### Using Dependency Injection (for testing or custom implementations)

```csharp
// Create with custom dependencies
IFileSystem fileSystem = new RealFileSystem();
ILogger logger = new ConsoleLogger();
var config = FileProcessingConfig.Default();

var service = new FileProcessingService(fileSystem, logger, config);
```

### Available Abstractions

| Interface | Default Implementation | Purpose |
|-----------|----------------------|---------|
| `IFileSystem` | `RealFileSystem` | File I/O operations |
| `ILogger` | `ConsoleLogger` | Console output |
| `ICodecFactory` | `CodecFactory` | JPK codec creation |

## CLI Argument Changes

The CLI now uses System.CommandLine for argument parsing.

### ReFrontier CLI

```bash
# 1.x syntax (still works)
./ReFrontier mhfdat.bin --log --recursive

# 2.0 preferred syntax
./ReFrontier mhfdat.bin --log --recursive
```

Most CLI arguments remain compatible. Key options:

| Option | Description |
|--------|-------------|
| `--log` | Generate metadata for re-encryption |
| `--recursive` / `--nonRecursive` | Control recursive unpacking |
| `--compress=<type>,<level>` | Compress with specified type (0-4) and level |
| `--encrypt` | Encrypt output file |
| `--pack` | Repack directory |
| `--cleanUp` | Delete intermediate files |

### FrontierTextTool CLI

```bash
# Extract text to CSV
./FrontierTextTool extract mhfdat.bin --output texts.csv

# Insert text from CSV
./FrontierTextTool insert mhfdat.bin --input texts.csv --output mhfdat_new.bin
```

### FrontierDataTool CLI

```bash
# Extract data structures
./FrontierDataTool extract mhfdat.bin MeleeWeapons --output weapons.csv

# Import data from CSV
./FrontierDataTool import mhfdat.bin MeleeWeapons --input weapons.csv --output mhfdat_new.bin
```

## Auto-Preprocessing

FrontierTextTool and FrontierDataTool now automatically handle encrypted (ECD/EXF) and compressed (JPK) files. You no longer need to manually decrypt/decompress before processing.

```bash
# 1.x workflow (manual preprocessing required)
./ReFrontier mhfdat.bin --log           # Decrypt/decompress first
./FrontierTextTool extract output/mhfdat.bin.decd --output texts.csv

# 2.0 workflow (automatic preprocessing)
./FrontierTextTool extract mhfdat.bin --output texts.csv
```

## Service Layer

New service classes provide better separation of concerns:

| Service | Responsibility |
|---------|---------------|
| `FileProcessingService` | Encryption/decryption operations |
| `PackingService` | JPK encoding and archive packing |
| `UnpackingService` | JPK decoding and archive unpacking |
| `FilePreprocessor` | Auto-detect and preprocess files |

## File Output Behavior

In 1.x, decryption and decompression would overwrite the original file by default. The `--noFileRewrite` option was added to disable this behavior.

In 2.0, the original file is **never** overwritten. Output always goes to a new file:

| Operation | Input | Output |
|-----------|-------|--------|
| ECD Decryption | `file.bin` | `file.bin.decd` |
| EXF Decryption | `file.bin` | `file.bin.dexf` |
| JPK Decompression | `file.jkr` | `file.jkr.bin` (or similar) |

This is safer and more predictable. If you relied on the old overwrite behavior, update your scripts to use the new output paths.

## Migration Checklist

- [ ] Update any code using static methods to use service instances
- [ ] Review CLI scripts for argument compatibility
- [ ] Remove manual decrypt/decompress steps for FrontierTextTool/FrontierDataTool
- [ ] Update any custom integrations to use the new DI pattern
- [ ] Update scripts that relied on `--noFileRewrite` or in-place file modification
