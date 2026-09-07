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
/// <para>
/// Note that the ambient <see cref="System.Globalization.CultureInfo"/> is deliberately left alone.
/// Blazor refuses a culture change during start-up unless the whole ICU dataset is bundled, which
/// costs well over a megabyte of download, and the only thing this app needs a culture for is the
/// thousands separator in a shopkeeper's purse. That one number is formatted explicitly against
/// <see cref="Locale.FormatCulture"/> instead. Do not swap this for setting the ambient culture
/// without weighing that download against a single comma.
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
    /// <remarks>
    /// Runs before the app renders, so it reads the address bar through JavaScript rather than
    /// through Blazor's <c>NavigationManager</c>, which is not initialised until the host is
    /// running. Settling the language any later would draw the first screen in the wrong one.
    /// </remarks>
    public async Task InitialiseAsync()
    {
        Locale? chosen = Match(await _browser.GetQueryParameterAsync(QueryParameter));

        chosen ??= Match(await _browser.ReadStorageAsync(StorageKey));
        chosen ??= Match(await _browser.GetBrowserLanguageAsync());

        Current = chosen ?? Locale.English;
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
