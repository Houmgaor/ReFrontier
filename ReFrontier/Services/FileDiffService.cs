using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Hashing;
using System.Text;

using LibReFrontier;
using LibReFrontier.Abstractions;
using LibReFrontier.Exceptions;

using ReFrontier.Jpk;

namespace ReFrontier.Services
{
    /// <summary>
    /// Service for structurally comparing two MHF game files layer by layer.
    /// Peels encryption/compression layers and compares the resulting structures.
    /// </summary>
    public class FileDiffService
    {
        private readonly ILogger _logger;
        private readonly ICodecFactory _codecFactory;

        /// <summary>
        /// Create a new FileDiffService with default dependencies.
        /// </summary>
        public FileDiffService()
            : this(new ConsoleLogger(), new DefaultCodecFactory())
        {
        }

        /// <summary>
        /// Create a new FileDiffService with injectable dependencies.
        /// </summary>
        public FileDiffService(ILogger logger, ICodecFactory codecFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _codecFactory = codecFactory ?? throw new ArgumentNullException(nameof(codecFactory));
        }

        /// <summary>
        /// Compare two files from disk.
        /// </summary>
        /// <param name="filePath1">Path to the first file.</param>
        /// <param name="filePath2">Path to the second file.</param>
        /// <returns>Diff result with all differences found.</returns>
        public DiffResult Compare(string filePath1, string filePath2)
        {
            byte[] buf1 = File.ReadAllBytes(filePath1);
            byte[] buf2 = File.ReadAllBytes(filePath2);
            return CompareBuffers(buf1, buf2, Path.GetFileName(filePath1), Path.GetFileName(filePath2));
        }

        /// <summary>
        /// Compare two buffers structurally, peeling layers recursively.
        /// </summary>
        /// <param name="buf1">First file data.</param>
        /// <param name="buf2">Second file data.</param>
        /// <param name="name1">Display name for file 1.</param>
        /// <param name="name2">Display name for file 2.</param>
        /// <returns>Diff result with all differences found.</returns>
        public DiffResult CompareBuffers(byte[] buf1, byte[] buf2, string name1, string name2)
        {
            var result = new DiffResult { File1 = name1, File2 = name2 };

            var layers1 = PeelAllLayers(buf1);
            var layers2 = PeelAllLayers(buf2);

            result.FormatChain1 = BuildFormatChain(layers1);
            result.FormatChain2 = BuildFormatChain(layers2);

            // Compare layer by layer
            int maxLayers = Math.Max(layers1.Count, layers2.Count);
            for (int i = 0; i < maxLayers; i++)
            {
                if (i >= layers1.Count)
                {
                    result.Differences.Add(new DiffEntry
                    {
                        Layer = layers2[i].LayerName,
                        Property = "Presence",
                        Value1 = null,
                        Value2 = "present"
                    });
                    continue;
                }
                if (i >= layers2.Count)
                {
                    result.Differences.Add(new DiffEntry
                    {
                        Layer = layers1[i].LayerName,
                        Property = "Presence",
                        Value1 = "present",
                        Value2 = null
                    });
                    continue;
                }

                var layer1 = layers1[i];
                var layer2 = layers2[i];

                // Format mismatch at this layer
                if (layer1.LayerName != layer2.LayerName)
                {
                    result.Differences.Add(new DiffEntry
                    {
                        Layer = "Format",
                        Property = $"Layer[{i}]",
                        Value1 = layer1.LayerName,
                        Value2 = layer2.LayerName
                    });
                    break; // Can't compare further if formats diverge
                }

                // Compare metadata for this layer
                foreach (var kvp in layer1.Metadata)
                {
                    string key = kvp.Key;
                    string val1 = kvp.Value;
                    layer2.Metadata.TryGetValue(key, out string? val2);

                    if (val2 == null)
                    {
                        result.Differences.Add(new DiffEntry
                        {
                            Layer = layer1.LayerName,
                            Property = key,
                            Value1 = val1,
                            Value2 = null
                        });
                    }
                    else if (val1 != val2)
                    {
                        result.Differences.Add(new DiffEntry
                        {
                            Layer = layer1.LayerName,
                            Property = key,
                            Value1 = val1,
                            Value2 = val2
                        });
                    }
                }

                // Check for keys in layer2 not in layer1
                foreach (var kvp in layer2.Metadata)
                {
                    if (!layer1.Metadata.ContainsKey(kvp.Key))
                    {
                        result.Differences.Add(new DiffEntry
                        {
                            Layer = layer2.LayerName,
                            Property = kvp.Key,
                            Value1 = null,
                            Value2 = kvp.Value
                        });
                    }
                }

                // For archive/FTXT layers, compare entries
                if (layer1.LayerName == "SimpleArchive" || layer1.LayerName == "MOMO")
                {
                    int magicSize = layer1.LayerName == "MOMO" ? 8 : 4;
                    result.Differences.AddRange(CompareArchiveEntries(layer1.RawBuffer, layer2.RawBuffer, magicSize));
                }
                else if (layer1.LayerName == "MHA")
                {
                    result.Differences.AddRange(CompareMhaEntries(layer1.RawBuffer, layer2.RawBuffer));
                }
                else if (layer1.LayerName == "FTXT")
                {
                    result.Differences.AddRange(CompareFtxtStrings(layer1.RawBuffer, layer2.RawBuffer));
                }
            }

            return result;
        }

