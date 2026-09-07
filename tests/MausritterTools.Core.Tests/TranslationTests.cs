using System.Text.Json.Nodes;
using MausritterTools.Core.Data;
using MausritterTools.Core.Generation;
using MausritterTools.Core.Model;
using MausritterTools.Core.Randomness;

namespace MausritterTools.Core.Tests;

/// <summary>
/// Guards the two promises the translation layer makes: that every language says everything, and
/// that changing language does not change the settlement.
/// </summary>
public class TranslationTests
{
    /// <summary>Every language the app ships, other than the one the data files are written in.</summary>
    public static TheoryData<string> TranslatedLocales =>
        [.. Locale.All.Where(l => !l.IsCanonical).Select(l => l.Code)];

    /// <summary>
    /// Property names that are the same in every language wherever they appear.
    /// </summary>
    /// <remarks>
    /// Identifiers, cross-references, dice expressions and prices. None of them is prose, and
    /// translating one would either break a lookup or corrupt a calculation.
    /// </remarks>
    private static readonly HashSet<string> UniversalKeyFields = new(StringComparer.Ordinal)
    {
        "id", "shape", "terrain", "categories", "itemNames",
        "generatedBy", "sourceFiles", "url", "licenceUrl", "version", "work", "licence",
        "payment", "number", "priceText", "wagesText", "sizeRoll", "roll", "template",
        "locale"
    };

    /// <summary>
    /// Grammatical metadata a translation adds and the canonical files have no use for.
    /// </summary>
    private static readonly HashSet<string> TranslationOnlyFields = new(StringComparer.Ordinal)
    {
        "nameGender", "nameBGenders", "signNounGenders", "phrase", "label", "translationNote"
    };

    /// <summary>
    /// The two places where <c>name</c> is a key rather than a caption, and what stands in for it.
    /// </summary>
    /// <remarks>
    /// The sharpest edge in the whole feature, and the reason this is spelt out by path rather than
    /// by property name: <c>name</c> is ordinary prose almost everywhere, but on a gear item and a
    /// hireling it is the value three files join on. Translating one of those does not fail loudly.
    /// It empties a shop's shelves and blanks an item card, and looks like a generator bug months
    /// later. The gear item shows <c>label</c> instead; a hireling's name is never displayed at
    /// all, so it needs no caption.
    /// </remarks>
    private static readonly Dictionary<string, string?> KeysByPath = new(StringComparer.Ordinal)
    {
        ["srd/gear.json:$.categories[].items[].name"] = "label",
        ["srd/hirelings.json:$.hirelings[].name"] = null,

        // German cannot assemble a place from a preposition and a name, because the article and
        // adjective agree with the noun's gender. It supplies the whole phrase instead.
        ["house/hosts.json:$.hosts[].preposition"] = "phrase"
    };

    /// <summary>Subtrees a translation defines for itself, which have no canonical counterpart.</summary>
    private static readonly HashSet<string> TranslationOwnedSubtrees = new(StringComparer.Ordinal)
    {
        "ui.json:$.grammar.articles"
    };

    [Theory]
    [MemberData(nameof(TranslatedLocales))]
    public void EveryDataFileHasATranslation(string code)
    {
        Locale locale = Locale.FromCode(code);

        foreach (string path in DataFiles)
        {
            string overlay = Path.Combine(
                TestData.DataRoot, locale.OverlayRoot.Replace('/', Path.DirectorySeparatorChar), path);

            Assert.True(File.Exists(overlay), $"No {locale.EnglishName} translation for '{path}'.");
        }
    }

    /// <summary>
    /// A translated table must have exactly as many rows as the original, in the same order.
    /// </summary>
    /// <remarks>
    /// This is the invariant that makes a shared link mean anything. Every value in a settlement is
    /// an index into a table, so a German table with one row fewer would silently shift every roll
    /// after it and turn the same seed into a different place.
    /// </remarks>
    [Theory]
    [MemberData(nameof(TranslatedLocales))]
    public void TranslatedTablesLineUpWithTheOriginals(string code)
    {
        Locale locale = Locale.FromCode(code);

        foreach (string path in DataFiles)
        {
            JsonNode canonical = Read(Path.Combine(TestData.DataRoot, Native(path)));
            JsonNode translated = Read(Path.Combine(
                TestData.DataRoot, Native(locale.OverlayRoot), Native(path)));

            List<string> problems = [];
            Compare(canonical, translated, $"{path}:$", problems);

            Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
        }
    }

