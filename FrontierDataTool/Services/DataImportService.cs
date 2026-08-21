using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using CsvHelper;

using FrontierDataTool.Enums;
using FrontierDataTool.Offsets;
using FrontierDataTool.Structs;

using LibReFrontier;
using LibReFrontier.Abstractions;

using ReFrontier;

namespace FrontierDataTool.Services
{
    /// <summary>
    /// Service for importing and modifying game data.
    /// </summary>
    public class DataImportService
    {
        private readonly IFileSystem _fileSystem;
        private readonly ILogger _logger;
        private readonly BinaryReaderService _binaryReader;
        /// <summary>Layout the caller insisted on, or null to work it out from the files.</summary>
        private OffsetProfile? _pinnedOffsets;

        /// <summary>
        /// Layout used by the import in progress, settled once the files are readable.
        /// </summary>
        private OffsetProfile _offsets;

        /// <summary>
        /// Create a new DataImportService with default dependencies.
        /// </summary>
        public DataImportService()
            : this(new RealFileSystem(), new ConsoleLogger())
        {
        }

        /// <summary>
        /// Create a new DataImportService with injectable dependencies.
        /// </summary>
        public DataImportService(IFileSystem fileSystem, ILogger logger)
            : this(fileSystem, logger, OffsetProfiles.Default)
        {
        }

        /// <summary>
        /// Create a new DataImportService writing a particular game version's layout.
        /// </summary>
        /// <param name="fileSystem">File system to read and write through.</param>
        /// <param name="logger">Where to report progress.</param>
        /// <param name="offsets">Where the data sits in this version's files.</param>
        public DataImportService(IFileSystem fileSystem, ILogger logger, OffsetProfile offsets)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _binaryReader = new BinaryReaderService();
            _pinnedOffsets = offsets ?? throw new ArgumentNullException(nameof(offsets));
            _offsets = _pinnedOffsets;
        }

        /// <summary>
        /// Create a service that works out the game version from the files it is given.
        /// </summary>
        /// <param name="fileSystem">File system to read and write through.</param>
        /// <param name="logger">Where to report progress.</param>
        /// <returns>A service that detects the layout instead of assuming one.</returns>
        public static DataImportService WithDetectedOffsets(IFileSystem fileSystem, ILogger logger)
        {
            var service = new DataImportService(fileSystem, logger, OffsetProfiles.Default);
            service._pinnedOffsets = null;
            return service;
        }

        /// <summary>
        /// Settle on the layout to write these files with, detecting it unless one was pinned.
        /// </summary>
        /// <param name="mhfdat">Preprocessed mhfdat.bin, or null when this import does not touch it.</param>
        /// <param name="mhfpac">Preprocessed mhfpac.bin, or null when this import does not touch it.</param>
        /// <param name="mhfinf">Preprocessed mhfinf.bin, or null when this import does not touch it.</param>
        private void ResolveOffsets(string? mhfdat, string? mhfpac, string? mhfinf)
        {
            if (_pinnedOffsets is not null)
            {
                _offsets = _pinnedOffsets;
                return;
            }

            _offsets = OffsetProfileDetector.Detect(
                mhfdat is null ? null : _fileSystem.ReadAllBytes(mhfdat),
                mhfpac is null ? null : _fileSystem.ReadAllBytes(mhfpac),
                mhfinf is null ? null : _fileSystem.ReadAllBytes(mhfinf));
            _logger.WriteLine($"Offset profile: {_offsets.Id} ({_offsets.Description}).");
        }

        /// <summary>
        /// Import armor data from CSV back into mhfdat.bin.
        /// </summary>
        /// <param name="mhfdat">Path to mhfdat.bin.</param>
        /// <param name="csvPath">Path to Armor.csv.</param>
        /// <param name="mhfpac">Path to mhfpac.bin (for skill name lookup).</param>
        public void ImportArmorData(string mhfdat, string csvPath, string mhfpac)
        {
            var preprocessor = new FilePreprocessor();

            var (processedMhfdat, cleanupMhfdat) = preprocessor.AutoPreprocess(mhfdat, createMetaFile: true);
            var (processedMhfpac, cleanupMhfpac) = preprocessor.AutoPreprocess(mhfpac, createMetaFile: true);

            try
            {
                ImportArmorDataInternal(processedMhfdat, csvPath, processedMhfpac);
            }
            finally
            {
                cleanupMhfdat();
                cleanupMhfpac();
            }
        }

