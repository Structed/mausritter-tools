using System.Text.Json.Serialization;

namespace MausritterTools.Core.Data;

/// <summary>
/// The settlement services catalogue.
/// </summary>
/// <remarks>
/// Mausritter has no shops or services table. Every entry here is this project's invention,
/// anchored where possible to a quoted SRD hook in <see cref="ServiceDefinition.SrdBasis"/>. The
/// UI must present this layer as an unofficial house rule.
/// </remarks>
public sealed record ServiceTables
{
    [JsonPropertyName("_source")]
    public DataProvenance Source { get => field ?? new(); init; } = new();

    /// <summary>How far a shop's prices may drift from the listed price, either way.</summary>
    public int PriceVariancePercent { get; init; }

    public string PriceVarianceNote { get => field ?? ""; init; } = "";

    public IReadOnlyList<ShopCountRange> ShopCountBySize { get => field ?? []; init; } = [];

    public IReadOnlyList<ServiceDefinition> Services { get => field ?? []; init; } = [];

    public IReadOnlyList<SignPattern> ShopSignPatterns { get => field ?? []; init; } = [];

    public IReadOnlyList<string> ShopSignAdjectives { get => field ?? []; init; } = [];

    public IReadOnlyList<string> ShopQuirks { get => field ?? []; init; } = [];

    public ShopCountRange? CountForSize(int sizeValue) =>
        ShopCountBySize.FirstOrDefault(r => r.SizeValue == sizeValue);
}

/// <summary>How many shops a settlement of a given size supports.</summary>
public sealed record ShopCountRange
{
    public int SizeValue { get; init; }

    public string Name { get => field ?? ""; init; } = "";

    public int Min { get; init; }

    public int Max { get; init; }
}

/// <summary>A kind of shop or service that can appear in a settlement.</summary>
public sealed record ServiceDefinition
{
    public string Id { get => field ?? ""; init; } = "";

    public string Name { get => field ?? ""; init; } = "";

    /// <summary>What the proprietor is called, e.g. "smith".</summary>
    public string KeeperTitle { get => field ?? ""; init; } = "";

    /// <summary>Smallest settlement size this service appears in.</summary>
    public int MinSize { get; init; } = 1;

    /// <summary>Relative likelihood of being chosen.</summary>
    public int Weight { get; init; } = 50;

    public bool AllowDuplicates { get; init; }

    /// <summary>
    /// Gated by geography rather than size. The SRD makes human-made goods available only in
    /// "mouse settlements near human populations".
    /// </summary>
    public bool RequiresHumanTown { get; init; }

    /// <summary>Baseline adjustment applied to this service's prices.</summary>
    public int PriceModifierPercent { get; init; }

    public StockProfile Stock { get => field ?? new(); init; } = new();

    public IReadOnlyList<string> SignNouns { get => field ?? []; init; } = [];

    /// <summary>
    /// The gender of each noun in <see cref="SignNouns"/>, position for position.
    /// </summary>
    /// <remarks>Empty in English; supplied by translations that need it to choose an article.</remarks>
    public IReadOnlyList<string> SignNounGenders { get => field ?? []; init; } = [];

    /// <summary>The quoted SRD rule this service is derived from, shown in the UI for transparency.</summary>
    public string SrdBasis { get => field ?? ""; init; } = "";

    public string Blurb { get => field ?? ""; init; } = "";

    /// <summary>A service with no sellable stock, such as a bank.</summary>
    public bool ServiceOnly { get; init; }

    public string? ServiceTerms { get; init; }

    public bool OffersRepairs { get; init; }

    public bool OffersHirelings { get; init; }

    public bool BuysSpells { get; init; }

    public bool BuysTreasure { get; init; }

    public bool PricesPerHex { get; init; }

    /// <summary>Whether this service can appear in the given settlement.</summary>
    public bool IsAvailable(int sizeValue, bool nearHumanTown) =>
        sizeValue >= MinSize && (!RequiresHumanTown || nearHumanTown);

    /// <summary>The gender of the sign noun at <paramref name="index"/>, or <c>null</c> if untagged.</summary>
    public string? SignNounGenderAt(int index) =>
        index >= 0 && index < SignNounGenders.Count ? SignNounGenders[index] : null;
}

/// <summary>Which gear categories a shop stocks, and how deeply.</summary>
public sealed record StockProfile
{
    public IReadOnlyList<string> Categories { get => field ?? []; init; } = [];

