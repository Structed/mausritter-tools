using System.Text.Json;
using MausritterTools.Core.Data;
using MausritterTools.Core.Generation;
using MausritterTools.Core.Interop.FantasiaArchive;
using MausritterTools.Core.Model;
using MausritterTools.Core.Serialization;

namespace MausritterTools.Core.Tests;

/// <summary>
/// Guards the shape of what this exports.
/// </summary>
/// <remarks>
/// Fantasia Archive validates nothing at all on import — no version, no checksum, no schema — so
/// every one of these mistakes would import "successfully" and simply produce a broken project. The
/// tests are the only thing standing in for a validator.
/// </remarks>
public class FantasiaArchiveExportTests
{
    private static readonly DateTimeOffset Timestamp =
        new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    private static (Settlement Settlement, GenerationOptions Options) Build(
        uint seed = 4242, GameData? data = null)
    {
        GenerationOptions options = new() { Seed = seed, Size = 6, NearHumanTown = true };
        return (new SettlementGenerator(data ?? TestData.Game).Generate(options), options);
    }

    private static FantasiaArchiveExport Export(uint seed = 4242, GameData? data = null)
    {
        GameData game = data ?? TestData.Game;
        (Settlement settlement, GenerationOptions options) = Build(seed, game);

        return FantasiaArchiveExporter.Export(
            settlement, options, game.Text, Timestamp, game.Locale.Code);
    }

    private static IReadOnlyList<JsonElement> Documents(FantasiaArchiveExport export, string type) =>
        PouchDump.ReadDocuments(
            export.Files.Single(f => f.Name == PouchDump.FileNameFor(type)).Content);

    private static IEnumerable<JsonElement> AllDocuments(FantasiaArchiveExport export) =>
        export.Files.SelectMany(file => PouchDump.ReadDocuments(file.Content));

    private static JsonElement Field(JsonElement document, string fieldId) =>
        document.GetProperty("extraFields")
            .EnumerateArray()
            .Single(field => field.GetProperty("id").GetString() == fieldId)
            .GetProperty("value");

    /// <summary>A folder document carries only the settings block, so not every field is present.</summary>
    private static bool TryField(JsonElement document, string fieldId, out JsonElement value)
    {
        foreach (JsonElement field in document.GetProperty("extraFields").EnumerateArray())
        {
            if (field.GetProperty("id").GetString() == fieldId)
            {
                value = field.GetProperty("value");
                return true;
            }
        }

        value = default;
        return false;
    }

    [Fact]
    public void OneFileIsWrittenPerDocumentTypeAndIsNamedAfterIt()
    {
        FantasiaArchiveExport export = Export();

        // The app derives the database name from the file name, so these are not cosmetic.
        Assert.Equal(
            ["locations.txt", "characters.txt", "guilds.txt", "items.txt"],
            export.Files.Select(file => file.Name));
    }

    [Fact]
    public void EachDumpDeclaresItselfCorrectly()
    {
        FantasiaArchiveExport export = Export();

        foreach (string type in FantasiaArchiveBlueprints.AllTypes)
        {
            string content = export.Files.Single(f => f.Name == PouchDump.FileNameFor(type)).Content;
            string[] lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            Assert.Equal(3, lines.Length);

            using JsonDocument header = JsonDocument.Parse(lines[0]);
            JsonElement info = header.RootElement.GetProperty("db_info");

            Assert.Equal(type, info.GetProperty("db_name").GetString());

            int count = Documents(export, type).Count;
            Assert.Equal(count, info.GetProperty("doc_count").GetInt32());

            using JsonDocument trailer = JsonDocument.Parse(lines[2]);
            Assert.Equal(count, trailer.RootElement.GetProperty("seq").GetInt32());
        }
    }

    /// <summary>
    /// The loader writes with <c>new_edits: false</c> and will not invent a revision, so a document
    /// without one is dropped without a word.
    /// </summary>
    [Fact]
    public void EveryDocumentCarriesARevisionThatMatchesItsHistory()
    {
        foreach (JsonElement document in AllDocuments(Export()))
        {
            string revision = document.GetProperty("_rev").GetString()!;

            Assert.Matches("^1-[0-9a-f]{32}$", revision);

            JsonElement revisions = document.GetProperty("_revisions");
            Assert.Equal(1, revisions.GetProperty("start").GetInt32());
            Assert.Equal(
                revision["1-".Length..],
                revisions.GetProperty("ids").EnumerateArray().Single().GetString());
        }
    }

