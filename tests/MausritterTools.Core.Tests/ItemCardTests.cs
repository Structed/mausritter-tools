using System.Buffers.Binary;
using MausritterTools.Core.Data;
using MausritterTools.Core.Generation;
using MausritterTools.Core.Model;

namespace MausritterTools.Core.Tests;

/// <summary>
/// Covers the item card face: slot shape, usage dots, stats, artwork and how the name is fitted.
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
    // "Wielded in: Both paws", and paw slots sit side by side.
    [InlineData("Heavy")]
    [InlineData("Heavy ranged")]
    public void TwoPawedWeaponsAreTwoSlotsWide(string name) =>
        Assert.Equal(CardShape.Wide, ItemCard.ShapeFor(Item("weapons-and-armour", name)));

    [Theory]
    // Worn across stacked slots, which is why the official armour card template is one by two.
    [InlineData("Light armour")]
    [InlineData("Heavy armour")]
    public void ArmourIsTwoSlotsTall(string name) =>
        Assert.Equal(CardShape.Tall, ItemCard.ShapeFor(Item("weapons-and-armour", name)));

    [Theory]
    [InlineData("Heavy", 2)]
    [InlineData("Heavy ranged", 2)]
    [InlineData("Light armour", 2)]
    [InlineData("Heavy armour", 2)]
    [InlineData("Light", 1)]
    [InlineData("Medium", 1)]
    public void SlotCountFollowsTheShapeWhicheverWayItRuns(string name, int slots) =>
        Assert.Equal(slots, ItemCard.SlotsFor(Item("weapons-and-armour", name)));

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

    [Theory]
    [InlineData("Improvised", "d6")]
    [InlineData("Light", "d6")]
    [InlineData("Medium", "d6/d8")]
    [InlineData("Heavy", "d10")]
    [InlineData("Light ranged", "d6")]
    [InlineData("Heavy ranged", "d8")]
    public void WeaponsCarryTheDamageTheSrdGivesThem(string name, string damage) =>
        Assert.Equal(damage, ItemCard.StatFor(Item("weapons-and-armour", name), TestData.Game.Text));

    [Theory]
    [InlineData("Light armour")]
    [InlineData("Heavy armour")]
    public void ArmourCarriesItsDefenceAndSaysItInTheLoadedLanguage(string name)
    {
        GearItem armour = Item("weapons-and-armour", name);

        Assert.Equal("1 def", ItemCard.StatFor(armour, TestData.Game.Text));

        // A dice expression is a key and prints as it stands; the word beside a number is prose.
        UiText german = TestData.In(MausritterLocales.FromCode("de")).Text;
        Assert.NotEqual(ItemCard.StatFor(armour, TestData.Game.Text), ItemCard.StatFor(armour, german));
        Assert.Contains("1", ItemCard.StatFor(armour, german)!, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("tools-mouse-made", "Bedroll")]
    [InlineData("light-sources", "Torches")]
    public void OrdinaryGearHasNoStatBox(string category, string name) =>
        Assert.Null(ItemCard.StatFor(Item(category, name), TestData.Game.Text));

    [Theory]
    [InlineData("weapons-and-armour", "Medium")]
    [InlineData("weapons-and-armour", "Heavy armour")]
    [InlineData("light-sources", "Electric lantern")]
    [InlineData("lodging-and-food", "Travel rations")]
    public void GearTheOfficialSetDrawsGetsItsPicture(string category, string name) =>
        Assert.NotNull(ItemCard.ArtFor(Item(category, name)));

    [Fact]
    public void GearTheOfficialSetHasNoDrawingForStaysArtFree() =>
        Assert.Null(ItemCard.ArtFor(Item("tools-mouse-made", "Hourglass")));

    [Fact]
    public void ArtIsKeyedOnTheNameSoATranslatedCardKeepsItsPicture()
    {
        // The label is what the reader sees and changes per language; the name is the join key.
        GearItem translated = Item("weapons-and-armour", "Medium") with { Label = "Mittel" };

        Assert.Equal(ItemCard.ArtFor(Item("weapons-and-armour", "Medium")), ItemCard.ArtFor(translated));
    }

    [Fact]
    public void EveryDrawingTheCardRulesAskForIsActuallyShipped()
    {
        Assert.All(ItemCard.ArtFiles, file =>
            Assert.True(File.Exists(Path.Combine(ArtFolder, file)), $"Missing item card art '{file}'."));
    }

    [Fact]
    public void EveryTwoPawedWeaponIsDrawnStandingUpright()
    {
        // A two-pawed weapon's card runs across two slots while its drawing stands upright, so the
        // stylesheet lays the drawing along the card. That is only right while this holds.
        GearItem[] wide =
        [
            .. TestData.Game.Gear.Categories
                .SelectMany(category => category.Items)
                .Where(item => ItemCard.ShapeFor(item) == CardShape.Wide)
        ];

        Assert.NotEmpty(wide);
        Assert.All(wide, item =>
        {
            string? art = ItemCard.ArtFor(item);
            Assert.NotNull(art);

            (int width, int height) = PngSize(Path.Combine(ArtFolder, Path.GetFileName(art)));
            Assert.True(height > width, $"{art} is {width}x{height}, so it should not be laid down.");
        });
    }

    private static string ArtFolder => Path.Combine(
        TestData.DataRoot, "..", ItemCard.ArtFolder.Replace('/', Path.DirectorySeparatorChar));

    private static (int Width, int Height) PngSize(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);

        return (BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)),
            BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4)));
    }

    [Theory]
    [InlineData("weapons-and-armour", "Medium", "sword, axe, etc.")]
    [InlineData("weapons-and-armour", "Arrows", "quiver")]
    [InlineData("light-sources", "Electric lantern", "has six usage dots")]
    public void TheFootOfTheCardSaysWhatKindOfThingItIs(string category, string name, string line) =>
        Assert.Equal(line, ItemCard.ClassLineFor(Item(category, name)));

    [Fact]
    public void GearWithNothingToSayAboutItsKindHasNoClassLine() =>
        Assert.Null(ItemCard.ClassLineFor(Item("light-sources", "Torches")));

    [Theory]
    [InlineData("Oil")]
    [InlineData("Lantern")]
    public void AShortNameIsSetAtFullSize(string name) =>
        Assert.Equal(
            ItemCard.MaxTitleUnits,
            ItemCard.TitleSizeFor(Item("light-sources", name).DisplayLabel, CardShape.Single));

    [Fact]
    public void ALongNameIsSteppedDownUntilItFitsAboveTheRule()
    {
        // The German gear table runs far longer than the English one the official card was drawn
        // around, so the name has to give way rather than overrun the rule.
        double english = ItemCard.TitleSizeFor("Padlock and key", CardShape.Single);
        double german = ItemCard.TitleSizeFor("Vorhängeschloss mit Schlüssel", CardShape.Single);

        Assert.True(german < english, $"{german} should be smaller than {english}");
        Assert.True(german > 0);
    }

    [Fact]
    public void AWideCardHasRoomToKeepALongerNameLarge() =>
        Assert.True(
            ItemCard.TitleSizeFor("Zweipfotige Waffe", CardShape.Wide) >
            ItemCard.TitleSizeFor("Zweipfotige Waffe", CardShape.Single));

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
