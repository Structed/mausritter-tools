namespace MausritterTools.Core.Data;

/// <summary>
/// Every string the app shows that does not come from a game table.
/// </summary>
/// <remarks>
/// <para>
/// Loaded from <c>data/i18n/{locale}/ui.json</c> rather than from a <c>.resx</c>, so that UI text
/// and table text share one mechanism, one file format and one place to add a language. It also
/// keeps satellite assemblies out of a WebAssembly download that is already paying for ICU.
/// </para>
/// <para>
/// English is loaded first and the chosen language is merged over it, so a key that has not been
/// translated yet degrades to English rather than to an empty label.
/// <c>TranslationCompletenessTests</c> is what stops that being used as an excuse.
/// </para>
/// <para>
/// Fields whose name ends in <c>Html</c> are rendered as markup rather than escaped text, because
/// the prose they carry contains links and emphasis that a translator must be able to move around
/// inside the sentence. They are authored in this repository and never come from user input.
/// </para>
/// <para>
/// Every non-nullable property coerces null in its getter: the JSON source generator discards
/// property initialisers, so a key absent from a file arrives as null whatever default is written
/// here.
/// </para>
/// </remarks>
public sealed record UiText
{
    /// <summary>The locale this file was written for, used to verify the right file was loaded.</summary>
    public string Locale { get => field ?? ""; init; } = "";

    public AppText App { get => field ?? new(); init; } = new();

    public NavText Nav { get => field ?? new(); init; } = new();

    public HomeText Home { get => field ?? new(); init; } = new();

    public AboutText About { get => field ?? new(); init; } = new();

    public NotFoundText NotFound { get => field ?? new(); init; } = new();

    public SettlementText Settlement { get => field ?? new(); init; } = new();

    public FieldRowText FieldRow { get => field ?? new(); init; } = new();

    public ShopText Shop { get => field ?? new(); init; } = new();

    public ItemCardText ItemCard { get => field ?? new(); init; } = new();

    public LicenceText Licence { get => field ?? new(); init; } = new();

    public ErrorText Error { get => field ?? new(); init; } = new();

    public GrammarText Grammar { get => field ?? new(); init; } = new();
}

/// <summary>Site-wide strings, including the ones that live in the document head.</summary>
public sealed record AppText
{
    public string Title { get => field ?? ""; init; } = "";

    public string Description { get => field ?? ""; init; } = "";

    /// <summary>Joins a page name to the site name, e.g. "About — Mausritter Tools".</summary>
    public string PageTitlePattern { get => field ?? "{page}"; init; } = "{page}";
}

public sealed record NavText
{
    public string Brand { get => field ?? ""; init; } = "";

    public string MenuTitle { get => field ?? ""; init; } = "";

    public string Home { get => field ?? ""; init; } = "";

    public string Settlement { get => field ?? ""; init; } = "";

    public string About { get => field ?? ""; init; } = "";

    public string LanguageLabel { get => field ?? ""; init; } = "";
}

public sealed record HomeText
{
    public string Heading { get => field ?? ""; init; } = "";

    public string LeadHtml { get => field ?? ""; init; } = "";

    public string NoServerHtml { get => field ?? ""; init; } = "";

    public string ToolsHeading { get => field ?? ""; init; } = "";

    public string SettlementItemHtml { get => field ?? ""; init; } = "";

    public string AboutLinkText { get => field ?? ""; init; } = "";
}

public sealed record AboutText
{
    public string Heading { get => field ?? ""; init; } = "";

    public string LeadHtml { get => field ?? ""; init; } = "";

    public AboutSection HowItWorks { get => field ?? new(); init; } = new();

    public AboutSection Official { get => field ?? new(); init; } = new();

    public AboutSection Translation { get => field ?? new(); init; } = new();

    public AboutSection Source { get => field ?? new(); init; } = new();

    public AboutSection Support { get => field ?? new(); init; } = new();

    public DataSourcesText DataSources { get => field ?? new(); init; } = new();
}

/// <summary>A heading and its paragraphs, which is the shape every About section takes.</summary>
public sealed record AboutSection
{
    public string Heading { get => field ?? ""; init; } = "";

    public IReadOnlyList<string> ParagraphsHtml { get => field ?? []; init; } = [];
}

/// <summary>The page shown for an address that does not exist.</summary>
public sealed record NotFoundText
{
    public string Heading { get => field ?? ""; init; } = "";

    public string Message { get => field ?? ""; init; } = "";

