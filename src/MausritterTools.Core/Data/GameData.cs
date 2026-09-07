using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace MausritterTools.Core.Data;

/// <summary>
/// Every table the generator draws on, loaded once and validated as a set.
/// </summary>
public sealed class GameData
{
    public const string UiTextPath = "ui.json";
    public const string SettlementPath = "srd/settlement.json";
    public const string NpcPath = "srd/npc.json";
    public const string GearPath = "srd/gear.json";
    public const string HirelingsPath = "srd/hirelings.json";
    public const string SpellsPath = "srd/spells.json";
    public const string ServicesPath = "house/services.json";
    public const string NamesPath = "house/names.json";
    public const string HostsPath = "house/hosts.json";

    private GameData(
        Locale locale,
        UiText text,
        SettlementTables settlement,
        NpcTables npc,
        GearTables gear,
        HirelingTables hirelings,
        SpellTables spells,
        ServiceTables services,
        NameTables names,
        HostTables hosts)
    {
        Locale = locale;
        Text = text;
        Settlement = settlement;
        Npc = npc;
        Gear = gear;
        Hirelings = hirelings;
        Spells = spells;
        Services = services;
        Names = names;
        Hosts = hosts;
    }

    /// <summary>The language this data was loaded in.</summary>
    public Locale Locale { get; }

    /// <summary>Every string the app shows that is not a table entry.</summary>
    public UiText Text { get; }

    public SettlementTables Settlement { get; }

    public NpcTables Npc { get; }

    public GearTables Gear { get; }

    public HirelingTables Hirelings { get; }

    public SpellTables Spells { get; }

    public ServiceTables Services { get; }

    public NameTables Names { get; }

    public HostTables Hosts { get; }

    /// <summary>Every distinct attribution notice carried by the loaded data.</summary>
    public IReadOnlyList<DataProvenance> Provenance =>
    [
        Settlement.Source, Npc.Source, Gear.Source, Hirelings.Source,
        Spells.Source, Services.Source, Names.Source, Hosts.Source
    ];

    /// <summary>
    /// Loads just the UI text for a language.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="LoadAsync"/> because the layout needs words on every page, while
    /// the tables are only needed by the generator. Loading fifty kilobytes of gear prices to draw
    /// a navigation bar would be a poor trade.
    /// </remarks>
    public static Task<UiText> LoadTextAsync(
        IDataFileReader reader,
        Locale? locale = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reader);

        IDataFileReader localised = new LocalisingDataFileReader(reader, locale ?? Locale.English);

