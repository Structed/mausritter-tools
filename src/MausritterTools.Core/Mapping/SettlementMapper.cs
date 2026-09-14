using MausritterTools.Core.Data;
using MausritterTools.Core.Model;
using Structed.Inkwell.Data;
using Structed.Inkwell.Mapping;

namespace MausritterTools.Core.Mapping;

/// <summary>
/// Describes a settlement to the map generator.
/// </summary>
/// <remarks>
/// Everything Mausritter-specific about a settlement map is decided here: which host silhouette to
/// draw, what counts as waterside, and what earns a number on the map. The generator downstream
/// sees only geometry and a list of labels.
/// </remarks>
public static class SettlementMapper
{
    /// <summary>Words in a settlement's trade or features that imply open water.</summary>
    /// <remarks>
    /// Mausritter has no "is this on water" column, so it is inferred. A settlement of fishermice,
    /// or one with a water-wheel, should not be drawn on dry ground.
    /// </remarks>
    private static readonly string[] WaterCues =
    [
        "fishermice", "water-wheel", "raft", "riverboat", "dock", "bridge", "pond", "brook", "mill"
    ];

    /// <summary>
    /// Reduces a settlement to what the map generator needs.
    /// </summary>
    /// <param name="settlement">The settlement to draw.</param>
    /// <param name="grammar">
    /// Supplies the legend's phrasing. Optional so that geometry tests need not load a language;
    /// the defaults are the English ones.
    /// </param>
    public static MapBrief Brief(Settlement settlement, GrammarText? grammar = null)
    {
        ArgumentNullException.ThrowIfNull(settlement);

        GrammarText phrasing = grammar ?? new GrammarText();
        List<MapKeySubject> keys = [];

        if (settlement.Tavern is { } tavern)
        {
            keys.Add(new MapKeySubject(
                tavern.Name,
                TextTemplate.Format(phrasing.TavernLegendDetail, ("meal", tavern.SpecialtyMeal))));
        }

        foreach (Shop shop in settlement.Shops)
        {
            keys.Add(new MapKeySubject(
                shop.SignName,
                TextTemplate.Format(
                    phrasing.ShopLegendDetail,
                    ("service", shop.Service.Name),
                    ("keeper", shop.Keeper.FullName))));
        }

        return new MapBrief
        {
            Shape = settlement.Host.Shape,
            Scale = settlement.Size.SizeValue,
            Subject = settlement.Host.Name,
            HasWater = IsWaterside(settlement),
            Keys = keys
        };
    }

    /// <summary>Draws a settlement's map.</summary>
    /// <param name="seed">The map's own seed, so it can be redrawn without re-rolling the place.</param>
    public static PlaceMap Generate(Settlement settlement, uint seed, GrammarText? grammar = null) =>
        MapGenerator.Generate(Brief(settlement, grammar), seed);

    private static bool IsWaterside(Settlement settlement)
    {
        IEnumerable<string> text = settlement.Industries
            .Concat(settlement.NotableFeatures)
            .Append(settlement.Host.Name)
            .Append(settlement.Host.Description);

        return text.Any(entry =>
            WaterCues.Any(cue => entry.Contains(cue, StringComparison.OrdinalIgnoreCase)));
    }
}
