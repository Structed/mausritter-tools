namespace MausritterTools.Core.Interop.FantasiaArchive;

/// <summary>
/// The parts of Fantasia Archive's built-in document types this exporter writes to.
/// </summary>
/// <remarks>
/// <para>
/// Fantasia Archive is GPL-3.0. These are re-derived identifiers rather than copied source: the
/// handful of ids and select values needed to write a file its importer accepts, which are
/// functional interface facts. No blueprint definition, tooltip or value list is reproduced here.
/// </para>
/// <para>
/// Everything in this file is a <em>key</em> in someone else's application and is never translated,
/// exactly as this project's own ids are not. A German export still says
/// <c>"locationType": "Village"</c>, because that string is what the app's own dropdown offers; only
/// the prose around it changes language.
/// </para>
/// <para>
/// Targets Fantasia Archive v1, the format every released version reads. The v2 rewrite on
/// <c>master</c> replaces it with a single-file SQLite database and is deliberately not supported
/// while its schema is still pre-release.
/// </para>
/// </remarks>
public static class FantasiaArchiveBlueprints
{
    /// <summary>Places. Files as <c>locations.txt</c>.</summary>
    public const string Locations = "locations";

    /// <summary>People. Files as <c>characters.txt</c>.</summary>
    public const string Characters = "characters";

    /// <summary>Organisations and other groups, which is where shops fit. Files as <c>guilds.txt</c>.</summary>
    public const string Guilds = "guilds";

    /// <summary>Objects. Files as <c>items.txt</c>.</summary>
    public const string Items = "items";

    /// <summary>Every type this exporter emits, in the order the files are written.</summary>
    public static IReadOnlyList<string> AllTypes { get; } =
        [Locations, Characters, Guilds, Items];

    /// <summary>
    /// The URL every document carries, which the app's router depends on being exactly this shape.
    /// </summary>
    public static string Url(string type, string id) => $"/project/display-content/{type}/{id}";

    /// <summary>The icon shown in the document tree, one per type.</summary>
    public static string IconFor(string type) => type switch
    {
        Locations => "mdi-map-marker-radius",
        Characters => "mdi-account",
        Guilds => "mdi-account-group",
        Items => "mdi-sword",
        _ => ""
    };

    /// <summary>
    /// The document tree path a type's documents sit under, which is the blueprint's plural name.
    /// </summary>
    public static string HierarchicalPathFor(string type) => type switch
    {
        Locations => "Locations/Geography",
        Characters => "Characters",
        Guilds => "Organizations/Other groups",
        Items => "Items",
        _ => ""
    };

    /// <summary>Field ids every document type shares.</summary>
    public static class Common
    {
        public const string Name = "name";

        /// <summary>Parents a document to another of the same type, building the tree.</summary>
        public const string ParentDocument = "parentDoc";

        /// <summary>Turns a document into a folder rather than a leaf.</summary>
        public const string CategorySwitch = "categorySwitch";

        public const string CategoryDescription = "categoryDescription";

        public const string Description = "description";

        public const string Tags = "tags";

        public const string Order = "order";

        public const string OtherNames = "otherNames";
    }

    /// <summary>Field ids on a place.</summary>
    public static class Location
    {
        public const string LocationType = "locationType";

        /// <summary>Free text, not a number.</summary>
        public const string Population = "population";

        /// <summary>Free text, not a number.</summary>
        public const string Size = "size";

        public const string Traits = "traits";

        public const string Traditions = "traditions";

        /// <summary>Residents. Pairs with <see cref="Character.CurrentLocation"/>.</summary>
        public const string CurrentCharacters = "pairedCurrentCharactersNew";

        /// <summary>Organisations based here. Pairs with <see cref="Guild.ConnectedLocations"/>.</summary>
        public const string ConnectedGroups = "connectedOther";

        /// <summary>Objects found here. Pairs with <see cref="Item.ConnectedLocations"/>.</summary>
        public const string ConnectedItems = "pairedConnectedItems";
    }

    /// <summary>Field ids on a person.</summary>
    public static class Character
    {
        /// <summary>A free-text list, which is the only place a one-off job title fits.</summary>
        public const string Titles = "titles";

        public const string Age = "age";

        /// <summary>Unusual features, a free-text list.</summary>
        public const string Traits = "traits";

