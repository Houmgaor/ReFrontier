using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace FrontierDataTool.Offsets
{
    /// <summary>
    /// Finds offset profiles: the ones built into the executable, and ones given as files.
    /// </summary>
    public static class OffsetProfiles
    {
        private const string ResourcePrefix = "FrontierDataTool.Offsets.Profiles.";

        /// <summary>
        /// Id of the profile used when none is named, matching the versions the tool
        /// has always read.
        /// </summary>
        public const string DefaultId = "zz";

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        private static readonly Lazy<List<OffsetProfile>> BuiltInProfiles =
            new(LoadBuiltIn);

        /// <summary>
        /// Every profile embedded in the executable, ordered by id.
        /// </summary>
        public static IReadOnlyList<OffsetProfile> BuiltIn => BuiltInProfiles.Value;

        /// <summary>
        /// The profile used when none is named.
        /// </summary>
        public static OffsetProfile Default =>
            BuiltIn.FirstOrDefault(p => p.Id == DefaultId)
            ?? throw new InvalidOperationException(
                $"The built-in '{DefaultId}' offset profile is missing from the executable.");

        /// <summary>
        /// Read a profile from a JSON file.
        /// </summary>
        /// <param name="path">Path to the profile.</param>
        /// <returns>The profile it describes.</returns>
        /// <exception cref="InvalidOperationException">The file is not a usable profile.</exception>
        public static OffsetProfile Load(string path)
        {
            ArgumentNullException.ThrowIfNull(path);

            string json;
            try
            {
                json = File.ReadAllText(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new InvalidOperationException($"Cannot read the offset profile '{path}': {ex.Message}", ex);
            }

            var profile = Parse(json, path);
            OffsetProfileValidator.ThrowIfInvalid(profile, $"'{path}'");
            return profile;
        }

        /// <summary>
        /// Find a built-in profile by id.
        /// </summary>
        /// <param name="id">Profile id, as written in its file name.</param>
        /// <returns>The profile, or null when no built-in one has that id.</returns>
        public static OffsetProfile? FindBuiltIn(string id) =>
            BuiltIn.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Resolve what the user asked for: a built-in id, or a path to a profile.
        /// </summary>
        /// <param name="idOrPath">A built-in profile's id, or the path to a JSON profile.</param>
        /// <returns>The profile named.</returns>
        /// <exception cref="InvalidOperationException">Neither an id nor a readable file.</exception>
        public static OffsetProfile Resolve(string idOrPath)
        {
            ArgumentNullException.ThrowIfNull(idOrPath);

            var builtIn = FindBuiltIn(idOrPath);
            if (builtIn is not null)
            {
                return builtIn;
            }

            if (File.Exists(idOrPath))
            {
                return Load(idOrPath);
            }

            throw new InvalidOperationException(
                $"'{idOrPath}' is neither a built-in offset profile nor a file that exists. " +
                $"Built-in profiles: {string.Join(", ", BuiltIn.Select(p => p.Id))}.");
        }

        private static OffsetProfile Parse(string json, string source)
        {
            OffsetProfile? profile;
            try
            {
                profile = JsonSerializer.Deserialize<OffsetProfile>(json, SerializerOptions);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    $"The offset profile in {source} is not valid JSON: {ex.Message}", ex);
            }

            return profile ?? throw new InvalidOperationException($"The offset profile in {source} is empty.");
        }

        private static List<OffsetProfile> LoadBuiltIn()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var profiles = new List<OffsetProfile>();

            foreach (string name in assembly.GetManifestResourceNames()
                         .Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal)
                                     && n.EndsWith(".json", StringComparison.Ordinal)))
            {
                using var stream = assembly.GetManifestResourceStream(name)
                    ?? throw new InvalidOperationException($"Embedded profile '{name}' cannot be opened.");
                using var reader = new StreamReader(stream);

                var profile = Parse(reader.ReadToEnd(), $"the built-in profile '{name}'");
                OffsetProfileValidator.ThrowIfInvalid(profile, $"the built-in profile '{name}'");
                profiles.Add(profile);
            }

            return profiles
                .OrderBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// One line per built-in profile, for --help and for the message shown when
        /// nothing matches a file.
        /// </summary>
        /// <returns>A description of every built-in profile.</returns>
        public static string DescribeBuiltIn() =>
            string.Join(
                Environment.NewLine,
                BuiltIn.Select(p => string.Format(
                    CultureInfo.InvariantCulture, "  {0,-8} {1}", p.Id, p.Description)));
    }
}