        /// <summary>
        /// Internal implementation of ImportArmorData that works on preprocessed files.
        /// </summary>
        public void ImportArmorDataInternal(string mhfdat, string csvPath, string mhfpac)
        {
            ResolveOffsets(mhfdat, mhfpac, null);

            // Build skill name to ID lookup from mhfpac
            var skillLookup = BuildSkillLookup(mhfpac);
            _logger.WriteLine($"Loaded {skillLookup.Count} skill names for lookup.");
            var unresolvedSkills = new SortedSet<string>(StringComparer.Ordinal);

            // Read armor entries from CSV
            var armorEntries = LoadArmorCsv(csvPath);
            _logger.WriteLine($"Read {armorEntries.Count} armor entries from CSV.");

            // Load mhfdat.bin
            byte[] mhfdatData = _fileSystem.ReadAllBytes(mhfdat);
            using var ms = new MemoryStream(mhfdatData);
            using var br = new BinaryReader(ms);
            using var bw = new BinaryWriter(ms);

            // Process each armor class
            var dataPointers = _offsets.MhfDat.Armor.DataPointers;
            var slotNames = _offsets.MhfDat.Armor.SlotNames;

            for (int i = 0; i < dataPointers.Count; i++)
            {
                br.BaseStream.Seek(dataPointers[i].Start, SeekOrigin.Begin);
                int sOffset = br.ReadInt32();
                br.BaseStream.Seek(dataPointers[i].End, SeekOrigin.Begin);
                int eOffset = br.ReadInt32();

                int entryCount = (eOffset - sOffset) / BinaryReaderService.ARMOR_ENTRY_SIZE;

                var classEntries = armorEntries.Where(e => e.EquipClass == slotNames[i]).ToList();

                if (classEntries.Count != entryCount)
                {
                    _logger.Error($"Warning: CSV has {classEntries.Count} entries for {slotNames[i]}, but mhfdat expects {entryCount}. Skipping this class.");
                    continue;
                }

                _logger.WriteLine($"Writing {entryCount} {slotNames[i]} entries starting at 0x{sOffset:X8}");

                for (int j = 0; j < entryCount; j++)
                {
                    int entryOffset = sOffset + (j * BinaryReaderService.ARMOR_ENTRY_SIZE);
                    bw.BaseStream.Seek(entryOffset, SeekOrigin.Begin);
                    _binaryReader.WriteArmorEntry(bw, classEntries[j], skillLookup, unresolvedSkills);
                }
            }

            if (unresolvedSkills.Count > 0)
            {
                _logger.WriteLine(
                    $"Warning: {unresolvedSkills.Count} skill name(s) in the CSV match neither the " +
                    "game's names nor the English ones, and were written as skill 0 (None): " +
                    string.Join(", ", unresolvedSkills));
            }

            _fileSystem.CreateDirectory("output");
            string outputPath = Path.Combine("output", "mhfdat.bin");
            _fileSystem.WriteAllBytes(outputPath, mhfdatData);
            _logger.WriteLine($"Wrote modified data to {outputPath}");
        }

        /// <summary>
        /// Load armor entries from a CSV file.
        /// Auto-detects encoding (UTF-8 with BOM or Shift-JIS).
        /// </summary>
        public List<ArmorDataEntry> LoadArmorCsv(string csvPath)
        {
            using var stream = _fileSystem.OpenRead(csvPath);
            var encoding = TextFileConfiguration.DetectCsvEncoding(stream);
            using var textReader = new StreamReader(stream, encoding);
            using var csvReader = new CsvReader(textReader, TextFileConfiguration.CreateJapaneseCsvConfig());
            return csvReader.GetRecords<ArmorDataEntry>().ToList();
        }

        /// <summary>
        /// Import melee weapon data from CSV back into mhfdat.bin.
        /// </summary>
        /// <param name="mhfdat">Path to mhfdat.bin.</param>
        /// <param name="csvPath">Path to Melee.csv.</param>
        public void ImportMeleeData(string mhfdat, string csvPath)
        {
            var preprocessor = new FilePreprocessor();

            var (processedMhfdat, cleanupMhfdat) = preprocessor.AutoPreprocess(mhfdat, createMetaFile: true);

            try
            {
                ImportMeleeDataInternal(processedMhfdat, csvPath);
            }
            finally
            {
                cleanupMhfdat();
            }
        }

