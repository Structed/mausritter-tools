using System.Text.Json.Serialization;

namespace MausritterTools.Core.Data;

/// <summary>Mouse settlement tables, imported from the Mausritter SRD.</summary>
public sealed record SettlementTables
{
    [JsonPropertyName("_source")]
    public DataProvenance Source { get; init; } = new();

    public string SizeRoll { get; init; } = "";

    public IReadOnlyList<SettlementSize> Sizes { get; init; } = [];

    public string GovernanceRoll { get; init; } = "";

    public IReadOnlyList<GovernanceEntry> Governance { get; init; } = [];

    public IReadOnlyList<string> Inhabitants { get; init; } = [];

    public IReadOnlyList<string> NotableFeatures { get; init; } = [];

    public IReadOnlyList<string> Industries { get; init; } = [];

    public IReadOnlyList<string> Events { get; init; } = [];

    public NameSeedTable NameSeeds { get; init; } = new();

    public TavernTable Taverns { get; init; } = new();
}

/// <summary>
/// One row of the settlement size table.
/// </summary>
/// <remarks>
/// <see cref="HasTavern"/>, <see cref="FeatureCount"/> and <see cref="IndustryCount"/> encode SRD
/// prose as data: taverns appear in "hamlets and larger", cities get two notable features, and
/// "towns and cities" get two industries.
/// </remarks>
public sealed record SettlementSize
{
    public int Roll { get; init; }

    public int SizeValue { get; init; }

    public string Name { get; init; } = "";

    public string? Population { get; init; }

    public bool HasTavern { get; init; }

    public int FeatureCount { get; init; } = 1;

    public int IndustryCount { get; init; } = 1;

    /// <summary>e.g. "Village (150-300 mice)".</summary>
    public string Describe() => Population is null ? Name : $"{Name} ({Population})";
}

/// <summary>
/// One row of the governance table, which is looked up by <c>d6 + settlement size</c> and so
/// covers a range of totals rather than a single roll.
/// </summary>
public sealed record GovernanceEntry
{
    public int RollMin { get; init; }

    public int RollMax { get; init; }

    public string Text { get; init; } = "";

    public bool Contains(int roll) => roll >= RollMin && roll <= RollMax;
}

/// <summary>
/// The four parallel d12 columns used to build a settlement name.
/// </summary>
/// <remarks>
/// The SRD's instruction is "roll d12 twice, choose a start and an end, massage until it sounds
/// nice" — so the raw concatenation is explicitly meant to be cleaned up afterwards.
/// </remarks>
public sealed record NameSeedTable
{
    public string Note { get; init; } = "";

    public IReadOnlyList<string> StartA { get; init; } = [];

    public IReadOnlyList<string> StartB { get; init; } = [];

    public IReadOnlyList<string> EndA { get; init; } = [];

    public IReadOnlyList<string> EndB { get; init; } = [];
}

/// <summary>The tavern name and specialty meal columns.</summary>
public sealed record TavernTable
{
    public string Note { get; init; } = "";

    public IReadOnlyList<string> NameA { get; init; } = [];

    public IReadOnlyList<string> NameB { get; init; } = [];

    public IReadOnlyList<string> SpecialtyMeals { get; init; } = [];
}

/// <summary>Non-player mice tables, imported from the Mausritter SRD.</summary>
public sealed record NpcTables
{
    [JsonPropertyName("_source")]
    public DataProvenance Source { get; init; } = new();

    public IReadOnlyList<SocialPosition> SocialPositions { get; init; } = [];

    public IReadOnlyList<Birthsign> Birthsigns { get; init; } = [];

    public IReadOnlyList<string> Appearance { get; init; } = [];

    public IReadOnlyList<string> Quirk { get; init; } = [];

    public IReadOnlyList<string> Wants { get; init; } = [];

    public IReadOnlyList<string> Relationship { get; init; } = [];
}

/// <summary>A social position and the fee a mouse of that station commands.</summary>
public sealed record SocialPosition
{
    public int Roll { get; init; }

    public string Name { get; init; } = "";

