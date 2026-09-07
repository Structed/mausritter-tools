using MausritterTools.Core.Data;
using MausritterTools.Core.Model;
using MausritterTools.Core.Randomness;

namespace MausritterTools.Core.Generation;

/// <summary>
/// Generates a complete mouse settlement.
/// </summary>
/// <remarks>
/// Generation is deterministic: the same <see cref="GenerationOptions"/> always produce the same
/// settlement, which is what lets a settlement be shared as a short seed in a URL.
/// </remarks>
public sealed class SettlementGenerator(GameData data)
{
    private readonly GameData _data = data ?? throw new ArgumentNullException(nameof(data));

    public Settlement Generate(GenerationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        RollContext context = new(options);
        SettlementTables tables = _data.Settlement;
        GrammarText grammar = _data.Text.Grammar;

        SettlementSize size = RollSize(context, options);
        HostObject host = RollHost(context, options);

        int governanceRoll = RollGovernance(context, size.SizeValue);

        IReadOnlyList<Shop> shops = ShopGenerator.Generate(
            _data, context, size.SizeValue, options.NearHumanTown);

        shops = LinkKeepers(context, shops);

        return new Settlement
        {
            Seed = options.Seed,
            Name = RollName(context, tables, grammar),
            Size = size,
            Host = host,
            NearHumanTown = options.NearHumanTown,
            Governance = LookUpGovernance(context, governanceRoll),
            GovernanceRoll = governanceRoll,
            Inhabitants = context.Text("settlement/inhabitants", tables.Inhabitants),
            NotableFeatures = context.TextMany("settlement/features", tables.NotableFeatures, size.FeatureCount),
            Industries = context.TextMany("settlement/industries", tables.Industries, size.IndustryCount),
            Event = context.Text("settlement/event", tables.Events),
            Tavern = size.HasTavern ? RollTavern(context, grammar) : null,
            Shops = shops,
            Summary = Summarise(size, host, grammar),
            PinValues = context.PinValues
        };
    }

    /// <summary>
    /// Writes the one-line description at the top of the sheet, e.g. "A village of 150-300 mice,
    /// inside a hollow tree stump."
    /// </summary>
    /// <remarks>
    /// Every part of this sentence varies by language: whether the article is "A" or "Ein", whether
    /// the size noun keeps its capital, and where the host phrase sits. All of it therefore comes
    /// from the loaded text rather than from an interpolated string here.
    /// </remarks>
    private static string Summarise(SettlementSize size, HostObject host, GrammarText grammar)
    {
        string sizeName = grammar.LowercaseInlineNouns ? size.Name.ToLowerInvariant() : size.Name;

        string pattern = size.Population is null
            ? grammar.SummaryWithoutPopulation
            : grammar.SummaryWithPopulation;

        return TextTemplate.Format(
            pattern,
            ("article", ArticleTables.For(grammar.Articles.IndefiniteNominative, size.NameGender)),
            ("size", sizeName),
            ("population", size.Population),
            ("host", host.Describe()));
    }

    private string RollName(RollContext context, SettlementTables tables, GrammarText grammar)
    {
        if (context.TryGetPin("settlement/name", out string pinned))
        {
            context.Record("settlement/name", pinned);
            return pinned;
        }

        // A settlement's name is built rather than drawn from a row, so there is no position to
        // record: locking one stores the name itself.
        string name = NameForge.SettlementName(context.Dice("settlement/name"), tables.NameSeeds, grammar);
        context.Record("settlement/name", name);

        return name;
    }

    /// <summary>
    /// Rolls the settlement size, honouring an explicit override.
    /// </summary>
    /// <remarks>
    /// Mausritter rolls 2d6 and keeps the lower value, deliberately biasing toward the smallest
    /// settlements: "most mouse settlements are no more than a handful of families in an oak
    /// hollow or in an old farmhouse wall".
    /// </remarks>
    private SettlementSize RollSize(RollContext context, GenerationOptions options)
    {
        IReadOnlyList<SettlementSize> sizes = _data.Settlement.Sizes;
        if (sizes.Count == 0)
        {
            return new SettlementSize();
        }

        if (options.Size is { } forced)
        {
            int index = IndexOfSizeValue(sizes, Math.Clamp(forced, 1, sizes.Count));
            context.Record("settlement/size", PinReference.ForIndex(index));

            return sizes[index];
        }

        if (context.TryGetPin("settlement/size", out string pinned))
        {
            context.Record("settlement/size", pinned);

            // Older exports pinned the size by name; a position is preferred but both must open.
            SettlementSize? byPin = PinReference.TryGetIndex(pinned, out int pinnedIndex) && pinnedIndex < sizes.Count
                ? sizes[pinnedIndex]
                : sizes.FirstOrDefault(s => s.Name == pinned);

            if (byPin is not null)
            {
                return byPin;
            }
        }

        int rolled = context.Dice("settlement/size").RollLowestOfTwo(sizes.Count);
        int rolledIndex = IndexOfSizeValue(sizes, rolled);

        context.Record("settlement/size", PinReference.ForIndex(rolledIndex));
        return sizes[rolledIndex];
    }

