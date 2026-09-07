namespace MausritterTools.Core.Randomness;

/// <summary>
/// Dice and selection helpers expressed in the terms Mausritter's tables actually use.
/// </summary>
public sealed class DiceRoller(IRandomSource source)
{
    private readonly IRandomSource _source =
        source ?? throw new ArgumentNullException(nameof(source));

    /// <summary>Rolls a single die, returning a value in <c>[1, sides]</c>.</summary>
    public int Roll(int sides)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sides, 1);
        return (int)_source.NextUInt32((uint)sides) + 1;
    }

    /// <summary>Rolls <paramref name="count"/> dice and returns their total.</summary>
    public int RollSum(int count, int sides)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        int total = 0;
        for (int i = 0; i < count; i++)
        {
            total += Roll(sides);
        }

        return total;
    }

    /// <summary>
    /// Rolls two dice and keeps the lower result.
    /// </summary>
    /// <remarks>
    /// Mausritter uses this for settlement size, deliberately biasing toward tiny settlements:
    /// "Most mouse settlements are no more than a handful of families in an oak hollow."
    /// </remarks>
    public int RollLowestOfTwo(int sides) => Math.Min(Roll(sides), Roll(sides));

    /// <summary>Returns a uniformly chosen index in <c>[0, count)</c>.</summary>
    public int NextIndex(int count)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        return (int)_source.NextUInt32((uint)count);
    }

    /// <summary>Picks one element uniformly at random.</summary>
    public T Pick<T>(IReadOnlyList<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0)
        {
            throw new ArgumentException("Cannot pick from an empty collection.", nameof(items));
        }

        return items[NextIndex(items.Count)];
    }

    /// <summary>
    /// Picks up to <paramref name="count"/> distinct elements, preserving no particular order.
    /// </summary>
    /// <remarks>
    /// Returns fewer than requested when the source is smaller, rather than throwing, because
    /// callers ask for "two industries" without knowing how large the underlying table is.
    /// </remarks>
    public IReadOnlyList<T> PickDistinct<T>(IReadOnlyList<T> items, int count)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        int take = Math.Min(count, items.Count);
        if (take == 0)
        {
            return [];
        }

        // Partial Fisher-Yates over an index buffer, so elements need not be equatable.
        int[] indices = new int[items.Count];
        for (int i = 0; i < indices.Length; i++)
        {
            indices[i] = i;
        }

        var picked = new List<T>(take);
        for (int i = 0; i < take; i++)
        {
            int swap = i + NextIndex(indices.Length - i);
            (indices[i], indices[swap]) = (indices[swap], indices[i]);
            picked.Add(items[indices[i]]);
        }

        return picked;
    }

    /// <summary>Picks one element with probability proportional to its weight.</summary>
    public T PickWeighted<T>(IReadOnlyList<T> items, Func<T, int> weightSelector)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(weightSelector);
        if (items.Count == 0)
        {
            throw new ArgumentException("Cannot pick from an empty collection.", nameof(items));
        }

        int total = 0;
        foreach (T item in items)
        {
            total += Math.Max(0, weightSelector(item));
        }

        if (total <= 0)
        {
            return Pick(items);
        }

        int target = (int)_source.NextUInt32((uint)total);
        foreach (T item in items)
        {
            target -= Math.Max(0, weightSelector(item));
            if (target < 0)
            {
                return item;
            }
        }

        return items[^1];
    }

    /// <summary>Returns a shuffled copy of <paramref name="items"/>.</summary>
    public IReadOnlyList<T> Shuffle<T>(IReadOnlyList<T> items) =>
        PickDistinct(items, items.Count);

    /// <summary>Returns <c>true</c> with the given percentage chance.</summary>
    public bool Chance(int percent) => (int)_source.NextUInt32(100) < percent;
}
