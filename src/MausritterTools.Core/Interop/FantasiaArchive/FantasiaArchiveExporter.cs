using System.Globalization;
using System.Net;
using System.Text;
using MausritterTools.Core.Data;
using MausritterTools.Core.Generation;
using MausritterTools.Core.Model;
using MausritterTools.Core.Randomness;
using MausritterTools.Core.Serialization;

namespace MausritterTools.Core.Interop.FantasiaArchive;

/// <summary>One file in an exported project folder.</summary>
public sealed record ExportedFile(string Name, string Content);

/// <summary>A settlement rendered as a Fantasia Archive project folder.</summary>
/// <param name="FolderName">The folder the files belong in, which is what the user points the app at.</param>
/// <param name="Files">The database dumps, one per document type.</param>
public sealed record FantasiaArchiveExport(string FolderName, IReadOnlyList<ExportedFile> Files);

/// <summary>
/// Turns a settlement into a Fantasia Archive project folder.
/// </summary>
/// <remarks>
/// <para>
/// The settlement becomes a place, its tavern and shops become organisations, their keepers become
/// people and their stock becomes objects, all cross-linked. Fantasia Archive only maintains the far
/// side of a relationship when a user saves a document in its own UI, never on import, so both sides
/// of every pairing are written here by hand.
/// </para>
/// <para>
/// Everything the generator can rebuild from is carried in a single hidden entry on the settlement's
/// document, so a project that has been through Fantasia Archive can be brought back here and
/// re-rolled.
/// </para>
/// </remarks>
public static class FantasiaArchiveExporter
{
    /// <summary>Identifies this tool's state so a foreign file is not mistaken for one of ours.</summary>
    public const string StateFormatId = "mausritter-tools/fantasia-archive-state";

    /// <summary>Renders a settlement as a project folder.</summary>
    public static FantasiaArchiveExport Export(
        Settlement settlement,
        GenerationOptions options,
        UiText text,
        DateTimeOffset? timestamp = null,
        string? locale = null)
    {
        ArgumentNullException.ThrowIfNull(settlement);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(text);

        DateTimeOffset written = timestamp ?? DateTimeOffset.UtcNow;
        Builder builder = new(settlement, options, text, written, locale);

        return new FantasiaArchiveExport(FolderName(settlement), builder.Build());
    }

    /// <summary>The folder an export belongs in, e.g. <c>owlmill-c21p6</c>.</summary>
    public static string FolderName(Settlement settlement)
    {
        ArgumentNullException.ThrowIfNull(settlement);

        // The same slug the JSON export uses, minus its extension, so the two sit together tidily
        // in a downloads folder.
        string fileName = SettlementSerializer.SuggestFileName(settlement);
        return Path.GetFileNameWithoutExtension(fileName);
    }

    /// <summary>
    /// Assembles one export.
    /// </summary>
    /// <remarks>
    /// Identities for every document are minted up front, because a relationship has to name a
    /// document that may not have been built yet, and both ends have to agree.
    /// </remarks>
    private sealed class Builder
    {
        private const string MausritterTag = "Mausritter";

        private readonly Settlement _settlement;
        private readonly GenerationOptions _options;
        private readonly UiText _text;
        private readonly DateTimeOffset _timestamp;
        private readonly string? _locale;
        private readonly CultureInfo _culture;

        private readonly Identity _settlementId;
        private readonly string _signature;
        private readonly Dictionary<string, Identity> _categories = [];
        private readonly List<Business> _businesses = [];
        private readonly List<StockedItem> _items = [];

