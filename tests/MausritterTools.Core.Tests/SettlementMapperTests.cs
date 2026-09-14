using MausritterTools.Core.Data;
using MausritterTools.Core.Generation;
using MausritterTools.Core.Mapping;
using MausritterTools.Core.Model;
using Structed.Inkwell.Mapping;

namespace MausritterTools.Core.Tests;

/// <summary>
/// Covers the decisions that turn a settlement into a map brief.
/// </summary>
/// <remarks>
/// The layout itself is the engine's and is tested there against a brief directly. What is left
/// here is what only Mausritter can answer: which silhouette the host object implies, what counts
/// as waterside, and what earns a number on the map.
/// </remarks>
public class SettlementMapperTests
{
    private static Settlement Settle(uint seed, int? size = null, bool nearHumanTown = true) =>
        new SettlementGenerator(TestData.Game).Generate(new GenerationOptions
        {
            Seed = seed,
            Size = size,
            NearHumanTown = nearHumanTown
        });

    [Fact]
    public void TheHostObjectDecidesTheSilhouetteAndTheLabel()
    {
        Settlement settlement = Settle(77, 4);
        PlaceMap map = SettlementMapper.Generate(settlement, 77);

        Assert.Equal(settlement.Host.Shape, map.Shape);
        Assert.Equal(settlement.Host.Name, map.Subject);
    }

    [Fact]
    public void TheSettlementSizeBecomesTheMapScale()
    {
        for (int size = 1; size <= 6; size++)
        {
            Assert.Equal(size, SettlementMapper.Brief(Settle(5, size)).Scale);
        }
    }

    [Fact]
    public void ShopsAndTavernAreKeyedToBuildings()
    {
        Settlement settlement = Settle(31337, 6);
        PlaceMap map = SettlementMapper.Generate(settlement, 31337);

        int expected = settlement.Shops.Count + (settlement.Tavern is null ? 0 : 1);
        int keyed = map.Buildings.Count(b => b.IsKeyed);

        Assert.True(keyed > 0, "Nothing was keyed to the map.");
        Assert.Equal(keyed, map.Legend.Count);
        Assert.Equal(Math.Min(expected, map.Buildings.Count), keyed);
    }

    [Fact]
    public void TheTavernIsKeyedFirstSoItLandsOnTheMainStreet()
    {
        for (uint seed = 1; seed <= 60; seed++)
        {
            Settlement settlement = Settle(seed, 6);
            if (settlement.Tavern is not { } tavern)
            {
                continue;
            }

            Assert.Equal(tavern.Name, SettlementMapper.Brief(settlement).Keys[0].Name);
        }
    }

    [Fact]
    public void EveryLegendEntryIsFilledIn()
    {
        for (uint seed = 1; seed <= 20; seed++)
        {
            PlaceMap map = SettlementMapper.Generate(Settle(seed, 6), seed);

            Assert.Equal(Enumerable.Range(1, map.Legend.Count), map.Legend.Select(l => l.Key));

            foreach (MapLegendEntry entry in map.Legend)
            {
                Assert.False(string.IsNullOrWhiteSpace(entry.Name));
                Assert.False(string.IsNullOrWhiteSpace(entry.Detail));
                Assert.DoesNotContain('{', entry.Detail);
            }
        }
    }

    [Fact]
    public void WaterAppearsWhenTheSettlementImpliesIt()
    {
        // Fishermice, a water-wheel mill or a riverboat dock should put water on the map.
        bool sawWater = false;
        bool sawDryLand = false;

        for (uint seed = 1; seed <= 120; seed++)
        {
            Settlement settlement = Settle(seed, 5);
            PlaceMap map = SettlementMapper.Generate(settlement, seed);

            bool implied = settlement.Industries
                .Concat(settlement.NotableFeatures)
                .Any(t =>
                    t.Contains("fishermice", StringComparison.OrdinalIgnoreCase) ||
                    t.Contains("water-wheel", StringComparison.OrdinalIgnoreCase) ||
                    t.Contains("riverboat", StringComparison.OrdinalIgnoreCase));

            if (implied)
            {
                Assert.NotNull(map.Water);
                sawWater = true;
            }
            else if (map.Water is null)
            {
                sawDryLand = true;
            }
        }

        Assert.True(sawWater, "Never generated a settlement whose trade implies water.");
        Assert.True(sawDryLand, "Every settlement had water, which cannot be right.");
    }

    [Fact]
    public void LegendPhrasingFollowsTheLanguage()
    {
        Settlement settlement = Settle(2468, 6);

        MapBrief english = SettlementMapper.Brief(settlement, TestData.Grammar);
        MapBrief german = SettlementMapper.Brief(settlement, TestData.In(MausritterLocales.German).Text.Grammar);

        Assert.Equal(english.Keys.Count, german.Keys.Count);
        Assert.NotEqual(
            english.Keys.Select(k => k.Detail),
            german.Keys.Select(k => k.Detail));
    }
}
