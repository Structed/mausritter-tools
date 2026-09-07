using System.Text.Json.Serialization;

namespace MausritterTools.Core.Data;

/// <summary>Mouse settlement tables, imported from the Mausritter SRD.</summary>
public sealed record SettlementTables
{
    [JsonPropertyName("_source")]
    public DataProvenance Source { get => field ?? new(); init; } = new();

    public string SizeRoll { get => field ?? ""; init; } = "";

    public IReadOnlyList<SettlementSize> Sizes { get => field ?? []; init; } = [];

    public string GovernanceRoll { get => field ?? ""; init; } = "";

    public IReadOnlyList<GovernanceEntry> Governance { get => field ?? []; init; } = [];

    public IReadOnlyList<string> Inhabitants { get => field ?? []; init; } = [];

    public IReadOnlyList<string> NotableFeatures { get => field ?? []; init; } = [];

    public IReadOnlyList<string> Industries { get => field ?? []; init; } = [];

    public IReadOnlyList<string> Events { get => field ?? []; init; } = [];

    public NameSeedTable NameSeeds { get => field ?? new(); init; } = new();

    public TavernTable Taverns { get => field ?? new(); init; } = new();
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

    public string Name { get => field ?? ""; init; } = "";

    /// <summary>
    /// The grammatical gender of <see cref="Name"/>, <c>m</c>, <c>f</c> or <c>n</c>.
    /// </summary>
    /// <remarks>
    /// Absent in English, which needs no such thing. Supplied by translations so the settlement
    /// summary can open with the right article: "Ein Dorf", but "Eine Stadt".
    /// </remarks>
    public string? NameGender { get; init; }

    public string? Population { get; init; }

    public bool HasTavern { get; init; }

    public int FeatureCount { get; init; } = 1;

    public int IndustryCount { get; init; } = 1;

    /// <summary>e.g. "Village (150-300 mice)".</summary>
    public string Describe(GrammarText grammar)
    {
        ArgumentNullException.ThrowIfNull(grammar);

        return Population is null
            ? Name
            : TextTemplate.Format(
                grammar.SizeWithPopulation, ("name", Name), ("population", Population));
    }
}

/// <summary>
/// One row of the governance table, which is looked up by <c>d6 + settlement size</c> and so
/// covers a range of totals rather than a single roll.
/// </summary>
public sealed record GovernanceEntry
{
    public int RollMin { get; init; }

    public int RollMax { get; init; }

    public string Text { get => field ?? ""; init; } = "";

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
    public string Note { get => field ?? ""; init; } = "";

    public IReadOnlyList<string> StartA { get => field ?? []; init; } = [];

    public IReadOnlyList<string> StartB { get => field ?? []; init; } = [];

    public IReadOnlyList<string> EndA { get => field ?? []; init; } = [];

    public IReadOnlyList<string> EndB { get => field ?? []; init; } = [];
}

/// <summary>The tavern name and specialty meal columns.</summary>
public sealed record TavernTable
{
    public string Note { get => field ?? ""; init; } = "";

    public IReadOnlyList<string> NameA { get => field ?? []; init; } = [];

    public IReadOnlyList<string> NameB { get => field ?? []; init; } = [];

    /// <summary>
    /// The gender of each noun in <see cref="NameB"/>, position for position.
    /// </summary>
    /// <remarks>
    /// Empty in English. A translation supplies it so the sign can pick between "Zum" and "Zur";
    /// the adjective needs no such treatment because its dative ending is the same for all three
    /// genders, so the translated <see cref="NameA"/> column ships already declined.
    /// </remarks>
    public IReadOnlyList<string> NameBGenders { get => field ?? []; init; } = [];

    public IReadOnlyList<string> SpecialtyMeals { get => field ?? []; init; } = [];

    /// <summary>The gender of the noun at <paramref name="index"/>, or <c>null</c> if untagged.</summary>
    public string? GenderAt(int index) =>
        index >= 0 && index < NameBGenders.Count ? NameBGenders[index] : null;
}

/// <summary>Non-player mice tables, imported from the Mausritter SRD.</summary>
public sealed record NpcTables
{
    [JsonPropertyName("_source")]
    public DataProvenance Source { get => field ?? new(); init; } = new();

    public IReadOnlyList<SocialPosition> SocialPositions { get => field ?? []; init; } = [];