    [Fact]
    public void EveryDocumentIsIdentifiedTheWayTheAppExpects()
    {
        foreach (JsonElement document in AllDocuments(Export()))
        {
            string id = document.GetProperty("_id").GetString()!;
            string type = document.GetProperty("type").GetString()!;

            // A version 4 UUID, which is what the app mints for itself.
            Assert.Matches("^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$", id);

            // The id is duplicated into `id`, and the router depends on this exact URL.
            Assert.Equal(id, document.GetProperty("id").GetString());
            Assert.Equal($"/project/display-content/{type}/{id}", document.GetProperty("url").GetString());

            Assert.Contains(type, FantasiaArchiveBlueprints.AllTypes);
            Assert.False(string.IsNullOrEmpty(document.GetProperty("icon").GetString()));
            Assert.False(string.IsNullOrEmpty(document.GetProperty("hierarchicalPath").GetString()));
        }
    }

    [Fact]
    public void DocumentIdsAreUnique()
    {
        List<string> ids = [.. AllDocuments(Export()).Select(d => d.GetProperty("_id").GetString()!)];

        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// The app only pairs a relationship's far side when a user saves in its own UI, never on
    /// import, so an export that writes one side leaves a link that shows on one document only.
    /// </summary>
    [Fact]
    public void EveryPairedRelationshipIsWrittenFromBothEnds()
    {
        FantasiaArchiveExport export = Export();

        HashSet<(string From, string Field, string To)> links = [];

        foreach (JsonElement document in AllDocuments(export))
        {
            string id = document.GetProperty("_id").GetString()!;

            foreach (JsonElement field in document.GetProperty("extraFields").EnumerateArray())
            {
                string fieldId = field.GetProperty("id").GetString()!;

                foreach (JsonElement target in Targets(field.GetProperty("value")))
                {
                    string pairedField = target.GetProperty("pairedField").GetString()!;
                    if (pairedField.Length == 0)
                    {
                        // One-directional by design, so there is nothing to answer it.
                        continue;
                    }

                    links.Add((id, fieldId, target.GetProperty("_id").GetString()!));

                    // The far side must name this document back, through the field it claims.
                    Assert.Contains(
                        (target.GetProperty("_id").GetString()!, pairedField, id),
                        LinksOf(export));
                }
            }
        }

        Assert.NotEmpty(links);
    }

    [Fact]
    public void RelationshipTargetsNameARealDocumentOfTheRightType()
    {
        FantasiaArchiveExport export = Export();

        Dictionary<string, string> typesById = AllDocuments(export)
            .ToDictionary(d => d.GetProperty("_id").GetString()!, d => d.GetProperty("type").GetString()!);

        foreach (JsonElement document in AllDocuments(export))
        {
            foreach (JsonElement field in document.GetProperty("extraFields").EnumerateArray())
            {
                foreach (JsonElement target in Targets(field.GetProperty("value")))
                {
                    string id = target.GetProperty("_id").GetString()!;

                    Assert.True(typesById.ContainsKey(id), $"Relationship points at unknown document {id}.");
                    Assert.Equal(typesById[id], target.GetProperty("type").GetString());
                }
            }
        }
    }

    [Fact]
    public void ShopsAndKeepersAndStockAllBecomeDocuments()
    {
        GameData game = TestData.Game;
        (Settlement settlement, _) = Build(data: game);
        FantasiaArchiveExport export = Export();

        Assert.Single(Documents(export, FantasiaArchiveBlueprints.Locations));

        int businesses = settlement.Shops.Count + (settlement.Tavern is null ? 0 : 1);

        // Counted the way a reader would count it off the sheet, rather than by re-implementing the
        // exporter's own grouping rule, which would make this test agree with any rule at all.
        int distinctItems = settlement.Shops
            .SelectMany(shop => shop.Stock)
            .Select(entry => entry.DisplayName)
            .Distinct(StringComparer.Ordinal)
            .Count();

        // Each file also carries the folder document that keeps a merge tidy.
        Assert.Equal(businesses + 1, Documents(export, FantasiaArchiveBlueprints.Guilds).Count);
        Assert.Equal(businesses + 1, Documents(export, FantasiaArchiveBlueprints.Characters).Count);
        Assert.Equal(distinctItems + 1, Documents(export, FantasiaArchiveBlueprints.Items).Count);
    }

    /// <summary>
    /// A name is not an identity: the tables hold a blank book and a reading book, and a small
    /// padlock and a large one. Merging a pair like that drops one from the export entirely and
    /// files its price under the other.
    /// </summary>
    [Fact]
    public void GearThatSharesANameButNotAPriceStaysTwoObjects()
    {
        // Searched rather than hard-coded, since which seeds stock a pair is a property of the
        // tables and would change under them.
        for (uint seed = 1; seed <= 200; seed++)
        {
            (Settlement settlement, _) = Build(seed);

            List<StockEntry> stock = [.. settlement.Shops.SelectMany(shop => shop.Stock)];

            List<string> collidingNames =
            [
                .. stock
                    .GroupBy(entry => entry.Item.Name, StringComparer.Ordinal)
                    .Where(group => group.Select(e => e.DisplayName).Distinct(StringComparer.Ordinal).Count() > 1)
                    .Select(group => group.Key)
            ];

            if (collidingNames.Count == 0)
            {
                continue;
            }

            FantasiaArchiveExport export = Export(seed);

            List<string> exported =
            [
                .. Documents(export, FantasiaArchiveBlueprints.Items)
                    .Where(d => Field(d, FantasiaArchiveBlueprints.Common.CategorySwitch).ValueKind
                        != JsonValueKind.True)
                    .Select(d => Field(d, FantasiaArchiveBlueprints.Common.Name).GetString()!)
            ];

            foreach (string name in collidingNames)
            {
                IEnumerable<string> expected = stock
                    .Where(e => string.Equals(e.Item.Name, name, StringComparison.Ordinal))
                    .Select(e => e.DisplayName)
                    .Distinct(StringComparer.Ordinal);

                foreach (string variant in expected)
                {
                    Assert.Contains(variant, exported);
                }
            }

            return;
        }

        Assert.Fail("No settlement in the first 200 seeds stocked two variants of one gear name.");
    }

    [Fact]
    public void ExportingTheSameSettlementTwiceProducesTheSameFiles()
    {
        FantasiaArchiveExport first = Export();
        FantasiaArchiveExport second = Export();

        Assert.Equal(
            first.Files.Select(f => (f.Name, f.Content)),
            second.Files.Select(f => (f.Name, f.Content)));
    }

    [Fact]
    public void DifferentSettlementsDoNotShareDocumentIds()
    {
        HashSet<string> first = [.. AllDocuments(Export(1)).Select(d => d.GetProperty("_id").GetString()!)];
        HashSet<string> second = [.. AllDocuments(Export(2)).Select(d => d.GetProperty("_id").GetString()!)];

        Assert.Empty(first.Intersect(second, StringComparer.Ordinal));
    }

    /// <summary>
    /// Two settlements can share a seed and still be different places, because the size, the
    /// terrain, the human-town switch and every lock change what that seed produces.
    /// </summary>
    /// <remarks>
    /// Ids drawn from the seed alone would collide across all of them, and the app would then
    /// discard the second export in silence rather than importing it, because it loads with
    /// <c>new_edits: false</c>. The reader would be shown the older settlement and told nothing.
    /// </remarks>
    [Theory]
    [InlineData("size")]
    [InlineData("humanTown")]
    [InlineData("terrain")]
    [InlineData("pin")]
    [InlineData("reroll")]
    public void SettlementsSharingASeedButNothingElseDoNotShareDocumentIds(string change)
    {
        GenerationOptions baseline = new() { Seed = 909, Size = 5 };

        GenerationOptions varied = change switch
        {
            "size" => baseline with { Size = 6 },
            "humanTown" => baseline with { NearHumanTown = true },
            "terrain" => baseline with { Terrain = "forest" },
            "pin" => baseline.WithPin("settlement/name", "Nibblewick"),
            "reroll" => baseline.WithReroll("settlement/event"),
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };

        Assert.Equal(baseline.Seed, varied.Seed);
        Assert.Empty(IdsOf(baseline).Intersect(IdsOf(varied), StringComparer.Ordinal));
    }

    private static HashSet<string> IdsOf(GenerationOptions options)
    {
        GameData game = TestData.Game;
        Settlement settlement = new SettlementGenerator(game).Generate(options);

        FantasiaArchiveExport export = FantasiaArchiveExporter.Export(
            settlement, options, game.Text, Timestamp, game.Locale.Code);

        return [.. AllDocuments(export).Select(d => d.GetProperty("_id").GetString()!)];
    }

    /// <summary>
    /// One piece of gear is one object in the world, however many shops sell it.
    /// </summary>
    [Fact]
    public void AnItemSoldInSeveralShopsIsOneDocumentLinkedToEach()
    {
        FantasiaArchiveExport export = Export();

        List<JsonElement> items =
        [
            .. Documents(export, FantasiaArchiveBlueprints.Items)
                .Where(d => Field(d, FantasiaArchiveBlueprints.Common.CategorySwitch).ValueKind
                    != JsonValueKind.True)
        ];

        List<string> names =
            [.. items.Select(d => Field(d, FantasiaArchiveBlueprints.Common.Name).GetString()!)];

        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());

        // A city stocks the same staples in more than one shop, so at least one item must show it.
        Assert.Contains(items, item =>
            TryField(item, FantasiaArchiveBlueprints.Item.ConnectedGroups, out JsonElement groups) &&
            Targets(groups).Count() > 1);
    }

