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
    /// Service for validating structural integrity of MHF game files
    /// without writing any output files.
    /// </summary>
    public class FileValidationService
    {
        private readonly ILogger _logger;
        private readonly ICodecFactory _codecFactory;

        /// <summary>
        /// Create a new FileValidationService with default dependencies.
        /// </summary>
        public FileValidationService()
            : this(new ConsoleLogger(), new DefaultCodecFactory())
        {
        }

        /// <summary>
        /// Create a new FileValidationService with injectable dependencies.
        /// </summary>
        public FileValidationService(ILogger logger, ICodecFactory codecFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _codecFactory = codecFactory ?? throw new ArgumentNullException(nameof(codecFactory));
        }

        /// <summary>
        /// Validate a file from disk.
        /// </summary>
        /// <param name="filePath">Path to the file to validate.</param>
        /// <returns>Validation result with all checks performed.</returns>
        public ValidationResult Validate(string filePath)
        {
            byte[] buffer = File.ReadAllBytes(filePath);
            return ValidateBuffer(buffer, filePath);
        }

        /// <summary>
        /// Validate a buffer, auto-detecting format via magic bytes.
        /// Recursively validates inner layers (e.g. ECD payload may be JPK).
        /// </summary>
        /// <param name="buffer">File data to validate.</param>
        /// <param name="filePath">File path for reporting.</param>
        /// <returns>Validation result with all checks performed.</returns>
        public ValidationResult ValidateBuffer(byte[] buffer, string filePath)
        {
            var result = new ValidationResult { FilePath = filePath };

            if (buffer.Length < 4)
            {
                result.Checks.Add(new ValidationCheck
                {
                    Layer = "Unknown",
                    CheckName = "MinimumSize",
                    Passed = false,
                    Detail = $"File is {buffer.Length} bytes, minimum 4 bytes required for format detection"
                });
                return result;
            }

            uint magic = BitConverter.ToUInt32(buffer, 0);

            if (magic == FileMagic.ECD)
            {
                result.Checks.AddRange(ValidateEcd(buffer));
            }
            else if (magic == FileMagic.EXF)
            {
                result.Checks.AddRange(ValidateExf(buffer));
            }
            else if (magic == FileMagic.JKR)
            {
                result.Checks.AddRange(ValidateJpk(buffer));
            }
            else if (magic == FileMagic.MOMO)
            {
                result.Checks.AddRange(ValidateMomo(buffer));
            }
            else if (magic == FileMagic.MHA)
            {
                result.Checks.AddRange(ValidateMha(buffer));
            }
            else if (magic == FileMagic.FTXT)
            {
                result.Checks.AddRange(ValidateFtxt(buffer));
            }
            else
            {
                // Try as simple archive (no magic header)
                var archiveChecks = ValidateSimpleArchive(buffer);
                if (archiveChecks.Count > 0 && archiveChecks.TrueForAll(c => c.Passed))
                {
                    result.Checks.AddRange(archiveChecks);
                }
                else
                {
                    result.Checks.Add(new ValidationCheck
                    {
                        Layer = "Unknown",
                        CheckName = "FormatDetection",
                        Passed = false,
                        Detail = $"Unrecognized format (magic: 0x{magic:X8})"
                    });
                }
            }

            return result;
        }

        /// <summary>
        /// Validate an ECD encrypted container.
        /// </summary>
        public List<ValidationCheck> ValidateEcd(byte[] buffer)
        {
            var checks = new List<ValidationCheck>();

            // Magic check
            uint magic = BitConverter.ToUInt32(buffer, 0);
            checks.Add(new ValidationCheck
            {
                Layer = "ECD",
                CheckName = "Magic",
                Passed = magic == FileMagic.ECD,
                Detail = $"0x{magic:X8}"
            });

            // Size check
            if (buffer.Length < FileFormatConstants.EncryptionHeaderLength)
            {
                checks.Add(new ValidationCheck
                {
                    Layer = "ECD",
                    CheckName = "HeaderSize",
                    Passed = false,
                    Detail = $"Buffer is {buffer.Length} bytes, minimum {FileFormatConstants.EncryptionHeaderLength} required"
                });
                return checks;
            }

            checks.Add(new ValidationCheck
            {
                Layer = "ECD",
                CheckName = "HeaderSize",
                Passed = true,
                Detail = $"{buffer.Length} bytes"
            });

            // Key index check
            int keyIndex = BitConverter.ToUInt16(buffer, 4);
            bool keyValid = keyIndex >= 0 && keyIndex <= 5;
            checks.Add(new ValidationCheck
            {
                Layer = "ECD",
                CheckName = "KeyIndex",
                Passed = keyValid,
                Detail = $"Key index {keyIndex} (valid range 0-5)"
            });

            if (!keyValid)
                return checks;

            // Payload size check
            uint payloadSize = BitConverter.ToUInt32(buffer, 8);
            bool payloadFits = payloadSize <= (uint)(buffer.Length - FileFormatConstants.EncryptionHeaderLength);
            checks.Add(new ValidationCheck
            {
                Layer = "ECD",
                CheckName = "PayloadSize",
                Passed = payloadFits,
                Detail = $"{payloadSize} bytes (available: {buffer.Length - FileFormatConstants.EncryptionHeaderLength})"
            });

            if (!payloadFits)
                return checks;

            // CRC32 check: decrypt and verify
            uint expectedCrc32 = BitConverter.ToUInt32(buffer, 12);
            byte[] decryptBuffer = new byte[buffer.Length];
            Array.Copy(buffer, decryptBuffer, buffer.Length);

            try
            {
                Crypto.DecodeEcd(decryptBuffer);

                // Extract decrypted payload and compute CRC32
                byte[] payload = new byte[payloadSize];
                Array.Copy(decryptBuffer, FileFormatConstants.EncryptionHeaderLength, payload, 0, (int)payloadSize);
                uint actualCrc32 = Crc32.HashToUInt32(payload);

                bool crcMatch = expectedCrc32 == actualCrc32;
                checks.Add(new ValidationCheck
                {
                    Layer = "ECD",
                    CheckName = "CRC32",
                    Passed = crcMatch,
                    Detail = crcMatch
                        ? $"Matches (0x{actualCrc32:X8})"
                        : $"Expected 0x{expectedCrc32:X8}, got 0x{actualCrc32:X8}"
                });

                // Recurse into decrypted payload
                if (crcMatch && payload.Length >= 4)
                {
                    var innerResult = ValidateBuffer(payload, "");
                    if (innerResult.IsRecognized)
                        checks.AddRange(innerResult.Checks);
                }
            }
            catch (Exception ex) when (ex is DecryptionException || ex is IndexOutOfRangeException)
            {
                checks.Add(new ValidationCheck
                {
                    Layer = "ECD",
                    CheckName = "Decryption",
                    Passed = false,
                    Detail = $"Decryption failed: {ex.Message}"
                });
            }

            return checks;
        }

        /// <summary>
        /// Validate an EXF encrypted container.
        /// </summary>
        public List<ValidationCheck> ValidateExf(byte[] buffer)
        {
            var checks = new List<ValidationCheck>();

            uint magic = BitConverter.ToUInt32(buffer, 0);
            checks.Add(new ValidationCheck
            {
                Layer = "EXF",
                CheckName = "Magic",
                Passed = magic == FileMagic.EXF,
                Detail = $"0x{magic:X8}"
            });

            if (buffer.Length < FileFormatConstants.EncryptionHeaderLength)
            {
                checks.Add(new ValidationCheck
                {
                    Layer = "EXF",
                    CheckName = "HeaderSize",
                    Passed = false,
                    Detail = $"Buffer is {buffer.Length} bytes, minimum {FileFormatConstants.EncryptionHeaderLength} required"
                });
                return checks;
            }

            checks.Add(new ValidationCheck
            {
                Layer = "EXF",
                CheckName = "HeaderSize",
                Passed = true,
                Detail = $"{buffer.Length} bytes"
            });

            // Try decryption
            byte[] decryptBuffer = new byte[buffer.Length];
            Array.Copy(buffer, decryptBuffer, buffer.Length);

            try
            {
                Crypto.DecodeExf(decryptBuffer);

                checks.Add(new ValidationCheck
                {
                    Layer = "EXF",
                    CheckName = "Decryption",
                    Passed = true,
                    Detail = "Decryption succeeded"
                });

                // Recurse into decrypted payload
                if (decryptBuffer.Length > FileFormatConstants.EncryptionHeaderLength)
                {
                    byte[] payload = new byte[decryptBuffer.Length - FileFormatConstants.EncryptionHeaderLength];
                    Array.Copy(decryptBuffer, FileFormatConstants.EncryptionHeaderLength, payload, 0, payload.Length);

                    if (payload.Length >= 4)
                    {
                        var innerResult = ValidateBuffer(payload, "");
                        if (innerResult.IsRecognized)
                            checks.AddRange(innerResult.Checks);
                    }
                }
            }
            catch (Exception ex) when (ex is DecryptionException || ex is IndexOutOfRangeException)
            {
                checks.Add(new ValidationCheck
                {
                    Layer = "EXF",
                    CheckName = "Decryption",
                    Passed = false,
                    Detail = $"Decryption failed: {ex.Message}"
                });
            }

            return checks;
        }

        /// <summary>
        /// Validate a JPK compressed container.
        /// </summary>
        public List<ValidationCheck> ValidateJpk(byte[] buffer)
        {
            var checks = new List<ValidationCheck>();

            uint magic = BitConverter.ToUInt32(buffer, 0);
            checks.Add(new ValidationCheck
            {
                Layer = "JPK",
                CheckName = "Magic",
                Passed = magic == FileMagic.JKR,
                Detail = $"0x{magic:X8}"
            });

            if (buffer.Length < 16)
            {
                checks.Add(new ValidationCheck
                {
                    Layer = "JPK",
                    CheckName = "HeaderSize",
                    Passed = false,
                    Detail = $"Buffer is {buffer.Length} bytes, minimum 16 required for JPK header"
                });
                return checks;
            }

            // Read header fields
            int compressionType = BitConverter.ToUInt16(buffer, 6);
            int startOffset = BitConverter.ToInt32(buffer, 8);
            int outSize = BitConverter.ToInt32(buffer, 12);

            // Compression type check
            var compressionTypes = Enum.GetValues<CompressionType>();
            bool typeValid = compressionType >= 0 && compressionType < compressionTypes.Length;
            string typeName = typeValid ? compressionTypes[compressionType].ToString() : "INVALID";
            checks.Add(new ValidationCheck
            {
                Layer = "JPK",
                CheckName = "CompressionType",
                Passed = typeValid,
                Detail = $"{typeName} ({compressionType})"
            });

            if (!typeValid)
                return checks;

            // Decompressed size check
            bool sizeValid = outSize > 0;
            checks.Add(new ValidationCheck
            {
                Layer = "JPK",
                CheckName = "DeclaredSize",
                Passed = sizeValid,
                Detail = $"{outSize} bytes"
            });

            if (!sizeValid)
                return checks;

            // Try decompression
            try
            {
                var decoder = _codecFactory.CreateDecoder(compressionTypes[compressionType]);
                byte[] outBuffer = ArrayPool<byte>.Shared.Rent(outSize);
                try
                {
                    using var ms = new MemoryStream(buffer);
                    ms.Seek(startOffset, SeekOrigin.Begin);
                    decoder.ProcessOnDecode(ms, outBuffer, outSize);

                    checks.Add(new ValidationCheck
                    {
                        Layer = "JPK",
                        CheckName = "Decompression",
                        Passed = true,
                        Detail = $"Decompressed {outSize} bytes successfully"
                    });

                    // Recurse into decompressed data
                    byte[] decompressed = new byte[outSize];
                    Array.Copy(outBuffer, decompressed, outSize);

                    if (decompressed.Length >= 4)
                    {
                        var innerResult = ValidateBuffer(decompressed, "");
                        if (innerResult.IsRecognized)
                            checks.AddRange(innerResult.Checks);
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(outBuffer);
                }
            }
            catch (Exception ex) when (ex is ReFrontierException || ex is IndexOutOfRangeException || ex is ArgumentOutOfRangeException)
            {
                checks.Add(new ValidationCheck
                {
                    Layer = "JPK",
                    CheckName = "Decompression",
                    Passed = false,
                    Detail = $"Decompression failed: {ex.Message}"
                });
            }

            return checks;
        }

        /// <summary>
        /// Validate a MOMO simple archive.
        /// </summary>
        public List<ValidationCheck> ValidateMomo(byte[] buffer)
        {
            var checks = new List<ValidationCheck>();

            uint magic = BitConverter.ToUInt32(buffer, 0);
            checks.Add(new ValidationCheck
            {
                Layer = "MOMO",
                CheckName = "Magic",
                Passed = magic == FileMagic.MOMO,
                Detail = $"0x{magic:X8}"
            });

            if (buffer.Length < 12)
            {
                checks.Add(new ValidationCheck
                {
                    Layer = "MOMO",
                    CheckName = "MinimumSize",
                    Passed = false,
                    Detail = $"Buffer is {buffer.Length} bytes, too small for MOMO archive"
                });
                return checks;
            }

            // MOMO has 8-byte magic header, then entry count
            // Delegate to shared archive validation with magicSize=8
            var archiveChecks = ValidateArchiveEntries(buffer, 8, "MOMO");
            checks.AddRange(archiveChecks);

            return checks;
        }

        /// <summary>
        /// Validate an MHA named archive.
        /// </summary>
        public List<ValidationCheck> ValidateMha(byte[] buffer)
        {
            var checks = new List<ValidationCheck>();

            uint magic = BitConverter.ToUInt32(buffer, 0);
            checks.Add(new ValidationCheck
            {
                Layer = "MHA",
                CheckName = "Magic",
                Passed = magic == FileMagic.MHA,
                Detail = $"0x{magic:X8}"
            });

            // MHA header: 4 magic + 4 pointerEntryMeta + 4 count + 4 pointerEntryNames + 4 namesBlockLen + 2 unk1 + 2 unk2 = 24 bytes
            if (buffer.Length < 24)
            {
                checks.Add(new ValidationCheck
                {
                    Layer = "MHA",
                    CheckName = "HeaderSize",
                    Passed = false,
                    Detail = $"Buffer is {buffer.Length} bytes, minimum 24 required for MHA header"
                });
                return checks;
            }

            int pointerEntryMeta = BitConverter.ToInt32(buffer, 4);
            int count = BitConverter.ToInt32(buffer, 8);
            int pointerEntryNames = BitConverter.ToInt32(buffer, 12);

            // Count check
            bool countValid = count > 0 && count <= 9999;
            checks.Add(new ValidationCheck
            {
                Layer = "MHA",
                CheckName = "EntryCount",
                Passed = countValid,
                Detail = $"{count} entries"
            });

            if (!countValid)
                return checks;

            // Metadata pointer check
            bool metaInBounds = pointerEntryMeta >= 0 && pointerEntryMeta < buffer.Length;
            checks.Add(new ValidationCheck
            {
                Layer = "MHA",
                CheckName = "MetadataPointer",
                Passed = metaInBounds,
                Detail = $"Offset 0x{pointerEntryMeta:X8}"
            });

            if (!metaInBounds)
                return checks;

            // Names pointer check
            bool namesInBounds = pointerEntryNames >= 0 && pointerEntryNames < buffer.Length;
            checks.Add(new ValidationCheck
            {
                Layer = "MHA",
                CheckName = "NamesPointer",
                Passed = namesInBounds,
                Detail = $"Offset 0x{pointerEntryNames:X8}"
            });

            if (!namesInBounds)
                return checks;

            // Validate each entry's bounds
            bool allEntriesValid = true;
            string? failDetail = null;
            for (int i = 0; i < count; i++)
            {
                int metaOffset = pointerEntryMeta + i * FileFormatConstants.MhaEntryMetadataSize;
                if (metaOffset + FileFormatConstants.MhaEntryMetadataSize > buffer.Length)
                {
                    allEntriesValid = false;
                    failDetail = $"Entry {i} metadata at 0x{metaOffset:X8} overflows buffer";
                    break;
                }

                int entryOffset = BitConverter.ToInt32(buffer, metaOffset + 4);
                int entrySize = BitConverter.ToInt32(buffer, metaOffset + 8);

                if (entryOffset < 0 || entrySize < 0 || entryOffset + entrySize > buffer.Length)
                {
                    allEntriesValid = false;
                    failDetail = $"Entry {i}: offset 0x{entryOffset:X8} + size 0x{entrySize:X8} overflows buffer ({buffer.Length} bytes)";
                    break;
                }
            }

            checks.Add(new ValidationCheck
            {
                Layer = "MHA",
                CheckName = "EntryBounds",
                Passed = allEntriesValid,
                Detail = allEntriesValid ? $"{count} entries, all within bounds" : failDetail
            });

            return checks;
        }

        /// <summary>
        /// Validate a simple archive (no magic header, 4-byte header with count).
        /// </summary>
        public List<ValidationCheck> ValidateSimpleArchive(byte[] buffer)
        {
            return ValidateArchiveEntries(buffer, 4, "SimpleArchive");
        }

        /// <summary>
        /// Validate a stage container.
        /// </summary>
        public List<ValidationCheck> ValidateStageContainer(byte[] buffer)
        {
            var checks = new List<ValidationCheck>();

            // Stage container header: 3 segments of (offset, size) = 24 bytes, then restCount + unkHeader = 8 bytes
            int minSize = FileFormatConstants.StageContainerHeaderSize + FileFormatConstants.StageContainerRestHeaderSize;
            if (buffer.Length < minSize)
            {
                checks.Add(new ValidationCheck
                {
                    Layer = "StageContainer",
                    CheckName = "HeaderSize",
                    Passed = false,
                    Detail = $"Buffer is {buffer.Length} bytes, minimum {minSize} required"
                });
                return checks;
            }

            // Validate 3 fixed segments
            bool segmentsValid = true;
            string? segmentFail = null;
            for (int i = 0; i < 3; i++)
            {
                int offset = BitConverter.ToInt32(buffer, i * 8);
                int size = BitConverter.ToInt32(buffer, i * 8 + 4);

                if (size == 0)
                    continue; // Empty segments are allowed

                if (offset < 0 || size < 0 || offset + size > buffer.Length)
                {
                    segmentsValid = false;
                    segmentFail = $"Segment {i}: offset 0x{offset:X8} + size 0x{size:X8} overflows buffer ({buffer.Length} bytes)";
                    break;
                }
            }

            checks.Add(new ValidationCheck
            {
                Layer = "StageContainer",
                CheckName = "SegmentBounds",
                Passed = segmentsValid,
                Detail = segmentsValid ? "3 segments within bounds" : segmentFail
            });

            if (!segmentsValid)
                return checks;

            // Rest entries
            int restCount = BitConverter.ToInt32(buffer, FileFormatConstants.StageContainerHeaderSize);
            bool restCountValid = restCount >= 0 && restCount <= 9999;
            checks.Add(new ValidationCheck
            {
                Layer = "StageContainer",
                CheckName = "RestEntryCount",
                Passed = restCountValid,
                Detail = $"{restCount} entries"
            });

            if (!restCountValid)
                return checks;

            // Validate rest entries
            bool restValid = true;
            string? restFail = null;
            int restStart = FileFormatConstants.StageContainerHeaderSize + FileFormatConstants.StageContainerRestHeaderSize;
            for (int i = 0; i < restCount; i++)
            {
                int entryPos = restStart + i * FileFormatConstants.StageContainerRestEntrySize;
                if (entryPos + FileFormatConstants.StageContainerRestEntrySize > buffer.Length)
                {
                    restValid = false;
                    restFail = $"Rest entry {i} at 0x{entryPos:X8} overflows buffer";
                    break;
                }

                int offset = BitConverter.ToInt32(buffer, entryPos);
                int size = BitConverter.ToInt32(buffer, entryPos + 4);

                if (size == 0)
                    continue;

                if (offset < 0 || size < 0 || offset + size > buffer.Length)
                {
                    restValid = false;
                    restFail = $"Rest entry {i}: offset 0x{offset:X8} + size 0x{size:X8} overflows buffer ({buffer.Length} bytes)";
                    break;
                }
            }

            checks.Add(new ValidationCheck
            {
                Layer = "StageContainer",
                CheckName = "RestEntryBounds",
                Passed = restValid,
                Detail = restValid ? $"{restCount} rest entries, all within bounds" : restFail
            });

            return checks;
        }

        /// <summary>
        /// Validate an FTXT text file.
        /// </summary>
        public List<ValidationCheck> ValidateFtxt(byte[] buffer)
        {
            var checks = new List<ValidationCheck>();

            if (buffer.Length < FileFormatConstants.FtxtHeaderLength)
            {
                checks.Add(new ValidationCheck
                {
                    Layer = "FTXT",
                    CheckName = "HeaderSize",
                    Passed = false,
                    Detail = $"Buffer is {buffer.Length} bytes, minimum {FileFormatConstants.FtxtHeaderLength} required"
                });
                return checks;
            }

            checks.Add(new ValidationCheck
            {
                Layer = "FTXT",
                CheckName = "HeaderSize",
                Passed = true,
                Detail = $"{buffer.Length} bytes"
            });

            // String count at offset 10 (2 bytes)
            int stringCount = BitConverter.ToInt16(buffer, 10);
            bool countValid = stringCount >= 0;
            checks.Add(new ValidationCheck
            {
                Layer = "FTXT",
                CheckName = "StringCount",
                Passed = countValid,
                Detail = $"{stringCount} strings"
            });

            if (!countValid)
                return checks;

            // Try reading all strings
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                int pos = FileFormatConstants.FtxtHeaderLength;
                for (int i = 0; i < stringCount; i++)
                {
                    if (pos >= buffer.Length)
                    {
                        checks.Add(new ValidationCheck
                        {
                            Layer = "FTXT",
                            CheckName = "StringBounds",
                            Passed = false,
                            Detail = $"String {i} at offset 0x{pos:X8} overflows buffer"
                        });
                        return checks;
                    }

                    // Find null terminator
                    int nullPos = Array.IndexOf(buffer, (byte)0, pos);
                    if (nullPos < 0 || nullPos > buffer.Length)
                    {
                        checks.Add(new ValidationCheck
                        {
                            Layer = "FTXT",
                            CheckName = "StringBounds",
                            Passed = false,
                            Detail = $"String {i} at offset 0x{pos:X8} has no null terminator"
                        });
                        return checks;
                    }
                    pos = nullPos + 1;
                }

                checks.Add(new ValidationCheck
                {
                    Layer = "FTXT",
                    CheckName = "StringBounds",
                    Passed = true,
                    Detail = $"All {stringCount} strings readable"
                });
            }
            catch (Exception ex)
            {
                checks.Add(new ValidationCheck
                {
                    Layer = "FTXT",
                    CheckName = "StringBounds",
                    Passed = false,
                    Detail = $"Error reading strings: {ex.Message}"
                });
            }

            return checks;
        }

        /// <summary>
        /// Shared validation for simple archive entries (MOMO and SimpleArchive).
        /// </summary>
        private List<ValidationCheck> ValidateArchiveEntries(byte[] buffer, int magicSize, string layerName)
        {
            var checks = new List<ValidationCheck>();

            if (buffer.Length < magicSize + 4)
            {
                checks.Add(new ValidationCheck
                {
                    Layer = layerName,
                    CheckName = "MinimumSize",
                    Passed = false,
                    Detail = $"Buffer is {buffer.Length} bytes, too small"
                });
                return checks;
            }

            uint count = BitConverter.ToUInt32(buffer, magicSize);
            bool countValid = count > 0 && count <= 9999;
            checks.Add(new ValidationCheck
            {
                Layer = layerName,
                CheckName = "EntryCount",
                Passed = countValid,
                Detail = $"{count} entries"
            });

            if (!countValid)
                return checks;

            // Check entry table fits
            int entryTableEnd = magicSize + 4 + (int)count * FileFormatConstants.SimpleArchiveEntrySize;
            if (entryTableEnd > buffer.Length)
            {
                checks.Add(new ValidationCheck
                {
                    Layer = layerName,
                    CheckName = "EntryTable",
                    Passed = false,
                    Detail = $"Entry table needs {entryTableEnd} bytes but buffer is {buffer.Length}"
                });
                return checks;
            }

            // Validate each entry
            bool allValid = true;
            string? failDetail = null;
            int completeSize = magicSize;
            for (int i = 0; i < (int)count; i++)
            {
                int entryBase = magicSize + 4 + i * FileFormatConstants.SimpleArchiveEntrySize;
                int offset = BitConverter.ToInt32(buffer, entryBase);
                int size = BitConverter.ToInt32(buffer, entryBase + 4);

                completeSize += size;

                if (size < 4 || offset < 4 || offset + size > buffer.Length)
                {
                    allValid = false;
                    failDetail = $"Entry {i}: offset 0x{offset:X8} + size 0x{size:X8} invalid (buffer: {buffer.Length} bytes)";
                    break;
                }
            }

            if (allValid && completeSize > buffer.Length)
            {
                allValid = false;
                failDetail = $"Total extracted size {completeSize} exceeds file size {buffer.Length}";
            }

            checks.Add(new ValidationCheck
            {
                Layer = layerName,
                CheckName = "EntryBounds",
                Passed = allValid,
                Detail = allValid ? $"{count} entries, all within bounds" : failDetail
            });

            return checks;
        }

        /// <summary>
        /// Print validation result for a single file.
        /// </summary>
        public void PrintResult(ValidationResult result)
        {
            _logger.WriteLine($"Validating: {Path.GetFileName(result.FilePath)}");

            foreach (var check in result.Checks)
            {
                string status = check.Passed ? "PASS" : "FAIL";
                string detail = check.Detail != null ? $": {check.Detail}" : "";
                _logger.WriteLine($"  [{status}] {check.Layer}: {check.CheckName}{detail}");
            }

            if (!result.IsRecognized)
                _logger.WriteLine("Result: UNKNOWN FORMAT");
            else if (result.IsValid)
                _logger.WriteLine("Result: VALID");
            else
                _logger.WriteLine($"Result: INVALID - {result.FirstFailure?.Layer} {result.FirstFailure?.CheckName}: {result.FirstFailure?.Detail}");
        }

        /// <summary>
        /// Print summary for multiple validation results.
        /// </summary>
        public void PrintSummary(List<ValidationResult> results)
        {
            int valid = 0, invalid = 0, unknown = 0;

            _logger.WriteLine($"Validating {results.Count} files...");

            foreach (var result in results)
            {
                string fileName = Path.GetFileName(result.FilePath);

                if (!result.IsRecognized)
                {
                    _logger.WriteLine($"  {fileName}: UNKNOWN FORMAT");
                    unknown++;
                }
                else if (result.IsValid)
                {
                    _logger.WriteLine($"  {fileName}: VALID ({result.FormatChain})");
                    valid++;
                }
                else
                {
                    var fail = result.FirstFailure;
                    _logger.WriteLine($"  {fileName}: INVALID - {fail?.Layer} {fail?.CheckName} ({fail?.Detail})");
                    invalid++;
                }
            }

            _logger.WriteLine($"Summary: {valid} valid, {invalid} invalid, {unknown} unknown");
        }
    }
}