    public string BackLink { get => field ?? ""; init; } = "";
}

public sealed record DataSourcesText
{
    public string Heading { get => field ?? ""; init; } = "";

    public string ContentColumn { get => field ?? ""; init; } = "";

    public string SourceColumn { get => field ?? ""; init; } = "";

    public string LicenceColumn { get => field ?? ""; init; } = "";

    /// <summary>Shown in place of a source link for content this project wrote itself.</summary>
    public string OriginalSource { get => field ?? ""; init; } = "";

    public string OriginalLicence { get => field ?? ""; init; } = "";
}

public sealed record SettlementText
{
    public string PageTitle { get => field ?? ""; init; } = "";

    public string Heading { get => field ?? ""; init; } = "";

    public string LeadHtml { get => field ?? ""; init; } = "";

    public string LoadErrorHeading { get => field ?? ""; init; } = "";

    public string Loading { get => field ?? ""; init; } = "";

    public GeneratorBarText Controls { get => field ?? new(); init; } = new();

    public SettlementSectionsText Sections { get => field ?? new(); init; } = new();

    public SettlementLabelsText Labels { get => field ?? new(); init; } = new();

    public MapText Map { get => field ?? new(); init; } = new();

    public StatusText Status { get => field ?? new(); init; } = new();
}

public sealed record GeneratorBarText
{
    public string SeedLabel { get => field ?? ""; init; } = "";

    public string SizeLabel { get => field ?? ""; init; } = "";

    public string SizeRollOption { get => field ?? ""; init; } = "";

    public string TerrainLabel { get => field ?? ""; init; } = "";

    public string TerrainAnyOption { get => field ?? ""; init; } = "";

    /// <summary>
    /// Terrain labels, in the same order as the terrain values the page defines. The values
    /// themselves are keys matched against the host tables and are never translated.
    /// </summary>
    public IReadOnlyList<string> TerrainOptions { get => field ?? []; init; } = [];

    public string NearHumanLabel { get => field ?? ""; init; } = "";

    public string NearHumanTitle { get => field ?? ""; init; } = "";

    public string RollNew { get => field ?? ""; init; } = "";

    public string RerollUnlocked { get => field ?? ""; init; } = "";

    public string CopyLink { get => field ?? ""; init; } = "";

    public string ExportJson { get => field ?? ""; init; } = "";

    public string ImportJson { get => field ?? ""; init; } = "";

    public string Print { get => field ?? ""; init; } = "";
}

public sealed record SettlementSectionsText
{
    public string SeedTag { get => field ?? ""; init; } = "";

    public string Settlement { get => field ?? ""; init; } = "";

    public string Tavern { get => field ?? ""; init; } = "";

    public string Shops { get => field ?? ""; init; } = "";

    public string HouseRuleTag { get => field ?? ""; init; } = "";

    public string HouseRuleTitle { get => field ?? ""; init; } = "";

    public string NoShops { get => field ?? ""; init; } = "";

    public string ItemCards { get => field ?? ""; init; } = "";

    public string ItemCardsNote { get => field ?? ""; init; } = "";

    public string OfficialNoticeHeading { get => field ?? ""; init; } = "";

    public string OfficialNoticeHtml { get => field ?? ""; init; } = "";
}

public sealed record SettlementLabelsText
{
    public string Name { get => field ?? ""; init; } = "";

    public string Size { get => field ?? ""; init; } = "";

    public string Built { get => field ?? ""; init; } = "";

    public string Governance { get => field ?? ""; init; } = "";

    public string Inhabitants { get => field ?? ""; init; } = "";

    public string Feature { get => field ?? ""; init; } = "";

    public string Features { get => field ?? ""; init; } = "";

    public string Industry { get => field ?? ""; init; } = "";

    public string Industries { get => field ?? ""; init; } = "";

    public string RightNow { get => field ?? ""; init; } = "";

    public string Sign { get => field ?? ""; init; } = "";

    public string Specialty { get => field ?? ""; init; } = "";

    public string Landlord { get => field ?? ""; init; } = "";

    /// <summary>e.g. "Inside a hollow tree stump — a fallen oak, hollowed out by rot".</summary>
    public string BuiltValue { get => field ?? ""; init; } = "";

    /// <summary>e.g. "Council of elders (d6 + size = 7)".</summary>
    public string GovernanceValue { get => field ?? ""; init; } = "";