        /// <summary>
        /// Peel all layers from a buffer, returning ordered list of layer info.
        /// Each layer is detected, its metadata extracted, and the inner payload passed to the next iteration.
        /// </summary>
        internal List<LayerInfo> PeelAllLayers(byte[] buffer)
        {
            var layers = new List<LayerInfo>();
            byte[] current = buffer;

            while (current.Length >= 4)
            {
                uint magic = BitConverter.ToUInt32(current, 0);
                LayerInfo? layer = null;

                if (magic == FileMagic.ECD)
                {
                    layer = PeelEcd(current);
                }
                else if (magic == FileMagic.EXF)
                {
                    layer = PeelExf(current);
                }
                else if (magic == FileMagic.JKR)
                {
                    layer = PeelJpk(current);
                }
                else if (magic == FileMagic.MOMO)
                {
                    layer = PeelMomo(current);
                }
                else if (magic == FileMagic.MHA)
                {
                    layer = PeelMha(current);
                }
                else if (magic == FileMagic.FTXT)
                {
                    layer = PeelFtxt(current);
                }
                else
                {
                    // Try as simple archive
                    layer = TryPeelSimpleArchive(current);
                    if (layer == null)
                    {
                        // Raw payload — terminal layer
                        var rawLayer = new LayerInfo
                        {
                            LayerName = "Raw",
                            RawBuffer = current
                        };
                        uint crc = Crc32.HashToUInt32(current);
                        rawLayer.Metadata["Size"] = current.Length.ToString();
                        rawLayer.Metadata["CRC32"] = $"0x{crc:X8}";
                        layers.Add(rawLayer);
                        break;
                    }
                }

                if (layer != null)
                {
                    layers.Add(layer);
                    if (layer.InnerPayload != null && layer.InnerPayload.Length >= 4)
                    {
                        current = layer.InnerPayload;
                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    break;
                }
            }

            return layers;
        }

        private LayerInfo PeelEcd(byte[] buffer)
        {
            var layer = new LayerInfo { LayerName = "ECD", RawBuffer = buffer };

            if (buffer.Length < FileFormatConstants.EncryptionHeaderLength)
                return layer;

            int keyIndex = BitConverter.ToUInt16(buffer, 4);
            uint payloadSize = BitConverter.ToUInt32(buffer, 8);
            uint expectedCrc32 = BitConverter.ToUInt32(buffer, 12);

            layer.Metadata["KeyIndex"] = keyIndex.ToString();
            layer.Metadata["PayloadSize"] = payloadSize.ToString();
            layer.Metadata["CRC32"] = $"0x{expectedCrc32:X8}";

            // Decrypt to get inner payload
            byte[] decryptBuffer = new byte[buffer.Length];
            Array.Copy(buffer, decryptBuffer, buffer.Length);

            try
            {
                Crypto.DecodeEcd(decryptBuffer);
                if (payloadSize <= (uint)(buffer.Length - FileFormatConstants.EncryptionHeaderLength))
                {
                    byte[] payload = new byte[payloadSize];
                    Array.Copy(decryptBuffer, FileFormatConstants.EncryptionHeaderLength, payload, 0, (int)payloadSize);
                    layer.InnerPayload = payload;
                }
            }
            catch (Exception ex) when (ex is DecryptionException || ex is IndexOutOfRangeException)
            {
                // Can't peel further
            }

            return layer;
        }

        private LayerInfo PeelExf(byte[] buffer)
        {
            var layer = new LayerInfo { LayerName = "EXF", RawBuffer = buffer };

            if (buffer.Length < FileFormatConstants.EncryptionHeaderLength)
                return layer;

            layer.Metadata["PayloadSize"] = (buffer.Length - FileFormatConstants.EncryptionHeaderLength).ToString();

            byte[] decryptBuffer = new byte[buffer.Length];
            Array.Copy(buffer, decryptBuffer, buffer.Length);

            try
            {
                Crypto.DecodeExf(decryptBuffer);
                if (decryptBuffer.Length > FileFormatConstants.EncryptionHeaderLength)
                {
                    byte[] payload = new byte[decryptBuffer.Length - FileFormatConstants.EncryptionHeaderLength];
                    Array.Copy(decryptBuffer, FileFormatConstants.EncryptionHeaderLength, payload, 0, payload.Length);
                    layer.InnerPayload = payload;
                }
            }
            catch (Exception ex) when (ex is DecryptionException || ex is IndexOutOfRangeException)
            {
                // Can't peel further
            }

            return layer;
        }

        private LayerInfo PeelJpk(byte[] buffer)
        {
            var layer = new LayerInfo { LayerName = "JPK", RawBuffer = buffer };

            if (buffer.Length < 16)
                return layer;

            int compressionType = BitConverter.ToUInt16(buffer, 6);
            int startOffset = BitConverter.ToInt32(buffer, 8);
            int outSize = BitConverter.ToInt32(buffer, 12);

            var compressionTypes = Enum.GetValues<CompressionType>();
            bool typeValid = compressionType >= 0 && compressionType < compressionTypes.Length;
            string typeName = typeValid ? compressionTypes[compressionType].ToString() : "INVALID";

            layer.Metadata["CompressionType"] = $"{typeName} ({compressionType})";
            layer.Metadata["DecompressedSize"] = outSize.ToString();

            if (!typeValid || outSize <= 0)
                return layer;

            try
            {
                var decoder = _codecFactory.CreateDecoder(compressionTypes[compressionType]);
                byte[] outBuffer = ArrayPool<byte>.Shared.Rent(outSize);
                try
                {
                    using var ms = new MemoryStream(buffer);
                    ms.Seek(startOffset, SeekOrigin.Begin);
                    decoder.ProcessOnDecode(ms, outBuffer, outSize);

                    byte[] decompressed = new byte[outSize];
                    Array.Copy(outBuffer, decompressed, outSize);
                    layer.InnerPayload = decompressed;
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(outBuffer);
                }
            }
            catch (Exception ex) when (ex is ReFrontierException || ex is IndexOutOfRangeException || ex is ArgumentOutOfRangeException)
            {
                // Can't peel further
            }

            return layer;
        }

        private LayerInfo PeelMomo(byte[] buffer)
        {
            var layer = new LayerInfo { LayerName = "MOMO", RawBuffer = buffer };

            if (buffer.Length < 12)
                return layer;

            uint count = BitConverter.ToUInt32(buffer, 8);
            layer.Metadata["EntryCount"] = count.ToString();

            return layer; // MOMO is a terminal container — entries compared separately
        }

        private LayerInfo PeelMha(byte[] buffer)
        {
            var layer = new LayerInfo { LayerName = "MHA", RawBuffer = buffer };

            if (buffer.Length < 24)
                return layer;

            int count = BitConverter.ToInt32(buffer, 8);
            layer.Metadata["EntryCount"] = count.ToString();

            return layer; // MHA is a terminal container — entries compared separately
        }

        private LayerInfo PeelFtxt(byte[] buffer)
        {
            var layer = new LayerInfo { LayerName = "FTXT", RawBuffer = buffer };

            if (buffer.Length < FileFormatConstants.FtxtHeaderLength)
                return layer;

            int stringCount = BitConverter.ToInt16(buffer, 10);
            layer.Metadata["StringCount"] = stringCount.ToString();

            return layer; // FTXT is a terminal container — strings compared separately
        }

        private LayerInfo? TryPeelSimpleArchive(byte[] buffer)
        {
            if (buffer.Length < 8)
                return null;

            uint count = BitConverter.ToUInt32(buffer, 4);
            if (count == 0 || count > 9999)
                return null;

            // Validate entry table fits
            int entryTableEnd = 4 + 4 + (int)count * FileFormatConstants.SimpleArchiveEntrySize;
            if (entryTableEnd > buffer.Length)
                return null;

            // Validate at least the first entry
            int completeSize = 4; // magic size
            for (int i = 0; i < (int)count; i++)
            {
                int entryBase = 4 + 4 + i * FileFormatConstants.SimpleArchiveEntrySize;
                int offset = BitConverter.ToInt32(buffer, entryBase);
                int size = BitConverter.ToInt32(buffer, entryBase + 4);
                completeSize += size;

                if (size < 4 || offset < 4 || offset + size > buffer.Length)
                    return null;
            }

            if (completeSize > buffer.Length)
                return null;

            var layer = new LayerInfo { LayerName = "SimpleArchive", RawBuffer = buffer };
            layer.Metadata["EntryCount"] = count.ToString();
            return layer;
        }

        /// <summary>
        /// Compare archive entries (SimpleArchive/MOMO) between two buffers.
        /// </summary>
        internal List<DiffEntry> CompareArchiveEntries(byte[] buf1, byte[] buf2, int magicSize)
        {
            var diffs = new List<DiffEntry>();
            string layerName = magicSize == 8 ? "MOMO" : "SimpleArchive";

            if (buf1.Length < magicSize + 4 || buf2.Length < magicSize + 4)
                return diffs;

            uint count1 = BitConverter.ToUInt32(buf1, magicSize);
            uint count2 = BitConverter.ToUInt32(buf2, magicSize);

            if (count1 != count2)
            {
                diffs.Add(new DiffEntry
                {
                    Layer = layerName,
                    Property = "EntryCount",
                    Value1 = count1.ToString(),
                    Value2 = count2.ToString()
                });
            }

            int minCount = (int)Math.Min(count1, count2);
            for (int i = 0; i < minCount; i++)
            {
                int entryBase1 = magicSize + 4 + i * FileFormatConstants.SimpleArchiveEntrySize;
                int entryBase2 = magicSize + 4 + i * FileFormatConstants.SimpleArchiveEntrySize;

                if (entryBase1 + 8 > buf1.Length || entryBase2 + 8 > buf2.Length)
                    break;

                int offset1 = BitConverter.ToInt32(buf1, entryBase1);
                int size1 = BitConverter.ToInt32(buf1, entryBase1 + 4);
                int offset2 = BitConverter.ToInt32(buf2, entryBase2);
                int size2 = BitConverter.ToInt32(buf2, entryBase2 + 4);

                if (size1 != size2)
                {
                    diffs.Add(new DiffEntry
                    {
                        Layer = layerName,
                        Property = $"Entry[{i}].Size",
                        Value1 = $"0x{size1:X}",
                        Value2 = $"0x{size2:X}"
                    });
                }

                // Compare content CRC32
                if (offset1 + size1 <= buf1.Length && offset2 + size2 <= buf2.Length && size1 > 0 && size2 > 0)
                {
                    byte[] data1 = new byte[size1];
                    byte[] data2 = new byte[size2];
                    Array.Copy(buf1, offset1, data1, 0, size1);
                    Array.Copy(buf2, offset2, data2, 0, size2);

                    uint crc1 = Crc32.HashToUInt32(data1);
                    uint crc2 = Crc32.HashToUInt32(data2);

                    if (crc1 != crc2)
                    {
                        diffs.Add(new DiffEntry
                        {
                            Layer = layerName,
                            Property = $"Entry[{i}].CRC32",
                            Value1 = $"0x{crc1:X8}",
                            Value2 = $"0x{crc2:X8}"
                        });
                    }
                }
            }

            // Report entries only in one file
            if (count1 > count2)
            {
                for (int i = (int)count2; i < (int)count1; i++)
                {
                    diffs.Add(new DiffEntry
                    {
                        Layer = layerName,
                        Property = $"Entry[{i}]",
                        Value1 = "present",
                        Value2 = null
                    });
                }
            }
            else if (count2 > count1)
            {
                for (int i = (int)count1; i < (int)count2; i++)
                {
                    diffs.Add(new DiffEntry
                    {
                        Layer = layerName,
                        Property = $"Entry[{i}]",
                        Value1 = null,
                        Value2 = "present"
                    });
                }
            }

            return diffs;
        }

        /// <summary>
        /// Compare MHA archive entries by name.
        /// </summary>
        internal List<DiffEntry> CompareMhaEntries(byte[] buf1, byte[] buf2)
        {
            var diffs = new List<DiffEntry>();

            if (buf1.Length < 24 || buf2.Length < 24)
                return diffs;

            int count1 = BitConverter.ToInt32(buf1, 8);
            int count2 = BitConverter.ToInt32(buf2, 8);

            if (count1 != count2)
            {
                diffs.Add(new DiffEntry
                {
                    Layer = "MHA",
                    Property = "EntryCount",
                    Value1 = count1.ToString(),
                    Value2 = count2.ToString()
                });
            }

            var entries1 = ReadMhaEntries(buf1, count1);
            var entries2 = ReadMhaEntries(buf2, count2);

            // Build lookup by name for file 2
            var lookup2 = new Dictionary<string, MhaEntryInfo>();
            foreach (var entry in entries2)
            {
                lookup2[entry.Name] = entry;
            }

            // Compare entries present in file 1
            var matched = new HashSet<string>();
            foreach (var entry1 in entries1)
            {
                if (lookup2.TryGetValue(entry1.Name, out var entry2))
                {
                    matched.Add(entry1.Name);

                    if (entry1.Size != entry2.Size)
                    {
                        diffs.Add(new DiffEntry
                        {
                            Layer = "MHA",
                            Property = $"{entry1.Name}.Size",
                            Value1 = $"0x{entry1.Size:X}",
                            Value2 = $"0x{entry2.Size:X}"
                        });
                    }

                    if (entry1.Crc32 != entry2.Crc32)
                    {
                        diffs.Add(new DiffEntry
                        {
                            Layer = "MHA",
                            Property = $"{entry1.Name}.CRC32",
                            Value1 = $"0x{entry1.Crc32:X8}",
                            Value2 = $"0x{entry2.Crc32:X8}"
                        });
                    }
                }
                else
                {
                    diffs.Add(new DiffEntry
                    {
                        Layer = "MHA",
                        Property = entry1.Name,
                        Value1 = "present",
                        Value2 = null
                    });
                }
            }

            // Entries only in file 2
            foreach (var entry2 in entries2)
            {
                if (!matched.Contains(entry2.Name))
                {
                    diffs.Add(new DiffEntry
                    {
                        Layer = "MHA",
                        Property = entry2.Name,
                        Value1 = null,
                        Value2 = "present"
                    });
                }
            }

            return diffs;
        }

        /// <summary>
        /// Compare FTXT strings one by one.
        /// </summary>
        internal List<DiffEntry> CompareFtxtStrings(byte[] buf1, byte[] buf2)
        {
            var diffs = new List<DiffEntry>();

            if (buf1.Length < FileFormatConstants.FtxtHeaderLength || buf2.Length < FileFormatConstants.FtxtHeaderLength)
                return diffs;

            int count1 = BitConverter.ToInt16(buf1, 10);
            int count2 = BitConverter.ToInt16(buf2, 10);

            if (count1 != count2)
            {
                diffs.Add(new DiffEntry
                {
                    Layer = "FTXT",
                    Property = "StringCount",
                    Value1 = count1.ToString(),
                    Value2 = count2.ToString()
                });
            }

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var strings1 = ReadFtxtStrings(buf1, count1);
            var strings2 = ReadFtxtStrings(buf2, count2);

            int minCount = Math.Min(strings1.Count, strings2.Count);
            for (int i = 0; i < minCount; i++)
            {
                if (strings1[i] != strings2[i])
                {
                    diffs.Add(new DiffEntry
                    {
                        Layer = "FTXT",
                        Property = $"String[{i}]",
                        Value1 = strings1[i],
                        Value2 = strings2[i]
                    });
                }
            }

            // Strings only in one file
            if (strings1.Count > strings2.Count)
            {
                for (int i = strings2.Count; i < strings1.Count; i++)
                {
                    diffs.Add(new DiffEntry
                    {
                        Layer = "FTXT",
                        Property = $"String[{i}]",
                        Value1 = strings1[i],
                        Value2 = null
                    });
                }
            }
            else if (strings2.Count > strings1.Count)
            {
                for (int i = strings1.Count; i < strings2.Count; i++)
                {
                    diffs.Add(new DiffEntry
                    {
                        Layer = "FTXT",
                        Property = $"String[{i}]",
                        Value1 = null,
                        Value2 = strings2[i]
                    });
                }
            }

            return diffs;
        }

        /// <summary>
        /// Print the diff result in a human-readable format.
        /// </summary>
        public void PrintResult(DiffResult result)
        {
            _logger.WriteLine($"Comparing: {result.File1} vs {result.File2}");

            if (result.FormatChain1 == result.FormatChain2)
            {
                _logger.WriteLine($"  Format: {result.FormatChain1} (identical structure)");
            }
            else
            {
                _logger.WriteLine($"  Format 1: {result.FormatChain1}");
                _logger.WriteLine($"  Format 2: {result.FormatChain2}");
            }

            if (result.AreIdentical)
            {
                _logger.WriteLine("  All layers identical.");
                _logger.WriteLine("Summary: No differences");
                return;
            }

            foreach (var diff in result.Differences)
            {
                if (diff.Value1 == null)
                {
                    _logger.WriteLine($"  [NEW]  {diff.Layer}: {diff.Property} (only in file 2, value: {diff.Value2})");
                }
                else if (diff.Value2 == null)
                {
                    _logger.WriteLine($"  [DEL]  {diff.Layer}: {diff.Property} (only in file 1, value: {diff.Value1})");
                }
                else
                {
                    _logger.WriteLine($"  [DIFF] {diff.Layer}: {diff.Property} {diff.Value1} vs {diff.Value2}");
                }
            }

            _logger.WriteLine($"Summary: {result.Differences.Count} difference{(result.Differences.Count != 1 ? "s" : "")}");
        }

        private static string BuildFormatChain(List<LayerInfo> layers)
        {
            var names = new List<string>();
            foreach (var layer in layers)
            {
                if (!names.Contains(layer.LayerName))
                    names.Add(layer.LayerName);
            }
            return string.Join(" > ", names);
        }

        private static List<MhaEntryInfo> ReadMhaEntries(byte[] buffer, int count)
        {
            var entries = new List<MhaEntryInfo>();
            if (buffer.Length < 24 || count <= 0)
                return entries;

            int pointerEntryMeta = BitConverter.ToInt32(buffer, 4);
            int pointerEntryNames = BitConverter.ToInt32(buffer, 12);

            for (int i = 0; i < count; i++)
            {
                int metaOffset = pointerEntryMeta + i * FileFormatConstants.MhaEntryMetadataSize;
                if (metaOffset + FileFormatConstants.MhaEntryMetadataSize > buffer.Length)
                    break;

                int stringOffset = BitConverter.ToInt32(buffer, metaOffset);
                int entryOffset = BitConverter.ToInt32(buffer, metaOffset + 4);
                int entrySize = BitConverter.ToInt32(buffer, metaOffset + 8);
                int fileId = BitConverter.ToInt32(buffer, metaOffset + 16);

                // Read name
                string name = $"entry_{i}";
                int namePos = pointerEntryNames + stringOffset;
                if (namePos >= 0 && namePos < buffer.Length)
                {
                    int nullPos = Array.IndexOf(buffer, (byte)0, namePos);
                    if (nullPos > namePos)
                    {
                        name = Encoding.UTF8.GetString(buffer, namePos, nullPos - namePos);
                    }
                }

                // Compute CRC32 of content
                uint crc32 = 0;
                if (entryOffset >= 0 && entrySize > 0 && entryOffset + entrySize <= buffer.Length)
                {
                    byte[] data = new byte[entrySize];
                    Array.Copy(buffer, entryOffset, data, 0, entrySize);
                    crc32 = Crc32.HashToUInt32(data);
                }

                entries.Add(new MhaEntryInfo { Name = name, Size = entrySize, FileId = fileId, Crc32 = crc32 });
            }

            return entries;
        }

        private static List<string> ReadFtxtStrings(byte[] buffer, int count)
        {
            var strings = new List<string>();
            int pos = FileFormatConstants.FtxtHeaderLength;

            for (int i = 0; i < count; i++)
            {
                if (pos >= buffer.Length)
                    break;

                int nullPos = Array.IndexOf(buffer, (byte)0, pos);
                if (nullPos < 0)
                    break;

                string s = Encoding.GetEncoding("shift_jis").GetString(buffer, pos, nullPos - pos);
                strings.Add(s);
                pos = nullPos + 1;
            }

            return strings;
        }
    }

    /// <summary>
    /// Information about a single peeled layer during diff comparison.
    /// </summary>
    internal class LayerInfo
    {
        public string LayerName { get; set; } = "";
        public Dictionary<string, string> Metadata { get; set; } = new();
        public byte[] RawBuffer { get; set; } = Array.Empty<byte>();
        public byte[]? InnerPayload { get; set; }
    }

    /// <summary>
    /// MHA entry information for diff comparison.
    /// </summary>
    internal class MhaEntryInfo
    {
        public string Name { get; set; } = "";
        public int Size { get; set; }
        public int FileId { get; set; }
        public uint Crc32 { get; set; }
    }
}
