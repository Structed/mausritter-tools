using MausritterTools.Core.Data;
using MausritterTools.Core.Generation;

namespace MausritterTools.Web.Services;

/// <summary>
/// Loads the game data once per language and shares it across the app.
/// </summary>
/// <remarks>
/// Loading happens here rather than during start-up so that a missing or malformed data file
/// surfaces as a readable message on the page instead of a blank screen and a console stack trace.
/// Each language is cached separately, so switching back and forth costs one download apiece.
/// </remarks>
public sealed class GameDataProvider(HttpClient httpClient, LocaleState locale)
{
    private readonly HttpClient _httpClient =
        httpClient ?? throw new ArgumentNullException(nameof(httpClient));

    private readonly LocaleState _locale = locale ?? throw new ArgumentNullException(nameof(locale));

    private readonly Dictionary<string, Task<GameData>> _loading = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Task<UiText>> _text = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SettlementGenerator> _generators = new(StringComparer.Ordinal);

    /// <summary>
    /// Loads only the UI text for the current language, which is all the layout needs.
    /// </summary>
    /// <remarks>
    /// Falls back to a blank set rather than throwing. A page with missing labels is still a page;
    /// a layout that throws takes the whole app down, including the message explaining why.
    /// </remarks>
    public Task<UiText> GetTextAsync()
    {
        Locale target = _locale.Current;

        if (_text.TryGetValue(target.Code, out Task<UiText>? existing))
        {
            return existing;
        }

        Task<UiText> load = GameData.LoadTextAsync(new HttpDataFileReader(_httpClient), target);
        _text[target.Code] = load;

        return load;
    }

    /// <summary>Loads the data for the current language, reusing an earlier load where possible.</summary>
    public Task<GameData> GetDataAsync() => GetDataAsync(_locale.Current);

    /// <summary>Loads the data for a specific language.</summary>
    public Task<GameData> GetDataAsync(Locale target)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (_loading.TryGetValue(target.Code, out Task<GameData>? existing))
        {
            return existing;
        }

        Task<GameData> load = GameData.LoadAsync(new HttpDataFileReader(_httpClient), target);
        _loading[target.Code] = load;

        return load;
    }

    /// <summary>Gets a generator bound to the current language's data.</summary>
    public async Task<SettlementGenerator> GetGeneratorAsync()
    {
        Locale target = _locale.Current;
        GameData data = await GetDataAsync(target);

        if (!_generators.TryGetValue(target.Code, out SettlementGenerator? generator))
        {
            generator = new SettlementGenerator(data);
            _generators[target.Code] = generator;
        }

        return generator;
    }
}
