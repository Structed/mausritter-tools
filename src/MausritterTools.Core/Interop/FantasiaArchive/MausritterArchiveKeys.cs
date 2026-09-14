using Structed.Inkwell.Interop.FantasiaArchive;

namespace MausritterTools.Core.Interop.FantasiaArchive;

/// <summary>
/// The Fantasia Archive keys that are this tool's rather than the app's.
/// </summary>
/// <remarks>
/// Everything in <see cref="FantasiaArchiveBlueprints"/> is a fact about Fantasia Archive. What is
/// here is what Mausritter contributes to an export: where this tool hides its own state, and how a
/// settlement's size is expressed in a vocabulary that never contemplated mice.
/// </remarks>
public static class MausritterArchiveKeys
{
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
    /// The name matters, and is now frozen: every export ever written carries it, and a different
    /// one would make those files unreadable. The app's "repair project" tool deletes six specific
    /// field ids outright (<c>strength</c>, <c>constitution</c>, <c>dexterity</c>, <c>intellect</c>,
    /// <c>wisdom</c> and <c>charisma</c>), so this must not be one of them, and stays obviously
    /// namespaced so it never collides with a field a future blueprint introduces.
    /// </para>
    /// </remarks>
    public const string StateField = "mausritterToolsState";

    /// <summary>Identifies this tool's state so a foreign file is not mistaken for one of ours.</summary>
    public const string StateFormatId = "mausritter-tools/fantasia-archive-state";

    /// <summary>The name the settlement export is nested under inside the hidden state.</summary>
    public const string StatePayloadName = "settlement";

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
}
