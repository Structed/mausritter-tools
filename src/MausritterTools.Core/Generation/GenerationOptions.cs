namespace MausritterTools.Core.Generation;

/// <summary>
/// Inputs to a settlement roll.
/// </summary>
/// <remarks>
/// <para>
/// Generation is a pure function of these options, which is what makes a seed URL meaningful: the
/// same options always rebuild the same settlement.
/// </para>
/// <para>
/// <see cref="Pins"/> and <see cref="Rerolls"/> together implement "lock the bits you like".
/// A pinned path keeps its value through a re-roll, while bumping a path's re-roll counter changes
/// only that one value and leaves its neighbours untouched.
/// </para>
/// </remarks>
public sealed record GenerationOptions
{
    /// <summary>The root seed all values are derived from.</summary>
    public uint Seed { get; init; }

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

    /// <summary>Values held fixed across a re-roll, keyed by field path.</summary>
    public IReadOnlyDictionary<string, string> Pins { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// How many times each path has been individually re-rolled. The count is folded into the
    /// field's stream, so bumping it yields a different value without disturbing anything else.
    /// </summary>
    public IReadOnlyDictionary<string, int> Rerolls { get; init; } =
        new Dictionary<string, int>();

    public bool IsPinned(string path) => Pins.ContainsKey(path);

    /// <summary>Returns a copy with <paramref name="path"/> pinned to <paramref name="value"/>.</summary>
    public GenerationOptions WithPin(string path, string value)
    {
        Dictionary<string, string> pins = new(Pins, StringComparer.Ordinal) { [path] = value };
        return this with { Pins = pins };
    }

    /// <summary>Returns a copy with <paramref name="path"/> no longer pinned.</summary>
    public GenerationOptions WithoutPin(string path)
    {
        Dictionary<string, string> pins = new(Pins, StringComparer.Ordinal);
        pins.Remove(path);
        return this with { Pins = pins };
    }

    /// <summary>Returns a copy with every pin whose path starts with the given prefix removed.</summary>
    public GenerationOptions WithoutPinsUnder(string pathPrefix)
    {
        Dictionary<string, string> pins = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> pin in Pins)
        {
            if (!pin.Key.StartsWith(pathPrefix, StringComparison.Ordinal))
            {
                pins[pin.Key] = pin.Value;
            }
        }

        return this with { Pins = pins };
    }

    /// <summary>Returns a copy in which <paramref name="path"/> re-rolls to a new value.</summary>
    public GenerationOptions WithReroll(string path)
    {
        Dictionary<string, int> rerolls = new(Rerolls, StringComparer.Ordinal);
        rerolls[path] = rerolls.GetValueOrDefault(path) + 1;

        // A pinned field would ignore the new roll, so re-rolling implies releasing the pin.
        Dictionary<string, string> pins = new(Pins, StringComparer.Ordinal);
        pins.Remove(path);

        return this with { Rerolls = rerolls, Pins = pins };
    }
}
