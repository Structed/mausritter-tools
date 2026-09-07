using MausritterTools.Core.Data;

namespace MausritterTools.Core.Model;

/// <summary>A fully generated mouse settlement.</summary>
public sealed record Settlement
{
    /// <summary>The root seed. The same seed and options always rebuild this settlement.</summary>
    public uint Seed { get; init; }

    public string Name { get; init; } = "";

    public SettlementSize Size { get; init; } = new();

    /// <summary>The host object the settlement is built in or around.</summary>
    public HostObject Host { get; init; } = new();

    /// <summary>Whether human-made goods can be found here.</summary>
    public bool NearHumanTown { get; init; }

    public string Governance { get; init; } = "";

    /// <summary>The <c>d6 + settlement size</c> total that produced <see cref="Governance"/>.</summary>
    public int GovernanceRoll { get; init; }

    public string Inhabitants { get; init; } = "";

    /// <summary>Cities have two; everywhere else has one.</summary>
    public IReadOnlyList<string> NotableFeatures { get; init; } = [];

    /// <summary>Towns and cities have two; everywhere else has one.</summary>
    public IReadOnlyList<string> Industries { get; init; } = [];

    public string Event { get; init; } = "";

    /// <summary>Present only in hamlets and larger.</summary>
    public Tavern? Tavern { get; init; }

    public IReadOnlyList<Shop> Shops { get; init; } = [];

    /// <summary>
    /// What each field path would be locked to, keyed by path.
    /// </summary>
    /// <remarks>
    /// Table-drawn values are recorded as a position rather than as text, so that locking a value
    /// and then switching language keeps the lock pointing at the same row instead of stranding one
    /// line of the sheet in the wrong language.
    /// </remarks>
    public IReadOnlyDictionary<string, string> PinValues { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// e.g. "A village of 150-300 mice, inside a hollow tree stump".
    /// </summary>
    /// <remarks>
    /// Composed by the generator rather than here, because the sentence's shape is a property of
    /// the language and only the generator has the loaded text for it.
    /// </remarks>
    public string Summary { get; init; } = "";
}

/// <summary>The settlement's tavern or inn.</summary>
public sealed record Tavern
{
    public string Name { get; init; } = "";

    public string SpecialtyMeal { get; init; } = "";

    public MouseNpc Keeper { get; init; } = new();
}

/// <summary>A shop or service, its proprietor, and what it has in stock.</summary>
public sealed record Shop
{
    /// <summary>Stable identifier used for field paths and map keying, e.g. <c>shop/2</c>.</summary>
    public string Id { get; init; } = "";

    /// <summary>Position in the settlement's list, used as the map key number.</summary>
    public int Index { get; init; }

    public ServiceDefinition Service { get; init; } = new();

    /// <summary>The name over the door.</summary>
    public string SignName { get; init; } = "";

    public string Quirk { get; init; } = "";

    public MouseNpc Keeper { get; init; } = new();

    public IReadOnlyList<StockEntry> Stock { get; init; } = [];

    /// <summary>How far this shop's prices drift from the listed price, as a percentage.</summary>
    public int PriceAdjustmentPercent { get; init; }

    public string ServiceName => Service.Name;
}

/// <summary>One line of a shop's stock.</summary>
public sealed record StockEntry
{
    public GearItem Item { get; init; } = new();

    /// <summary>
    /// The gear category this item came from, e.g. <c>light-sources</c>.
    /// </summary>
    /// <remarks>
    /// Recorded when the stock is built rather than looked up again later. Two categories can list
    /// the same item name, so a reverse lookup is both fragile and ambiguous.
    /// </remarks>
    public string CategoryId { get; init; } = "";

    /// <summary>
    /// The adjusted price in pips, or <c>null</c> when the listed price is a modifier such as
    /// "x10p" or "10%" rather than an amount.
    /// </summary>
    public int? Pips { get; init; }

    /// <summary>What to show the player, already adjusted for this shop.</summary>
    public string PriceText { get; init; } = "";

    /// <summary>How many are in stock, or <c>null</c> when the shop never runs out.</summary>
    public int? Quantity { get; init; }

    public string DisplayName => Item.DisplayName;
}

/// <summary>A named mouse, rolled up from the SRD's non-player mice tables.</summary>
public sealed record MouseNpc
{
    public string GivenName { get; init; } = "";

    public string FamilyName { get; init; } = "";

    public string Appearance { get; init; } = "";

    public string Quirk { get; init; } = "";

    public string Wants { get; init; } = "";

    /// <summary>How they relate to another mouse in the settlement, when a tie was rolled.</summary>
    public string? RelationshipKind { get; init; }

    /// <summary>The mouse they are tied to, when a tie was rolled.</summary>
    public string? RelatedTo { get; init; }

    public SocialPosition Position { get; init; } = new();

    /// <summary>Pips on hand, rolled from the social position's payment expression.</summary>
    public int Purse { get; init; }

    public Birthsign Birthsign { get; init; } = new();

    public string FullName => $"{GivenName} {FamilyName}";

    /// <summary>e.g. "Wise / Mysterious".</summary>
    public string Disposition => Birthsign.Disposition;

    /// <summary>
    /// The relationship as a readable note, when there is one.
    /// </summary>
    /// <remarks>
    /// Composed by the generator via <see cref="DescribeRelationship"/>, since whether the noun is
    /// lower-cased in the middle of a sentence depends on the language.
    /// </remarks>
    public string? RelationshipSummary { get; init; }

    /// <summary>
    /// Writes a keeper's tie to another mouse, e.g. "Rush Thistledown: worked together".
    /// </summary>
    /// <remarks>
    /// Phrased as "name: kind" rather than "kind of name" because the SRD's relationship column
    /// mixes nouns with phrases. "Parent of Rush" reads well, but "Worked together of Rush" does
    /// not; "Rush Thistledown: worked together" works for every entry in the table.
    /// </remarks>
    public static string? DescribeRelationship(string? kind, string? relatedTo, GrammarText grammar)
    {
        ArgumentNullException.ThrowIfNull(grammar);

        if (kind is not { Length: > 0 } || relatedTo is null)
        {
            return null;
        }

        // English lets a noun sit mid-sentence in lower case; German capitalises every noun, and
        // lower-casing one there would simply look like a typo.
        string phrased = grammar.LowercaseInlineNouns
            ? char.ToLowerInvariant(kind[0]) + kind[1..]
            : kind;

        return TextTemplate.Format(
            grammar.RelationshipSummary, ("other", relatedTo), ("kind", phrased));
    }
}