    /// <summary>
    /// The app's own select values are keys in someone else's application, so they stay English even
    /// when everything around them is translated. Translating one would leave the dropdown empty.
    /// </summary>
    [Fact]
    public void SelectValuesStayEnglishKeysInATranslatedExport()
    {
        GameData german = TestData.In(Locale.German);
        (Settlement settlement, GenerationOptions options) = Build(data: german);

        FantasiaArchiveExport export = FantasiaArchiveExporter.Export(
            settlement, options, german.Text, Timestamp, german.Locale.Code);

        JsonElement location = Documents(export, FantasiaArchiveBlueprints.Locations).Single();

        Assert.Equal(
            FantasiaArchiveBlueprints.LocationTypeForSize(settlement.Size.SizeValue),
            Field(location, FantasiaArchiveBlueprints.Location.LocationType).GetString());

        // "City" for a Stadt: the key the app's dropdown offers, not the word the reader sees.
        Assert.Equal("City", Field(location, FantasiaArchiveBlueprints.Location.LocationType).GetString());

        string[] permitted =
        [
            "Guild", "Trade group", "Company", "Mercenary group", "Security group", "Military group",
            "Economical group", "Civil group", "Criminal group", "Academic group", "Faction",
            "Charity", "Spy/Underground network", "Secret society", "Other"
        ];

        foreach (JsonElement guild in Documents(export, FantasiaArchiveBlueprints.Guilds))
        {
            if (!TryField(guild, FantasiaArchiveBlueprints.Guild.GroupType, out JsonElement groupType) ||
                groupType.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (JsonElement value in groupType.EnumerateArray())
            {
                Assert.Contains(value.GetString(), permitted);
            }
        }
    }

    /// <summary>
    /// The app's repair tool deletes these six field ids outright, so nothing may be stored under
    /// one of them.
    /// </summary>
    [Fact]
    public void NoFieldUsesAnIdTheRepairToolDeletes()
    {
        string[] stripped = ["strength", "constitution", "dexterity", "intellect", "wisdom", "charisma"];

        foreach (JsonElement document in AllDocuments(Export()))
        {
            foreach (JsonElement field in document.GetProperty("extraFields").EnumerateArray())
            {
                Assert.DoesNotContain(field.GetProperty("id").GetString(), stripped);
            }
        }
    }

    /// <summary>The licence has to travel with the content, since a document can be read alone.</summary>
    [Fact]
    public void EveryDocumentCarriesTheAttribution()
    {
        foreach (JsonElement document in AllDocuments(Export()))
        {
            string prose =
                (Field(document, FantasiaArchiveBlueprints.Common.Description).GetString() ?? "") +
                (Field(document, FantasiaArchiveBlueprints.Common.CategoryDescription).GetString() ?? "");

            Assert.Contains("Mausritter", prose, StringComparison.Ordinal);
            Assert.Contains("CC BY 4.0", prose, StringComparison.Ordinal);
        }
    }

    /// <summary>A German export is a fan translation, and CC BY requires that to be declared.</summary>
    [Fact]
    public void ATranslatedExportDeclaresItselfAsOne()
    {
        GameData german = TestData.In(Locale.German);
        (Settlement settlement, GenerationOptions options) = Build(data: german);

        FantasiaArchiveExport export = FantasiaArchiveExporter.Export(
            settlement, options, german.Text, Timestamp, german.Locale.Code);

        string note = german.Text.Licence.TranslationNote ?? "";
        Assert.NotEmpty(note);

        JsonElement location = Documents(export, FantasiaArchiveBlueprints.Locations).Single();
        Assert.Contains(
            note,
            System.Net.WebUtility.HtmlDecode(
                Field(location, FantasiaArchiveBlueprints.Common.Description).GetString()!),
            StringComparison.Ordinal);
    }

    [Fact]
    public void TheSettlementDocumentCarriesTheStateAndNothingElseDoes()
    {
        FantasiaArchiveExport export = Export();

        List<JsonElement> carriers =
        [
            .. AllDocuments(export)
                .Where(d => PouchDump.FieldString(d, FantasiaArchiveBlueprints.StateField) is not null)
        ];

        JsonElement carrier = Assert.Single(carriers);
        Assert.Equal(FantasiaArchiveBlueprints.Locations, carrier.GetProperty("type").GetString());
    }

    private static IEnumerable<JsonElement> Targets(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object ||
            !value.TryGetProperty("value", out JsonElement inner))
        {
            yield break;
        }

        if (inner.ValueKind == JsonValueKind.Object)
        {
            yield return inner;
        }
        else if (inner.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement target in inner.EnumerateArray())
            {
                yield return target;
            }
        }
    }

    private static HashSet<(string From, string Field, string To)> LinksOf(FantasiaArchiveExport export)
    {
        HashSet<(string, string, string)> links = [];

        foreach (JsonElement document in AllDocuments(export))
        {
            string id = document.GetProperty("_id").GetString()!;

            foreach (JsonElement field in document.GetProperty("extraFields").EnumerateArray())
            {
                string fieldId = field.GetProperty("id").GetString()!;

                foreach (JsonElement target in Targets(field.GetProperty("value")))
                {
                    links.Add((id, fieldId, target.GetProperty("_id").GetString()!));
                }
            }
        }

        return links;
    }
}

