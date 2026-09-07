using MausritterTools.Core.Data;

namespace MausritterTools.Core.Tests;

/// <summary>
/// Locates the data files the web app ships, so tests exercise the real content.
/// </summary>
internal static class TestData
{
    private static readonly Lazy<GameData> Shared = new(() => LoadAsync().GetAwaiter().GetResult());

    private static readonly Dictionary<string, GameData> Localised = new(StringComparer.Ordinal);

    /// <summary>The loaded and validated game data, shared across tests.</summary>
    public static GameData Game => Shared.Value;

    /// <summary>The English UI text and sentence patterns.</summary>
    public static GrammarText Grammar => Game.Text.Grammar;

    public static string DataRoot { get; } = FindDataRoot();

    public static Task<GameData> LoadAsync(Locale? locale = null) =>
        GameData.LoadAsync(new FileSystemDataFileReader(DataRoot), locale);

    /// <summary>The data as loaded in one language, so translations can be compared against it.</summary>
    public static GameData In(Locale locale)
    {
        ArgumentNullException.ThrowIfNull(locale);

        lock (Localised)
        {
            if (!Localised.TryGetValue(locale.Code, out GameData? data))
            {
                data = LoadAsync(locale).GetAwaiter().GetResult();
                Localised[locale.Code] = data;
            }

            return data;
        }
    }

    private static string FindDataRoot()
    {
        // Walk up from the test binary until the repository root is recognisable, so the tests do
        // not depend on the working directory the runner happens to use.
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            string candidate = Path.Combine(
                directory.FullName, "src", "MausritterTools.Web", "wwwroot", "data");

            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate wwwroot/data by walking up from '{AppContext.BaseDirectory}'.");
    }
}
