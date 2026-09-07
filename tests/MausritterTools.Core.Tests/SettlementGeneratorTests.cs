using MausritterTools.Core.Data;
using MausritterTools.Core.Generation;
using MausritterTools.Core.Model;

namespace MausritterTools.Core.Tests;

public class SettlementGeneratorTests
{
    private static SettlementGenerator Generator() => new(TestData.Game);

    private static Settlement Generate(uint seed, int? size = null, bool nearHumanTown = false) =>
        Generator().Generate(new GenerationOptions
        {
            Seed = seed,
            Size = size,
            NearHumanTown = nearHumanTown
        });

    [Fact]
    public void SameSeedProducesAnIdenticalSettlement()
    {
        // The whole premise of a shareable seed URL.
        Settlement first = Generate(123456);
        Settlement second = Generate(123456);

        Assert.Equal(first.Name, second.Name);
        Assert.Equal(first.Size.Name, second.Size.Name);
        Assert.Equal(first.Governance, second.Governance);
        Assert.Equal(first.Event, second.Event);
        Assert.Equal(first.Host.Id, second.Host.Id);
        Assert.Equal(first.Shops.Count, second.Shops.Count);
        Assert.Equal(
            first.Shops.Select(s => (s.SignName, s.Keeper.FullName, s.Stock.Count)),
            second.Shops.Select(s => (s.SignName, s.Keeper.FullName, s.Stock.Count)));
    }

    [Fact]
    public void DifferentSeedsProduceDifferentSettlements()
    {
        string[] names = [.. Enumerable.Range(1, 40).Select(i => Generate((uint)i * 7919).Name)];

        Assert.True(names.Distinct().Count() > 20, $"Only {names.Distinct().Count()} distinct names in 40 rolls.");
    }