/// <summary>Guards the journey out to Fantasia Archive and back.</summary>
public class FantasiaArchiveRoundTripTests
{
    private static (Settlement Settlement, GenerationOptions Options, GameData Data) Build(
        GenerationOptions options, Locale? locale = null)
    {
        GameData data = locale is null ? TestData.Game : TestData.In(locale);
        return (new SettlementGenerator(data).Generate(options), options, data);
    }

    private static IReadOnlyList<string> Dumps(FantasiaArchiveExport export) =>
        [.. export.Files.Select(file => file.Content)];

    [Fact]
    public void AnExportedSettlementComesBackIdentical()
    {
        GenerationOptions options = new() { Seed = 31337, Size = 5, NearHumanTown = true, Terrain = "forest" };
        (Settlement original, _, GameData data) = Build(options);

        FantasiaArchiveExport export =
            FantasiaArchiveExporter.Export(original, options, data.Text, locale: "en");

        SettlementImport imported = FantasiaArchiveImporter.Read(Dumps(export));
        Settlement rebuilt = new SettlementGenerator(data).Generate(imported.Options);

        Assert.Equal("en", imported.Locale);
        Assert.Equal(original.Seed, rebuilt.Seed);
        Assert.Equal(original.Name, rebuilt.Name);
        Assert.Equal(original.Host.Id, rebuilt.Host.Id);
        Assert.Equal(
            original.Shops.Select(s => (s.Id, s.SignName, s.Keeper.FullName, s.Stock.Count)),
            rebuilt.Shops.Select(s => (s.Id, s.SignName, s.Keeper.FullName, s.Stock.Count)));
    }

