using Structed.Inkwell.Data;

namespace MausritterTools.Core.Data;

/// <summary>
/// The languages these tools are published in.
/// </summary>
/// <remarks>
/// English is canonical: the data files under <c>data/srd</c> and <c>data/house</c> are written in
/// it, and every key in them — ids, dice expressions, gear category names and the item names the
/// card rules match on — stays English in every language. Translations are overlays that replace
/// display text only, and is an unofficial fan translation.
/// </remarks>
public static class MausritterLocales
{
    /// <summary>The language the data files are written in.</summary>
    public static Locale English => Locale.English;

    public static Locale German { get; } = new("de", "Deutsch", "German");

    public static Locale Polish { get; } = new("pl", "Polski", "Polish");

    /// <summary>Every locale the app ships, in the order a picker should list them.</summary>
    public static LocaleSet All { get; } = new(English, German, Polish);

    /// <summary>
    /// Resolves a locale from a code, tolerating region-tagged forms such as <c>de-AT</c> and
    /// falling back to English.
    /// </summary>
    public static Locale FromCode(string? code) => All.FromCode(code);
}
