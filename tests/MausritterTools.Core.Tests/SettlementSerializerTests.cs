using MausritterTools.Core.Generation;
using MausritterTools.Core.Model;
using MausritterTools.Core.Randomness;
using MausritterTools.Core.Serialization;

namespace MausritterTools.Core.Tests;

public class SettlementSerializerTests
{
    private static SettlementGenerator Generator() => new(TestData.Game);

    private static (Settlement Settlement, GenerationOptions Options) Build(uint seed = 2468)
    {
        GenerationOptions options = new() { Seed = seed, Size = 5, NearHumanTown = true };
        return (Generator().Generate(options), options);
    }

    [Fact]
    public void ExportedFileRoundTripsToAnIdenticalSettlement()
    {
        (Settlement original, GenerationOptions options) = Build();

        string json = SettlementSerializer.ToJson(original, options);
        Settlement rebuilt = Generator().Generate(SettlementSerializer.FromJson(json));

        Assert.Equal(original.Name, rebuilt.Name);
        Assert.Equal(original.Seed, rebuilt.Seed);
        Assert.Equal(original.Size.Name, rebuilt.Size.Name);
        Assert.Equal(original.Governance, rebuilt.Governance);
        Assert.Equal(original.Event, rebuilt.Event);
        Assert.Equal(original.Host.Id, rebuilt.Host.Id);
        Assert.Equal(
            original.Shops.Select(s => (s.SignName, s.Keeper.FullName, s.Stock.Count)),
            rebuilt.Shops.Select(s => (s.SignName, s.Keeper.FullName, s.Stock.Count)));
    }

    [Fact]
    public void LocksAndHandEditsSurviveTheRoundTrip()
    {
        GenerationOptions options = new GenerationOptions { Seed = 99, Size = 4 }
            .WithPin("settlement/name", "Nibblewick")
            .WithPin("settlement/event", "The cheese has gone missing")
            .WithReroll("settlement/inhabitants");

        Settlement original = Generator().Generate(options);
        string json = SettlementSerializer.ToJson(original, options);

        GenerationOptions restored = SettlementSerializer.FromJson(json);
        Settlement rebuilt = Generator().Generate(restored);

        Assert.Equal("Nibblewick", rebuilt.Name);
        Assert.Equal("The cheese has gone missing", rebuilt.Event);
        Assert.Equal(original.Inhabitants, rebuilt.Inhabitants);
        Assert.Equal(1, restored.Rerolls["settlement/inhabitants"]);
    }

    [Fact]
    public void GenerationSettingsSurviveTheRoundTrip()
    {
        GenerationOptions options = new()
        {
            Seed = 4711,
            Size = 6,
            NearHumanTown = true,
            Terrain = "forest"
        };

        GenerationOptions restored = SettlementSerializer.FromJson(
            SettlementSerializer.ToJson(Generator().Generate(options), options));

        Assert.Equal(4711u, restored.Seed);
        Assert.Equal(6, restored.Size);
        Assert.True(restored.NearHumanTown);
        Assert.Equal("forest", restored.Terrain);
    }

    [Fact]
    public void ExportIsHumanReadable()
    {
        (Settlement settlement, GenerationOptions options) = Build();

        string json = SettlementSerializer.ToJson(settlement, options);

        // The snapshot exists so the file is worth something to a reader who has never used this
        // tool, so the settlement's actual content must be present as plain text.
        Assert.Contains(settlement.Name, json, StringComparison.Ordinal);
        Assert.Contains(settlement.Event, json, StringComparison.Ordinal);
        Assert.Contains(settlement.Shops[0].SignName, json, StringComparison.Ordinal);
        Assert.Contains(settlement.Shops[0].Keeper.FullName, json, StringComparison.Ordinal);
        Assert.Contains("\n", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportCarriesTheRequiredAttribution()
    {
        (Settlement settlement, GenerationOptions options) = Build();

        string json = SettlementSerializer.ToJson(settlement, options);

        Assert.Contains("Mausritter", json, StringComparison.Ordinal);
        Assert.Contains("Losing Games", json, StringComparison.Ordinal);
        Assert.Contains("CC BY 4.0", json, StringComparison.Ordinal);
        Assert.Contains("house rules", json, StringComparison.Ordinal);
    }

    [Fact]
    public void SeedIsExportedInItsShareableForm()
    {
        (Settlement settlement, GenerationOptions options) = Build(123456);

        Assert.Contains(
            $"\"seed\": \"{SeedCodec.Encode(123456)}\"",
            SettlementSerializer.ToJson(settlement, options),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"format\":\"something-else\",\"version\":1}")]
    public void FilesFromOtherToolsAreRejected(string json) =>
        Assert.Throws<SettlementFormatException>(() => SettlementSerializer.FromJson(json));

    [Fact]
    public void FilesFromNewerVersionsAreRejectedWithAClearMessage()
    {
        SettlementFormatException ex = Assert.Throws<SettlementFormatException>(
            () => SettlementSerializer.FromJson(
                $"{{\"format\":\"{SettlementDocument.FormatId}\",\"version\":99}}"));

        Assert.Contains("newer version", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MalformedJsonIsRejectedWithAClearMessage()
    {
        SettlementFormatException ex = Assert.Throws<SettlementFormatException>(
            () => SettlementSerializer.FromJson("{not json at all"));

        Assert.Contains("valid JSON", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyInputIsRejected(string? json) =>
        Assert.ThrowsAny<ArgumentException>(() => SettlementSerializer.FromJson(json!));

    [Fact]
    public void SuggestedFileNameIsSafeAndIdentifiable()
    {
        (Settlement settlement, _) = Build();

        string fileName = SettlementSerializer.SuggestFileName(settlement);

        Assert.EndsWith(".json", fileName, StringComparison.Ordinal);
        Assert.Contains(SeedCodec.Encode(settlement.Seed), fileName, StringComparison.Ordinal);
        Assert.DoesNotContain(' ', fileName);
        Assert.All(
            Path.GetInvalidFileNameChars(),
            invalid => Assert.DoesNotContain(invalid, fileName));
    }

    [Fact]
    public void SuggestedFileNameCopesWithAnAwkwardName()
    {
        Settlement settlement = new() { Name = "   ///   ", Seed = 7 };

        Assert.Equal("settlement-7.json", SettlementSerializer.SuggestFileName(settlement));
    }
}
