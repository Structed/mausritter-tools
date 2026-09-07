using MausritterTools.Core.Data;

namespace MausritterTools.Core.Tests;

/// <summary>
/// Loads the JSON the app actually ships and asserts against the real Mausritter tables. These
/// double as a regression check on the SRD importer: if a re-import drops or mangles a table, the
/// counts here fail rather than the app quietly generating a poorer settlement.
/// </summary>
public class GameDataTests
{
    [Fact]
    public async Task ShippedDataLoadsAndValidates()
    {
        GameData data = await TestData.LoadAsync();

        Assert.NotNull(data.Settlement);
        Assert.NotNull(data.Services);
    }

    [Fact]
    public void SettlementSizeTableMatchesTheSrd()
    {
        IReadOnlyList<SettlementSize> sizes = TestData.Game.Settlement.Sizes;

        Assert.Equal(6, sizes.Count);
        Assert.Equal(
            ["Farm/manor", "Crossroads", "Hamlet", "Village", "Town", "City"],
            sizes.Select(s => s.Name));
    }

    [Theory]
    // "Hamlets and larger settlements will furnish a friendly tavern or inn."
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(6, true)]
    public void TavernAvailabilityFollowsTheSrdProse(int sizeValue, bool expected) =>
        Assert.Equal(expected, TestData.Game.Settlement.Sizes.Single(s => s.SizeValue == sizeValue).HasTavern);

    [Theory]
    // "What feature sets this settlement apart? Cities have two."
    [InlineData(1, 1)]
    [InlineData(5, 1)]
    [InlineData(6, 2)]
    public void OnlyCitiesGetTwoNotableFeatures(int sizeValue, int expected) =>
        Assert.Equal(expected, TestData.Game.Settlement.Sizes.Single(s => s.SizeValue == sizeValue).FeatureCount);

    [Theory]
    // "What trade do the mice work? Towns and cities have two."
    [InlineData(1, 1)]
    [InlineData(4, 1)]
    [InlineData(5, 2)]
    [InlineData(6, 2)]
    public void TownsAndCitiesGetTwoIndustries(int sizeValue, int expected) =>
        Assert.Equal(expected, TestData.Game.Settlement.Sizes.Single(s => s.SizeValue == sizeValue).IndustryCount);

    [Fact]
    public void GovernanceCoversEveryReachableTotalExactlyOnce()
    {
        // Governance is rolled as d6 + settlement size, so totals run from 2 to 12.
        for (int roll = 2; roll <= 12; roll++)
        {
            Assert.Equal(1, TestData.Game.Settlement.Governance.Count(g => g.Contains(roll)));
        }
    }

    [Fact]
    public void DetailTablesAreFullTwentyEntryTables()
    {
        SettlementTables settlement = TestData.Game.Settlement;

        Assert.Equal(20, settlement.Inhabitants.Count);
        Assert.Equal(20, settlement.NotableFeatures.Count);
        Assert.Equal(20, settlement.Industries.Count);
        Assert.Equal(20, settlement.Events.Count);
    }

    [Fact]
    public void NameSeedAndTavernColumnsAreAllTwelveEntries()
    {
        SettlementTables settlement = TestData.Game.Settlement;

        Assert.Equal(12, settlement.NameSeeds.StartA.Count);
        Assert.Equal(12, settlement.NameSeeds.StartB.Count);
        Assert.Equal(12, settlement.NameSeeds.EndA.Count);
        Assert.Equal(12, settlement.NameSeeds.EndB.Count);

        Assert.Equal(12, settlement.Taverns.NameA.Count);
        Assert.Equal(12, settlement.Taverns.NameB.Count);
        Assert.Equal(12, settlement.Taverns.SpecialtyMeals.Count);
    }

    [Fact]
    public void NpcDetailColumnsAreAllTwentyEntries()
    {
        NpcTables npc = TestData.Game.Npc;

        Assert.Equal(20, npc.Appearance.Count);
        Assert.Equal(20, npc.Quirk.Count);
        Assert.Equal(20, npc.Wants.Count);
        Assert.Equal(20, npc.Relationship.Count);
        Assert.Equal(6, npc.SocialPositions.Count);
        Assert.Equal(6, npc.Birthsigns.Count);
    }

    [Fact]
    public void BirthsignsSplitIntoVirtueAndVice()
    {
        Birthsign star = TestData.Game.Npc.Birthsigns.Single(b => b.Name == "Star");

        Assert.Equal("Brave", star.Virtue);
        Assert.Equal("Reckless", star.Vice);
    }

    [Fact]
    public void GearHasAllEightPricedCategories() =>
        Assert.Equal(8, TestData.Game.Gear.Categories.Count);

    [Fact]
    public void MouseMadeToolsRetainTheirFullList() =>
        Assert.Equal(32, TestData.Game.Gear.FindCategory("tools-mouse-made")!.Items.Count);

    [Fact]
    public void PlainPricesAreParsedIntoPips()
    {
        GearItem bedroll = TestData.Game.Gear
            .FindCategory("tools-mouse-made")!
            .Items.Single(i => i.Name == "Bedroll");

        Assert.Equal(10, bedroll.Pips);
        Assert.True(bedroll.HasNumericPrice);
    }

    [Fact]
    public void ModifierPricesAreKeptAsTextRatherThanInventedNumbers()
    {
        // "x10p" for silvering and "10%" per repair dot are modifiers, not sums. Treating them as
        // numbers would put a 10 pip price tag on silvered weapons.
        IReadOnlyList<GearItem> weapons = TestData.Game.Gear.FindCategory("weapons-and-armour")!.Items;

        GearItem silvered = weapons.Single(i => i.Name.StartsWith("Silvered", StringComparison.Ordinal));
        GearItem repairs = weapons.Single(i => i.Name == "Repairs");

        Assert.Null(silvered.Pips);
        Assert.Equal("x10p", silvered.PriceText);
        Assert.False(silvered.HasNumericPrice);

        Assert.Null(repairs.Pips);
        Assert.Equal("10%", repairs.PriceText);
    }

    [Fact]
    public void QualifiersDistinguishItemVariants()
    {
        IReadOnlyList<GearItem> books =
        [
            .. TestData.Game.Gear.FindCategory("tools-mouse-made")!.Items.Where(i => i.Name == "Book")
        ];

        Assert.Equal(2, books.Count);
        Assert.Contains(books, b => b.DisplayName == "Book, blank" && b.Pips == 300);
        Assert.Contains(books, b => b.DisplayName == "Book, reading" && b.Pips == 600);
    }

    [Fact]
    public void RateParentheticalsBecomeStructuredUnits()
    {
        // "(per night)" is a rate rather than a description, so it is promoted to PerUnit and the
        // app can render "1p per night" instead of a bare "1p".
        GearItem bed = TestData.Game.Gear
            .FindCategory("lodging-and-food")!
            .Items.Single(i => i.Name == "Bunkhouse bed");

        Assert.Equal("night", bed.PerUnit);
        Assert.Null(bed.Note);
        Assert.Equal(1, bed.Pips);
    }

    [Fact]
    public void DescriptiveParentheticalsStayAsNotes()
    {
        GearItem improvised = TestData.Game.Gear
            .FindCategory("weapons-and-armour")!
            .Items.Single(i => i.Name == "Improvised");

        Assert.Equal("twig, rock, etc.", improvised.Note);
        Assert.Null(improvised.PerUnit);
    }

    [Fact]
    public void HirelingsAndSpellsImportCompletely()
    {
        Assert.Equal(9, TestData.Game.Hirelings.Hirelings.Count);
        Assert.Equal(15, TestData.Game.Spells.Spells.Count);
        Assert.Contains(TestData.Game.Spells.Spells, s => s.Name == "Magic Missile");
    }

    [Fact]
    public void EverySrdFileCarriesTheRequiredAttribution()
    {
        // The CC BY 4.0 grant is conditional on attribution, so a SRD-derived file without one is
        // a licensing problem, not merely a missing string.
        foreach (DataProvenance provenance in TestData.Game.Provenance.Where(p => p.IsSrdDerived))
        {
            Assert.False(string.IsNullOrWhiteSpace(provenance.Attribution));
            Assert.Contains("Mausritter", provenance.Attribution!, StringComparison.Ordinal);
            Assert.Contains("Losing Games", provenance.Attribution!, StringComparison.Ordinal);
            Assert.Equal("CC BY 4.0", provenance.Licence);
        }
    }

    [Fact]
    public void HouseRuleFilesDeclareThemselvesUnofficial()
    {
        // Mausritter has no services table, so this layer must never look official.
        Assert.Contains("House rule", TestData.Game.Services.Source.Status ?? "", StringComparison.OrdinalIgnoreCase);
        Assert.Contains("House rule", TestData.Game.Names.Source.Status ?? "", StringComparison.OrdinalIgnoreCase);
    }
}
