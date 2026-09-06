using MausritterTools.Core.Data;
using MausritterTools.Core.Generation;
using MausritterTools.Core.Model;

namespace MausritterTools.Core.Tests;

/// <summary>
/// Covers the item card face: slot shape and usage dots.
/// </summary>
/// <remarks>
/// These exist because a browser check found every weapon and light source rendering without usage
/// dots. The category was being reverse-looked-up from the item name after generation, which is
/// ambiguous, so it is now recorded on the stock entry when the shop is built.
/// </remarks>
public class ItemCardTests
{
    private static GearItem Item(string category, string name) =>
        TestData.Game.Gear.FindCategory(category)!.Items.First(i => i.Name == name);

    [Fact]
    public void MostItemsOccupyASingleSlot() =>
        Assert.Equal(CardShape.Single, ItemCard.ShapeFor(Item("tools-mouse-made", "Bedroll")));

    [Theory]
    // "Some larger items, such as two-handed weapons and armour take up two slots."
    [InlineData("Light armour")]
    [InlineData("Heavy armour")]
    [InlineData("Heavy")]
    [InlineData("Heavy ranged")]
    public void ArmourAndTwoHandedWeaponsAreTwoSlotsWide(string name) =>
        Assert.Equal(CardShape.Wide, ItemCard.ShapeFor(Item("weapons-and-armour", name)));

    [Theory]
    [InlineData("Light")]
    [InlineData("Medium")]
    [InlineData("Arrows")]
    public void OneHandedWeaponsAndAmmoStaySingleSlot(string name) =>
        Assert.Equal(CardShape.Single, ItemCard.ShapeFor(Item("weapons-and-armour", name)));

    [Theory]
    [InlineData("weapons-and-armour", "Light")]
    [InlineData("weapons-and-armour", "Medium")]
    [InlineData("weapons-and-armour", "Arrows")]
    [InlineData("light-sources", "Torches")]
    [InlineData("light-sources", "Lantern")]
    [InlineData("light-sources", "Oil")]
    public void WeaponsAndLightSourcesCarryThreeUsageDots(string category, string name) =>
        Assert.Equal(ItemCard.StandardUsageDots, ItemCard.UsageDotsFor(Item(category, name), category));

    [Fact]
    public void TheElectricLanternIsTheSixDotException()
    {
        // The SRD's one explicit exception to "most items have three usage dots".
        GearItem lantern = Item("light-sources", "Electric lantern");

        Assert.Equal(6, ItemCard.UsageDotsFor(lantern, "light-sources"));
        Assert.Equal(6, ItemCard.UsageDotsFor(lantern, categoryId: null));
    }

    [Fact]
    public void TravelRationsDepleteEvenThoughTheyAreNotAWeapon() =>
        Assert.Equal(
            ItemCard.StandardUsageDots,
            ItemCard.UsageDotsFor(Item("lodging-and-food", "Travel rations"), "lodging-and-food"));

    [Theory]
    [InlineData("tools-mouse-made", "Bucket")]
    [InlineData("tools-mouse-made", "Mirror")]
    [InlineData("clothing", "Winter cloak")]
    public void OrdinaryToolsHaveNoUsageDots(string category, string name) =>
        Assert.Equal(0, ItemCard.UsageDotsFor(Item(category, name), category));

    [Theory]
    [InlineData("lodging-and-food", "Bunkhouse bed")]
    [InlineData("lodging-and-food", "Hot bath")]
    [InlineData("transport-hire", "Rabbit wagon")]
    public void ServicesAreNotCardedAsObjects(string category, string name)
    {
        GearItem item = Item(category, name);

        Assert.False(ItemCard.IsCardable(item));
        Assert.Equal(0, ItemCard.UsageDotsFor(item, category));
    }

    [Fact]
    public void ModifierPricedEntriesAreNotCarded()
    {
        // "Repairs, 10%" and "Silvered weapons, x10p" are fees, not objects.
        Assert.False(ItemCard.IsCardable(Item("weapons-and-armour", "Repairs")));
        Assert.False(ItemCard.IsCardable(Item("weapons-and-armour", "Silvered weapons")));
    }

    [Fact]
    public void GeneratedStockCarriesItsCategory()
    {
        SettlementGenerator generator = new(TestData.Game);

        for (uint seed = 1; seed <= 40; seed++)
        {
            Settlement settlement = generator.Generate(new GenerationOptions
            {
                Seed = seed,
                Size = 6,
                NearHumanTown = true
            });

            foreach (Shop shop in settlement.Shops)
            {
                foreach (StockEntry entry in shop.Stock)
                {
                    Assert.False(string.IsNullOrEmpty(entry.CategoryId), $"{entry.DisplayName} lost its category");
                    Assert.NotNull(TestData.Game.Gear.FindCategory(entry.CategoryId));

                    // The recorded category must actually contain the item.
                    Assert.Contains(
                        TestData.Game.Gear.FindCategory(entry.CategoryId)!.Items,
                        i => i.DisplayName == entry.Item.DisplayName);
                }
            }
        }
    }

    [Fact]
    public void GeneratedWeaponsAndLightsRenderWithUsageDots()
    {
        // The exact failure seen in the browser: cards with no dots on a smith's and a chandler's
        // stock, because the category never reached the card.
        SettlementGenerator generator = new(TestData.Game);

        List<StockEntry> consumables = [];

        for (uint seed = 1; seed <= 60; seed++)
        {
            Settlement settlement = generator.Generate(new GenerationOptions
            {
                Seed = seed,
                Size = 6,
                NearHumanTown = true
            });

            consumables.AddRange(settlement.Shops
                .SelectMany(s => s.Stock)
                .Where(e => e.CategoryId is "weapons-and-armour" or "light-sources")
                .Where(e => ItemCard.IsCardable(e.Item)));
        }

        Assert.NotEmpty(consumables);
        Assert.All(consumables, e =>
            Assert.True(
                ItemCard.UsageDotsFor(e.Item, e.CategoryId) > 0,
                $"{e.DisplayName} from {e.CategoryId} rendered without usage dots"));
    }
}
