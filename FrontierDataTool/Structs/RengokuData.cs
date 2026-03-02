using System.Collections.Generic;

namespace FrontierDataTool.Structs
{
    /// <summary>
    /// Floor stats entry from rengoku_data.bin (0x18 = 24 bytes per entry).
    /// Defines floor properties for a Hunting Road floor.
    /// </summary>
    public class RengokuFloorStats
    {
        /// <summary>
        /// Road mode: "Multi" or "Solo".
        /// </summary>
        public string? RoadMode { get; set; }

        /// <summary>
        /// Floor number (1-based).
        /// </summary>
        public uint FloorNumber { get; set; }

        /// <summary>
        /// Index into the spawn tables array for this floor.
        /// </summary>
        public uint SpawnTableUsed { get; set; }

        public uint Unk0 { get; set; }

        /// <summary>
        /// Point multiplier 1.
        /// </summary>
        public float PointMulti1 { get; set; }

        /// <summary>
        /// Point multiplier 2.
        /// </summary>
        public float PointMulti2 { get; set; }

        /// <summary>
        /// Whether this floor loops back (boolean as u32).
        /// </summary>
        public uint FinalLoop { get; set; }
    }

    /// <summary>
    /// Spawn table entry from rengoku_data.bin (0x20 = 32 bytes per entry).
    /// Defines a possible monster spawn combination.
    /// </summary>
    public class RengokuSpawnEntry
    {
        /// <summary>
        /// Road mode: "Multi" or "Solo".
        /// </summary>
        public string? RoadMode { get; set; }

        /// <summary>
        /// Index of the spawn table this entry belongs to.
        /// </summary>
        public int TableIndex { get; set; }

        /// <summary>
        /// First monster ID (em### format, 32-bit).
        /// </summary>
        public uint MonsterID1 { get; set; }

        /// <summary>
        /// First monster variant.
        /// </summary>
        public uint MonsterVariant1 { get; set; }

        /// <summary>
        /// Second monster ID (em### format, 32-bit).
        /// </summary>
        public uint MonsterID2 { get; set; }

        /// <summary>
        /// Second monster variant.
        /// </summary>
        public uint MonsterVariant2 { get; set; }

        /// <summary>
        /// Monster stat table index.
        /// </summary>
        public uint MonsterStatTable { get; set; }

        /// <summary>
        /// Map zone override (0xFFFFFFFF = default map).
        /// </summary>
        public uint MapZoneOverride { get; set; }

        /// <summary>
        /// Spawn weighting for random selection.
        /// </summary>
        public uint SpawnWeighting { get; set; }

        /// <summary>
        /// Additional flag.
        /// </summary>
        public uint AdditionalFlag { get; set; }
    }

    /// <summary>
    /// Wrapper for JSON serialization of all rengoku data.
    /// </summary>
    public class RengokuData
    {
        public List<RengokuFloorStats> Floors { get; set; } = new();
        public List<RengokuSpawnEntry> Spawns { get; set; } = new();
    }
}
