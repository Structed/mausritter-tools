using System.Globalization;

namespace MausritterTools.Core.Data;

/// <summary>
/// A language the tools are published in.
/// </summary>
/// <remarks>
/// English is canonical: the data files under <c>data/srd</c> and <c>data/house</c> are written in
/// it, and every key in them — ids, dice expressions, gear category names and the item names the
/// card rules match on — stays English in every language. Other locales supply a translation
/// overlay that replaces display text only.
/// </remarks>
public sealed record Locale
{
    /// <summary>The locale the data files themselves are written in.</summary>
    public const string CanonicalCode = "en";

    private Locale(string code, string nativeName, string englishName)
    {
        Code = code;
        NativeName = nativeName;
        EnglishName = englishName;
    }

    /// <summary>The ISO 639-1 code, e.g. <c>de</c>.</summary>
    public string Code { get; }

    /// <summary>The language's name in itself, e.g. "Deutsch", which is what a picker should show.</summary>
    public string NativeName { get; }

    public string EnglishName { get; }

    /// <summary>
    /// <c>true</c> for the language the data files are written in, which therefore needs no overlay.
    /// </summary>
    public bool IsCanonical => Code == CanonicalCode;

    public static Locale English { get; } = new(CanonicalCode, "English", "English");

    public static Locale German { get; } = new("de", "Deutsch", "German");

    /// <summary>Every locale the app ships, in the order a picker should list them.</summary>
    public static IReadOnlyList<Locale> All { get; } = [English, German];

    /// <summary>
    /// Resolves a locale from a code, tolerating the region-tagged forms a browser reports such as
    /// <c>de-AT</c> or <c>en-GB</c>. Falls back to <see cref="English"/> rather than throwing,
    /// because the input is usually a browser setting or a URL parameter.
    /// </summary>
    public static Locale FromCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return English;
        }

        ReadOnlySpan<char> trimmed = code.AsSpan().Trim();
        int separator = trimmed.IndexOfAny('-', '_');
        if (separator > 0)
        {
            trimmed = trimmed[..separator];
        }

        foreach (Locale locale in All)
        {
            if (trimmed.Equals(locale.Code, StringComparison.OrdinalIgnoreCase))
            {
                return locale;
            }
        }

        return English;
    }

    /// <summary>The data root a locale's overlay files live under, e.g. <c>i18n/de</c>.</summary>
    public string OverlayRoot => $"i18n/{Code}";

    /// <summary>
    /// How to format numbers for a reader of this language.
    /// </summary>
    /// <remarks>
    /// Resolved once and used explicitly, rather than by setting the ambient culture: Blazor
    /// WebAssembly will not allow the app culture to change unless the entire ICU dataset is
    /// bundled, and a megabyte of globalization data is a poor trade for a thousands separator.
    /// Falls back to the invariant culture, because a purse printed with the wrong separator is a
    /// far smaller problem than a page that will not render.
    /// </remarks>
    public CultureInfo FormatCulture => field ??= Resolve(Code);

    private static CultureInfo Resolve(string code)
    {
        try
        {
            return CultureInfo.GetCultureInfo(code);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }

    public override string ToString() => Code;
}