        return ReadAsync(localised, UiTextPath, GameDataJsonContext.Default.UiText, cancellationToken);
    }

    /// <summary>Loads and validates every data file, in the given language.</summary>
    /// <remarks>
    /// The reader is wrapped so that a non-English language has its translation overlay merged into
    /// each file before it is deserialised. Everything downstream — validation, generation, the UI —
    /// therefore sees one ordinary set of tables and needs to know nothing about languages.
    /// </remarks>
    /// <exception cref="GameDataException">The data is missing, malformed or inconsistent.</exception>
    public static async Task<GameData> LoadAsync(
        IDataFileReader reader,
        Locale? locale = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reader);

        Locale resolved = locale ?? Locale.English;
        IDataFileReader localised = new LocalisingDataFileReader(reader, resolved);

        GameData data = new(
            resolved,
            await ReadAsync(localised, UiTextPath, GameDataJsonContext.Default.UiText, cancellationToken),
            await ReadAsync(localised, SettlementPath, GameDataJsonContext.Default.SettlementTables, cancellationToken),
            await ReadAsync(localised, NpcPath, GameDataJsonContext.Default.NpcTables, cancellationToken),
            await ReadAsync(localised, GearPath, GameDataJsonContext.Default.GearTables, cancellationToken),
            await ReadAsync(localised, HirelingsPath, GameDataJsonContext.Default.HirelingTables, cancellationToken),
            await ReadAsync(localised, SpellsPath, GameDataJsonContext.Default.SpellTables, cancellationToken),
            await ReadAsync(localised, ServicesPath, GameDataJsonContext.Default.ServiceTables, cancellationToken),
            await ReadAsync(localised, NamesPath, GameDataJsonContext.Default.NameTables, cancellationToken),
            await ReadAsync(localised, HostsPath, GameDataJsonContext.Default.HostTables, cancellationToken));

        data.Validate();
        return data;
    }

    private static async Task<T> ReadAsync<T>(
        IDataFileReader reader,
        string path,
        JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken)
    {
        try
        {
            await using Stream stream = await reader.OpenAsync(path, cancellationToken);
            T? value = await JsonSerializer.DeserializeAsync(stream, typeInfo, cancellationToken);

            return value ?? throw new GameDataException($"'{path}' deserialised to null.");
        }
        catch (Exception ex) when (ex is not GameDataException and not OperationCanceledException)
        {
            throw new GameDataException($"Could not load data file '{path}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Checks the loaded tables for the mistakes that would otherwise surface as a silently broken
    /// settlement, such as a governance roll that lands in a gap or a service pointing at a gear
    /// category that does not exist.
    /// </summary>
    private void Validate()
    {
        List<string> problems = [];

        ValidateSettlement(problems);
        ValidateNpc(problems);
        ValidateGear(problems);
        ValidateServices(problems);
        ValidateHouseContent(problems);

        if (problems.Count > 0)
        {
            throw new GameDataException(
                "The Mausritter data files are inconsistent:" +
                Environment.NewLine +
                string.Join(Environment.NewLine, problems.Select(p => $"  - {p}")));
        }
    }

    private void ValidateSettlement(List<string> problems)
    {
        RequireNotEmpty(problems, Settlement.Sizes, "settlement sizes");
        RequireNotEmpty(problems, Settlement.Inhabitants, "settlement inhabitants");
        RequireNotEmpty(problems, Settlement.NotableFeatures, "settlement notable features");
        RequireNotEmpty(problems, Settlement.Industries, "settlement industries");
        RequireNotEmpty(problems, Settlement.Events, "settlement events");

        foreach (int expected in Enumerable.Range(1, 6))
        {
            if (Settlement.Sizes.All(s => s.SizeValue != expected))
            {
                problems.Add($"No settlement size with sizeValue {expected}.");
            }
        }

        foreach (SettlementSize size in Settlement.Sizes)
        {
            if (size.FeatureCount < 1)
            {
                problems.Add($"Settlement size '{size.Name}' has featureCount {size.FeatureCount}; expected at least 1.");
            }

            if (size.IndustryCount < 1)
            {
                problems.Add($"Settlement size '{size.Name}' has industryCount {size.IndustryCount}; expected at least 1.");
            }
        }

        // Governance is looked up by d6 + size, so every total from 1+1 to 6+6 must resolve.
        for (int roll = 2; roll <= 12; roll++)
        {
            int matches = Settlement.Governance.Count(g => g.Contains(roll));
            if (matches == 0)
            {
                problems.Add($"No governance entry covers a roll of {roll}.");
            }
            else if (matches > 1)
            {
                problems.Add($"{matches} governance entries overlap on a roll of {roll}.");
            }
        }

        RequireNotEmpty(problems, Settlement.NameSeeds.StartA, "name seed column startA");
        RequireNotEmpty(problems, Settlement.NameSeeds.StartB, "name seed column startB");
        RequireNotEmpty(problems, Settlement.NameSeeds.EndA, "name seed column endA");
        RequireNotEmpty(problems, Settlement.NameSeeds.EndB, "name seed column endB");

        RequireNotEmpty(problems, Settlement.Taverns.NameA, "tavern name column A");
        RequireNotEmpty(problems, Settlement.Taverns.NameB, "tavern name column B");
        RequireNotEmpty(problems, Settlement.Taverns.SpecialtyMeals, "tavern specialty meals");
    }

    private void ValidateNpc(List<string> problems)
    {
        RequireNotEmpty(problems, Npc.SocialPositions, "NPC social positions");
        RequireNotEmpty(problems, Npc.Birthsigns, "NPC birthsigns");
        RequireNotEmpty(problems, Npc.Appearance, "NPC appearance");
        RequireNotEmpty(problems, Npc.Quirk, "NPC quirk");
        RequireNotEmpty(problems, Npc.Wants, "NPC wants");
        RequireNotEmpty(problems, Npc.Relationship, "NPC relationship");
    }

    private void ValidateGear(List<string> problems)
    {
        RequireNotEmpty(problems, Gear.Categories, "gear categories");

        foreach (GearCategory category in Gear.Categories)
        {
            if (category.Items.Count == 0)
            {
                problems.Add($"Gear category '{category.Id}' has no items.");
            }
        }

        string[] duplicateIds =
        [
            .. Gear.Categories
                .GroupBy(c => c.Id)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
        ];

        foreach (string id in duplicateIds)
        {
            problems.Add($"Gear category id '{id}' appears more than once.");
        }
    }

    private void ValidateServices(List<string> problems)
    {
        RequireNotEmpty(problems, Services.Services, "services");
        RequireNotEmpty(problems, Services.ShopSignAdjectives, "shop sign adjectives");
        RequireNotEmpty(problems, Services.ShopSignPatterns, "shop sign patterns");
        RequireNotEmpty(problems, Services.ShopQuirks, "shop quirks");

        foreach (int sizeValue in Enumerable.Range(1, 6))
        {
            if (Services.CountForSize(sizeValue) is null)
            {
                problems.Add($"No shop count range for settlement size {sizeValue}.");
            }
        }

        foreach (ShopCountRange range in Services.ShopCountBySize)
        {
            if (range.Min > range.Max)
            {
                problems.Add($"Shop count range for size {range.SizeValue} has min {range.Min} above max {range.Max}.");
            }
        }

        foreach (ServiceDefinition service in Services.Services)
        {
            if (service.MinSize is < 1 or > 6)
            {
                problems.Add($"Service '{service.Id}' has minSize {service.MinSize}; expected 1-6.");
            }

            if (service.Weight <= 0)
            {
                problems.Add($"Service '{service.Id}' has weight {service.Weight}; it could never be chosen.");
            }

            // The cross-file check that matters most: a typo here would silently empty a shop.
            foreach (string categoryId in service.Stock.Categories)
            {
                if (Gear.FindCategory(categoryId) is null)
                {
                    problems.Add($"Service '{service.Id}' stocks unknown gear category '{categoryId}'.");
                }
            }

            // An allow-listed item that does not exist would silently shrink the shop's shelves.
            foreach (string itemName in service.Stock.ItemNames)
            {
                bool exists = service.Stock.Categories
                    .Select(Gear.FindCategory)
                    .OfType<GearCategory>()
                    .SelectMany(category => category.Items)
                    .Any(item => string.Equals(item.Name, itemName, StringComparison.OrdinalIgnoreCase));

                if (!exists)
                {
                    problems.Add(
                        $"Service '{service.Id}' lists item '{itemName}', which is not in any category it stocks.");
                }
            }

            bool stocksSomething = service.Stock.Categories.Count > 0 && service.Stock.MaxItems > 0;
            if (!service.ServiceOnly && !stocksSomething)
            {
                problems.Add($"Service '{service.Id}' has no stock but is not marked serviceOnly.");
            }

            if (service.Stock.MinItems > service.Stock.MaxItems)
            {
                problems.Add($"Service '{service.Id}' has minItems above maxItems.");
            }

            if (service.SignNouns.Count == 0)
            {
                problems.Add($"Service '{service.Id}' has no sign nouns, so it cannot be named.");
            }
        }

        string[] duplicateIds =
        [
            .. Services.Services
                .GroupBy(s => s.Id)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
        ];

        foreach (string id in duplicateIds)
        {
            problems.Add($"Service id '{id}' appears more than once.");
        }
    }

    private void ValidateHouseContent(List<string> problems)
    {
        RequireNotEmpty(problems, Names.GivenNames, "mouse given names");
        RequireNotEmpty(problems, Names.FamilyNames, "mouse family names");
        RequireNotEmpty(problems, Hosts.Hosts, "settlement host objects");

        foreach (HostObject host in Hosts.Hosts)
        {
            if (Hosts.Shapes.Count > 0 && !Hosts.Shapes.ContainsKey(host.Shape))
            {
                problems.Add($"Host '{host.Id}' uses unknown shape '{host.Shape}'.");
            }
        }
    }

    private static void RequireNotEmpty<T>(List<string> problems, IReadOnlyCollection<T> items, string description)
    {
        if (items.Count == 0)
        {
            problems.Add($"The {description} table is empty.");
        }
    }
}