        public Builder(
            Settlement settlement,
            GenerationOptions options,
            UiText text,
            DateTimeOffset timestamp,
            string? locale)
        {
            _settlement = settlement;
            _options = options;
            _text = text;
            _timestamp = timestamp;
            _locale = locale;

            // Only the shopkeeper's purse needs this, for its thousands separator. The ambient
            // culture is deliberately never changed, so it is resolved explicitly here.
            _culture = (locale is { Length: > 0 } code ? Locale.FromCode(code) : Locale.English)
                .FormatCulture;

            _signature = Signature(options);
            _settlementId = Mint("fa/locations/settlement");

            foreach (string type in new[]
                     {
                         FantasiaArchiveBlueprints.Characters,
                         FantasiaArchiveBlueprints.Items
                     })
            {
                _categories[type] = Mint($"fa/{type}/category");
            }

            // The tavern first, then the shops in order, which is the same order the map numbers
            // them in, so a premises' position in this list is its number on the map.
            if (settlement.Tavern is { } tavern)
            {
                _businesses.Add(new Business(
                    Mint("fa/locations/tavern"),
                    Mint("fa/characters/tavern/keeper"),
                    tavern.Name,
                    text.Settlement.Sections.Tavern,
                    text.Settlement.Labels.Landlord,
                    tavern.Keeper,
                    null,
                    _businesses.Count + 1));
            }

            foreach (Shop shop in settlement.Shops)
            {
                _businesses.Add(new Business(
                    Mint($"fa/locations/{shop.Id}"),
                    Mint($"fa/characters/{shop.Id}/keeper"),
                    shop.SignName,
                    shop.ServiceName,
                    shop.Service.KeeperTitle,
                    shop.Keeper,
                    shop,
                    _businesses.Count + 1));
            }

            CollectItems();
        }

        public IReadOnlyList<ExportedFile> Build() =>
        [
            File(FantasiaArchiveBlueprints.Locations,
                [BuildSettlement(), .. _businesses.Select(BuildPremises)]),
            File(FantasiaArchiveBlueprints.Characters,
                [Category(FantasiaArchiveBlueprints.Characters), .. _businesses.Select(BuildKeeper)]),
            File(FantasiaArchiveBlueprints.Items,
                [Category(FantasiaArchiveBlueprints.Items), .. _items.Select(BuildItem)])
        ];

        private Identity Mint(string path)
        {
            (string id, string revision) = DocumentIdentity.For(_settlement.Seed, $"{_signature}|{path}");
            return new Identity(id, revision);
        }