    /// <summary>e.g. "Rush Thistledown — scarred. Hums constantly. Wants: a quiet life.".</summary>
    public string KeeperValue { get => field ?? ""; init; } = "";
}

public sealed record MapText
{
    public string Heading { get => field ?? ""; init; } = "";

    public string RedrawTitle { get => field ?? ""; init; } = "";

    public string DownloadTitle { get => field ?? ""; init; } = "";

    public string Caption { get => field ?? ""; init; } = "";

    /// <summary>The SVG's accessible label, e.g. "Map of the settlement, a hollow tree stump".</summary>
    public string AriaLabel { get => field ?? ""; init; } = "";
}

/// <summary>The transient messages shown under the toolbar.</summary>
public sealed record StatusText
{
    public string MapRedrawn { get => field ?? ""; init; } = "";

    public string MapDownloaded { get => field ?? ""; init; } = "";

    public string RolledNewKeepingLocks { get => field ?? ""; init; } = "";

    public string RerolledExceptOne { get => field ?? ""; init; } = "";

    public string RerolledExceptMany { get => field ?? ""; init; } = "";

    public string InvalidSeed { get => field ?? ""; init; } = "";

    public string Unlocked { get => field ?? ""; init; } = "";

    public string Locked { get => field ?? ""; init; } = "";

    public string Edited { get => field ?? ""; init; } = "";

    public string LinkCopied { get => field ?? ""; init; } = "";

    public string ClipboardFailed { get => field ?? ""; init; } = "";

    public string Exported { get => field ?? ""; init; } = "";

    public string Imported { get => field ?? ""; init; } = "";

    public string ImportFailed { get => field ?? ""; init; } = "";

    public string ReadFailed { get => field ?? ""; init; } = "";

    /// <summary>Shown after switching language, since the sheet is rebuilt in the new one.</summary>
    public string LanguageChanged { get => field ?? ""; init; } = "";
}

public sealed record FieldRowText
{
    public string LockTitle { get => field ?? ""; init; } = "";

    public string UnlockTitle { get => field ?? ""; init; } = "";

    public string EditTitle { get => field ?? ""; init; } = "";

    public string RerollTitle { get => field ?? ""; init; } = "";
}

public sealed record ShopText
{
    public string MapKeyTitle { get => field ?? ""; init; } = "";

    public string Quirk { get => field ?? ""; init; } = "";

    public string Terms { get => field ?? ""; init; } = "";

    public string Repairs { get => field ?? ""; init; } = "";

    public string RepairsText { get => field ?? ""; init; } = "";

    public string Spells { get => field ?? ""; init; } = "";

    public string SpellsText { get => field ?? ""; init; } = "";

    /// <summary>e.g. "Guildmouse · 340p on paw · born under the Moon (Wise / Mysterious)".</summary>
    public string KeeperMeta { get => field ?? ""; init; } = "";

    public string Wants { get => field ?? ""; init; } = "";

    public string InStock { get => field ?? ""; init; } = "";

    public string Quantity { get => field ?? ""; init; } = "";

    public string Price { get => field ?? ""; init; } = "";

    public string LookingForWork { get => field ?? ""; init; } = "";

    public string Number { get => field ?? ""; init; } = "";

    public string PerDay { get => field ?? ""; init; } = "";

    public string PriceAbove { get => field ?? ""; init; } = "";

    public string PriceBelow { get => field ?? ""; init; } = "";

    public string WhereThisComesFrom { get => field ?? ""; init; } = "";
}

public sealed record ItemCardText
{
    public string UsageDotsTitle { get => field ?? ""; init; } = "";
}

public sealed record LicenceText
{
    public string BasedOnHtml { get => field ?? ""; init; } = "";

    public string IndependentHtml { get => field ?? ""; init; } = "";

    public string CopyrightHtml { get => field ?? ""; init; } = "";

    public string HouseRuleNote { get => field ?? ""; init; } = "";

    /// <summary>Shown only in translations, to mark them as unofficial.</summary>
    public string? TranslationNote { get; init; }
}

public sealed record ErrorText
{
    public string Unhandled { get => field ?? ""; init; } = "";

    public string Reload { get => field ?? ""; init; } = "";
}

