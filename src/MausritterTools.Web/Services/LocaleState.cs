using System.Globalization;
using MausritterTools.Core.Data;

namespace MausritterTools.Web.Services;

/// <summary>
/// Holds the language the visitor is reading in, and remembers it between visits.
/// </summary>
/// <remarks>
/// <para>
/// The first visit follows the browser, because a German-speaking GM should not have to find a
/// setting before the tool is useful. After that the visitor's own choice wins, and a
/// <c>lang</c> parameter in a shared link wins over both, so a settlement sent to someone arrives
/// in the language it was written up in.
/// </para>
/// <para>
/// Changing language does not change the settlement. Every table is translated row for row, so the
/// seed still resolves to the same rows; only the words on them differ.
/// </para>
/// </remarks>
public sealed class LocaleState(BrowserInterop browser)
{
    /// <summary>The query parameter a shared link carries the language in.</summary>
    public const string QueryParameter = "lang";

    private const string StorageKey = "mausritter-tools.locale";

    private readonly BrowserInterop _browser =
        browser ?? throw new ArgumentNullException(nameof(browser));

    /// <summary>The language currently being read.</summary>
    public Locale Current { get; private set; } = Locale.English;

    /// <summary>Raised after the language changes, so pages can rebuild in the new one.</summary>
    public event Func<Task>? Changed;

    /// <summary>
    /// Chooses the starting language: an explicit link, then a remembered choice, then the browser.
    /// </summary>
    public async Task InitialiseAsync(string? fromQuery)
    {
        Locale? chosen = Match(fromQuery);

        if (chosen is null)
        {
            chosen = Match(await _browser.ReadStorageAsync(StorageKey));
        }

        chosen ??= Match(await _browser.GetBrowserLanguageAsync()) ?? Locale.English;

        Current = chosen;
        ApplyCulture(Current);
    }

    /// <summary>Switches language, remembering the choice.</summary>
    public async Task SetAsync(Locale locale)
    {
        ArgumentNullException.ThrowIfNull(locale);

        if (locale.Code == Current.Code)
        {
            return;
        }

        Current = locale;
        ApplyCulture(locale);

        await _browser.WriteStorageAsync(StorageKey, locale.Code);

        if (Changed is { } handler)
        {
            await handler.Invoke();
        }
    }

    /// <summary>
    /// Tells the browser which language the document is in, once the text for it is loaded.
    /// </summary>
    public Task ApplyToDocumentAsync(UiText text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return _browser.ApplyLanguageAsync(
            Current.Code, text.App.Description, text.Error.Unhandled, text.Error.Reload);
    }

    /// <summary>
    /// Sets the thread culture, which is what makes a purse of 1000 pips read as "1,000" in English
    /// and "1.000" in German.
    /// </summary>
    private static void ApplyCulture(Locale locale)
    {
        CultureInfo culture = CultureInfo.GetCultureInfo(locale.Code);

        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    /// <summary>Resolves a code we actually ship, or nothing.</summary>
    private static Locale? Match(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        Locale resolved = Locale.FromCode(code);

        // FromCode falls back to English, which is indistinguishable from an English request; only
        // treat it as a match when the code really did name a language we have.
        return resolved.IsCanonical && !code.StartsWith(Locale.CanonicalCode, StringComparison.OrdinalIgnoreCase)
            ? null
            : resolved;
    }
}
