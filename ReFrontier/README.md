# ReFrontier

ReFrontier is a tool to decompress game data for Monster Hunter Frontier.
The main documentation for the tool is at [../README.md](../README.md).

## Usage

You can either drag-an-drop files/folder onto the ReFrontier executable,
or open a terminal at its location.

To get help, use:

```shell
./ReFrontier.exe --help
```

### General arguments

Any command will leave the terminal open until you press enter.
Use ``--close`` to close the terminal after running ReFrontier.
Use ``--help`` to display the CLI help.

### Decrypt

ReFrontier does decryption (ECD → JPK) then decompression by default.

You simply need to drag and drop the path of the file or folders of ReFrontier you want to decrypt.
It also work with folders.

If you want to reuse the file in the game, don't forget to add ``--log`` to generate a .meta file.
If you file is named "mhfdat.bin", the meta file will be mhfdat.bin.meta.

The decryption options are:

```text
--log: Write log file (required for crypting back)
--decryptOnly: Decrypt ECD files without unpacking
--noDecryption: Don't decrypt ECD files, no unpacking
```

### Decompress

Decompressing a file *replaces* the old file by its new format, don't forget to backup important data.
You can recognized a compressed file by its "JKR" header (in the file first bytes).

The unpacking options are:

```text
--cleanUp: Delete simple archives after unpacking
--stageContainer: Unpack file as stage-specific container
--autoStage: Automatically attempt to unpack containers that might be stage-specific
--nonRecursive: Do not unpack recursively
--ignoreJPK: Do not decompress JPK files
```

### Data edition

This solution also includes [FrontierTextTool](../FrontierTextTool/README.md) and [FrontierDataTool](../FrontierDataTool/README.md) to extract text and data from the files respectively.

### Compress

You can compress back to supported formats.
Neither the compression type nor level are really important, since the game will figure out how to decompress from the file header.

A good match is JPK type 3, with any compression level from 50 to 90.
You can declare compression up to "100%", but the compression efficiency frops sharply at "81.91".

The options are:

```text
--pack: Repack directory (requires log file  - double check file extensions therein and make sure you account for encryption, compression)
--compress=[type],[level]: Pack file with jpk [type] at compression [level] (example: --compress=3,10)
```

The output will be a file, or a folder with the same name in the `output/` directory.

It has the following output on vanilla mhfdat.bin:

```text
File compressed using type 4 (level 1): 9453891 bytes (64,26 % saved) in 0:04.48
File compressed using type 4 (level 2): 8589271 bytes (67,53 % saved) in 0:06.84

[...]

File compressed using type 4 (level 100): 6045761 bytes (77,15 % saved) in 2:05.03
Vanilla file from COG with type 4       : 5363764 bytes (79,72 % saved)
```

### Encrypt

To a encrypt a *compressed* file use ``--encrypt``.
It encrypts the input file with the ECD algorithm.

It will implicitely look for mhfdat.bin.meta file in the same folder, see the [decryption](#decrypt) section.
The file can now be used in Frontier.

You can compress and encrypt a file in the same command, just add both arguments.
