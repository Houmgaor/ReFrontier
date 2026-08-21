using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FrontierDataTool.Offsets
{
    /// <summary>
    /// Reads and writes offsets as hex strings such as "0x6BD40".
    /// </summary>
    /// <remarks>
    /// Every offset in these files is quoted in hex everywhere else -- in the patterns, in
    /// the decompiler, in this project's own history -- and the misalignment behind the
    /// quest bug was a 0x20 that is obvious in hex and invisible as 441152 vs 441120.
    /// Plain JSON numbers are still accepted so a generated profile need not be rewritten.
    /// </remarks>
    public sealed class HexIntConverter : JsonConverter<int>
    {
        /// <inheritdoc/>
        public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number)
            {
                return reader.GetInt32();
            }

            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException(
                    $"Expected an offset as a hex string or a number, found {reader.TokenType}.");
            }

            string? text = reader.GetString();
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new JsonException("Expected an offset, found an empty string.");
            }

            text = text.Trim();
            bool hex = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
            string digits = hex ? text[2..] : text;

            if (int.TryParse(
                    digits,
                    hex ? NumberStyles.HexNumber : NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int value))
            {
                return value;
            }

            throw new JsonException($"'{text}' is not an offset: expected a hex string such as \"0x6BD40\".");
        }

        /// <inheritdoc/>
        public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(writer);
            writer.WriteStringValue($"0x{value:X}");
        }
    }
}
