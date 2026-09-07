using MausritterTools.Core.Data;

namespace MausritterTools.Core.Model;

/// <summary>How an item occupies inventory, which is what an item card depicts.</summary>
public enum CardShape
{
    /// <summary>A single inventory slot.</summary>
    Single,

    /// <summary>Two slots side by side. Used for armour and two-handed weapons.</summary>
    Wide
}

/// <summary>
/// Derives the card face for a piece of gear: its slot shape, usage dots and type label.
/// </summary>
/// <remarks>
/// <para>
/// Mausritter's inventory is card-based. "Most items take up one inventory slot. Some larger items,
/// such as two-handed weapons and armour take up two slots", and "most items have three usage dots".
/// </para>
/// <para>
/// The SRD never tabulates which specific items are two-slot or which carry usage, so the mapping
/// below is this project's reading of the rules rather than an official table. Item card templates
/// and art are explicitly reusable under the Mausritter Third Party Licence.
/// </para>
/// </remarks>
public static class ItemCard
{
    /// <summary>The usage dots on a standard item.</summary>
    public const int StandardUsageDots = 3;

    /// <summary>
    /// The electric lantern is the SRD's one explicit exception, at six dots.
    /// </summary>
    public const int ElectricLanternUsageDots = 6;

    /// <summary>Gear categories whose contents are consumed or worn down in play.</summary>
    private static readonly HashSet<string> CategoriesWithUsage =
        new(StringComparer.Ordinal) { "weapons-and-armour", "light-sources" };

    /// <summary>Items that occupy two slots: armour, and weapons wielded in both paws.</summary>
    private static readonly HashSet<string> TwoSlotItems =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Light armour", "Heavy armour", "Heavy", "Heavy ranged"
        };

    /// <summary>Consumables outside the usage categories that still deplete.</summary>
    private static readonly HashSet<string> AdditionalConsumables =
        new(StringComparer.OrdinalIgnoreCase) { "Travel rations", "Matches" };

    /// <summary>Items that are services or fees rather than objects, so never become cards.</summary>
    private static readonly HashSet<string> NotObjects =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Repairs", "Silvered weapons", "Bunkhouse bed", "Private room",
            "Hot bath", "Night out on the town", "Rabbit wagon", "River raft", "Pigeon flight"
        };

    /// <summary>How many slots wide the card is.</summary>
    public static CardShape ShapeFor(GearItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return TwoSlotItems.Contains(item.Name) ? CardShape.Wide : CardShape.Single;
    }

    /// <summary>How many usage dots the card carries, or zero when the item has none.</summary>
    public static int UsageDotsFor(GearItem item, string? categoryId = null)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.Name.Equals("Electric lantern", StringComparison.OrdinalIgnoreCase))
        {
            return ElectricLanternUsageDots;
        }

        if (NotObjects.Contains(item.Name))
        {
            return 0;
        }

        bool consumable =
            (categoryId is not null && CategoriesWithUsage.Contains(categoryId)) ||
            AdditionalConsumables.Contains(item.Name);

        return consumable ? StandardUsageDots : 0;
    }

    /// <summary>
    /// Whether the item is a physical object a player could slot into their inventory, as opposed
    /// to a service such as a night's lodging or a repair fee.
    /// </summary>
    public static bool IsCardable(GearItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return item.HasNumericPrice && !NotObjects.Contains(item.Name);
    }
}