        /// <summary>Where they live. Pairs with <see cref="Location.CurrentCharacters"/>.</summary>
        public const string CurrentLocation = "pairedCurrentLocationNew";

        /// <summary>What they run. Pairs with <see cref="Guild.LeadingCharacters"/>.</summary>
        public const string LeadingGroups = "leadingOtherLeaders";
    }

    /// <summary>Field ids on an organisation.</summary>
    public static class Guild
    {
        /// <summary>A multi-select, so its value is a list of keys.</summary>
        public const string GroupType = "groupType";

        /// <summary>Where it operates from. One-directional, so it needs no reverse side.</summary>
        public const string Headquarters = "headquarters";

        /// <summary>Member count, and free text rather than a number.</summary>
        public const string Population = "population";

        /// <summary>What its members are called.</summary>
        public const string FollowerName = "followerName";

        public const string Traditions = "traditions";

        /// <summary>Who runs it. Pairs with <see cref="Character.LeadingGroups"/>.</summary>
        public const string LeadingCharacters = "leadingCharacters";

        /// <summary>Where it sits. Pairs with <see cref="Location.ConnectedGroups"/>.</summary>
        public const string ConnectedLocations = "connectedLocations";

        /// <summary>What it deals in. Pairs with <see cref="Item.ConnectedGroups"/>.</summary>
        public const string ConnectedItems = "pairedConnectedItems";
    }

    /// <summary>Field ids on an object.</summary>
    public static class Item
    {
        /// <summary>
        /// A free-text list of labelled notes.
        /// </summary>
        /// <remarks>
        /// Fantasia Archive has no price, quantity or rarity field on an item, so everything a
        /// shopper would want to know goes here, where it renders as "Price: 20p".
        /// </remarks>
        public const string Features = "features";

        /// <summary>Where it can be found. Pairs with <see cref="Location.ConnectedItems"/>.</summary>
        public const string ConnectedLocations = "pairedConnectedLocations";

        /// <summary>Who sells it. Pairs with <see cref="Guild.ConnectedItems"/>.</summary>
        public const string ConnectedGroups = "pairedConnectedOtherGroups";
    }

    /// <summary>
    /// The field this tool's own state rides in on the settlement's location document.
    /// </summary>
    /// <remarks>
    /// <para>
    /// No blueprint declares this id, which is precisely the point. Fantasia Archive renders a
    /// document by walking the <em>blueprint's</em> fields and looking each one up, so an entry it
    /// has never heard of is invisible in the UI and cannot be edited by hand, while its save path
    /// only ever appends to the field list and never rebuilds it. The entry therefore survives
    /// being opened, edited and saved, which is what makes the round trip work.
    /// </para>
    /// <para>
    /// The name matters. The app's "repair project" tool deletes six specific field ids outright
    /// (<c>strength</c>, <c>constitution</c>, <c>dexterity</c>, <c>intellect</c>, <c>wisdom</c> and
    /// <c>charisma</c>), so this must not be one of them, and should stay obviously namespaced so it
    /// never collides with a field a future blueprint introduces.
    /// </para>
    /// </remarks>
    public const string StateField = "mausritterToolsState";

    /// <summary>
    /// Location types, which are fixed strings in the app's own dropdown.
    /// </summary>
    /// <remarks>
    /// Mausritter's two smallest settlements have no counterpart: a farm or manor is just a
    /// building, and a crossroads is nothing the list contemplates. Both keep their real Mausritter
    /// size as a tag, so choosing the nearest available key loses nothing.
    /// </remarks>
    public static string LocationTypeForSize(int sizeValue) => sizeValue switch
    {
        1 => "Building",
        2 => "Other",
        3 => "Hamlet",
        4 => "Village",
        5 => "Town",
        6 => "City",
        _ => "Other"
    };

    /// <summary>
    /// Group types, which are fixed strings in the app's own multi-select.
    /// </summary>
    /// <remarks>
    /// Keyed off the service id rather than its name, so a German export still writes the key the
    /// app expects. Almost everything a mouse settlement offers is a trade; the exceptions are the
    /// ones the SRD itself frames as something else.
    /// </remarks>
    public static IReadOnlyList<string> GroupTypeForService(string serviceId) => serviceId switch
    {
        "bank" => ["Economical group"],
        "scriptorium" => ["Academic group"],
        "hireling-hall" => ["Civil group"],
        _ => ["Trade group"]
    };
}