    /// <summary>
    /// Every piece of prose in the canonical files is said in the translation too.
    /// </summary>
    [Theory]
    [MemberData(nameof(TranslatedLocales))]
    public void EveryTranslatableStringIsTranslated(string code)
    {
        Locale locale = Locale.FromCode(code);
        List<string> missing = [];

        foreach (string path in DataFiles)
        {
            JsonNode canonical = Read(Path.Combine(TestData.DataRoot, Native(path)));
            JsonNode translated = Read(Path.Combine(
                TestData.DataRoot, Native(locale.OverlayRoot), Native(path)));

            RequireTranslations(canonical, translated, $"{path}:$", missing);
        }

        Assert.True(
            missing.Count == 0,
            $"{missing.Count} untranslated {locale.EnglishName} entries:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, missing.Take(40)));
    }

    /// <summary>
    /// The same seed produces the same settlement in every language: the same size, the same host,
    /// the same shops in the same order, the same rows of every table.
    /// </summary>
    [Theory]
    [MemberData(nameof(TranslatedLocales))]
    public void ASeedProducesTheSameSettlementInEveryLanguage(string code)
    {
        Locale locale = Locale.FromCode(code);

        SettlementGenerator canonical = new(TestData.In(Locale.English));
        SettlementGenerator translated = new(TestData.In(locale));

        for (uint seed = 1; seed <= 60; seed++)
        {
            GenerationOptions options = new() { Seed = seed };

            Settlement english = canonical.Generate(options);
            Settlement other = translated.Generate(options);

            Assert.Equal(english.Size.SizeValue, other.Size.SizeValue);
            Assert.Equal(english.GovernanceRoll, other.GovernanceRoll);
            Assert.Equal(english.NotableFeatures.Count, other.NotableFeatures.Count);
            Assert.Equal(english.Industries.Count, other.Industries.Count);
            Assert.Equal(english.Shops.Count, other.Shops.Count);
            Assert.Equal(english.Tavern is null, other.Tavern is null);

            // Services are identified by id, which never changes; only their names do.
            Assert.Equal(
                [.. english.Shops.Select(s => s.Service.Id)],
                [.. other.Shops.Select(s => s.Service.Id)]);

            // The shelves must hold the same goods, which is what proves the item-name keys were
            // left alone: translate one and the allow-lists stop matching and the shop empties.
            Assert.Equal(
                [.. english.Shops.Select(s => s.Stock.Count)],
                [.. other.Shops.Select(s => s.Stock.Count)]);

            // Compared by key and in key order, because each shop lists its stock alphabetically by
            // the name the reader sees, and that ordering properly belongs to the language.
            Assert.Equal(
                [.. english.Shops.SelectMany(s => s.Stock).Select(e => e.Item.Name).Order(StringComparer.Ordinal)],
                [.. other.Shops.SelectMany(s => s.Stock).Select(e => e.Item.Name).Order(StringComparer.Ordinal)]);

            // A lock on a table entry stores a position, so those must be identical; a lock on a
            // name stores the words, and those are exactly what a translation is meant to change.
            Assert.Equal(
                english.PinValues.Keys.Order(StringComparer.Ordinal),
                other.PinValues.Keys.Order(StringComparer.Ordinal));

            foreach ((string path, string pin) in english.PinValues)
            {
                if (PinReference.IsReference(pin))
                {
                    Assert.Equal(pin, other.PinValues[path]);
                }
                else
                {
                    Assert.False(
                        PinReference.IsReference(other.PinValues[path]),
                        $"'{path}' is a literal in English but a table position in {locale.EnglishName}.");
                }
            }
        }
    }

    /// <summary>
    /// A value locked in one language is still the same value after switching to another.
    /// </summary>
    [Theory]
    [MemberData(nameof(TranslatedLocales))]
    public void ALockSurvivesAChangeOfLanguage(string code)
    {
        Locale locale = Locale.FromCode(code);

        SettlementGenerator canonical = new(TestData.In(Locale.English));
        SettlementGenerator translated = new(TestData.In(locale));

        string[] paths =
        [
            "settlement/size", "settlement/host", "settlement/governance",
            "settlement/inhabitants", "settlement/features", "settlement/industries",
            "settlement/event"
        ];

        for (uint seed = 1; seed <= 30; seed++)
        {
            Settlement before = translated.Generate(new GenerationOptions { Seed = seed });

            // Lock everything, exactly as the page does, then re-read it in the other language.
            GenerationOptions locked = new() { Seed = seed };
            foreach (string path in paths)
            {
                locked = locked.WithPin(path, before.PinValues[path]);
            }

            Settlement english = canonical.Generate(locked with { Seed = seed + 5000 });
            Settlement again = translated.Generate(locked with { Seed = seed + 5000 });

            // The lock held in both languages: the German reading is unchanged, and the English one
            // names the same rows rather than carrying German words across.
            Assert.Equal(before.Size.Name, again.Size.Name);
            Assert.Equal(before.Event, again.Event);
            Assert.Equal(before.Inhabitants, again.Inhabitants);
            Assert.Equal(before.Size.SizeValue, english.Size.SizeValue);
            Assert.NotEqual(english.Event, again.Event);
        }
    }

    /// <summary>
    /// Every tavern and shop sign a translation can produce is grammatical.
    /// </summary>
    /// <remarks>
    /// German signs read "Zum krummen Käfer" or "Zur roten Rose": the article agrees with the
    /// noun's gender. A noun with no gender would leave the article blank and the sign starting
    /// mid-phrase, so every noun a sign can draw on must be tagged.
    /// </remarks>
    [Theory]
    [MemberData(nameof(TranslatedLocales))]
    public void SignNounsAreTaggedWhereTheLanguageNeedsAnArticle(string code)
    {
        Locale locale = Locale.FromCode(code);
        GameData data = TestData.In(locale);

        if (data.Text.Grammar.Articles.DativeDefinite.Count == 0)
        {
            return;
        }

        TavernTable taverns = data.Settlement.Taverns;
        Assert.Equal(taverns.NameB.Count, taverns.NameBGenders.Count);

        foreach (ServiceDefinition service in data.Services.Services)
        {
            Assert.True(
                service.SignNouns.Count == service.SignNounGenders.Count,
                $"Service '{service.Id}' has {service.SignNouns.Count} sign nouns but " +
                $"{service.SignNounGenders.Count} genders.");
        }

        IReadOnlyDictionary<string, string> articles = data.Text.Grammar.Articles.DativeDefinite;

        foreach (string gender in taverns.NameBGenders.Concat(
                     data.Services.Services.SelectMany(s => s.SignNounGenders)))
        {
            Assert.True(articles.ContainsKey(gender), $"No article for gender '{gender}'.");
        }
    }

    /// <summary>Generated names never leave a placeholder or a stray gap behind.</summary>
    [Theory]
    [MemberData(nameof(TranslatedLocales))]
    public void GeneratedNamesAreWellFormed(string code)
    {
        GameData data = TestData.In(Locale.FromCode(code));

        for (uint seed = 1; seed <= 200; seed++)
        {
            string tavern = NameForge.TavernName(
                new DiceRoller(SeedDerivation.CreateStream(seed, "tavern")),
                data.Settlement.Taverns,
                data.Text.Grammar);

            AssertReadable(tavern);

            foreach (ServiceDefinition service in data.Services.Services)
            {
                AssertReadable(NameForge.ShopSign(
                    new DiceRoller(SeedDerivation.CreateStream(seed, "sign")),
                    data.Services,
                    service,
                    "Distelflaum",
                    data.Text.Grammar));
            }
        }

        static void AssertReadable(string name)
        {
            Assert.False(string.IsNullOrWhiteSpace(name));
            Assert.DoesNotContain('{', name);
            Assert.DoesNotContain('}', name);
            Assert.DoesNotContain("  ", name, StringComparison.Ordinal);
            Assert.False(name.StartsWith(' ') || name.EndsWith(' '), $"'{name}' has a stray space.");
        }
    }

    /// <summary>The sentences a translation composes read cleanly.</summary>
    [Theory]
    [MemberData(nameof(TranslatedLocales))]
    public void ComposedSentencesReadCleanly(string code)
    {
        SettlementGenerator generator = new(TestData.In(Locale.FromCode(code)));

        for (uint seed = 1; seed <= 60; seed++)
        {
            Settlement settlement = generator.Generate(new GenerationOptions { Seed = seed });

            Assert.EndsWith(".", settlement.Summary, StringComparison.Ordinal);
            Assert.DoesNotContain("  ", settlement.Summary, StringComparison.Ordinal);
            Assert.DoesNotContain('{', settlement.Summary);

            foreach (Shop shop in settlement.Shops)
            {
                if (shop.Keeper.RelationshipSummary is { } relationship)
                {
                    Assert.DoesNotContain('{', relationship);
                    Assert.Contains(": ", relationship, StringComparison.Ordinal);
                }
            }
        }
    }

    private static readonly string[] DataFiles =
    [
        GameData.UiTextPath,
        GameData.SettlementPath,
        GameData.NpcPath,
        GameData.GearPath,
        GameData.HirelingsPath,
        GameData.SpellsPath,
        GameData.ServicesPath,
        GameData.NamesPath,
        GameData.HostsPath
    ];

    private static string Native(string path) => path.Replace('/', Path.DirectorySeparatorChar);

    /// <summary>Strips array positions, so one rule can describe every row of a table.</summary>
    private static string Shape(string path)
    {
        System.Text.StringBuilder builder = new(path.Length);
        bool inIndex = false;

        foreach (char c in path)
        {
            if (c == '[')
            {
                inIndex = true;
                builder.Append("[]");
            }
            else if (c == ']')
            {
                inIndex = false;
            }
            else if (!inIndex)
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    /// <summary>Whether this location holds a key rather than something a reader sees.</summary>
    private static bool IsKey(string path, string property) =>
        UniversalKeyFields.Contains(property) || KeysByPath.ContainsKey(Shape(path));

    private static JsonNode Read(string path) =>
        JsonNode.Parse(File.ReadAllText(path), documentOptions: LocalisingDataFileReader.DocumentOptions)
        ?? throw new InvalidOperationException($"'{path}' is empty.");

    /// <summary>Reports any place the two trees disagree in shape.</summary>
    private static void Compare(JsonNode canonical, JsonNode? translated, string path, List<string> problems)
    {
        if (translated is null || TranslationOwnedSubtrees.Contains(Shape(path)))
        {
            return;
        }

        switch (canonical, translated)
        {
            case (JsonObject canonicalObject, JsonObject translatedObject):
                foreach (KeyValuePair<string, JsonNode?> property in translatedObject)
                {
                    string childPath = $"{path}.{property.Key}";

                    // A translation may add grammatical metadata the original has no use for.
                    if (canonicalObject.ContainsKey(property.Key))
                    {
                        if (canonicalObject[property.Key] is { } child)
                        {
                            Compare(child, property.Value, childPath, problems);
                        }
                    }
                    else if (!TranslationOnlyFields.Contains(property.Key))
                    {
                        problems.Add(
                            $"{childPath} is not a field of the canonical file. " +
                            "A typo here would silently translate nothing.");
                    }
                }

                break;

            case (JsonArray canonicalArray, JsonArray translatedArray):
                if (canonicalArray.Count != translatedArray.Count)
                {
                    problems.Add(
                        $"{path} has {translatedArray.Count} entries but the original has " +
                        $"{canonicalArray.Count}. Tables are rolled on by position.");
                    break;
                }

                for (int i = 0; i < canonicalArray.Count; i++)
                {
                    if (canonicalArray[i] is { } element)
                    {
                        Compare(element, translatedArray[i], $"{path}[{i}]", problems);
                    }
                }

                break;

            case (JsonObject or JsonArray, _):
            case (_, JsonObject or JsonArray):
                problems.Add($"{path} changed shape between the original and the translation.");
                break;
        }
    }

    /// <summary>Reports every prose string the translation does not supply a rendering for.</summary>
    private static void RequireTranslations(
        JsonNode canonical, JsonNode? translated, string path, List<string> missing)
    {
        if (TranslationOwnedSubtrees.Contains(Shape(path)))
        {
            return;
        }

        switch (canonical)
        {
            case JsonObject canonicalObject:
                JsonObject? translatedObject = translated as JsonObject;

                foreach (KeyValuePair<string, JsonNode?> property in canonicalObject)
                {
                    if (property.Value is null)
                    {
                        continue;
                    }

                    string childPath = $"{path}.{property.Key}";

                    // A key is the same word in every language, but where a caption stands in for
                    // one, that caption has to be there.
                    if (IsKey(childPath, property.Key))
                    {
                        if (KeysByPath.TryGetValue(Shape(childPath), out string? caption) &&
                            caption is not null &&
                            translatedObject?[caption] is not JsonValue)
                        {
                            missing.Add($"{path}.{caption} (stands in for '{property.Key}')");
                        }

                        continue;
                    }

                    RequireTranslations(
                        property.Value, translatedObject?[property.Key], childPath, missing);
                }

                break;

            case JsonArray canonicalArray:
                JsonArray? translatedArray = translated as JsonArray;

                for (int i = 0; i < canonicalArray.Count; i++)
                {
                    if (canonicalArray[i] is { } element)
                    {
                        RequireTranslations(
                            element,
                            translatedArray is not null && i < translatedArray.Count ? translatedArray[i] : null,
                            $"{path}[{i}]",
                            missing);
                    }
                }

                break;

            case JsonValue value when value.TryGetValue(out string? text) && NeedsTranslating(text):
                if (translated is not JsonValue)
                {
                    missing.Add($"{path} (\"{Shorten(text)}\")");
                }

                break;
        }
    }

    /// <summary>
    /// Whether a string is prose rather than a token.
    /// </summary>
    /// <remarks>
    /// A locale code, a bare number or a lone symbol reads the same everywhere and demanding a
    /// translation for it would only produce busywork.
    /// </remarks>
    private static bool NeedsTranslating(string? text) =>
        text is { Length: > 1 } && text.Any(char.IsLetter);

    private static string Shorten(string text) =>
        text.Length <= 50 ? text : text[..50] + "…";
}