        /// <summary>
        /// Internal implementation of ImportMeleeData that works on preprocessed files.
        /// </summary>
        public void ImportMeleeDataInternal(string mhfdat, string csvPath)
        {
            ResolveOffsets(mhfdat, null, null);

            // Read melee entries from CSV
            var meleeEntries = LoadMeleeCsv(csvPath);
            _logger.WriteLine($"Read {meleeEntries.Count} melee weapon entries from CSV.");

            // Load mhfdat.bin
            byte[] mhfdatData = _fileSystem.ReadAllBytes(mhfdat);
            using var ms = new MemoryStream(mhfdatData);
            using var br = new BinaryReader(ms);
            using var bw = new BinaryWriter(ms);

            // Get melee weapon data offsets
            br.BaseStream.Seek(_offsets.MhfDat.Weapons.MeleeStart, SeekOrigin.Begin);
            int sOffset = br.ReadInt32();
            br.BaseStream.Seek(_offsets.MhfDat.Weapons.MeleeEnd, SeekOrigin.Begin);
            int eOffset = br.ReadInt32();

            int entryCount = (eOffset - sOffset) / BinaryReaderService.MELEE_WEAPON_ENTRY_SIZE;

            if (meleeEntries.Count != entryCount)
            {
                _logger.Error($"Warning: CSV has {meleeEntries.Count} entries, but mhfdat expects {entryCount}. Aborting.");
                return;
            }

            _logger.WriteLine($"Writing {entryCount} melee weapon entries starting at 0x{sOffset:X8}");

            for (int i = 0; i < entryCount; i++)
            {
                int entryOffset = sOffset + (i * BinaryReaderService.MELEE_WEAPON_ENTRY_SIZE);
                bw.BaseStream.Seek(entryOffset, SeekOrigin.Begin);
                _binaryReader.WriteMeleeWeaponEntry(bw, meleeEntries[i]);
            }

            _fileSystem.CreateDirectory("output");
            string outputPath = Path.Combine("output", "mhfdat.bin");
            _fileSystem.WriteAllBytes(outputPath, mhfdatData);
            _logger.WriteLine($"Wrote modified melee data to {outputPath}");
        }

        /// <summary>
        /// Load melee weapon entries from a CSV file.
        /// Auto-detects encoding (UTF-8 with BOM or Shift-JIS).
        /// </summary>
        public List<MeleeWeaponEntry> LoadMeleeCsv(string csvPath)
        {
            using var stream = _fileSystem.OpenRead(csvPath);
            var encoding = TextFileConfiguration.DetectCsvEncoding(stream);
            using var textReader = new StreamReader(stream, encoding);
            using var csvReader = new CsvReader(textReader, TextFileConfiguration.CreateJapaneseCsvConfig());
            return csvReader.GetRecords<MeleeWeaponEntry>().ToList();
        }

        /// <summary>
        /// Import ranged weapon data from CSV back into mhfdat.bin.
        /// </summary>
        /// <param name="mhfdat">Path to mhfdat.bin.</param>
        /// <param name="csvPath">Path to Ranged.csv.</param>
        public void ImportRangedData(string mhfdat, string csvPath)
        {
            var preprocessor = new FilePreprocessor();

            var (processedMhfdat, cleanupMhfdat) = preprocessor.AutoPreprocess(mhfdat, createMetaFile: true);

            try
            {
                ImportRangedDataInternal(processedMhfdat, csvPath);
            }
            finally
            {
                cleanupMhfdat();
            }
        }

        /// <summary>
        /// Internal implementation of ImportRangedData that works on preprocessed files.
        /// </summary>
        public void ImportRangedDataInternal(string mhfdat, string csvPath)
        {
            ResolveOffsets(mhfdat, null, null);

            // Read ranged entries from CSV
            var rangedEntries = LoadRangedCsv(csvPath);
            _logger.WriteLine($"Read {rangedEntries.Count} ranged weapon entries from CSV.");

            // Load mhfdat.bin
            byte[] mhfdatData = _fileSystem.ReadAllBytes(mhfdat);
            using var ms = new MemoryStream(mhfdatData);
            using var br = new BinaryReader(ms);
            using var bw = new BinaryWriter(ms);

            // Get ranged weapon data offsets
            br.BaseStream.Seek(_offsets.MhfDat.Weapons.RangedStart, SeekOrigin.Begin);
            int sOffset = br.ReadInt32();
            br.BaseStream.Seek(_offsets.MhfDat.Weapons.RangedEnd, SeekOrigin.Begin);
            int eOffset = br.ReadInt32();

            int entryCount = (eOffset - sOffset) / BinaryReaderService.RANGED_WEAPON_ENTRY_SIZE;

            if (rangedEntries.Count != entryCount)
            {
                _logger.Error($"Warning: CSV has {rangedEntries.Count} entries, but mhfdat expects {entryCount}. Aborting.");
                return;
            }

            _logger.WriteLine($"Writing {entryCount} ranged weapon entries starting at 0x{sOffset:X8}");

            for (int i = 0; i < entryCount; i++)
            {
                int entryOffset = sOffset + (i * BinaryReaderService.RANGED_WEAPON_ENTRY_SIZE);
                bw.BaseStream.Seek(entryOffset, SeekOrigin.Begin);
                _binaryReader.WriteRangedWeaponEntry(bw, rangedEntries[i]);
            }

            _fileSystem.CreateDirectory("output");
            string outputPath = Path.Combine("output", "mhfdat.bin");
            _fileSystem.WriteAllBytes(outputPath, mhfdatData);
            _logger.WriteLine($"Wrote modified ranged data to {outputPath}");
        }