    [Fact]
    public void LocksAndHandEditsSurviveTheJourney()
    {
        GenerationOptions options = new GenerationOptions { Seed = 777, Size = 4 }
            .WithPin("settlement/name", "Nibblewick")
            .WithReroll("settlement/event");

        (Settlement original, _, GameData data) = Build(options);

        FantasiaArchiveExport export =
            FantasiaArchiveExporter.Export(original, options, data.Text, locale: "en");

        GenerationOptions restored = FantasiaArchiveImporter.Read(Dumps(export)).Options;

        Assert.Equal("Nibblewick", restored.Pins["settlement/name"]);
        Assert.Equal(1, restored.Rerolls["settlement/event"]);
        Assert.Equal(options.Seed, restored.Seed);
        Assert.Equal(options.Size, restored.Size);
    }

    [Fact]
    public void TheLanguageItWasWrittenInComesBackWithIt()
    {
        GenerationOptions options = new() { Seed = 24, Size = 5 };
        (Settlement settlement, _, GameData german) = Build(options, Locale.German);

        FantasiaArchiveExport export = FantasiaArchiveExporter.Export(
            settlement, options, german.Text, locale: Locale.German.Code);

        Assert.Equal(Locale.German.Code, FantasiaArchiveImporter.Read(Dumps(export)).Locale);
    }

