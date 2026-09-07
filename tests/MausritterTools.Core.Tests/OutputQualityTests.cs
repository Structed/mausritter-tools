using MausritterTools.Core.Data;
using MausritterTools.Core.Generation;
using MausritterTools.Core.Model;

namespace MausritterTools.Core.Tests;

/// <summary>
/// Guards the presentation details that make generated output usable at the table. Each of these
/// covers a specific flaw found by reading real generator output.
/// </summary>
public class OutputQualityTests
{
    private static Settlement Generate(uint seed, int? size = null, bool nearHumanTown = true) =>
        new SettlementGenerator(TestData.Game).Generate(new GenerationOptions
        {
            Seed = seed,
            Size = size,
            NearHumanTown = nearHumanTown
        });

    [Fact]
    public void HostDescriptionReadsAsASentence()
    {
        // Was "inside old farmhouse wall" before the article was added.
        foreach (HostObject host in TestData.Game.Hosts.Hosts)
        {
            string described = host.Describe();

            Assert.DoesNotContain("  ", described, StringComparison.Ordinal);
            Assert.False(
                described.EndsWith(' '),
                $"'{described}' has a trailing space.");
        }

        HostObject wall = TestData.Game.Hosts.Hosts.Single(h => h.Id == "old-farmhouse-wall");
        HostObject stump = TestData.Game.Hosts.Hosts.Single(h => h.Id == "hollow-tree-stump");
        HostObject furniture = TestData.Game.Hosts.Hosts.Single(h => h.Id == "dumped-furniture");

        Assert.Equal("inside an old farmhouse wall", wall.Describe());
        Assert.Equal("inside a hollow tree stump", stump.Describe());

        // A mass noun takes no article: "in dumped furniture", not "in a dumped furniture".
        Assert.Equal("in dumped furniture", furniture.Describe());
    }

