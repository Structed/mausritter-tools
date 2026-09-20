using System.Globalization;
using MausritterTools.Core.Data;
using Structed.Inkwell.Data;

namespace MausritterTools.Core.Model;

/// <summary>How an item occupies inventory, which is what an item card depicts.</summary>
public enum CardShape
{
    /// <summary>A single inventory slot.</summary>
    Single,

    /// <summary>Two slots side by side. Used for weapons wielded in both paws.</summary>
    Wide,

    /// <summary>Two slots stacked. Used for armour.</summary>
    Tall
}

/// <summary>
/// Derives the card face for a piece of gear: its slot shape, usage dots, stat and artwork.
/// </summary>
/// <remarks>
/// <para>
/// Mausritter's inventory is card-based. "Most items take up one inventory slot. Some larger items,
/// such as two-handed weapons and armour take up two slots", and "most items have three usage dots".
/// </para>
/// <para>
/// Which items those are is stated per class in the SRD's inventory chapter rather than tabulated:
/// Heavy and Heavy ranged are "Wielded in: Both paws", light armour is worn "in: Off paw and one
/// body slot" and heavy armour "in: Two body slots". Paws sit side by side, so a two-handed weapon
/// is a wide card; the worn slots stack, and the official armour card template is one slot wide by
/// two tall, so armour is a tall card.
/// </para>
/// <para>
/// Item card templates and art are explicitly reusable under the Mausritter Third Party Licence,
/// which permits reuse of "the item card templates and item card art" by name.
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

    /// <summary>Where the imported item card artwork is served from.</summary>
    public const string ArtFolder = "images/items";

    /// <summary>
    /// The largest the item name is ever set, in card units, matching the official card.
    /// </summary>
    public const double MaxTitleUnits = 13;

    /// <summary>
    /// The card face is measured in units of a hundredth of an inventory slot, so every dimension
    /// is a share of a slot and holds at whatever size the card is finally printed. These few are
    /// the ones a fitting decision needs; the rest live in the stylesheet that draws the face.
    /// </summary>
    private const double SlotUnits = 100;

    /// <summary>The clear width the name has, after the margins the official card leaves it.</summary>
    private const double TitleSideMargins = 11;

    /// <summary>The room above the divider rule that the name has to fit into.</summary>
    private const double TitleBoxUnits = 21;

    /// <summary>Line spacing when a long name has to wrap.</summary>
    private const double TitleLineSpacing = 1.02;

    /// <summary>
    /// The width of an average Texturina glyph at weight 800, as a share of its size. Eyeballed
    /// rather than measured: it only has to be close enough to choose between the sizes below.
    /// </summary>
    private const double AverageGlyphWidth = 0.52;

    /// <summary>The sizes the name may be set at, largest first.</summary>
    private static readonly double[] TitleSizes = [MaxTitleUnits, 10.5, 8.5, 7];

    /// <summary>Gear categories whose contents are consumed or worn down in play.</summary>
    private static readonly HashSet<string> CategoriesWithUsage =
        new(StringComparer.Ordinal) { "weapons-and-armour", "light-sources" };

    /// <summary>Weapons the SRD wields in both paws, which sit side by side.</summary>
    private static readonly HashSet<string> TwoHandedWeapons =
        new(StringComparer.OrdinalIgnoreCase) { "Heavy", "Heavy ranged" };

    /// <summary>Armour, which is worn across two stacked slots.</summary>
    private static readonly HashSet<string> Armour =
        new(StringComparer.OrdinalIgnoreCase) { "Light armour", "Heavy armour" };

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

    /// <summary>
    /// The official item card illustration for a piece of gear, keyed by the item's name.
    /// </summary>
    /// <remarks>
    /// Keyed on <see cref="GearItem.Name"/> rather than its label for the same reason every other
    /// rule here is: the name is the cross-file key and stays canonical in every translation, so a
    /// German card keeps its picture. Gear the official set has no drawing for is left art-free,
    /// which is how the official studio renders an item with no image chosen.
    /// </remarks>
    private static readonly Dictionary<string, string> ArtByName =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Improvised"] = "item-improvised.png",
            ["Light"] = "item-light-1.png",
            ["Medium"] = "item-medium-2.png",
            ["Heavy"] = "item-heavy-2.png",
            ["Light ranged"] = "item-light-ranged.png",
            ["Heavy ranged"] = "item-heavy-ranged.png",
            ["Arrows"] = "item-quiver.png",
            ["Stones"] = "item-stones.png",
            ["Light armour"] = "item-light-armour.png",
            ["Heavy armour"] = "item-heavy-armour.png",
            ["Needle"] = "item-light-2.png",
            ["Torches"] = "item-torch.png",
            ["Lantern"] = "item-lantern.png",
            ["Electric lantern"] = "item-electric-lantern.png",
            ["Travel rations"] = "item-rations.png"
        };

    /// <summary>Every artwork file the card rules can ask for.</summary>
    public static IReadOnlyCollection<string> ArtFiles => ArtByName.Values;

    /// <summary>How many slots the card occupies, and in which direction.</summary>
    public static CardShape ShapeFor(GearItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (Armour.Contains(item.Name))
        {
            return CardShape.Tall;
        }

        return TwoHandedWeapons.Contains(item.Name) ? CardShape.Wide : CardShape.Single;
    }

    /// <summary>How many inventory slots the item takes up.</summary>
    public static int SlotsFor(GearItem item) => ShapeFor(item) == CardShape.Single ? 1 : 2;

    /// <summary>
    /// The boxed stat in the card's top right: a weapon's damage or a piece of armour's defence.
    /// </summary>
    /// <remarks>
    /// A dice expression is printed as it stands, because it is a key rather than prose. Defence is
    /// a number, so the word beside it is composed from the UI text and reads in whichever language
    /// is loaded.
    /// </remarks>
    public static string? StatFor(GearItem item, UiText text)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(text);

        if (item.Damage is { Length: > 0 } damage)
        {
            return damage;
        }

        return item.Defence is { } defence
            ? TextTemplate.Format(text.ItemCard.Defence, ("count", defence.ToString(CultureInfo.InvariantCulture)))
            : null;
    }

    /// <summary>The path to the item's illustration, or <c>null</c> when it has none.</summary>
    public static string? ArtFor(GearItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return ArtByName.TryGetValue(item.Name, out string? file) ? $"{ArtFolder}/{file}" : null;
    }

    /// <summary>
    /// How large to set the item's name on its card, in card units.
    /// </summary>
    /// <remarks>
    /// The official renderer condenses the name horizontally until it fits on one line. That works
    /// for the English names it was drawn around, but not for German compounds such as
    /// "Vorhängeschloss mit Schlüssel", which would be squeezed past reading. This steps the size
    /// down a short ladder and lets the name wrap instead, which keeps the same intent: the name
    /// is as large as it can be while still sitting above the rule.
    /// </remarks>
    public static double TitleSizeFor(string label, CardShape shape)
    {
        ArgumentNullException.ThrowIfNull(label);

        double available = (shape == CardShape.Wide ? SlotUnits * 2 : SlotUnits) - TitleSideMargins;

        foreach (double size in TitleSizes)
        {
            double width = label.Length * AverageGlyphWidth * size;
            double lines = Math.Max(1, Math.Ceiling(width / available));

            if (lines * size * TitleLineSpacing <= TitleBoxUnits)
            {
                return size;
            }
        }

        return TitleSizes[^1];
    }

    /// <summary>
    /// The line along the foot of the card saying what kind of thing the item is.
    /// </summary>
    /// <remarks>
    /// The official card names the item's class there. The SRD gives each weapon class an example
    /// line of its own — "sword, axe, etc." — which says the same thing in the same place, so it
    /// is used where there is one; otherwise the item's qualifier stands in.
    /// </remarks>
    public static string? ClassLineFor(GearItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.Note is { Length: > 0 } note)
        {
            return note;
        }

        return item.Qualifier is { Length: > 0 } qualifier ? qualifier : null;
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
