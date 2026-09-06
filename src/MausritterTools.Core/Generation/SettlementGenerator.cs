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

        SettlementSize size = RollSize(context, options);
        HostObject host = RollHost(context, options);

        int governanceRoll = RollGovernance(context, size.SizeValue);

        IReadOnlyList<Shop> shops = ShopGenerator.Generate(
            _data, context, size.SizeValue, options.NearHumanTown);

        shops = LinkKeepers(context, shops);

        return new Settlement
        {
            Seed = options.Seed,
            Name = RollName(context, tables),
            Size = size,
            Host = host,
            NearHumanTown = options.NearHumanTown,
            Governance = LookUpGovernance(context, governanceRoll),
            GovernanceRoll = governanceRoll,
            Inhabitants = context.Text("settlement/inhabitants", tables.Inhabitants),
            NotableFeatures = context.TextMany("settlement/features", tables.NotableFeatures, size.FeatureCount),
            Industries = context.TextMany("settlement/industries", tables.Industries, size.IndustryCount),
            Event = context.Text("settlement/event", tables.Events),
            Tavern = size.HasTavern ? RollTavern(context) : null,
            Shops = shops
        };
    }

    private string RollName(RollContext context, SettlementTables tables) =>
        context.TryGetPin("settlement/name", out string pinned)
            ? pinned
            : NameForge.SettlementName(context.Dice("settlement/name"), tables.NameSeeds);

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

        int roll;
        if (options.Size is { } forced)
        {
            roll = Math.Clamp(forced, 1, sizes.Count);
        }
        else if (context.TryGetPin("settlement/size", out string pinned) &&
                 sizes.FirstOrDefault(s => s.Name == pinned) is { } pinnedSize)
        {
            return pinnedSize;
        }
        else
        {
            roll = context.Dice("settlement/size").RollLowestOfTwo(sizes.Count);
        }

        return sizes.FirstOrDefault(s => s.SizeValue == roll) ?? sizes[^1];
    }

    private HostObject RollHost(RollContext context, GenerationOptions options)
    {
        IReadOnlyList<HostObject> candidates = _data.Hosts.ForTerrain(options.Terrain);
        if (candidates.Count == 0)
        {
            return new HostObject();
        }

        if (context.TryGetPin("settlement/host", out string pinned) &&
            candidates.FirstOrDefault(h => h.Name == pinned) is { } pinnedHost)
        {
            return pinnedHost;
        }

        return context.Dice("settlement/host").PickWeighted(candidates, h => h.Weight);
    }

    /// <summary>Governance is rolled as d6 plus the settlement size, so bigger places rank higher.</summary>
    private int RollGovernance(RollContext context, int sizeValue) =>
        context.Dice("settlement/governance").Roll(6) + sizeValue;

    private string LookUpGovernance(RollContext context, int roll)
    {
        if (context.TryGetPin("settlement/governance", out string pinned))
        {
            return pinned;
        }

        GovernanceEntry? entry = _data.Settlement.Governance.FirstOrDefault(g => g.Contains(roll));
        return entry?.Text ?? "";
    }

    private Tavern RollTavern(RollContext context)
    {
        TavernTable taverns = _data.Settlement.Taverns;

        string name = context.TryGetPin("tavern/name", out string pinnedName)
            ? pinnedName
            : NameForge.TavernName(context.Dice("tavern/name"), taverns);

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

            linked[i] = linked[i] with
            {
                Keeper = linked[i].Keeper with
                {
                    RelationshipKind = context.Text(path, _data.Npc.Relationship),
                    RelatedTo = linked[other].Keeper.FullName
                }
            };
        }

        return linked;
    }
}
