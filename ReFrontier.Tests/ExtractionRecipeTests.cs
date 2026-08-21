using System;
using System.Text;

using LibReFrontier;

namespace ReFrontier.Tests
{
    /// <summary>
    /// Tests for <see cref="ExtractionRecipe"/> serialization.
    ///
    /// <para>The roundtrip tests matter more than they look: a recipe whose layers come
    /// back empty makes a restore silently skip compression and encryption, producing a
    /// file that looks plausible but is unusable in game.</para>
    /// </summary>
    public class ExtractionRecipeTests
    {
        [Fact]
        public void Serialize_Roundtrip_PreservesAllLayers()
        {
            var recipe = new ExtractionRecipe
            {
                SourceFile = "mhfdat.bin",
                ExtractedFile = "mhfdat.bin.decd.bin",
            };
            recipe.Layers.Add(new RecipeLayer
            {
                Kind = RecipeLayerKind.Ecd,
                MetaFile = "mhfdat.bin.meta",
                OriginalSize = 7383160,
            });
            recipe.Layers.Add(new RecipeLayer
            {
                Kind = RecipeLayerKind.Jpk,
                Algorithm = CompressionType.HFI,
                OriginalSize = 7383144,
            });

            var parsed = ExtractionRecipe.Deserialize(recipe.Serialize());

            Assert.NotNull(parsed);
            Assert.Equal(ExtractionRecipe.CurrentVersion, parsed.Version);
            Assert.Equal("mhfdat.bin", parsed.SourceFile);
            Assert.Equal("mhfdat.bin.decd.bin", parsed.ExtractedFile);
            Assert.Equal(2, parsed.Layers.Count);

            Assert.Equal(RecipeLayerKind.Ecd, parsed.Layers[0].Kind);
            Assert.Equal("mhfdat.bin.meta", parsed.Layers[0].MetaFile);
            Assert.Equal(7383160, parsed.Layers[0].OriginalSize);

            Assert.Equal(RecipeLayerKind.Jpk, parsed.Layers[1].Kind);
            Assert.Equal(CompressionType.HFI, parsed.Layers[1].Algorithm);
            Assert.Null(parsed.Layers[1].Level);
        }

        [Theory]
        [InlineData(CompressionType.RW)]
        [InlineData(CompressionType.HFIRW)]
        [InlineData(CompressionType.LZ)]
        [InlineData(CompressionType.HFI)]
        public void Serialize_Roundtrip_PreservesEveryAlgorithm(CompressionType algorithm)
        {
            var recipe = new ExtractionRecipe { SourceFile = "file.bin" };
            recipe.Layers.Add(new RecipeLayer { Kind = RecipeLayerKind.Jpk, Algorithm = algorithm });

            var parsed = ExtractionRecipe.Deserialize(recipe.Serialize());

            Assert.NotNull(parsed);
            Assert.Single(parsed.Layers);
            Assert.Equal(algorithm, parsed.Layers[0].Algorithm);
        }

        [Fact]
        public void Serialize_WritesAlgorithmAsName_NotNumber()
        {
            var recipe = new ExtractionRecipe { SourceFile = "file.bin" };
            recipe.Layers.Add(new RecipeLayer { Kind = RecipeLayerKind.Jpk, Algorithm = CompressionType.HFI });

            string json = Encoding.UTF8.GetString(recipe.Serialize());

            // Recipes are meant to be readable and hand-editable by modders.
            Assert.Contains("\"HFI\"", json, StringComparison.Ordinal);
            Assert.Contains("\"Jpk\"", json, StringComparison.Ordinal);
        }

        [Fact]
        public void Serialize_OmitsUnsetFields()
        {
            var recipe = new ExtractionRecipe { SourceFile = "file.bin" };
            recipe.Layers.Add(new RecipeLayer { Kind = RecipeLayerKind.Jpk, Algorithm = CompressionType.LZ });

            string json = Encoding.UTF8.GetString(recipe.Serialize());

            Assert.DoesNotContain("MetaFile", json, StringComparison.Ordinal);
            Assert.DoesNotContain("Level", json, StringComparison.Ordinal);
        }

        [Fact]
        public void Deserialize_InvalidJson_ReturnsNull()
        {
            Assert.Null(ExtractionRecipe.Deserialize(Encoding.UTF8.GetBytes("not a recipe at all")));
        }

        [Fact]
        public void Deserialize_EmptyLayers_ReturnsRecipeWithNoLayers()
        {
            byte[] json = Encoding.UTF8.GetBytes("""{"Version":1,"SourceFile":"a.bin","Layers":[]}""");

            var parsed = ExtractionRecipe.Deserialize(json);

            Assert.NotNull(parsed);
            Assert.Empty(parsed.Layers);
        }
    }
}