    [Fact]
    public void TheArchiveHoldsTheProjectFolderAndKeepsTheInstructionsOutOfIt()
    {
        GenerationOptions options = new() { Seed = 5150, Size = 5 };
        (Settlement settlement, _, GameData data) = Build(options);

        FantasiaArchiveExport export =
            FantasiaArchiveExporter.Export(settlement, options, data.Text, locale: "en");

        byte[] zip = FantasiaArchivePackage.Create(export, "how to import");

        using MemoryStream stream = new(zip);
        using System.IO.Compression.ZipArchive archive = new(stream);

        List<string> names = [.. archive.Entries.Select(entry => entry.FullName)];

        // The merge feeds every file in the folder to its database loader, so a readme sitting
        // beside the dumps would be parsed as a database and break the import.
        Assert.Contains(FantasiaArchivePackage.ReadMeName, names);
        Assert.DoesNotContain($"{export.FolderName}/{FantasiaArchivePackage.ReadMeName}", names);

        Assert.Equal(
            [.. export.Files.Select(file => $"{export.FolderName}/{file.Name}").Order(StringComparer.Ordinal)],
            [.. names.Where(name => name.Contains('/', StringComparison.Ordinal)).Order(StringComparer.Ordinal)]);
    }

    [Fact]
    public void AnArchiveRoundTripsThroughItsOwnZip()
    {
        GenerationOptions options = new() { Seed = 8080, Size = 6 };
        (Settlement original, _, GameData data) = Build(options);

        FantasiaArchiveExport export =
            FantasiaArchiveExporter.Export(original, options, data.Text, locale: "en");

        using MemoryStream stream = new(FantasiaArchivePackage.Create(export, "how to import"));
        SettlementImport imported =
            FantasiaArchiveImporter.Read(FantasiaArchivePackage.ExtractDumps(stream));

        Assert.Equal(original.Seed, imported.Options.Seed);
    }

    /// <summary>
    /// A place someone wrote by hand in Fantasia Archive has no seed, and a settlement cannot be
    /// worked backwards out of its prose.
    /// </summary>
    [Fact]
    public void AProjectThisToolDidNotWriteIsRefusedPlainly()
    {
        string foreign = PouchDump.Write(
            FantasiaArchiveBlueprints.Locations,
            [
                new Document
                {
                    Id = "0f7f2a4c-1b2c-4d3e-8f5a-6b7c8d9e0f1a",
                    Type = FantasiaArchiveBlueprints.Locations,
                    Revision = "1-" + new string('a', 32),
                    Fields = [new DocumentField("name", new TextValue("Somewhere else"))]
                }
            ],
            DateTimeOffset.UnixEpoch);

        SettlementFormatException error =
            Assert.Throws<SettlementFormatException>(() => FantasiaArchiveImporter.Read([foreign]));

        Assert.Contains("no settlement written by this tool", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RubbishIsRefusedRatherThanCrashing()
    {
        Assert.Throws<SettlementFormatException>(
            () => FantasiaArchiveImporter.Read(["not json at all", "{\"half\":"]));
    }

    /// <summary>
    /// An editor that saves a byte-order mark must not turn a good export into a foreign file.
    /// </summary>
    [Fact]
    public void AByteOrderMarkDoesNotDisguiseOneOfOurOwnExports()
    {
        GenerationOptions options = new() { Seed = 606, Size = 4 };
        (Settlement original, _, GameData data) = Build(options);

        string json = '\uFEFF' + SettlementSerializer.ToJson(original, options, locale: "en");

        Assert.True(SettlementSerializer.IsSettlementJson(json));
        Assert.Equal(original.Seed, SettlementSerializer.Read(json).Options.Seed);

        // And it is still recognisable as ours rather than being mistaken for someone else's file.
        Settlement rebuilt = new SettlementGenerator(data).Generate(SettlementSerializer.FromJson(json));
        Assert.Equal(original.Name, rebuilt.Name);
    }
}