        /// <summary>
        /// Load ranged weapon entries from a CSV file.
        /// Auto-detects encoding (UTF-8 with BOM or Shift-JIS).
        /// </summary>
        public List<RangedWeaponEntry> LoadRangedCsv(string csvPath)
        {
            using var stream = _fileSystem.OpenRead(csvPath);
            var encoding = TextFileConfiguration.DetectCsvEncoding(stream);
            using var textReader = new StreamReader(stream, encoding);
            using var csvReader = new CsvReader(textReader, TextFileConfiguration.CreateJapaneseCsvConfig());
            return csvReader.GetRecords<RangedWeaponEntry>().ToList();
        }

        /// <summary>
        /// Import quest data from CSV back into mhfinf.bin.
        /// Quest string fields (Title, TextMain, TextSubA, TextSubB) are reimported when
        /// pointer offset fields are present in the CSV (from a recent export).
        /// </summary>
        /// <param name="mhfinf">Path to mhfinf.bin.</param>
        /// <param name="csvPath">Path to InfQuests.csv.</param>
        public void ImportQuestData(string mhfinf, string csvPath)
        {
            var preprocessor = new FilePreprocessor();

            var (processedMhfinf, cleanupMhfinf) = preprocessor.AutoPreprocess(mhfinf, createMetaFile: true);

            try
            {
                ImportQuestDataInternal(processedMhfinf, csvPath);
            }
            finally
            {
                cleanupMhfinf();
            }
        }

        /// <summary>
        /// Internal implementation of ImportQuestData that works on preprocessed files.
        /// </summary>
        public void ImportQuestDataInternal(string mhfinf, string csvPath)
        {
            ResolveOffsets(null, null, mhfinf);

            // Read quest entries from CSV
            var questEntries = LoadQuestCsv(csvPath);
            _logger.WriteLine($"Read {questEntries.Count} quest entries from CSV.");

            // Calculate expected total count
            var questSections = _offsets.MhfInf.QuestSections;
            int expectedCount = _offsets.MhfInf.TotalQuestCount;

            if (questEntries.Count != expectedCount)
            {
                _logger.Error($"Warning: CSV has {questEntries.Count} entries, but mhfinf expects {expectedCount}. Aborting.");
                return;
            }

            // Load mhfinf.bin into a resizable stream
            byte[] mhfinfData = _fileSystem.ReadAllBytes(mhfinf);
            using var ms = new MemoryStream();
            ms.Write(mhfinfData, 0, mhfinfData.Length);
            using var bw = new BinaryWriter(ms);

            int currentEntry = 0;

            foreach (var section in questSections)
            {
                _logger.WriteLine($"Writing {section.Count} quest entries starting at 0x{section.Offset:X8}");

                bw.BaseStream.Seek(section.Offset, SeekOrigin.Begin);

                for (int i = 0; i < section.Count; i++)
                {
                    long entryStart = bw.BaseStream.Position;
                    _binaryReader.WriteQuestEntry(bw, questEntries[currentEntry]);
                    currentEntry++;

                    // Step over the whole entry, not just the fields written: the rest of
                    // it holds values this tool does not model, which must survive untouched.
                    bw.BaseStream.Seek(entryStart + _offsets.MhfInf.QuestEntrySize, SeekOrigin.Begin);
                }
            }

            // Build and append string table for quest text
            bool hasPointerOffsets = questEntries.Exists(e => e.TitlePtrFileOffset != 0);

            if (hasPointerOffsets)
            {
                // Append new string table at end of file
                int stringTableBase = (int)ms.Length;
                ms.Seek(0, SeekOrigin.End);

                var stringOffsets = new List<(long ptrFileOffset, int stringFileOffset)>();

                foreach (var entry in questEntries)
                {
                    if (entry.TitlePtrFileOffset != 0)
                    {
                        byte[] encoded = BinaryReaderService.EncodeStringToCp932(entry.Title);
                        int offset = (int)ms.Position;
                        bw.Write(encoded);
                        stringOffsets.Add((entry.TitlePtrFileOffset, offset));
                    }

                    if (entry.TextMainPtrFileOffset != 0)
                    {
                        byte[] encoded = BinaryReaderService.EncodeStringToCp932(entry.TextMain);
                        int offset = (int)ms.Position;
                        bw.Write(encoded);
                        stringOffsets.Add((entry.TextMainPtrFileOffset, offset));
                    }

                    if (entry.TextSubAPtrFileOffset != 0)
                    {
                        byte[] encoded = BinaryReaderService.EncodeStringToCp932(entry.TextSubA);
                        int offset = (int)ms.Position;
                        bw.Write(encoded);
                        stringOffsets.Add((entry.TextSubAPtrFileOffset, offset));
                    }

                    if (entry.TextSubBPtrFileOffset != 0)
                    {
                        byte[] encoded = BinaryReaderService.EncodeStringToCp932(entry.TextSubB);
                        int offset = (int)ms.Position;
                        bw.Write(encoded);
                        stringOffsets.Add((entry.TextSubBPtrFileOffset, offset));
                    }
                }

                // Update pointer fields to point to new strings
                foreach (var (ptrFileOffset, stringFileOffset) in stringOffsets)
                {
                    ms.Seek(ptrFileOffset, SeekOrigin.Begin);
                    bw.Write(stringFileOffset);
                }

                _logger.WriteLine($"Appended string table at 0x{stringTableBase:X8} ({stringOffsets.Count} strings, {ms.Length - stringTableBase} bytes).");
            }
            else
            {
                _logger.WriteLine("No pointer offsets found in CSV; quest text was not modified (use a recent export to include pointer offsets).");
            }

            _fileSystem.CreateDirectory("output");
            string outputPath = Path.Combine("output", "mhfinf.bin");
            _fileSystem.WriteAllBytes(outputPath, ms.ToArray());
            _logger.WriteLine($"Wrote modified quest data to {outputPath}");
        }