    /// <summary>
    /// Restricts stock to these item names.
    /// </summary>
    /// <remarks>
    /// The SRD's mouse-made tools are one undifferentiated list of hardware, so a shop that draws
    /// from it freely ends up with an apothecary selling wooden poles. Specialist shops name the
    /// items that suit them instead. An empty list means the whole category is fair game.
    /// </remarks>
    public IReadOnlyList<string> ItemNames { get => field ?? []; init; } = [];

    public int MinItems { get; init; }

    public int MaxItems { get; init; }

    /// <summary>Whether an item from a stocked category may appear on the shelves.</summary>
    public bool Allows(GearItem item) =>
        ItemNames.Count == 0 ||
        ItemNames.Contains(item.Name, StringComparer.OrdinalIgnoreCase);
}

/// <summary>A template for a shop sign, e.g. <c>The {adjective} {noun}</c>.</summary>
public sealed record SignPattern
{
    public string Id { get => field ?? ""; init; } = "";

    public string Template { get => field ?? ""; init; } = "";

    public int Weight { get; init; } = 10;
}

/// <summary>Mouse name lists. Original content; the SRD has no name tables.</summary>
public sealed record NameTables
{
    [JsonPropertyName("_source")]
    public DataProvenance Source { get => field ?? new(); init; } = new();

    public IReadOnlyList<string> GivenNames { get => field ?? []; init; } = [];

    public IReadOnlyList<string> FamilyNames { get => field ?? []; init; } = [];
}

/// <summary>
/// The human-scale objects a mouse settlement is built in or around.
/// </summary>
/// <remarks>
/// The landmark names are SRD text; the shape and layout metadata is original, and exists to give
/// the map generator a bounded container to draw inside.
/// </remarks>
public sealed record HostTables
{
    [JsonPropertyName("_source")]
    public DataProvenance Source { get => field ?? new(); init; } = new();

    public string DesignNote { get => field ?? ""; init; } = "";

    public IReadOnlyDictionary<string, string> Shapes
    {
        get => field ?? new Dictionary<string, string>();
        init;
    } = new Dictionary<string, string>();

    public IReadOnlyList<HostObject> Hosts { get => field ?? []; init; } = [];

    /// <summary>Hosts that suit the given terrain, falling back to all of them.</summary>
    public IReadOnlyList<HostObject> ForTerrain(string? terrain)
    {
        if (string.IsNullOrWhiteSpace(terrain))
        {
            return Hosts;
        }

        List<HostObject> matching = [.. Hosts.Where(h => h.Terrain.Contains(terrain))];
        return matching.Count > 0 ? matching : Hosts;
    }
}

/// <summary>One host object, with the metadata the map generator needs.</summary>
public sealed record HostObject
{
    public string Id { get => field ?? ""; init; } = "";

    public string Name { get => field ?? ""; init; } = "";

    /// <summary>How the settlement sits relative to the host, e.g. "inside", "beneath".</summary>
    public string Preposition { get => field ?? "in"; init; } = "in";

    /// <summary>Layout archetype: hollow, linear, vessel, boxy, warren or sprawl.</summary>
    public string Shape { get => field ?? "hollow"; init; } = "hollow";

    public IReadOnlyList<string> Terrain { get => field ?? []; init; } = [];

    public int Weight { get; init; } = 50;

    /// <summary>Whether the settlement stacks upward or downward as well as outward.</summary>
    public bool Vertical { get; init; }

    /// <summary>
    /// The article to use before the name, e.g. "a" or "an". Empty for mass nouns such as
    /// "dumped furniture"; when absent it is derived from the first letter.
    /// </summary>
    public string? Article { get; init; }

    public string Description { get => field ?? ""; init; } = "";

    /// <summary>
    /// The complete prepositional phrase, e.g. "in einem hohlen Baumstumpf".
    /// </summary>
    /// <remarks>
    /// English composes this from a preposition, an article chosen by first letter, and the name.
    /// German cannot: the article agrees with the noun's gender and the adjective declines with it,
    /// so the phrase is supplied whole rather than assembled from parts.
    /// </remarks>
    public string? Phrase { get; init; }

    /// <summary>e.g. "inside an old farmhouse stump".</summary>
    public string Describe()
    {
        if (Phrase is { Length: > 0 } phrase)
        {
            return phrase;
        }

        string name = Name.ToLowerInvariant();
        string article = Article ?? (name.Length > 0 && "aeiou".Contains(name[0]) ? "an" : "a");

        return article.Length == 0
            ? $"{Preposition} {name}"
            : $"{Preposition} {article} {name}";
    }
}