    [Fact]
    public void EveryGeneratedFieldIsPopulated()
    {
        for (uint seed = 1; seed <= 60; seed++)
        {
            Settlement settlement = Generate(seed);

            Assert.False(string.IsNullOrWhiteSpace(settlement.Name), $"seed {seed}: empty name");
            Assert.False(string.IsNullOrWhiteSpace(settlement.Governance), $"seed {seed}: empty governance");
            Assert.False(string.IsNullOrWhiteSpace(settlement.Inhabitants), $"seed {seed}: empty inhabitants");
            Assert.False(string.IsNullOrWhiteSpace(settlement.Event), $"seed {seed}: empty event");
            Assert.NotEmpty(settlement.NotableFeatures);
            Assert.NotEmpty(settlement.Industries);
            Assert.False(string.IsNullOrWhiteSpace(settlement.Host.Name), $"seed {seed}: empty host");
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void SizeOverrideIsRespected(int size) =>
        Assert.Equal(size, Generate(99, size).Size.SizeValue);

    [Fact]
    public void SizeOverrideIsClampedRatherThanThrowing()
    {
        Assert.Equal(1, Generate(1, -5).Size.SizeValue);
        Assert.Equal(6, Generate(1, 99).Size.SizeValue);
    }

    [Fact]
    public void UnforcedSizesFavourSmallSettlements()
    {
        // 2d6-take-lowest is the SRD's deliberate bias toward tiny places.
        int[] sizes = [.. Enumerable.Range(1, 600).Select(i => Generate((uint)i).Size.SizeValue)];

        double mean = sizes.Average();
        Assert.InRange(mean, 2.1, 3.0);
        Assert.True(
            sizes.Count(s => s == 1) > sizes.Count(s => s == 6),
            "Farms should outnumber cities.");
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(4, true)]
    [InlineData(5, true)]
    [InlineData(6, true)]
    public void TavernsAppearOnlyInHamletsAndLarger(int size, bool expectTavern)
    {
        for (uint seed = 1; seed <= 15; seed++)
        {
            Settlement settlement = Generate(seed, size);
            Assert.Equal(expectTavern, settlement.Tavern is not null);
        }
    }

    [Fact]
    public void TavernIsFullyDetailedWhenPresent()
    {
        Settlement settlement = Generate(4242, size: 5);

        Assert.NotNull(settlement.Tavern);
        Assert.StartsWith("The ", settlement.Tavern!.Name, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(settlement.Tavern.SpecialtyMeal));
        Assert.False(string.IsNullOrWhiteSpace(settlement.Tavern.Keeper.FullName));
    }

    [Theory]
    [InlineData(4, 1, 1)]
    [InlineData(5, 1, 2)]
    [InlineData(6, 2, 2)]
    public void MultiplicityRulesFollowTheSrd(int size, int features, int industries)
    {
        Settlement settlement = Generate(777, size);

        Assert.Equal(features, settlement.NotableFeatures.Count);
        Assert.Equal(industries, settlement.Industries.Count);
        Assert.Equal(features, settlement.NotableFeatures.Distinct().Count());
        Assert.Equal(industries, settlement.Industries.Distinct().Count());
    }

    [Fact]
    public void GovernanceRollAlwaysResolvesWithinRange()
    {
        for (int size = 1; size <= 6; size++)
        {
            for (uint seed = 1; seed <= 40; seed++)
            {
                Settlement settlement = Generate(seed, size);

                Assert.InRange(settlement.GovernanceRoll, 2, 12);
                Assert.InRange(settlement.GovernanceRoll, size + 1, size + 6);
                Assert.False(string.IsNullOrWhiteSpace(settlement.Governance));
            }
        }
    }

    [Fact]
    public void ShopCountStaysWithinTheRangeForItsSize()
    {
        foreach (ShopCountRange range in TestData.Game.Services.ShopCountBySize)
        {
            for (uint seed = 1; seed <= 30; seed++)
            {
                Settlement settlement = Generate(seed, range.SizeValue, nearHumanTown: true);

                Assert.InRange(settlement.Shops.Count, 0, range.Max);
            }
        }
    }

    [Fact]
    public void BiggerSettlementsHaveMoreShops()
    {
        double AverageShops(int size) =>
            Enumerable.Range(1, 40).Average(i => Generate((uint)i, size, nearHumanTown: true).Shops.Count);

        Assert.True(AverageShops(6) > AverageShops(3), "Cities should out-shop hamlets.");
        Assert.True(AverageShops(3) > AverageShops(1), "Hamlets should out-shop farms.");
    }

    [Fact]
    public void ShopsNeverBreachTheirMinimumSize()
    {
        for (int size = 1; size <= 6; size++)
        {
            for (uint seed = 1; seed <= 30; seed++)
            {
                foreach (Shop shop in Generate(seed, size, nearHumanTown: true).Shops)
                {
                    Assert.True(
                        shop.Service.MinSize <= size,
                        $"'{shop.Service.Id}' needs size {shop.Service.MinSize} but appeared at size {size}.");
                }
            }
        }
    }

    [Fact]
    public void HumanMadeGoodsRequireAHumanTownNearby()
    {
        // The SRD gates human-made goods on geography, not settlement size.
        for (uint seed = 1; seed <= 60; seed++)
        {
            foreach (Shop shop in Generate(seed, 6, nearHumanTown: false).Shops)
            {
                Assert.False(
                    shop.Service.RequiresHumanTown,
                    $"'{shop.Service.Id}' appeared without a human town nearby.");
            }
        }
    }

    [Fact]
    public void ScavengersStallCanAppearWhenAHumanTownIsNearby()
    {
        bool seen = Enumerable.Range(1, 80)
            .Select(i => Generate((uint)i, 4, nearHumanTown: true))
            .SelectMany(s => s.Shops)
            .Any(s => s.Service.Id == "scavengers-stall");

        Assert.True(seen, "A scavenger's stall should be reachable near a human town.");
    }

    [Fact]
    public void ShopsDoNotRepeatServicesThatDisallowDuplicates()
    {
        for (uint seed = 1; seed <= 40; seed++)
        {
            Settlement settlement = Generate(seed, 6, nearHumanTown: true);

            string[] nonDuplicable =
            [
                .. settlement.Shops.Where(s => !s.Service.AllowDuplicates).Select(s => s.Service.Id)
            ];

            Assert.Equal(nonDuplicable.Length, nonDuplicable.Distinct().Count());
        }
    }

    [Fact]
    public void EveryShopIsNamedStaffedAndNumbered()
    {
        Settlement settlement = Generate(31337, 6, nearHumanTown: true);

        Assert.NotEmpty(settlement.Shops);
        Assert.Equal(
            Enumerable.Range(1, settlement.Shops.Count),
            settlement.Shops.Select(s => s.Index));

        foreach (Shop shop in settlement.Shops)
        {
            Assert.False(string.IsNullOrWhiteSpace(shop.SignName), $"{shop.Id} has no sign");
            Assert.False(string.IsNullOrWhiteSpace(shop.Quirk), $"{shop.Id} has no quirk");
            Assert.False(string.IsNullOrWhiteSpace(shop.Keeper.FullName), $"{shop.Id} has no keeper");
            Assert.False(string.IsNullOrWhiteSpace(shop.Keeper.Appearance));
            Assert.NotNull(shop.Keeper.Position.Name);

            if (!shop.Service.ServiceOnly)
            {
                Assert.NotEmpty(shop.Stock);
            }
        }
    }

    [Fact]
    public void StockPricesAreAdjustedButModifiersAreLeftAlone()
    {
        for (uint seed = 1; seed <= 40; seed++)
        {
            foreach (Shop shop in Generate(seed, 6, nearHumanTown: true).Shops)
            {
                foreach (StockEntry entry in shop.Stock)
                {
                    if (entry.Item.HasNumericPrice)
                    {
                        Assert.NotNull(entry.Pips);
                        Assert.True(entry.Pips >= 1, "Adjusted prices must stay at least 1 pip.");

                        // Prices may carry a rate suffix, e.g. "5p per hex" for transport hire or
                        // "1p per night" for a bunkhouse bed.
                        Assert.StartsWith($"{entry.Pips}p", entry.PriceText, StringComparison.Ordinal);
                    }
                    else
                    {
                        // "x10p" and "10%" are modifiers; scaling them would be nonsense.
                        Assert.Null(entry.Pips);
                        Assert.Equal(entry.Item.PriceText, entry.PriceText);
                    }
                }
            }
        }
    }

    [Fact]
    public void StockNeverRepeatsAnItemWithinAShop()
    {
        for (uint seed = 1; seed <= 40; seed++)
        {
            foreach (Shop shop in Generate(seed, 6, nearHumanTown: true).Shops)
            {
                string[] names = [.. shop.Stock.Select(s => s.DisplayName)];
                Assert.Equal(names.Length, names.Distinct().Count());
            }
        }
    }

    [Fact]
    public void ServiceOnlyShopsCarryTermsInsteadOfStock()
    {
        Shop? bank = Enumerable.Range(1, 80)
            .Select(i => Generate((uint)i, 6, nearHumanTown: true))
            .SelectMany(s => s.Shops)
            .FirstOrDefault(s => s.Service.Id == "bank");

        Assert.NotNull(bank);
        Assert.Empty(bank!.Stock);
        Assert.False(string.IsNullOrWhiteSpace(bank.Service.ServiceTerms));
    }

    [Fact]
    public void KeeperRelationshipsPointAtOtherKeepersInTheSameSettlement()
    {
        for (uint seed = 1; seed <= 40; seed++)
        {
            Settlement settlement = Generate(seed, 6, nearHumanTown: true);
            string[] keepers = [.. settlement.Shops.Select(s => s.Keeper.FullName)];

            foreach (Shop shop in settlement.Shops.Where(s => s.Keeper.RelatedTo is not null))
            {
                Assert.Contains(shop.Keeper.RelatedTo!, keepers);
                Assert.NotEqual(shop.Keeper.FullName, shop.Keeper.RelatedTo);
                Assert.False(string.IsNullOrWhiteSpace(shop.Keeper.RelationshipKind));
                Assert.NotNull(shop.Keeper.RelationshipSummary);
            }
        }
    }

    [Fact]
    public void KeeperPursesReflectSocialPosition()
    {
        // Noblemice roll d4 x 1000p; poor mice roll d6p. A noble should never be the poorer.
        List<MouseNpc> keepers =
        [
            .. Enumerable.Range(1, 60)
                .Select(i => Generate((uint)i, 6, nearHumanTown: true))
                .SelectMany(s => s.Shops.Select(shop => shop.Keeper))
        ];

        foreach (MouseNpc keeper in keepers)
        {
            Assert.True(keeper.Purse >= 0);
        }

        double poor = keepers.Where(k => k.Position.Name == "Poor").Select(k => (double)k.Purse).DefaultIfEmpty(0).Average();
        double noble = keepers.Where(k => k.Position.Name == "Noblemouse").Select(k => (double)k.Purse).DefaultIfEmpty(0).Average();

        if (noble > 0 && poor > 0)
        {
            Assert.True(noble > poor, "Noblemice should carry more pips than poor mice.");
        }
    }
}
