using Structed.Inkwell.Generation;

namespace MausritterTools.Core.Generation;

/// <summary>
/// Inputs to a settlement roll.
/// </summary>
/// <remarks>
/// The seed, the locks and the re-roll counters are <see cref="RollPlan"/>'s, because every
/// generator needs them and needs them to behave identically. What is left here is what a
/// settlement in particular can be asked for.
/// </remarks>
public sealed record GenerationOptions : RollPlan
{
    /// <summary>
    /// Forces the settlement size to 1-6 instead of rolling it. Mausritter rolls 2d6 and keeps the
    /// lower value, which strongly favours tiny settlements, so a GM who wants a city needs a way
    /// to ask for one.
    /// </summary>
    public int? Size { get; init; }

    /// <summary>
    /// Whether the settlement sits near a human population. The SRD gates human-made goods on
    /// geography rather than size, so this is a separate switch.
    /// </summary>
    public bool NearHumanTown { get; init; }

    /// <summary>Restricts host object selection to one terrain, e.g. <c>forest</c>.</summary>
    public string? Terrain { get; init; }

    /// <summary>Returns a copy with <paramref name="path"/> pinned to <paramref name="value"/>.</summary>
    public GenerationOptions WithPin(string path, string value) =>
        this with { Pins = PinsWith(path, value) };

    /// <summary>Returns a copy with <paramref name="path"/> no longer pinned.</summary>
    public GenerationOptions WithoutPin(string path) =>
        this with { Pins = PinsWithout(path) };

    /// <summary>Returns a copy with every pin whose path starts with the given prefix removed.</summary>
    public GenerationOptions WithoutPinsUnder(string pathPrefix) =>
        this with { Pins = PinsWithoutPrefix(pathPrefix) };

    /// <summary>Returns a copy in which <paramref name="path"/> re-rolls to a new value.</summary>
    /// <remarks>A pinned field would ignore the new roll, so re-rolling releases the pin.</remarks>
    public GenerationOptions WithReroll(string path) =>
        this with { Rerolls = RerollsWith(path), Pins = PinsWithout(path) };
}
