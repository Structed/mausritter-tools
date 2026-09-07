using System.Text.RegularExpressions;
using MausritterTools.Core.Randomness;

namespace MausritterTools.Core.Generation;

/// <summary>
/// Evaluates the compact dice expressions the SRD tables use.
/// </summary>
/// <remarks>
/// Payments appear as "d6p", "d6 x 10p" or "d4 x 1000p", and hireling availability as "d2" or
/// "d6". Parsing them means the numbers come from the tables rather than being duplicated in code.
/// </remarks>
public static partial class DiceExpression
{
    [GeneratedRegex(@"^\s*(\d*)\s*d\s*(\d+)\s*(?:x\s*(\d+))?\s*p?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex Pattern { get; }

    /// <summary>
    /// Rolls an expression such as <c>d6 x 10p</c>.
    /// </summary>
    /// <returns>The rolled total, or <c>null</c> when the expression is not recognised.</returns>
    public static int? TryRoll(DiceRoller dice, string? expression)
    {
        ArgumentNullException.ThrowIfNull(dice);

        if (string.IsNullOrWhiteSpace(expression))
        {
            return null;
        }

        Match match = Pattern.Match(expression);
        if (!match.Success)
        {
            return null;
        }

        int count = match.Groups[1].Value is { Length: > 0 } c ? int.Parse(c) : 1;
        int sides = int.Parse(match.Groups[2].Value);
        int multiplier = match.Groups[3].Value is { Length: > 0 } m ? int.Parse(m) : 1;

        if (count < 1 || sides < 1)
        {
            return null;
        }

        return dice.RollSum(count, sides) * multiplier;
    }

    /// <summary>Rolls an expression, falling back to <paramref name="fallback"/> if unparsable.</summary>
    public static int Roll(DiceRoller dice, string? expression, int fallback = 0) =>
        TryRoll(dice, expression) ?? fallback;
}
