# ReFrontier

Tools for (un)packing, (de)crypting and editing various Monster Hunter Frontier Online files.

Huge thank you to enler for their help!

## In this fork (Houmgaor/)

This fork is 100% compatible with mhvuze/ReFrontier.
Yet it brings many improvements.

- Compatibility with Linux/Mac.
- Interface standardization.
- 5x times faster compression.
- Removed outdated/unused libraries.
- Removed memory-unsafe code.
- User documentation.
- Linting.

## Install

Download this repository and compile it. You need .NET.

```commandline
git clone https://github.com/Houmgaor/ReFrontier.git
cd ReFrontier
dotnet build
```

## Usage

You should find the executable "ReFrontier.exe" in `Refrontier/bin/Debug` (or similar path).
If you don't see it, run ``dotnet build`` to compile ReFrontier once again.

Now, you can either drag-an-drop files/folder onto this executable, or open a terminal at the location of the executable file.

For a simple use case:

1. Copy "mhfdat.bin" (or any file) from the `dat/` folder of MHFrontier.
2. Decrypt and decompress the file with

    ```shell
    ./ReFrontier.exe output/mhfdat.bin --log
    ```

3. Edit to you convenience.
4. Encrypt back

    ```shell
    ./ReFrontier.exe output/mhfdat.bin --encrypt
    ```

5. Compress

    ```shell
    ./ReFrontier.exe output/mhfdat.bin --compress=4,80
    ```

6. Replace mhfdat.bin by the new file.

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

```commandline
./ReFrontier.exe mhfdat.bin --log  # Decrypt mhfdat.bin, create mhfdat.bin.meta, then decompress mhfdat.bin
```

The decryption options are:

```text
--log: Write log file (required for crypting back)
--decryptOnly: Decrypt ECD files without unpacking
--noDecryption: Don't decrypt ECD files, no unpacking
```

### Decompress

Decompressing a file *replaces* the old file by its new format, don't forget to backup important data.

The unpacking options are:

```text
--cleanUp: Delete simple archives after unpacking
--stageContainer: Unpack file as stage-specific container
--autoStage: Automatically attempt to unpack containers that might be stage-specific
--nonRecursive: Do not unpack recursively
--ignoreJPK: Do not decompress JPK files
```

### Data edition

Once the files are decrypted and decompressed, you can edit them.
You can use the tools in the [see also](#see-also) section.

This project also includes FrontierTextText and FrontierDataTools to extract text and data from the files respectively.

### Compress

To compress it again JPK type 4, with maximal (100%) compression use:

```shell
./ReFrontier.exe mhfdat.bin --compress=4,100
```

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

```shell
./ReFrontier.exe mhfdat.bin --encrypt
```

It will implicitely look for mhfdat.bin.meta file in the same folder, see the [decryption](#decrypt) section.
The file can now be used in Frontier.

## See also

Some more useful tools and projects:

- [Blender plugin for 3D models](https://github.com/Houmgaor/Monster-Hunter-Frontier-Importer).
- [MHFrontier Server](https://github.com/ZeruLight/Erupe).
- [var-username/Monster-Hunter-Frontier-Patterns](https://github.com/var-username/Monster-Hunter-Frontier-Patterns) for binary files template.
