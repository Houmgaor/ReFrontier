# ReFrontier

Tools for (un)packing, (de)crypting and editing various Monster Hunter Frontier Online files.

Huge thank you to enler for their help!

## In this fork (Houmgaor/)

This fork is 100% compatible with mhvuze/ReFrontier.
Yet it brings many improvements.

- Compatibility with Linux/Mac.
- 4x times faster.
- Interface standardization.
- Compress and encrypt in one command.
- Better parsing for CSV files.
- Removes outdated/unused libraries.
- Removes memory-unsafe code.
- User documentation.
- Linting.

## Install

Grab the [lastest release](https://github.com/Houmgaor/ReFrontier/releases) and download the file corresponding to your OS.
Unzip the folder, you will find ReFrontier.exe inside (simply ReFrontier on linux/mac).

You can also [build from source](#build) for latest version.

## Usage

You can either drag-an-drop files/folder onto the ReFrontier executable,
or open a terminal at its location.

For a simple use case:

1. Copy "mhfdat.bin" (or any file) from the `dat/` folder of MHFrontier.
Put in the the same folder as the executable.
2. Decrypt and decompress the file with

    ```shell
    ./ReFrontier.exe mhfdat.bin --log
    ```

3. Edit to you convenience (view tools in [see also](#see-also)).
4. Compress and encrypt

    ```shell
    ./ReFrontier.exe mhfdat.bin --compress=4,80 --encrypt
    ```

5. Replace mhfdat.bin by the new file.

To get help, use:

```shell
./ReFrontier.exe --help
```

For each command, detailed explanations are available at [ReFrontier/README.md](./ReFrontier/README.md).

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
./ReFrontier.exe mhfdat.bin --log --decryptOnly  # Decrypt mhfdat.bin, create mhfdat.bin.meta, do not decompress mhfdat.bin
```

### Decompress

Decompressing a file *replaces* the old file by its new format, don't forget to backup important data.
You can recognized a compressed file by its "JKR" header (in the file first bytes).
To decompress such a file:

```commandline
./ReFrontier.exe mhfdat.bin  # If mhdat.bin is already decrypted, decompress it
```

### Data edition

Once the files are decrypted and decompressed, you can edit them.
You can use the tools in the [see also](#see-also) section.

This solution also includes [FrontierTextTool](./FrontierTextTool/README.md) and [FrontierDataTool](./FrontierDataTool/README.md) to extract text and data from the files respectively.

### Compress

To compress it again JPK type 4, with maximal (100) compression use:

```shell
./ReFrontier.exe mhfdat.bin --compress=4,100
```

The output will be a file, or a folder with the same name in the `output/` directory.

### Encrypt

To a encrypt a *compressed* file use ``--encrypt``.
It encrypts the input file with the ECD algorithm.

```shell
./ReFrontier.exe mhfdat.bin --encrypt
```

It will implicitely look for mhfdat.bin.meta file in the same folder, see the [decryption](#decrypt) section.
The file can now be used in Frontier.

You can compress and encrypt a file in the same command, just add both arguments.

## Build

You need .NET to build this project.

```commandline
git clone https://github.com/Houmgaor/ReFrontier.git
cd ReFrontier
# Remove configuration for debugging
dotnet build --configuration Release
```

You should find the executable "ReFrontier.exe" in `./Refrontier/bin/Release/net8.0/` (or similar path).

## See also

Some more useful tools and projects:

- [var-username/Monster-Hunter-Frontier-Patterns](https://github.com/var-username/Monster-Hunter-Frontier-Patterns) for binary files template.
- [FrontierTextHandler](https://github.com/Houmgaor/FrontierTextHandler) for text data edition.
- [Blender addon](https://github.com/Houmgaor/MHFrontier-Blender-Addon) to import 3D models.

To run a server: [MHFrontier Server](https://github.com/ZeruLight/Erupe).
