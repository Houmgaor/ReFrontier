# ReFrontier

Tools for \*packing, \*crypting and editing various Monster Hunter Frontier Online files.

Huge thank you to enler for their help!

## Usage

Use ``dotnet build`` to compile ReFrontier.
It will be generate an executable file in `Refrontier/bin/Debug/ReFrontier.exe`.

Now, you can either drag-an-drop files/folder onto this executable, or open a terminal at the location of the executable file.

To get help, use:

```shell
./ReFrontier.exe
```

You will see the following options:

```text
Unpacking Options:
-log: Write log file (required for repacking)
-cleanUp: Delete simple archives after unpacking
-stageContainer: Unpack file as stage-specific container (for certain stXXX.pac files and maybe others)
-autoStage: Automatically attempt to unpack containers that might be stage-specific (this is very experimental, since there is no reliable way to detect a stage-specific container)
-nonRecursive: Do not unpack recursively (useful for modifying specific files in archives, also check -noDecryption/-ignoreJPK)
-decryptOnly: Decrypt ecd files without unpacking
-noDecryption: Don't decrypt ecd files, no unpacking
-ignoreJPK: Do not decompress JPK files

Packing Options:
-pack: Repack directory (requires log file  - double check file extensions therein and make sure you account for encryption, compression)
-compress [type],[level]: Pack file with jpk [type] at compression [level] (example: -compress 3,10)
-encrypt: Encrypt input file with ecd algorithm

General Options:
-close: Close window after finishing process
```

## Decrypt/decompress

ReFrontier does decryption (ECD → JPK) then decompression by default.

You simply need to drag and drop the path of the file or folders of ReFrontier you want to decrypt.
It can also depack folders.

Decompressing a file *replaces* the old file by its new format, don't forget to backup important data.

If you want to reuse the file in the game, don't forget to add ``-log`` to generate a .meta file.
If you file is named "mhfdat.bin", the meta file will be mhfdat.bin.meta.

## Data edition

Once the files are decrypted and decompressed, you can edit them.
You can use the tools in the [see also](#see-also) section.

This project also includes FrontierTextText and FrontierDataTools to extract text and data from the files respectively.

## Compress

To compress it again JPK type 4 compression use:

```shell
./ReFrontier.exe mhfdat.bin -compress 4,100
```

It has the following output on vanilla mhfdat.bin:

```text
File compressed using type 4 (level 1): 9453891 bytes (64,26 % saved) in 0:04.48
File compressed using type 4 (level 2): 8589271 bytes (67,53 % saved) in 0:06.84

[...]

File compressed using type 4 (level 95): 6045761 bytes (77,15 % saved) in 2:04.26
File compressed using type 4 (level 100): 6045761 bytes (77,15 % saved) in 2:05.03
Vanilla file from COG with type 4       : 5363764 bytes (79,72 % saved)
```

The output will be a file, or a folder with the same name in the `output/` directory.

## Encrypt

To a encrypt a *compressed* file use:

```shell
./ReFrontier.exe mhfdat.bin -encrypt
```

It will implicitely look for mhfdat.bin.meta file in the same folder, see the [decryption](#decryptdecompress) section.
The file can now be used in Frontier.

## Disclaimer

Repo made public as of 2019-07-17 on occasion of the game in question being shutdown in December 2019.
Tool was intended for personal use only, so it might be a bit messy to foreign eyes.
You're likely looking for ReFrontier itself and can safely ignore the other tools.

## See also

Some more useful tools and projects:

- [Blender plugin for 3D models](https://github.com/Houmgaor/Monster-Hunter-Frontier-Importer).
- [MHFrontier Server](https://github.com/ZeruLight/Erupe).
- [var-username/Monster-Hunter-Frontier-Patterns](https://github.com/var-username/Monster-Hunter-Frontier-Patterns) for binary files template.

## In this fork (Houmgaor/)

- Compatibility with Linux/Mac.
- Removed outdated/unused libraries.
- User documentation.
- Linting.
