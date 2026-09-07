using MausritterTools.Core.Generation;
using MausritterTools.Core.Model;
using MausritterTools.Core.Randomness;

namespace MausritterTools.Core.Serialization;

/// <summary>
/// The exported form of a settlement.
/// </summary>
/// <remarks>
/// Carries two things at once. <see cref="Options"/> is what actually matters on import: it
/// reproduces the settlement exactly, including every lock and hand edit, and keeps it editable.
/// <see cref="Settlement"/> is a flattened snapshot for humans and other tools, so the file is
/// still worth something to a reader who has never heard of this app.
/// </remarks>
public sealed record SettlementDocument
{
    public const string FormatId = "mausritter-tools/settlement";
    public const int CurrentVersion = 1;

    public string Format { get; init; } = FormatId;

    public int Version { get; init; } = CurrentVersion;

    public string? GeneratedUtc { get; init; }

    /// <summary>
    /// Required attribution, restated in every exported file so it travels with the content.
    /// </summary>
    public string Attribution { get; init; } =
        "Settlement tables from Mausritter (https://mausritter.com), a product of Losing Games " +
        "and Isaac Williams, used under CC BY 4.0. Shops, services and mouse names are unofficial " +
        "house rules original to mausritter-tools.";

    public SettlementDocumentOptions Options { get; init; } = new();

    public SettlementSnapshot Settlement { get; init; } = new();
}

/// <summary>Everything needed to regenerate a settlement exactly.</summary>
public sealed record SettlementDocumentOptions
{
    /// <summary>The seed in its short shareable form.</summary>
    public string Seed { get; init; } = "";

    public int? Size { get; init; }

    public bool NearHumanTown { get; init; }

    public string? Terrain { get; init; }

    /// <summary>Locked and hand-edited values, keyed by field path.</summary>
    public IReadOnlyDictionary<string, string> Pins { get; init; } =
        new Dictionary<string, string>();

    /// <summary>How many times each field has been individually re-rolled.</summary>
    public IReadOnlyDictionary<string, int> Rerolls { get; init; } =
        new Dictionary<string, int>();

    public static SettlementDocumentOptions From(GenerationOptions options) => new()
    {
        Seed = SeedCodec.Encode(options.Seed),
        Size = options.Size,
        NearHumanTown = options.NearHumanTown,
        Terrain = options.Terrain,
        Pins = options.Pins,
        Rerolls = options.Rerolls
    };

    public GenerationOptions ToGenerationOptions() => new()
    {
        Seed = SeedCodec.DecodeOrRandom(Seed),
        Size = Size,
        NearHumanTown = NearHumanTown,
        Terrain = Terrain,
        Pins = new Dictionary<string, string>(Pins, StringComparer.Ordinal),
        Rerolls = new Dictionary<string, int>(Rerolls, StringComparer.Ordinal)
    };
}

/// <summary>A readable, flattened view of a generated settlement.</summary>
public sealed record SettlementSnapshot
{
    public string Name { get; init; } = "";

    public string Size { get; init; } = "";

    public string? Population { get; init; }

    public string Host { get; init; } = "";

    public string HostDescription { get; init; } = "";

    public bool NearHumanTown { get; init; }

    public string Governance { get; init; } = "";

    public int GovernanceRoll { get; init; }

    public string Inhabitants { get; init; } = "";

    public IReadOnlyList<string> NotableFeatures { get; init; } = [];

    public IReadOnlyList<string> Industries { get; init; } = [];

    public string Event { get; init; } = "";

    public TavernSnapshot? Tavern { get; init; }

    public IReadOnlyList<ShopSnapshot> Shops { get; init; } = [];

    public static SettlementSnapshot From(Settlement settlement) => new()
    {
        Name = settlement.Name,
        Size = settlement.Size.Name,
        Population = settlement.Size.Population,
        Host = settlement.Host.Name,
        HostDescription = settlement.Host.Description,
        NearHumanTown = settlement.NearHumanTown,
        Governance = settlement.Governance,
        GovernanceRoll = settlement.GovernanceRoll,
        Inhabitants = settlement.Inhabitants,
        NotableFeatures = settlement.NotableFeatures,
        Industries = settlement.Industries,
        Event = settlement.Event,
        Tavern = settlement.Tavern is null ? null : TavernSnapshot.From(settlement.Tavern),
        Shops = [.. settlement.Shops.Select(ShopSnapshot.From)]
    };
}

/// <summary>A flattened tavern.</summary>
public sealed record TavernSnapshot
{
    public string Name { get; init; } = "";

    public string SpecialtyMeal { get; init; } = "";

    public MouseSnapshot Keeper { get; init; } = new();

    public static TavernSnapshot From(Tavern tavern) => new()
    {
        Name = tavern.Name,
        SpecialtyMeal = tavern.SpecialtyMeal,
        Keeper = MouseSnapshot.From(tavern.Keeper)
    };
}

/// <summary>A flattened shop.</summary>
public sealed record ShopSnapshot
{
    public int Index { get; init; }

    public string Type { get; init; } = "";

    public string Sign { get; init; } = "";

    public string Quirk { get; init; } = "";

    public MouseSnapshot Keeper { get; init; } = new();

    public string? Terms { get; init; }

    public int PriceAdjustmentPercent { get; init; }

    public IReadOnlyList<StockSnapshot> Stock { get; init; } = [];

    public static ShopSnapshot From(Shop shop) => new()
    {
        Index = shop.Index,
        Type = shop.Service.Name,
        Sign = shop.SignName,
        Quirk = shop.Quirk,
        Keeper = MouseSnapshot.From(shop.Keeper),
        Terms = shop.Service.ServiceTerms,
        PriceAdjustmentPercent = shop.PriceAdjustmentPercent,
        Stock = [.. shop.Stock.Select(StockSnapshot.From)]
    };
}

/// <summary>A flattened line of stock.</summary>
public sealed record StockSnapshot
{
    public string Item { get; init; } = "";

    public string Price { get; init; } = "";

    public int? Quantity { get; init; }

    public static StockSnapshot From(StockEntry entry) => new()
    {
        Item = entry.DisplayName,
        Price = entry.PriceText,
        Quantity = entry.Quantity
    };
}

/// <summary>A flattened mouse.</summary>
public sealed record MouseSnapshot
{
    public string Name { get; init; } = "";

    public string Appearance { get; init; } = "";

    public string Quirk { get; init; } = "";

    public string Wants { get; init; } = "";

    public string Position { get; init; } = "";

    public int Purse { get; init; }

    public string Birthsign { get; init; } = "";

    public string Disposition { get; init; } = "";

    public string? Relationship { get; init; }

    public static MouseSnapshot From(MouseNpc npc) => new()
    {
        Name = npc.FullName,
        Appearance = npc.Appearance,
        Quirk = npc.Quirk,
        Wants = npc.Wants,
        Position = npc.Position.Name,
        Purse = npc.Purse,
        Birthsign = npc.Birthsign.Name,
        Disposition = npc.Disposition,
        Relationship = npc.RelationshipSummary
    };
}