        /// <summary>
        /// Everything that makes this settlement the settlement it is.
        /// </summary>
        /// <remarks>
        /// The seed alone is not enough. Changing the size, the terrain, the human-town switch or
        /// any lock or hand edit produces a different settlement from the same seed, and ids drawn
        /// from the seed alone would be identical across all of them. Because the app loads with
        /// <c>new_edits: false</c>, merging the second export would then be silently ignored rather
        /// than applied: the reader would see the older settlement and be told nothing.
        /// </remarks>
        private static string Signature(GenerationOptions options)
        {
            StringBuilder builder = new();

            builder
                .Append(options.Seed.ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(options.Size?.ToString(CultureInfo.InvariantCulture) ?? "").Append('|')
                .Append(options.Terrain ?? "").Append('|')
                .Append(options.NearHumanTown ? '1' : '0');

            foreach (KeyValuePair<string, string> pin in
                options.Pins.OrderBy(pin => pin.Key, StringComparer.Ordinal))
            {
                builder.Append("|pin:").Append(pin.Key).Append('=').Append(pin.Value);
            }

            foreach (KeyValuePair<string, int> reroll in
                options.Rerolls.OrderBy(reroll => reroll.Key, StringComparer.Ordinal))
            {
                builder.Append("|reroll:").Append(reroll.Key).Append('=')
                    .Append(reroll.Value.ToString(CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        /// <summary>
        /// Gathers the settlement's stock into one object per distinct piece of gear.
        /// </summary>
        /// <remarks>
        /// <para>
        /// One torch sold in three shops is one object in the world linked to three sellers, rather
        /// than three identical objects, which is both what a worldbuilding database expects and
        /// what keeps a second merge from multiplying them.
        /// </para>
        /// <para>
        /// A gear item's <c>name</c> alone is not its identity: the tables hold a blank book and a
        /// reading book, and a small padlock and a large one. Keying on the name alone would merge
        /// the pair, drop one of them from the export entirely, and file the survivor's price under
        /// the wrong item. The category and price tell them apart, and all three are untranslated,
        /// so a German export produces the same grouping and the same ids as an English one.
        /// </para>
        /// </remarks>
        private void CollectItems()
        {
            Dictionary<string, StockedItem> byIdentity = new(StringComparer.Ordinal);

            foreach (Business business in _businesses)
            {
                if (business.Shop is not { } shop)
                {
                    continue;
                }

                foreach (StockEntry entry in shop.Stock)
                {
                    if (entry.Item.Name.Length == 0)
                    {
                        continue;
                    }

                    string key = ItemKey(entry);

                    if (!byIdentity.TryGetValue(key, out StockedItem? item))
                    {
                        item = new StockedItem(Mint($"fa/items/{key}"), entry);
                        byIdentity[key] = item;
                        _items.Add(item);
                    }

                    // A shop cannot sell the same thing from two shelves, but guarding it here keeps
                    // the both-ends relationship symmetry true by construction.
                    if (!item.SoldBy.Any(sale => sale.Business == business))
                    {
                        item.SoldBy.Add((business, entry));
                    }
                }
            }
        }

        /// <summary>
        /// What makes one piece of gear a different object from another.
        /// </summary>
        /// <remarks>
        /// Every part of it is a key rather than prose, so this is stable across languages.
        /// </remarks>
        private static string ItemKey(StockEntry entry) =>
            string.Join(
                '|',
                entry.Item.Name,
                entry.CategoryId,
                entry.Item.Pips?.ToString(CultureInfo.InvariantCulture) ?? "");

        private ExportedFile File(string type, IReadOnlyList<Document> documents) =>
            new(PouchDump.FileNameFor(type), PouchDump.Write(type, documents, _timestamp));

        // ----- documents -------------------------------------------------------------------

        private Document BuildSettlement()
        {
            List<DocumentField> fields =
            [
                .. Settings(
                    FantasiaArchiveBlueprints.Locations,
                    _settlement.Name,
                    parent: null,
                    tags: SettlementTags(),
                    description: SettlementDescription()),

                new(FantasiaArchiveBlueprints.Location.LocationType,
                    new TextValue(FantasiaArchiveBlueprints.LocationTypeForSize(_settlement.Size.SizeValue))),

                // Text rather than a number, which is just as well: Mausritter states a population
                // as a range or as "1000+".
                new(FantasiaArchiveBlueprints.Location.Population,
                    new TextValue(_settlement.Size.Population ?? "")),

                new(FantasiaArchiveBlueprints.Location.Size, new TextValue(_settlement.Size.Name)),

                new(FantasiaArchiveBlueprints.Location.Traits,
                    new ListValue([.. _settlement.NotableFeatures.Select(f => new ListEntry(f))])),

                new(FantasiaArchiveBlueprints.Location.CurrentCharacters, new ManyRelationshipValue(
                    [.. _businesses.Select(b => b.KeeperId.Link(
                        FantasiaArchiveBlueprints.Characters,
                        FantasiaArchiveBlueprints.Character.CurrentLocation))])),

                new(FantasiaArchiveBlueprints.Location.ConnectedItems, new ManyRelationshipValue(
                    [.. _items.Select(i => i.Id.Link(
                        FantasiaArchiveBlueprints.Items,
                        FantasiaArchiveBlueprints.Item.ConnectedLocations))])),

                // Everything needed to rebuild the settlement, in a field no blueprint declares and
                // the app therefore never renders, edits or discards.
                new(FantasiaArchiveBlueprints.StateField, new TextValue(State()))
            ];

            return Build(FantasiaArchiveBlueprints.Locations, _settlementId, fields);
        }

        private Document BuildKeeper(Business business)
        {
            MouseNpc keeper = business.Keeper;

            List<DocumentField> fields =
            [
                .. Settings(
                    FantasiaArchiveBlueprints.Characters,
                    keeper.FullName,
                    parent: _categories[FantasiaArchiveBlueprints.Characters],
                    tags: [MausritterTag, _settlement.Name, business.ServiceName],
                    description: KeeperDescription(business)),

                new(FantasiaArchiveBlueprints.Character.Titles,
                    new ListValue([new ListEntry(Capitalise(business.KeeperTitle))])),

                new(FantasiaArchiveBlueprints.Character.Traits, new ListValue(
                    [new ListEntry(Capitalise(keeper.Appearance)), new ListEntry(Capitalise(keeper.Quirk))])),

                new(FantasiaArchiveBlueprints.Character.CurrentLocation, new ManyRelationshipValue(
                    [_settlementId.Link(
                        FantasiaArchiveBlueprints.Locations,
                        FantasiaArchiveBlueprints.Location.CurrentCharacters)])),

                // Tied to the premises rather than resident in it. Fantasia Archive has no
                // "proprietor" relationship between a mouse and a place, so who keeps which shop is
                // said in the title and in both descriptions instead of being overstated here.
                new(FantasiaArchiveBlueprints.Character.ConnectedPlaces, new ManyRelationshipValue(
                    [business.PlaceId.Link(
                        FantasiaArchiveBlueprints.Locations,
                        FantasiaArchiveBlueprints.Location.ConnectedCharacters)]))
            ];

            return Build(FantasiaArchiveBlueprints.Characters, business.KeeperId, fields);
        }

        /// <summary>
        /// A shop or the tavern, as a building inside the settlement.
        /// </summary>
        /// <remarks>
        /// A place rather than an organisation, because that is what it is here: a numbered building
        /// on the settlement's map, which the legend keys by that number. Modelling it as an
        /// organisation would also have to call a single mouse's stall a "trade group" with a member
        /// count, and would leave it sitting in a separate tree from the settlement it stands in.
        /// Parenting it to the settlement puts it where a reader would look for it.
        /// </remarks>
        private Document BuildPremises(Business business)
        {
            List<RelationshipTarget> stock =
            [
                .. _items
                    .Where(item => item.SoldBy.Any(sale => sale.Business == business))
                    .Select(item => item.Id.Link(
                        FantasiaArchiveBlueprints.Items,
                        FantasiaArchiveBlueprints.Item.ConnectedLocations))
            ];

            List<DocumentField> fields =
            [
                .. Settings(
                    FantasiaArchiveBlueprints.Locations,
                    business.SignName,
                    parent: _settlementId,
                    tags: [MausritterTag, _settlement.Name, business.ServiceName],
                    description: BusinessDescription(business),
                    // The tree sorts on this, so the shops line up in map order.
                    order: business.MapKey),

                new(FantasiaArchiveBlueprints.Location.LocationType,
                    new TextValue(FantasiaArchiveBlueprints.PremisesLocationType)),

                new(FantasiaArchiveBlueprints.Location.ConnectedCharacters, new ManyRelationshipValue(
                    [business.KeeperId.Link(
                        FantasiaArchiveBlueprints.Characters,
                        FantasiaArchiveBlueprints.Character.ConnectedPlaces)])),

                new(FantasiaArchiveBlueprints.Location.ConnectedItems, new ManyRelationshipValue(stock))
            ];

            return Build(FantasiaArchiveBlueprints.Locations, business.PlaceId, fields);
        }

        private Document BuildItem(StockedItem item)
        {
            GearItem gear = item.First.Item;

            List<ListEntry> features = [];
            foreach ((Business business, StockEntry entry) in item.SoldBy)
            {
                features.Add(new ListEntry(entry.PriceText, $"{_text.Shop.Price} — {business.SignName}"));

                if (entry.Quantity is { } quantity)
                {
                    features.Add(new ListEntry(
                        quantity.ToString(_culture),
                        $"{_text.Shop.Quantity} — {business.SignName}"));
                }
            }

            // Fantasia Archive has no notion of inventory slots or usage, so the card rules are
            // spelled out as notes rather than lost.
            int usage = ItemCard.UsageDotsFor(gear, item.First.CategoryId);
            if (usage > 0)
            {
                features.Add(new ListEntry(usage.ToString(_culture), "Usage dots"));
            }

            if (ItemCard.IsCardable(gear))
            {
                features.Add(new ListEntry(
                    ItemCard.ShapeFor(gear) == CardShape.Wide ? "2" : "1", "Inventory slots"));
            }

            List<DocumentField> fields =
            [
                .. Settings(
                    FantasiaArchiveBlueprints.Items,
                    gear.DisplayName,
                    parent: _categories[FantasiaArchiveBlueprints.Items],
                    tags: [MausritterTag, _settlement.Name],
                    description: ItemDescription(item)),

                new(FantasiaArchiveBlueprints.Item.Features, new ListValue(features)),

                // The settlement and every shop in it that stocks the thing, so it can be found
                // either by asking where to buy it or by browsing a particular shop's shelves.
                new(FantasiaArchiveBlueprints.Item.ConnectedLocations, new ManyRelationshipValue(
                [
                    _settlementId.Link(
                        FantasiaArchiveBlueprints.Locations,
                        FantasiaArchiveBlueprints.Location.ConnectedItems),

                    .. item.SoldBy.Select(sale => sale.Business.PlaceId.Link(
                        FantasiaArchiveBlueprints.Locations,
                        FantasiaArchiveBlueprints.Location.ConnectedItems))
                ]))
            ];

            return Build(FantasiaArchiveBlueprints.Items, item.Id, fields);
        }

        /// <summary>
        /// A folder document, so a merge lands as one tidy branch per type rather than as loose
        /// documents scattered through someone else's world.
        /// </summary>
        private Document Category(string type)
        {
            Identity identity = _categories[type];

            List<DocumentField> fields =
            [
                new(FantasiaArchiveBlueprints.Common.Name, new TextValue(_settlement.Name)),
                new(FantasiaArchiveBlueprints.Common.ParentDocument, new SingleRelationshipValue(null)),
                new(FantasiaArchiveBlueprints.Common.CategorySwitch, new SwitchValue(true)),
                new(FantasiaArchiveBlueprints.Common.Order, new NumberValue(0)),
                new(FantasiaArchiveBlueprints.Common.Tags, new StringsValue([MausritterTag, _settlement.Name])),
                new(FantasiaArchiveBlueprints.Common.OtherNames, new ListValue([])),
                new(FantasiaArchiveBlueprints.Common.CategoryDescription,
                    new TextValue(Html(Paragraph(_settlement.Summary), Attribution()))),
                new(FantasiaArchiveBlueprints.Common.Description, new TextValue(""))
            ];

            return Build(type, identity, fields);
        }

        private static Document Build(string type, Identity identity, IReadOnlyList<DocumentField> fields) =>
            new()
            {
                Id = identity.Id,
                Type = type,
                Revision = identity.Revision,
                Fields = fields
            };

        /// <summary>
        /// The document-settings block every type shares.
        /// </summary>
        /// <remarks>
        /// A document is parented to another of its own type, and the link is one-directional, so
        /// the parent needs no answering entry.
        /// </remarks>
        private IReadOnlyList<DocumentField> Settings(
            string type,
            string name,
            Identity? parent,
            IReadOnlyList<string> tags,
            string description,
            int order = 0) =>
        [
            new(FantasiaArchiveBlueprints.Common.Name, new TextValue(name)),
            new(FantasiaArchiveBlueprints.Common.ParentDocument, new SingleRelationshipValue(
                parent?.Link(type))),
            new(FantasiaArchiveBlueprints.Common.CategorySwitch, new SwitchValue(false)),
            new(FantasiaArchiveBlueprints.Common.Order, new NumberValue(order)),
            new(FantasiaArchiveBlueprints.Common.Tags, new StringsValue(tags)),
            new(FantasiaArchiveBlueprints.Common.OtherNames, new ListValue([])),
            new(FantasiaArchiveBlueprints.Common.CategoryDescription, new TextValue("")),
            new(FantasiaArchiveBlueprints.Common.Description, new TextValue(description))
        ];

        // ----- prose -----------------------------------------------------------------------

        private IReadOnlyList<string> SettlementTags()
        {
            List<string> tags = [MausritterTag, _settlement.Size.Name, _settlement.Host.Name];

            if (_settlement.NearHumanTown)
            {
                tags.Add(_text.Settlement.Controls.NearHumanLabel);
            }

            return tags;
        }

        private string SettlementDescription()
        {
            SettlementLabelsText labels = _text.Settlement.Labels;

            return Html(
                Paragraph(_settlement.Summary),
                Labelled(labels.Built, TextTemplate.Format(
                    labels.BuiltValue,
                    ("host", _settlement.Host.Name),
                    ("description", _settlement.Host.Description))),
                Labelled(labels.Governance, TextTemplate.Format(
                    labels.GovernanceValue,
                    ("governance", _settlement.Governance),
                    ("roll", _settlement.GovernanceRoll.ToString(_culture)))),
                Labelled(labels.Inhabitants, _settlement.Inhabitants),
                Labelled(
                    _settlement.NotableFeatures.Count == 1 ? labels.Feature : labels.Features,
                    string.Join(", ", _settlement.NotableFeatures)),
                Labelled(
                    _settlement.Industries.Count == 1 ? labels.Industry : labels.Industries,
                    string.Join(", ", _settlement.Industries)),
                Labelled(labels.RightNow, _settlement.Event),
                Attribution());
        }

        private string KeeperDescription(Business business)
        {
            MouseNpc keeper = business.Keeper;

            return Html(
                Paragraph($"{Capitalise(keeper.Appearance)}. {Capitalise(keeper.Quirk)}."),
                Paragraph(TextTemplate.Format(_text.Shop.Wants, ("wants", keeper.Wants))),
                Paragraph(TextTemplate.Format(
                    _text.Shop.KeeperMeta,
                    ("position", keeper.Position.Name),
                    ("purse", keeper.Purse.ToString("N0", _culture)),
                    ("birthsign", keeper.Birthsign.Name),
                    ("disposition", keeper.Disposition))),
                keeper.RelationshipSummary is { Length: > 0 } tie ? Paragraph(tie) : "",
                Attribution());
        }

        private string BusinessDescription(Business business)
        {
            List<string> blocks =
            [
                // The map number matters: the settlement's map keys this building by it.
                Paragraph(TextTemplate.Format(
                    _text.Shop.MapKeyTitle, ("index", business.MapKey.ToString(_culture)))),

                Paragraph(business.ServiceName),

                // Said in prose because Fantasia Archive has no relationship that means "keeps this
                // shop"; the mouse is linked to the building, and this is what that link means.
                Labelled(Capitalise(business.KeeperTitle), business.Keeper.FullName)
            ];

            if (business.Shop is { } shop)
            {
                blocks.Add(Labelled(_text.Shop.Quirk, shop.Quirk));

                if (shop.Service.ServiceTerms is { Length: > 0 } terms)
                {
                    blocks.Add(Labelled(_text.Shop.Terms, terms));
                }

                if (shop.Service.OffersRepairs)
                {
                    blocks.Add(Labelled(_text.Shop.Repairs, _text.Shop.RepairsText));
                }

                if (shop.Service.BuysSpells)
                {
                    blocks.Add(Labelled(_text.Shop.Spells, _text.Shop.SpellsText));
                }

                if (shop.PriceAdjustmentPercent != 0)
                {
                    string template = shop.PriceAdjustmentPercent > 0
                        ? _text.Shop.PriceAbove
                        : _text.Shop.PriceBelow;

                    blocks.Add(Paragraph(TextTemplate.Format(
                        template,
                        ("percent", Math.Abs(shop.PriceAdjustmentPercent).ToString(_culture)))));
                }

                blocks.Add(StockList(shop));

                // Every service is extrapolated from an SRD rule rather than drawn from a table, so
                // the rule it came from travels with it.
                blocks.Add(Labelled(_text.Shop.WhereThisComesFrom, shop.Service.SrdBasis));
            }
            else if (_settlement.Tavern is { } tavern)
            {
                blocks.Add(Labelled(_text.Settlement.Labels.Specialty, tavern.SpecialtyMeal));
            }

            blocks.Add(Attribution());
            return Html([.. blocks]);
        }

        private string StockList(Shop shop)
        {
            if (shop.Stock.Count == 0)
            {
                return "";
            }

            StringBuilder builder = new();
            builder.Append("<p><strong>").Append(Escape(_text.Shop.InStock)).Append("</strong></p><ul>");

            foreach (StockEntry entry in shop.Stock)
            {
                builder.Append("<li>").Append(Escape(entry.DisplayName));
                builder.Append(" — ").Append(Escape(entry.PriceText));

                if (entry.Quantity is { } quantity)
                {
                    builder.Append(" (").Append(Escape(_text.Shop.Quantity)).Append(' ')
                        .Append(quantity.ToString(_culture)).Append(')');
                }

                builder.Append("</li>");
            }

            builder.Append("</ul>");
            return builder.ToString();
        }

        private string ItemDescription(StockedItem item)
        {
            List<string> blocks = [];

            if (item.First.Item.Note is { Length: > 0 } note)
            {
                blocks.Add(Paragraph(note));
            }

            // The name is already the document's title, so the prose says the one thing the fields
            // cannot: where in this settlement it can actually be bought.
            blocks.Add(Labelled(
                _text.Shop.InStock,
                string.Join(", ", item.SoldBy.Select(sale => sale.Business.SignName))));

            blocks.Add(Attribution());
            return Html([.. blocks]);
        }

        /// <summary>
        /// The licence notice, carried by every document because each one can be read on its own.
        /// </summary>
        /// <remarks>
        /// CC BY requires attribution to travel with the content, and requires a modification to be
        /// declared, which is why a translated export also carries the note saying the translation is
        /// an unofficial one.
        /// </remarks>
        private string Attribution()
        {
            StringBuilder builder = new();
            builder.Append("<hr /><p><em>").Append(Escape(SettlementDocument.AttributionText));

            if (_text.Licence.TranslationNote is { Length: > 0 } note)
            {
                builder.Append(' ').Append(Escape(note));
            }

            builder.Append("</em></p>");
            return builder.ToString();
        }

        // ----- the round trip --------------------------------------------------------------

        /// <summary>
        /// Everything needed to rebuild this settlement, as it will be smuggled through the app.
        /// </summary>
        /// <remarks>
        /// The payload is this project's own export format verbatim, so there is only one restore
        /// path to maintain and to test. The owning document's id rides along so a settlement that
        /// was duplicated inside Fantasia Archive can be told apart from the original later.
        /// </remarks>
        private string State() => FantasiaArchiveState.Write(
            _settlementId.Id,
            SettlementSerializer.ToJson(_settlement, _options, _timestamp, _locale));

        // ----- helpers ---------------------------------------------------------------------

        private static string Html(params string[] blocks) =>
            string.Concat(blocks.Where(block => block.Length > 0));

        private static string Paragraph(string text) =>
            text.Length == 0 ? "" : $"<p>{Escape(text)}</p>";

        private static string Labelled(string label, string value) =>
            value.Length == 0 ? "" : $"<p><strong>{Escape(label)}:</strong> {Escape(value)}</p>";

        private static string Escape(string value) => WebUtility.HtmlEncode(value);

        private static string Capitalise(string value) =>
            value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value[1..];
    }

    /// <summary>A document's identity, minted once and referred to from both ends of a relationship.</summary>
    private sealed record Identity(string Id, string Revision)
    {
        public RelationshipTarget Link(string type, string pairedField = "") =>
            new(Id, type, pairedField);
    }

    /// <summary>A tavern or shop, its keeper, and the identities both were given.</summary>
    /// <param name="PlaceId">The premises, which is a location in its own right.</param>
    /// <param name="MapKey">
    /// Its number on the settlement map. The map numbers the tavern first and then the shops in
    /// order, which is the order these are built in, so the position carries across.
    /// </param>
    private sealed record Business(
        Identity PlaceId,
        Identity KeeperId,
        string SignName,
        string ServiceName,
        string KeeperTitle,
        MouseNpc Keeper,
        Shop? Shop,
        int MapKey);

    /// <summary>One distinct piece of gear, and every shop that stocks it.</summary>
    private sealed record StockedItem(Identity Id, StockEntry First)
    {
        public List<(Business Business, StockEntry Entry)> SoldBy { get; } = [];
    }
}
