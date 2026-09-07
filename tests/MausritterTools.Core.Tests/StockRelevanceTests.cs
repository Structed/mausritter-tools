using MausritterTools.Core.Data;
using MausritterTools.Core.Generation;
using MausritterTools.Core.Model;

namespace MausritterTools.Core.Tests;

/// <summary>
/// Checks that specialist shops stock things that belong in them. The SRD's mouse-made tools are
/// one undifferentiated list of hardware, so a shop drawing from it freely ends up with an
/// apothecary selling wooden poles.
/// </summary>
public class StockRelevanceTests
{
    private static IEnumerable<Shop> ShopsOfType(string serviceId, int settlements = 120)
    {
        SettlementGenerator generator = new(TestData.Game);

        return Enumerable.Range(1, settlements)
            .Select(i => generator.Generate(new GenerationOptions
            {
                Seed = (uint)i,
                Size = 6,
                NearHumanTown = true
            }))
            .SelectMany(s => s.Shops)
            .Where(s => s.Service.Id == serviceId);
    }

    [Theory]
    [InlineData("apothecary")]
    [InlineData("scriptorium")]
    [InlineData("curio-dealer")]
    public void CuratedShopsOnlyStockTheirAllowedItems(string serviceId)
    {
        ServiceDefinition service = TestData.Game.Services.Services.Single(s => s.Id == serviceId);

        Assert.NotEmpty(service.Stock.ItemNames);

        Shop[] shops = [.. ShopsOfType(serviceId)];
        Assert.NotEmpty(shops);

        foreach (Shop shop in shops)
        {
            foreach (StockEntry entry in shop.Stock)
            {
                Assert.Contains(entry.Item.Name, service.Stock.ItemNames, StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void ApothecaryNeverSellsHardware()
    {
        string[] wrong = ["Wooden pole", "Crowbar", "Shovel", "Caltrops", "Book", "Chalk", "Pick"];

        foreach (Shop shop in ShopsOfType("apothecary"))
        {
            foreach (StockEntry entry in shop.Stock)
            {
                Assert.DoesNotContain(entry.Item.Name, wrong, StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void ScriptoriumSellsBooksNotShovels()
    {
        Shop[] shops = [.. ShopsOfType("scriptorium")];
        Assert.NotEmpty(shops);

        Assert.Contains(shops, s => s.Stock.Any(e => e.Item.Name == "Book"));
        Assert.DoesNotContain(shops, s => s.Stock.Any(e => e.Item.Name == "Shovel"));
    }

    [Fact]
    public void CuratedShopsStillFillTheirShelves()
    {
        // An allow-list that is too narrow would leave a shop nearly empty.
        foreach (string serviceId in new[] { "apothecary", "scriptorium", "curio-dealer" })
        {
            ServiceDefinition service = TestData.Game.Services.Services.Single(s => s.Id == serviceId);

            foreach (Shop shop in ShopsOfType(serviceId))
            {
                Assert.InRange(shop.Stock.Count, Math.Min(2, service.Stock.MinItems), service.Stock.MaxItems);
            }
        }
    }

    [Fact]
    public void OnlyHumanTownGatedShopsSellHumanMadeGoods()
    {
        // The SRD makes human-made goods available only "in mouse settlements near human
        // populations", so a service that stocks them must itself require a human town.
        foreach (ServiceDefinition service in TestData.Game.Services.Services)
        {
            if (service.Stock.Categories.Contains("tools-human-made"))
            {
                Assert.True(
                    service.RequiresHumanTown,
                    $"'{service.Id}' stocks human-made goods but does not require a human town.");
            }
        }

        // Compared on the full display name, and only for items unique to the human-made list:
        // a padlock appears in both lists, distinguished solely by its "large" qualifier.
        HashSet<string> mouseMade =
        [
            .. TestData.Game.Gear.FindCategory("tools-mouse-made")!.Items.Select(i => i.DisplayName)
        ];

        HashSet<string> humanMadeOnly =
        [
            .. TestData.Game.Gear.FindCategory("tools-human-made")!
                .Items.Select(i => i.DisplayName)
                .Where(name => !mouseMade.Contains(name))
        ];

        Assert.NotEmpty(humanMadeOnly);

        SettlementGenerator generator = new(TestData.Game);

        for (uint seed = 1; seed <= 60; seed++)
        {
            Settlement settlement = generator.Generate(new GenerationOptions
            {
                Seed = seed,
                Size = 6,
                NearHumanTown = false
            });

            foreach (StockEntry entry in settlement.Shops.SelectMany(s => s.Stock))
            {
                Assert.DoesNotContain(entry.DisplayName, humanMadeOnly);
            }
        }
    }

    [Fact]
    public void EveryServiceThatSellsAnythingProducesStock()
    {
        SettlementGenerator generator = new(TestData.Game);

        HashSet<string> sawStock = [];
        HashSet<string> sawShop = [];

        for (uint seed = 1; seed <= 150; seed++)
        {
            Settlement settlement = generator.Generate(new GenerationOptions
            {
                Seed = seed,
                Size = 6,
                NearHumanTown = true
            });

            foreach (Shop shop in settlement.Shops)
            {
                sawShop.Add(shop.Service.Id);
                if (shop.Stock.Count > 0)
                {
                    sawStock.Add(shop.Service.Id);
                }
            }
        }

        foreach (ServiceDefinition service in TestData.Game.Services.Services.Where(s => !s.ServiceOnly))
        {
            if (sawShop.Contains(service.Id))
            {
                Assert.Contains(service.Id, sawStock);
            }
        }
    }
}