    /// <summary>Dice expression such as <c>d6 x 10p</c>.</summary>
    public string Payment { get; init; } = "";
}

/// <summary>A birthsign and its paired virtue and vice.</summary>
public sealed record Birthsign
{
    public int Roll { get; init; }

    public string Name { get; init; } = "";

    public string Disposition { get; init; } = "";

    public string Virtue { get; init; } = "";

    public string? Vice { get; init; }
}

/// <summary>Gear and prices, imported from the Mausritter SRD.</summary>
public sealed record GearTables
{
    [JsonPropertyName("_source")]
    public DataProvenance Source { get; init; } = new();

    public Currency Currency { get; init; } = new();

    public IReadOnlyList<GearCategory> Categories { get; init; } = [];

    public GearCategory? FindCategory(string id) =>
        Categories.FirstOrDefault(c => c.Id == id);
}

/// <summary>Mausritter's only currency.</summary>
public sealed record Currency
{
    public string Name { get; init; } = "pip";

    public string Abbreviation { get; init; } = "p";

    public string Note { get; init; } = "";
}

/// <summary>One priced category from the gear list.</summary>
public sealed record GearCategory
{
    public string Id { get; init; } = "";

    public string Name { get; init; } = "";

    /// <summary>Where the category can be bought, when the SRD says so.</summary>
    public string? Availability { get; init; }

    public IReadOnlyList<GearItem> Items { get; init; } = [];
}

/// <summary>
/// A single priced item.
/// </summary>
/// <remarks>
/// <see cref="Pips"/> is null whenever the price is not a plain amount. The SRD prices silvered
/// weapons at "x10p" and repairs at "10%", which are modifiers rather than sums, so those keep
/// only their <see cref="PriceText"/> and must be displayed verbatim.
/// </remarks>
public sealed record GearItem
{
    public string Name { get; init; } = "";

    /// <summary>Distinguishes variants, e.g. "blank" and "reading" for a book.</summary>
    public string? Qualifier { get; init; }

    /// <summary>Parenthetical detail, e.g. "dagger, needle, etc.".</summary>
    public string? Note { get; init; }

    public string PriceText { get; init; } = "";

    public int? Pips { get; init; }

    /// <summary>Set when the price is a rate, e.g. "per night".</summary>
    public string? PerUnit { get; init; }

    /// <summary>The item name with its qualifier, e.g. "Book, blank".</summary>
    public string DisplayName => Qualifier is null ? Name : $"{Name}, {Qualifier}";

    /// <summary><c>true</c> when the price is a plain number of pips that can be adjusted.</summary>
    public bool HasNumericPrice => Pips is > 0;
}

/// <summary>Hirelings, imported from the Mausritter SRD.</summary>
public sealed record HirelingTables
{
    [JsonPropertyName("_source")]
    public DataProvenance Source { get; init; } = new();

    public string Recruiting { get; init; } = "";

    public string Note { get; init; } = "";

    public IReadOnlyList<Hireling> Hirelings { get; init; } = [];
}

/// <summary>A type of hireling, how many are looking for work, and their daily wage.</summary>
public sealed record Hireling
{
    public string Name { get; init; } = "";

    /// <summary>
    /// Dice expression for how many are available, e.g. <c>d6</c> for a torchbearer or <c>d2</c>
    /// for a blacksmith. A low number implies a scarce, skilled role.
    /// </summary>
    public string Number { get; init; } = "";

    public string WagesText { get; init; } = "";

    public int? WagesPips { get; init; }
}

/// <summary>Spells, imported from the Mausritter SRD.</summary>
public sealed record SpellTables
{
    [JsonPropertyName("_source")]
    public DataProvenance Source { get; init; } = new();

    public string Roll { get; init; } = "";

    public string SaleValue { get; init; } = "";

    public IReadOnlyList<Spell> Spells { get; init; } = [];
}

/// <summary>One spell from the 2d8 list.</summary>
public sealed record Spell
{
    public int RollMin { get; init; }

    public int RollMax { get; init; }

    public string Name { get; init; } = "";

    public string Effect { get; init; } = "";

    public string Recharge { get; init; } = "";
}