/// <summary>
/// The sentences the domain layer composes, and the grammatical facts needed to compose them.
/// </summary>
/// <remarks>
/// English glues values together with fixed words and word order. German does not: articles agree
/// with gender, adjectives decline, and nouns stay capitalised mid-sentence. Rather than teach the
/// generators grammar, every joining sentence is a format string here and the few facts that vary
/// per language are data.
/// </remarks>
public sealed record GrammarText
{
    /// <summary>e.g. "A {size} of {population}, {host}.".</summary>
    public string SummaryWithPopulation { get => field ?? ""; init; } = "";

    /// <summary>e.g. "A {size}, {host}.".</summary>
    public string SummaryWithoutPopulation { get => field ?? ""; init; } = "";

    /// <summary>e.g. "{name} ({population})".</summary>
    public string SizeWithPopulation { get => field ?? "{name}"; init; } = "{name}";

    /// <summary>e.g. "{other}: {kind}".</summary>
    public string RelationshipSummary { get => field ?? ""; init; } = "";

    /// <summary>
    /// Whether a noun dropped into the middle of a sentence is lower-cased.
    /// </summary>
    /// <remarks>
    /// True for English, where "A Village of 300 mice" reads as a mistake. False for German, where
    /// every noun is capitalised and lower-casing one would be the mistake.
    /// </remarks>
    public bool LowercaseInlineNouns { get; init; }

    /// <summary>The map legend's entry for the tavern, e.g. "Tavern — {meal}".</summary>
    public string TavernLegendDetail
    {
        get => field ?? "Tavern — {meal}";
        init;
    } = "Tavern — {meal}";

    /// <summary>The map legend's entry for a shop, e.g. "{service} — {keeper}".</summary>
    public string ShopLegendDetail
    {
        get => field ?? "{service} — {keeper}";
        init;
    } = "{service} — {keeper}";

    /// <summary>
    /// How a tavern's sign is built, e.g. "The {adjective} {noun}" or "{article} {adjective} {noun}".
    /// </summary>
    public string TavernNamePattern { get => field ?? "{adjective} {noun}"; init; } = "{adjective} {noun}";

    /// <summary>Used when the tavern name columns are missing.</summary>
    public string DefaultTavernName { get => field ?? ""; init; } = "";

    public string UnnamedSettlement { get => field ?? ""; init; } = "";

    public string DefaultGivenName { get => field ?? ""; init; } = "";

    public string DefaultFamilyName { get => field ?? ""; init; } = "";

    /// <summary>A plain price, e.g. "{amount}{pip}" — "20p" in English, "20 P" in German.</summary>
    public string PricePlain { get => field ?? "{amount}{pip}"; init; } = "{amount}{pip}";

    /// <summary>A rate, e.g. "{amount}{pip} per {unit}".</summary>
    public string PricePerUnit
    {
        get => field ?? "{amount}{pip} per {unit}";
        init;
    } = "{amount}{pip} per {unit}";

    /// <summary>Travel, which the SRD prices by the hex.</summary>
    public string PricePerHex
    {
        get => field ?? "{amount}{pip} per hex";
        init;
    } = "{amount}{pip} per hex";

    public ArticleTables Articles { get => field ?? new(); init; } = new();
}

/// <summary>
/// Articles by grammatical gender, keyed <c>m</c>, <c>f</c> or <c>n</c>.
/// </summary>
/// <remarks>
/// Empty in English, which has no gendered articles. In German only two forms are needed, because
/// of a convenient accident: in the dative singular after a definite article the adjective ending
/// is <c>-en</c> for every gender, so a sign reads "Zum krummen Käfer", "Zur krummen Rose" and
/// "Zum krummen Blatt". Only the article changes, so the adjective columns can ship already
/// declined and no declension code is needed anywhere.
/// </remarks>
public sealed record ArticleTables
{
    /// <summary>"Ein" / "Eine", used to open the settlement summary.</summary>
    public IReadOnlyDictionary<string, string> IndefiniteNominative
    {
        get => field ?? new Dictionary<string, string>();
        init;
    } = new Dictionary<string, string>();

    /// <summary>"Zum" / "Zur", used on tavern and shop signs.</summary>
    public IReadOnlyDictionary<string, string> DativeDefinite
    {
        get => field ?? new Dictionary<string, string>();
        init;
    } = new Dictionary<string, string>();

    /// <summary>
    /// Looks up an article, returning an empty string when the language has none or the gender is
    /// unknown, so an English pattern that never mentions <c>{article}</c> costs nothing.
    /// </summary>
    public static string For(IReadOnlyDictionary<string, string> table, string? gender) =>
        gender is not null && table.TryGetValue(gender, out string? article) ? article : "";
}
