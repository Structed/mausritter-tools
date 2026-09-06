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
    public DataProvenance Source { get; init; } = new();

    /// <summary>How far a shop's prices may drift from the listed price, either way.</summary>
    public int PriceVariancePercent { get; init; }

    public string PriceVarianceNote { get; init; } = "";

    public IReadOnlyList<ShopCountRange> ShopCountBySize { get; init; } = [];

    public IReadOnlyList<ServiceDefinition> Services { get; init; } = [];

    public IReadOnlyList<SignPattern> ShopSignPatterns { get; init; } = [];

    public IReadOnlyList<string> ShopSignAdjectives { get; init; } = [];

    public IReadOnlyList<string> ShopQuirks { get; init; } = [];

    public ShopCountRange? CountForSize(int sizeValue) =>
        ShopCountBySize.FirstOrDefault(r => r.SizeValue == sizeValue);
}

/// <summary>How many shops a settlement of a given size supports.</summary>
public sealed record ShopCountRange
{
    public int SizeValue { get; init; }

    public string Name { get; init; } = "";

    public int Min { get; init; }

    public int Max { get; init; }
}

/// <summary>A kind of shop or service that can appear in a settlement.</summary>
public sealed record ServiceDefinition
{
    public string Id { get; init; } = "";

    public string Name { get; init; } = "";

    /// <summary>What the proprietor is called, e.g. "smith".</summary>
    public string KeeperTitle { get; init; } = "";

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

    public StockProfile Stock { get; init; } = new();

    public IReadOnlyList<string> SignNouns { get; init; } = [];

    /// <summary>The quoted SRD rule this service is derived from, shown in the UI for transparency.</summary>
    public string SrdBasis { get; init; } = "";

    public string Blurb { get; init; } = "";

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
}

/// <summary>Which gear categories a shop stocks, and how deeply.</summary>
public sealed record StockProfile
{
    public IReadOnlyList<string> Categories { get; init; } = [];

    public int MinItems { get; init; }

    public int MaxItems { get; init; }
}

/// <summary>A template for a shop sign, e.g. <c>The {adjective} {noun}</c>.</summary>
public sealed record SignPattern
{
    public string Id { get; init; } = "";

    public string Template { get; init; } = "";

    public int Weight { get; init; } = 10;
}

/// <summary>Mouse name lists. Original content; the SRD has no name tables.</summary>
public sealed record NameTables
{
    [JsonPropertyName("_source")]
    public DataProvenance Source { get; init; } = new();

    public IReadOnlyList<string> GivenNames { get; init; } = [];

    public IReadOnlyList<string> FamilyNames { get; init; } = [];
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
    public DataProvenance Source { get; init; } = new();

    public string DesignNote { get; init; } = "";

    public IReadOnlyDictionary<string, string> Shapes { get; init; } =
        new Dictionary<string, string>();

    public IReadOnlyList<HostObject> Hosts { get; init; } = [];

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
    public string Id { get; init; } = "";

    public string Name { get; init; } = "";

    /// <summary>How the settlement sits relative to the host, e.g. "inside", "beneath".</summary>
    public string Preposition { get; init; } = "in";

    /// <summary>Layout archetype: hollow, linear, vessel, boxy, warren or sprawl.</summary>
    public string Shape { get; init; } = "hollow";

    public IReadOnlyList<string> Terrain { get; init; } = [];

    public int Weight { get; init; } = 50;

    /// <summary>Whether the settlement stacks upward or downward as well as outward.</summary>
    public bool Vertical { get; init; }

    /// <summary>
    /// The article to use before the name, e.g. "a" or "an". Empty for mass nouns such as
    /// "dumped furniture"; when absent it is derived from the first letter.
    /// </summary>
    public string? Article { get; init; }

    public string Description { get; init; } = "";

    /// <summary>e.g. "inside an old farmhouse stump".</summary>
    public string Describe()
    {
        string name = Name.ToLowerInvariant();
        string article = Article ?? (name.Length > 0 && "aeiou".Contains(name[0]) ? "an" : "a");

        return article.Length == 0
            ? $"{Preposition} {name}"
            : $"{Preposition} {article} {name}";
    }
}
