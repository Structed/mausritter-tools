using MausritterTools.Core.Data;

namespace MausritterTools.Core.Tests;

/// <summary>
/// Locates the data files the web app ships, so tests exercise the real content.
/// </summary>
internal static class TestData
{
    private static readonly Lazy<GameData> Shared = new(() => LoadAsync().GetAwaiter().GetResult());

    /// <summary>The loaded and validated game data, shared across tests.</summary>
    public static GameData Game => Shared.Value;

    public static string DataRoot { get; } = FindDataRoot();

    public static Task<GameData> LoadAsync() =>
        GameData.LoadAsync(new FileSystemDataFileReader(DataRoot));

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
