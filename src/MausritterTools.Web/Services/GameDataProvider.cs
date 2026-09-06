using MausritterTools.Core.Data;
using MausritterTools.Core.Generation;

namespace MausritterTools.Web.Services;

/// <summary>
/// Loads the game data once and shares it across the app.
/// </summary>
/// <remarks>
/// Loading happens here rather than during start-up so that a missing or malformed data file
/// surfaces as a readable message on the page instead of a blank screen and a console stack trace.
/// </remarks>
public sealed class GameDataProvider(HttpClient httpClient)
{
    private readonly HttpClient _httpClient =
        httpClient ?? throw new ArgumentNullException(nameof(httpClient));

    private Task<GameData>? _loading;
    private SettlementGenerator? _generator;

    /// <summary>Loads the data, reusing the in-flight or completed load on later calls.</summary>
    public Task<GameData> GetDataAsync() =>
        _loading ??= GameData.LoadAsync(new HttpDataFileReader(_httpClient));

    /// <summary>Gets a generator bound to the loaded data.</summary>
    public async Task<SettlementGenerator> GetGeneratorAsync()
    {
        GameData data = await GetDataAsync();
        return _generator ??= new SettlementGenerator(data);
    }
}
