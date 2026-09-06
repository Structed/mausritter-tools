using MausritterTools.Core.Randomness;

namespace MausritterTools.Core.Generation;

/// <summary>
/// Resolves a single field: either its pinned value, or a roll from that field's own stream.
/// </summary>
/// <remarks>
/// Every value in a settlement is addressed by a path such as <c>shop/2/keeper/quirk</c>. Because
/// each path gets an independent stream derived from the root seed, re-rolling one field cannot
/// disturb any other, and pinning a field survives a re-roll of everything around it.
/// </remarks>
public sealed class RollContext(GenerationOptions options)
{
    private readonly GenerationOptions _options =
        options ?? throw new ArgumentNullException(nameof(options));

    public GenerationOptions Options => _options;

    /// <summary>Creates the dice for one field.</summary>
    public DiceRoller Dice(string path) =>
        new(SeedDerivation.CreateStream(_options.Seed, StreamKey(path)));

    /// <summary>
    /// The stream key for a path, incorporating how many times it has been individually re-rolled
    /// so that each re-roll draws from a different stream.
    /// </summary>
    private string StreamKey(string path) =>
        _options.Rerolls.TryGetValue(path, out int count) && count > 0
            ? $"{path}#{count}"
            : path;

    /// <summary>Returns the pinned value for a path, if there is one.</summary>
    public bool TryGetPin(string path, out string value)
    {
        if (_options.Pins.TryGetValue(path, out string? pinned) && pinned is not null)
        {
            value = pinned;
            return true;
        }

        value = "";
        return false;
    }

    /// <summary>Picks one entry from a table, honouring a pin on that path.</summary>
    public string Text(string path, IReadOnlyList<string> table)
    {
        if (TryGetPin(path, out string pinned))
        {
            return pinned;
        }

        return table.Count == 0 ? "" : Dice(path).Pick(table);
    }

    /// <summary>
    /// Picks several distinct entries from a table. Pins on this path store the whole selection as
    /// a newline-separated block so that a multi-valued field such as a city's two industries can
    /// be locked as a unit.
    /// </summary>
    public IReadOnlyList<string> TextMany(string path, IReadOnlyList<string> table, int count)
    {
        if (TryGetPin(path, out string pinned))
        {
            return pinned.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        return table.Count == 0 ? [] : Dice(path).PickDistinct(table, count);
    }
}