    public IReadOnlyList<Birthsign> Birthsigns { get => field ?? []; init; } = [];

    public IReadOnlyList<string> Appearance { get => field ?? []; init; } = [];

    public IReadOnlyList<string> Quirk { get => field ?? []; init; } = [];

    public IReadOnlyList<string> Wants { get => field ?? []; init; } = [];

    public IReadOnlyList<string> Relationship { get => field ?? []; init; } = [];
}

/// <summary>A social position and the fee a mouse of that station commands.</summary>
public sealed record SocialPosition
{
    public int Roll { get; init; }

    public string Name { get => field ?? ""; init; } = "";

    /// <summary>Dice expression such as <c>d6 x 10p</c>.</summary>
    public string Payment { get => field ?? ""; init; } = "";
}

/// <summary>A birthsign and its paired virtue and vice.</summary>
public sealed record Birthsign
{
    public int Roll { get; init; }

    public string Name { get => field ?? ""; init; } = "";

    public string Disposition { get => field ?? ""; init; } = "";

    public string Virtue { get => field ?? ""; init; } = "";

    public string? Vice { get; init; }
}

/// <summary>Gear and prices, imported from the Mausritter SRD.</summary>
public sealed record GearTables
{
    [JsonPropertyName("_source")]
    public DataProvenance Source { get => field ?? new(); init; } = new();

    public Currency Currency { get => field ?? new(); init; } = new();

    public IReadOnlyList<GearCategory> Categories { get => field ?? []; init; } = [];

    public GearCategory? FindCategory(string id) =>
        Categories.FirstOrDefault(c => c.Id == id);
}

/// <summary>Mausritter's only currency.</summary>
public sealed record Currency
{
    public string Name { get => field ?? "pip"; init; } = "pip";

    public string Abbreviation { get => field ?? "p"; init; } = "p";

    public string Note { get => field ?? ""; init; } = "";
}

/// <summary>One priced category from the gear list.</summary>
public sealed record GearCategory
{
    public string Id { get => field ?? ""; init; } = "";

    public string Name { get => field ?? ""; init; } = "";

    /// <summary>Where the category can be bought, when the SRD says so.</summary>
    public string? Availability { get; init; }

    public IReadOnlyList<GearItem> Items { get => field ?? []; init; } = [];
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
    public string Name { get => field ?? ""; init; } = "";

    /// <summary>Distinguishes variants, e.g. "blank" and "reading" for a book.</summary>
    public string? Qualifier { get; init; }

    /// <summary>Parenthetical detail, e.g. "dagger, needle, etc.".</summary>
    public string? Note { get; init; }

    public string PriceText { get => field ?? ""; init; } = "";

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
    public DataProvenance Source { get => field ?? new(); init; } = new();

    public string Recruiting { get => field ?? ""; init; } = "";

    public string Note { get => field ?? ""; init; } = "";

    public IReadOnlyList<Hireling> Hirelings { get => field ?? []; init; } = [];
}

/// <summary>A type of hireling, how many are looking for work, and their daily wage.</summary>
public sealed record Hireling
{
    public string Name { get => field ?? ""; init; } = "";

    /// <summary>
    /// Dice expression for how many are available, e.g. <c>d6</c> for a torchbearer or <c>d2</c>
    /// for a blacksmith. A low number implies a scarce, skilled role.
    /// </summary>
    public string Number { get => field ?? ""; init; } = "";

    public string WagesText { get => field ?? ""; init; } = "";

    public int? WagesPips { get; init; }
}

/// <summary>Spells, imported from the Mausritter SRD.</summary>
public sealed record SpellTables
{
    [JsonPropertyName("_source")]
    public DataProvenance Source { get => field ?? new(); init; } = new();

    public string Roll { get => field ?? ""; init; } = "";

    public string SaleValue { get => field ?? ""; init; } = "";

    public IReadOnlyList<Spell> Spells { get => field ?? []; init; } = [];
}

/// <summary>One spell from the 2d8 list.</summary>
public sealed record Spell
{
    public int RollMin { get; init; }

    public int RollMax { get; init; }

    public string Name { get => field ?? ""; init; } = "";

    public string Effect { get => field ?? ""; init; } = "";

    public string Recharge { get => field ?? ""; init; } = "";
}
