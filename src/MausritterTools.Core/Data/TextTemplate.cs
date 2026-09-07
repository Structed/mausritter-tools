using System.Text;

namespace MausritterTools.Core.Data;

/// <summary>
/// Fills <c>{placeholder}</c> slots in a localised format string.
/// </summary>
/// <remarks>
/// Named placeholders rather than <see cref="string.Format(string, object?[])"/>'s numbered ones,
/// because a translator reorders a sentence far more often than not: German pushes the verb to the
/// end, and "A village of 150-300 mice" becomes "Ein Dorf mit 150-300 Mäusen". A name survives that
/// reordering; an index silently swaps two values.
/// </remarks>
public static class TextTemplate
{
    /// <summary>
    /// Replaces every <c>{key}</c> in <paramref name="template"/> with its value.
    /// </summary>
    /// <remarks>
    /// A placeholder with no matching value is left in place rather than blanked, so a missing
    /// substitution shows up as a visible <c>{host}</c> in the UI instead of a hole in a sentence.
    /// </remarks>
    public static string Format(string template, params ReadOnlySpan<(string Key, string? Value)> values)
    {
        if (string.IsNullOrEmpty(template) || values.Length == 0)
        {
            return template ?? "";
        }

        StringBuilder builder = new(template);

        foreach ((string key, string? value) in values)
        {
            builder.Replace($"{{{key}}}", value ?? "");
        }

        return builder.ToString();
    }
}