    /// <summary>Finds the row for a size value, falling back to the largest as the table did.</summary>
    private static int IndexOfSizeValue(IReadOnlyList<SettlementSize> sizes, int sizeValue)
    {
        for (int i = 0; i < sizes.Count; i++)
        {
            if (sizes[i].SizeValue == sizeValue)
            {
                return i;
            }
        }

        return sizes.Count - 1;
    }

    private HostObject RollHost(RollContext context, GenerationOptions options)
    {
        IReadOnlyList<HostObject> candidates = _data.Hosts.ForTerrain(options.Terrain);
        if (candidates.Count == 0)
        {
            return new HostObject();
        }

        if (context.TryGetPin("settlement/host", out string pinned))
        {
            context.Record("settlement/host", pinned);

            HostObject? byPin = PinReference.TryGetIndex(pinned, out int index) && index < candidates.Count
                ? candidates[index]
                : candidates.FirstOrDefault(h => h.Name == pinned);

            if (byPin is not null)
            {
                return byPin;
            }
        }

        int hostIndex = context.Dice("settlement/host").PickWeightedIndex(candidates, h => h.Weight);
        context.Record("settlement/host", PinReference.ForIndex(hostIndex));

        return candidates[hostIndex];
    }

    /// <summary>Governance is rolled as d6 plus the settlement size, so bigger places rank higher.</summary>
    private int RollGovernance(RollContext context, int sizeValue) =>
        context.Dice("settlement/governance").Roll(6) + sizeValue;

    private string LookUpGovernance(RollContext context, int roll)
    {
        IReadOnlyList<GovernanceEntry> entries = _data.Settlement.Governance;

        if (context.TryGetPin("settlement/governance", out string pinned))
        {
            context.Record("settlement/governance", pinned);

            return PinReference.TryGetIndex(pinned, out int index) && index < entries.Count
                ? entries[index].Text
                : pinned;
        }

        int found = -1;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].Contains(roll))
            {
                found = i;
                break;
            }
        }

        if (found < 0)
        {
            context.Record("settlement/governance", "");
            return "";
        }

        context.Record("settlement/governance", PinReference.ForIndex(found));
        return entries[found].Text;
    }

    private Tavern RollTavern(RollContext context, GrammarText grammar)
    {
        TavernTable taverns = _data.Settlement.Taverns;

        string name;
        if (context.TryGetPin("tavern/name", out string pinnedName))
        {
            name = pinnedName;
        }
        else
        {
            name = NameForge.TavernName(context.Dice("tavern/name"), taverns, grammar);
        }

        // Like a settlement's name, a tavern sign is assembled rather than drawn from one row.
        context.Record("tavern/name", name);

        return new Tavern
        {
            Name = name,
            SpecialtyMeal = context.Text("tavern/meal", taverns.SpecialtyMeals),
            Keeper = NpcGenerator.Generate(_data, context, "tavern/keeper")
        };
    }

    /// <summary>
    /// Ties some shopkeepers to each other using the SRD's relationship column, so the settlement
    /// reads as a community rather than a list of unrelated strangers.
    /// </summary>
    private IReadOnlyList<Shop> LinkKeepers(RollContext context, IReadOnlyList<Shop> shops)
    {
        if (shops.Count < 2 || _data.Npc.Relationship.Count == 0)
        {
            return shops;
        }

        GrammarText grammar = _data.Text.Grammar;
        List<Shop> linked = [.. shops];

        for (int i = 0; i < linked.Count; i++)
        {
            string path = $"shop/{i}/keeper/relationship";
            DiceRoller dice = context.Dice(path);

            // Roughly half the keepers are connected to someone; more than that starts to feel
            // like everyone in town is related.
            if (!context.TryGetPin(path, out _) && !dice.Chance(50))
            {
                continue;
            }

            // Chosen from every other keeper rather than only earlier ones, which would otherwise
            // pile most of the relationships onto the first shop in the list.
            int other = dice.NextIndex(linked.Count - 1);
            if (other >= i)
            {
                other++;
            }

            string kind = context.Text(path, _data.Npc.Relationship);
            string relatedTo = linked[other].Keeper.FullName;

            linked[i] = linked[i] with
            {
                Keeper = linked[i].Keeper with
                {
                    RelationshipKind = kind,
                    RelatedTo = relatedTo,
                    RelationshipSummary = MouseNpc.DescribeRelationship(kind, relatedTo, grammar)
                }
            };
        }

        return linked;
    }
}