        /// <summary>
        /// Load quest entries from a CSV file.
        /// Auto-detects encoding (UTF-8 with BOM or Shift-JIS).
        /// </summary>
        public List<QuestData> LoadQuestCsv(string csvPath)
        {
            using var stream = _fileSystem.OpenRead(csvPath);
            var encoding = TextFileConfiguration.DetectCsvEncoding(stream);
            using var textReader = new StreamReader(stream, encoding);
            using var csvReader = new CsvReader(textReader, TextFileConfiguration.CreateJapaneseCsvConfig());
            return csvReader.GetRecords<QuestData>().ToList();
        }

        /// <summary>
        /// Build a dictionary mapping skill names to their IDs.
        /// </summary>
        /// <remarks>
        /// The game's own names are read first and always win. The English names are then
        /// added for any ID whose game name they do not collide with, so a CSV dumped with
        /// --english-skills imports without the user having to say so again: nothing in a
        /// CSV records which spelling produced it.
        /// </remarks>
        /// <param name="mhfpac">Path to a decrypted, decompressed mhfpac.</param>
        /// <returns>Skill name to ID, covering both the game's names and the English ones.</returns>
        public Dictionary<string, byte> BuildSkillLookup(string mhfpac)
        {
            var skillLookup = new Dictionary<string, byte>();

            using var ms = new MemoryStream(_fileSystem.ReadAllBytes(mhfpac));
            using var br = new BinaryReader(ms);

            br.BaseStream.Seek(_offsets.MhfPac.Skills.TreeNameStart, SeekOrigin.Begin);
            int sOffset = br.ReadInt32();
            br.BaseStream.Seek(_offsets.MhfPac.Skills.TreeNameEnd, SeekOrigin.Begin);
            int eOffset = br.ReadInt32();

            br.BaseStream.Seek(sOffset, SeekOrigin.Begin);
            byte id = 0;
            while (br.BaseStream.Position < eOffset)
            {
                string name = _binaryReader.StringFromPointer(br);
                if (!skillLookup.ContainsKey(name))
                {
                    skillLookup[name] = id;
                }
                id++;
            }

            foreach (var (englishName, englishId) in SkillLookup.IdsByEnglishName)
            {
                if (!skillLookup.ContainsKey(englishName))
                {
                    skillLookup[englishName] = englishId;
                }
            }

            return skillLookup;
        }