    [Fact]
    public void SettlementSummaryReadsCleanly()
    {
        for (uint seed = 1; seed <= 40; seed++)
        {
            string summary = Generate(seed).Summary;

            Assert.EndsWith(".", summary, StringComparison.Ordinal);
            Assert.DoesNotContain(" a a", summary, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("  ", summary, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void RelationshipSummaryIsGrammaticalForEveryTableEntry()
    {
        // The SRD's relationship column mixes nouns ("Parent") with phrases ("Worked together"),
        // so "Worked together of Rush Thistledown" was the original bug.
        foreach (string kind in TestData.Game.Npc.Relationship)
        {
            string summary = MouseNpc.DescribeRelationship(kind, "Clove Pennywhistle", TestData.Grammar)!;

            Assert.StartsWith("Clove Pennywhistle: ", summary, StringComparison.Ordinal);
            Assert.DoesNotContain(" of ", summary, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void RelationshipsAreSpreadAcrossKeepersNotPiledOnOne()
    {
        // Originally every keeper could only relate to an earlier shop, so most pointed at shop 1.
        List<string> targets = [];

        for (uint seed = 1; seed <= 60; seed++)
        {
            Settlement settlement = Generate(seed, 6);
            if (settlement.Shops.Count < 4)
            {
                continue;
            }

            foreach (Shop shop in settlement.Shops.Where(s => s.Keeper.RelatedTo is not null))
            {
                int targetIndex = settlement.Shops
                    .First(s => s.Keeper.FullName == shop.Keeper.RelatedTo).Index;

                targets.Add($"{seed}:{targetIndex}");
            }
        }

        Assert.NotEmpty(targets);

        // No single shop should absorb the bulk of a settlement's relationships.
        var worst = targets
            .GroupBy(t => t.Split(':')[0])
            .Select(g => new { Seed = g.Key, TopShare = g.GroupBy(x => x).Max(x => x.Count()) / (double)g.Count() })
            .OrderByDescending(x => x.TopShare)
            .First();

        Assert.True(worst.TopShare < 0.9, $"Seed {worst.Seed} funnelled {worst.TopShare:P0} of relationships to one keeper.");
    }

    [Fact]
    public void ShopQuirksAreDistinctWithinASettlement()
    {
        for (uint seed = 1; seed <= 60; seed++)
        {
            Settlement settlement = Generate(seed, 6);

            string[] quirks = [.. settlement.Shops.Select(s => s.Quirk)];

            // The quirk table is larger than any settlement's shop count, so duplicates are
            // avoidable rather than inevitable.
            if (quirks.Length <= TestData.Game.Services.ShopQuirks.Count)
            {
                Assert.Equal(quirks.Length, quirks.Distinct(StringComparer.OrdinalIgnoreCase).Count());
            }
        }
    }

    [Fact]
    public void ShopSignsDoNotRepeatTheirLeadingWords()
    {
        // "The Stubborn Quench" next door to "The Stubborn Pestle" reads as a bug.
        for (uint seed = 1; seed <= 60; seed++)
        {
            Settlement settlement = Generate(seed, 6);

            string[] prefixes =
            [
                .. settlement.Shops
                    .Select(s => s.SignName)
                    .Select(sign => sign[..Math.Max(0, sign.LastIndexOf(' '))])
                    .Where(p => p.Length > 0)
            ];

            Assert.Equal(prefixes.Length, prefixes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }
    }

    [Fact]
    public void HirelingCountsComeFromTheSrdNumberColumn()
    {
        // Skilled help is scarce in Mausritter: a torchbearer rolls d6 but a blacksmith rolls d2.
        Dictionary<string, List<int>> observed = [];

        for (uint seed = 1; seed <= 120; seed++)
        {
            foreach (Shop shop in Generate(seed, 6).Shops.Where(s => s.Service.OffersHirelings))
            {
                foreach (StockEntry entry in shop.Stock.Where(e => e.Quantity is not null))
                {
                    if (!observed.TryGetValue(entry.Item.Name, out List<int>? counts))
                    {
                        counts = [];
                        observed[entry.Item.Name] = counts;
                    }

                    counts.Add(entry.Quantity!.Value);
                }
            }
        }

        Assert.NotEmpty(observed);

        foreach ((string name, List<int> counts) in observed)
        {
            Hireling? hireling = TestData.Game.Hirelings.Hirelings
                .FirstOrDefault(h => string.Equals(h.Name, name, StringComparison.OrdinalIgnoreCase));

            if (hireling is null || !hireling.Number.StartsWith('d'))
            {
                continue;
            }

            int sides = int.Parse(hireling.Number[1..]);

            Assert.True(
                counts.Max() <= sides,
                $"'{name}' should never exceed {hireling.Number}, but {counts.Max()} were available.");
        }
    }

    [Fact]
    public void TransportPricesAreQuotedPerHex()
    {
        // The SRD prices transport hire per mouse, per hex; a bare pip figure would mislead.
        Shop? stables = Enumerable.Range(1, 120)
            .Select(i => Generate((uint)i, 6))
            .SelectMany(s => s.Shops)
            .FirstOrDefault(s => s.Service.PricesPerHex && s.Stock.Count > 0);

        Assert.NotNull(stables);
        Assert.All(stables!.Stock, e => Assert.Contains("per hex", e.PriceText, StringComparison.Ordinal));
    }

    [Fact]
    public void LodgingPricesKeepTheirNightlyRate()
    {
        Shop? victualler = Enumerable.Range(1, 120)
            .Select(i => Generate((uint)i, 4))
            .SelectMany(s => s.Shops)
            .FirstOrDefault(s => s.Stock.Any(e => e.Item.Name == "Bunkhouse bed"));

        Assert.NotNull(victualler);

        StockEntry bed = victualler!.Stock.First(e => e.Item.Name == "Bunkhouse bed");
        Assert.Contains("per night", bed.PriceText, StringComparison.Ordinal);
    }

    [Fact]
    public void NoGeneratedTextLeaksPlaceholdersOrStrayWhitespace()
    {
        for (uint seed = 1; seed <= 40; seed++)
        {
            Settlement settlement = Generate(seed, 6);

            List<string> texts =
            [
                settlement.Name, settlement.Governance, settlement.Inhabitants,
                settlement.Event, settlement.Summary,
                .. settlement.NotableFeatures, .. settlement.Industries,
                .. settlement.Shops.SelectMany(s => new[]
                {
                    s.SignName, s.Quirk, s.Keeper.FullName, s.Keeper.Appearance, s.Keeper.Wants
                })
            ];

            foreach (string text in texts)
            {
                Assert.False(string.IsNullOrWhiteSpace(text));
                Assert.DoesNotContain('{', text);
                Assert.DoesNotContain('}', text);
                Assert.Equal(text.Trim(), text);
            }
        }
    }
}