        /// <summary>
        /// Import rengoku floor stats from CSV back into rengoku_data.bin.
        /// </summary>
        /// <param name="rengokuPath">Path to rengoku_data.bin.</param>
        /// <param name="csvPath">Path to RengokuFloors.csv or RengokuSpawns.csv.</param>
        public void ImportRengokuData(string rengokuPath, string csvPath)
        {
            var preprocessor = new FilePreprocessor();
            var (processedPath, cleanup) = preprocessor.AutoPreprocess(rengokuPath, createMetaFile: true);

            try
            {
                string csvFilename = Path.GetFileName(csvPath).ToLowerInvariant();
                if (csvFilename.StartsWith("rengokufloor"))
                    ImportRengokuFloorsInternal(processedPath, csvPath);
                else if (csvFilename.StartsWith("rengokuspawn"))
                    ImportRengokuSpawnsInternal(processedPath, csvPath);
                else
                    _logger.Error($"Unknown rengoku CSV type '{csvFilename}'. Expected RengokuFloors.csv or RengokuSpawns.csv.");
            }
            finally
            {
                cleanup();
            }
        }

        /// <summary>
        /// Internal implementation: import floor stats CSV back into rengoku binary.
        /// </summary>
        public void ImportRengokuFloorsInternal(string rengokuPath, string csvPath)
        {
            var entries = LoadRengokuFloorsCsv(csvPath);
            _logger.WriteLine($"Read {entries.Count} floor stats entries from CSV.");

            byte[] data = _fileSystem.ReadAllBytes(rengokuPath);
            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);
            using var bw = new BinaryWriter(ms);

            string[] modeNames = ["Multi", "Solo"];
            int[] modeOffsets = [
                BinaryReaderService.RENGOKU_HEADER_SIZE,
                BinaryReaderService.RENGOKU_HEADER_SIZE + BinaryReaderService.ROAD_MODE_SIZE
            ];

            for (int m = 0; m < 2; m++)
            {
                string modeName = modeNames[m];
                br.BaseStream.Seek(modeOffsets[m], SeekOrigin.Begin);

                uint floorStatsCount = br.ReadUInt32();
                br.ReadUInt32(); // spawnCountCount
                br.ReadUInt32(); // spawnTablePointersCount
                uint floorStatsPointer = br.ReadUInt32();

                var modeEntries = entries.Where(e => e.RoadMode == modeName).ToList();

                if (modeEntries.Count != floorStatsCount)
                {
                    _logger.Error($"Warning: CSV has {modeEntries.Count} {modeName} floor entries, but binary expects {floorStatsCount}. Skipping.");
                    continue;
                }

                _logger.WriteLine($"Writing {floorStatsCount} {modeName} floor entries at 0x{floorStatsPointer:X8}");

                for (int i = 0; i < modeEntries.Count; i++)
                {
                    bw.BaseStream.Seek(floorStatsPointer + (i * BinaryReaderService.FLOOR_STATS_ENTRY_SIZE), SeekOrigin.Begin);
                    _binaryReader.WriteFloorStatsEntry(bw, modeEntries[i]);
                }
            }

            _fileSystem.CreateDirectory("output");
            string outputPath = Path.Combine("output", "rengoku_data.bin");
            _fileSystem.WriteAllBytes(outputPath, data);
            _logger.WriteLine($"Wrote modified floor data to {outputPath}");
        }

        /// <summary>
        /// Internal implementation: import spawn table CSV back into rengoku binary.
        /// </summary>
        public void ImportRengokuSpawnsInternal(string rengokuPath, string csvPath)
        {
            var entries = LoadRengokuSpawnsCsv(csvPath);
            _logger.WriteLine($"Read {entries.Count} spawn entries from CSV.");

            byte[] data = _fileSystem.ReadAllBytes(rengokuPath);
            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);
            using var bw = new BinaryWriter(ms);

            string[] modeNames = ["Multi", "Solo"];
            int[] modeOffsets = [
                BinaryReaderService.RENGOKU_HEADER_SIZE,
                BinaryReaderService.RENGOKU_HEADER_SIZE + BinaryReaderService.ROAD_MODE_SIZE
            ];

            for (int m = 0; m < 2; m++)
            {
                string modeName = modeNames[m];
                br.BaseStream.Seek(modeOffsets[m], SeekOrigin.Begin);

                br.ReadUInt32(); // floorStatsCount
                uint spawnCountCount = br.ReadUInt32();
                uint spawnTablePointersCount = br.ReadUInt32();
                br.ReadUInt32(); // floorStatsPointer
                uint spawnTablePointersPtr = br.ReadUInt32();
                uint spawnCountPointersPtr = br.ReadUInt32();

                // Read spawn count array
                br.BaseStream.Seek(spawnCountPointersPtr, SeekOrigin.Begin);
                var spawnCounts = new uint[spawnCountCount];
                for (int i = 0; i < spawnCountCount; i++)
                    spawnCounts[i] = br.ReadUInt32();

                // Read spawn table pointers
                br.BaseStream.Seek(spawnTablePointersPtr, SeekOrigin.Begin);
                var spawnTablePointers = new uint[spawnTablePointersCount];
                for (int i = 0; i < spawnTablePointersCount; i++)
                    spawnTablePointers[i] = br.ReadUInt32();

                var modeEntries = entries.Where(e => e.RoadMode == modeName).ToList();

                // Write spawn entries for each table
                for (int t = 0; t < spawnTablePointersCount; t++)
                {
                    var tableEntries = modeEntries.Where(e => e.TableIndex == t).ToList();
                    uint expectedCount = t < spawnCounts.Length ? spawnCounts[t] : 0;

                    if (tableEntries.Count != expectedCount)
                    {
                        _logger.Error($"Warning: CSV has {tableEntries.Count} entries for {modeName} table {t}, but binary expects {expectedCount}. Skipping.");
                        continue;
                    }

                    bw.BaseStream.Seek(spawnTablePointers[t], SeekOrigin.Begin);
                    for (int e = 0; e < tableEntries.Count; e++)
                    {
                        _binaryReader.WriteSpawnEntry(bw, tableEntries[e]);
                    }
                }

                _logger.WriteLine($"Wrote {modeEntries.Count} {modeName} spawn entries.");
            }

            _fileSystem.CreateDirectory("output");
            string outputPath = Path.Combine("output", "rengoku_data.bin");
            _fileSystem.WriteAllBytes(outputPath, data);
            _logger.WriteLine($"Wrote modified spawn data to {outputPath}");
        }

        /// <summary>
        /// Load rengoku floor stats entries from a CSV file.
        /// </summary>
        public List<RengokuFloorStats> LoadRengokuFloorsCsv(string csvPath)
        {
            using var stream = _fileSystem.OpenRead(csvPath);
            var encoding = TextFileConfiguration.DetectCsvEncoding(stream);
            using var textReader = new StreamReader(stream, encoding);
            using var csvReader = new CsvReader(textReader, TextFileConfiguration.CreateJapaneseCsvConfig());
            return csvReader.GetRecords<RengokuFloorStats>().ToList();
        }

        /// <summary>
        /// Load rengoku spawn entries from a CSV file.
        /// </summary>
        public List<RengokuSpawnEntry> LoadRengokuSpawnsCsv(string csvPath)
        {
            using var stream = _fileSystem.OpenRead(csvPath);
            var encoding = TextFileConfiguration.DetectCsvEncoding(stream);
            using var textReader = new StreamReader(stream, encoding);
            using var csvReader = new CsvReader(textReader, TextFileConfiguration.CreateJapaneseCsvConfig());
            return csvReader.GetRecords<RengokuSpawnEntry>().ToList();
        }

        /// <summary>
        /// Add all-items shop to file, change item prices, change armor prices.
        /// </summary>
        /// <param name="file">Input file path, usually mhfdat.bin.</param>
        public void ModShop(string file)
        {
            var preprocessor = new FilePreprocessor();

            var (processedFile, cleanup) = preprocessor.AutoPreprocess(file, createMetaFile: true);

            try
            {
                ModShopInternal(processedFile);
            }
            finally
            {
                cleanup();
            }
        }

        /// <summary>
        /// Internal implementation of ModShop that works on preprocessed files.
        /// </summary>
        public void ModShopInternal(string file)
        {
            int count;

            using (var msInput = new MemoryStream(_fileSystem.ReadAllBytes(file)))
            using (var brInput = new BinaryReader(msInput))
            using (var outputStream = _fileSystem.OpenWrite(file))
            using (var brOutput = new BinaryWriter(outputStream))
            {
                // Patch item prices
                brInput.BaseStream.Seek(0xFC, SeekOrigin.Begin);
                int sOffset = brInput.ReadInt32();
                brInput.BaseStream.Seek(0xA70, SeekOrigin.Begin);
                int eOffset = brInput.ReadInt32();

                count = (eOffset - sOffset) / BinaryReaderService.ITEM_ENTRY_SIZE;
                _logger.WriteLine($"Patching prices for {count} items starting at 0x{sOffset:X8}");

                for (int i = 0; i < count; i++)
                {
                    brOutput.BaseStream.Seek(sOffset + (i * BinaryReaderService.ITEM_ENTRY_SIZE) + 12, SeekOrigin.Begin);
                    brInput.BaseStream.Seek(sOffset + (i * BinaryReaderService.ITEM_ENTRY_SIZE) + 12, SeekOrigin.Begin);
                    int buyPrice = brInput.ReadInt32() / 50;
                    brOutput.Write(buyPrice);

                    brOutput.BaseStream.Seek(sOffset + (i * BinaryReaderService.ITEM_ENTRY_SIZE) + 16, SeekOrigin.Begin);
                    brInput.BaseStream.Seek(sOffset + (i * BinaryReaderService.ITEM_ENTRY_SIZE) + 16, SeekOrigin.Begin);
                    int sellPrice = brInput.ReadInt32() * 5;
                    brOutput.Write(sellPrice);
                }

                // Patch equip prices
                var armorDataPointers = _offsets.MhfDat.Armor.DataPointers;
                for (int i = 0; i < armorDataPointers.Count; i++)
                {
                    brInput.BaseStream.Seek(armorDataPointers[i].Start, SeekOrigin.Begin);
                    sOffset = brInput.ReadInt32();
                    brInput.BaseStream.Seek(armorDataPointers[i].End, SeekOrigin.Begin);
                    eOffset = brInput.ReadInt32();

                    count = (eOffset - sOffset) / BinaryReaderService.ARMOR_ENTRY_SIZE;
                    _logger.WriteLine($"Patching prices for {count} armor pieces starting at 0x{sOffset:X8}");

                    for (int j = 0; j < count; j++)
                    {
                        brOutput.BaseStream.Seek(sOffset + (j * BinaryReaderService.ARMOR_ENTRY_SIZE) + 12, SeekOrigin.Begin);
                        brOutput.Write(50);
                    }
                }
            }

            // Generate shop array
            count = 16700;
            byte[] shopArray = new byte[(count * BinaryReaderService.SHOP_ENTRY_SIZE) + 5 * 32];

            for (int i = 0; i < count; i++)
            {
                byte[] id = BitConverter.GetBytes((short)(i + 1));
                byte[] item = new byte[8];
                Array.Copy(id, item, 2);
                Array.Copy(item, 0, shopArray, i * BinaryReaderService.SHOP_ENTRY_SIZE, 8);
            }

            // Append modshop data to file
            byte[] inputArray = _fileSystem.ReadAllBytes(file);
            byte[] outputArray = new byte[inputArray.Length + shopArray.Length];
            Array.Copy(inputArray, outputArray, inputArray.Length);
            Array.Copy(shopArray, 0, outputArray, inputArray.Length, shopArray.Length);

            // Find and modify item shop data pointer
            byte[] needle = [0x0F, 01, 01, 00, 00, 00, 00, 00, 03, 01, 01, 00, 00, 00, 00, 00];
            int offsetData = ByteOperations.GetOffsetOfArray(outputArray, needle);

            if (offsetData != -1)
            {
                _logger.WriteLine($"Found shop inventory to modify at 0x{offsetData:X8}.");
                byte[] offsetArray = BitConverter.GetBytes(offsetData);
                Array.Reverse(offsetArray);
                int offsetPointer = ByteOperations.GetOffsetOfArray(outputArray, offsetArray);

                if (offsetPointer != -1)
                {
                    _logger.WriteLine($"Found shop pointer at 0x{offsetPointer:X8}.");
                    byte[] patchedPointer = BitConverter.GetBytes(inputArray.Length);
                    Array.Reverse(patchedPointer);
                    Array.Copy(patchedPointer, 0, outputArray, offsetPointer, patchedPointer.Length);
                }
                else
                {
                    _logger.WriteLine("Could not find shop pointer, please check manually and correct code.");
                }
            }
            else
            {
                _logger.WriteLine("Could not find shop needle, please check manually and correct code.");
            }

            // Find and modify Hunter Pearl Skill unlocks
            needle = [01, 00, 01, 00, 00, 00, 00, 00, 0x25, 00, 0x25, 00, 0x25, 00, 0x25, 00, 0x25, 00, 0x25, 00, 0x25, 00];
            offsetData = ByteOperations.GetOffsetOfArray(outputArray, needle);

            if (offsetData != -1)
            {
                _logger.WriteLine($"Found hunter pearl skill data to modify at 0x{offsetData:X8}.");
                byte[] pearlPatch = [02, 00, 02, 00, 02, 00, 02, 00, 02, 00, 02, 00, 02, 00];
                for (int i = 0; i < 108; i++)
                    Array.Copy(pearlPatch, 0, outputArray, offsetData + (i * BinaryReaderService.PEARL_ENTRY_SIZE) + 8, pearlPatch.Length);
            }
            else
            {
                _logger.WriteLine("Could not find pearl skill needle, please check manually and correct code.");
            }

            _fileSystem.WriteAllBytes(file, outputArray);
        }
    }
}
