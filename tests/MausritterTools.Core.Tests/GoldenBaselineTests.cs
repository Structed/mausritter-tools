using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MausritterTools.Core.Data;
using MausritterTools.Core.Generation;
using MausritterTools.Core.Interop.FantasiaArchive;
using MausritterTools.Core.Mapping;
using Structed.Inkwell.Mapping;
using MausritterTools.Core.Model;
using MausritterTools.Core.Rendering;
using MausritterTools.Core.Serialization;
using Structed.Inkwell.Data;

namespace MausritterTools.Core.Tests;

/// <summary>
/// Pins everything a shared link, a saved export or a merged Fantasia Archive project depends on.
/// </summary>
/// <remarks>
/// <para>
/// This is a refactoring harness rather than a behavioural test. Nothing here asserts that a value
/// is <em>correct</em>; it asserts that it has not <em>changed</em>. Moving code between assemblies
/// should be invisible to every one of these outputs, and this is what proves it.
/// </para>
/// <para>
/// The field paths in the pin section matter most. A path is not a label: it is hashed into the
/// random stream a field draws from, and it is the key a lock is filed under in every export and
/// every URL anyone has shared. Renaming one would silently change what an existing seed generates
/// and orphan every saved lock, and the only visible symptom would be this file.
/// </para>
/// <para>
/// Set <c>UPDATE_GOLDEN=1</c> to rewrite the baseline after a change that is genuinely intended.
/// Read the diff before you commit it.
/// </para>
/// </remarks>
public sealed class GoldenBaselineTests
{
    /// <summary>Fixed so the timestamp in an export never makes the baseline drift.</summary>
    private static readonly DateTimeOffset Timestamp =
        new(2024, 6, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GeneratedOutputMatchesTheBaseline()
    {
        string actual = BuildManifest();
        string path = BaselinePath();

        if (Environment.GetEnvironmentVariable("UPDATE_GOLDEN") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, actual);
        }

        Assert.True(File.Exists(path), $"The golden baseline is missing. Expected it at '{path}'.");

        string expected = File.ReadAllText(path).ReplaceLineEndings("\n");

        Assert.Equal(expected, actual.ReplaceLineEndings("\n"));
    }

    /// <summary>
    /// The cases the baseline covers, chosen to reach the paths that differ from one another.
    /// </summary>
    /// <remarks>
    /// A farm has no tavern and one industry; a city has a tavern, two of everything and the most
    /// shops, so it exercises the keeper-relationship pass that needs at least two shops. The third
    /// case carries a pin and a re-roll in German, which is the combination that proves a lock
    /// stores a table position rather than the words on the page.
    /// </remarks>
    private static IEnumerable<(string Name, GenerationOptions Options, Locale Locale)> Cases()
    {
        yield return ("smallest", new GenerationOptions { Seed = 0x5EED_1234, Size = 1 }, MausritterLocales.English);

        yield return ("city-near-humans", new GenerationOptions
        {
            Seed = 0x0C17_9000,
            Size = 6,
            NearHumanTown = true,
            Terrain = "forest"
        }, MausritterLocales.English);

        yield return ("pinned-and-rerolled-in-german", new GenerationOptions
        {
            Seed = 0xBEEF_CAFE,
            Size = 4,
            Pins = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["settlement/event"] = "#7",
                ["settlement/name"] = "Hollowbridge"
            },
            Rerolls = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["settlement/industries"] = 2,
                ["map"] = 1
            }
        }, MausritterLocales.German);

        // Water is not rolled for directly: it appears when the trade or features imply it, so the
        // only way to reach BuildWater deliberately is to lock an industry that names it.
        yield return ("waterside", new GenerationOptions
        {
            Seed = 0x0FF1_CE55,
            Size = 5,
            Terrain = "riverbank",
            Pins = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["settlement/industries"] = "#2\n#10"
            }
        }, MausritterLocales.English);
    }

    private static string BuildManifest()
    {
        StringBuilder manifest = new();

        manifest.Append(
            "Golden baseline. Regenerate with UPDATE_GOLDEN=1 and read the diff before committing.\n");
        manifest.Append($"format={SettlementDocument.FormatId} version={SettlementDocument.CurrentVersion}\n");
        manifest.Append($"fantasiaArchiveState={FantasiaArchiveExporter.StateFormatId}\n");

        foreach ((string name, GenerationOptions options, Locale locale) in Cases())
        {
            GameData data = TestData.In(locale);
            Settlement settlement = new SettlementGenerator(data).Generate(options);

            uint mapSeed = MapGenerator.SeedFor(options);
            PlaceMap map = SettlementMapper.Generate(settlement, mapSeed, data.Text.Grammar);
            string svg = SvgMapRenderer.Render(
                map, mapSeed, data.Text.Settlement.Map.AriaLabel, intrinsicSize: true);

            manifest.Append($"\n{Rule()}\ncase {name} [{locale.Code}]\n{Rule()}\n");

            AppendPins(manifest, settlement);
            AppendMap(manifest, map, svg);
            AppendFantasiaArchive(manifest, settlement, options, data.Text, locale);
            AppendJson(manifest, settlement, options, locale);
        }

        return manifest.ToString().ReplaceLineEndings("\n");
    }

    /// <summary>
    /// Records every field path and the table position it resolved to.
    /// </summary>
    /// <remarks>
    /// The single most load-bearing section in the file. These keys are the strings hashed into
    /// each field's random stream, so the set of them, their spelling and the position each one
    /// landed on together decide what every existing seed produces.
    /// </remarks>
    private static void AppendPins(StringBuilder manifest, Settlement settlement)
    {
        manifest.Append("\n-- field paths and pinned positions --\n");

        foreach (KeyValuePair<string, string> pin in
            settlement.PinValues.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            manifest.Append($"{pin.Key} = {Escape(pin.Value)}\n");
        }
    }

    private static void AppendMap(StringBuilder manifest, PlaceMap map, string svg)
    {
        manifest.Append("\n-- map --\n");
        manifest.Append($"boundary={Escape(map.Subject)} shape={map.Shape}\n");
        manifest.Append(Invariant($"canvas={map.Width:0.###}x{map.Height:0.###}\n"));
        manifest.Append(Invariant($"boundaryPoints={map.Boundary.Points.Count}\n"));
        manifest.Append(Invariant(
            $"roads={map.Roads.Count} buildings={map.Buildings.Count} scatter={map.Scatter.Count}\n"));
        manifest.Append($"water={(map.Water is null ? "none" : Invariant($"{map.Water.Points.Count} points"))}\n");

        // Enough geometry to say which building moved when the SVG hash changes, without pinning
        // every coordinate on the map.
        foreach (MapBuilding building in map.Buildings.Where(b => b.IsKeyed).OrderBy(b => b.Key))
        {
            manifest.Append(Invariant(
                $"keyed {building.Key}: {Escape(building.Label ?? "")} at {building.Centre.X:0.##},{building.Centre.Y:0.##} {building.Width:0.##}x{building.Depth:0.##} angle {building.Angle:0.###}\n"));
        }

        foreach (MapLegendEntry entry in map.Legend)
        {
            manifest.Append($"legend {entry.Key}: {Escape(entry.Name)} / {Escape(entry.Detail)}\n");
        }

        manifest.Append($"svgLength={svg.Length.ToString(CultureInfo.InvariantCulture)}\n");
        manifest.Append($"svgSha256={Sha256(svg)}\n");
    }

    /// <summary>
    /// Records the document identities an export mints.
    /// </summary>
    /// <remarks>
    /// Ids are derived from the whole generation state so that re-exporting an unchanged settlement
    /// and merging it again is a no-op. If they drift, a second merge stops being idempotent and
    /// silently duplicates every document in the reader's project instead.
    /// </remarks>
    private static void AppendFantasiaArchive(
        StringBuilder manifest,
        Settlement settlement,
        GenerationOptions options,
        UiText text,
        Locale locale)
    {
        FantasiaArchiveExport export =
            FantasiaArchiveExporter.Export(settlement, options, text, Timestamp, locale.Code);

        manifest.Append("\n-- fantasia archive --\n");
        manifest.Append($"folder={export.FolderName}\n");

        foreach (ExportedFile file in export.Files.OrderBy(f => f.Name, StringComparer.Ordinal))
        {
            IReadOnlyList<string> ids = DocumentIds(file.Content);
            manifest.Append($"{file.Name}: {ids.Count} document(s)\n");

            foreach (string id in ids)
            {
                manifest.Append($"  {id}\n");
            }
        }
    }

    private static void AppendJson(
        StringBuilder manifest, Settlement settlement, GenerationOptions options, Locale locale)
    {
        manifest.Append("\n-- json export --\n");
        manifest.Append(
            SettlementSerializer.ToJson(settlement, options, Timestamp, locale.Code)
                .ReplaceLineEndings("\n"));
        manifest.Append('\n');
    }

    /// <summary>Pulls the <c>_id</c> of every document out of a newline-delimited dump.</summary>
    private static IReadOnlyList<string> DocumentIds(string dump)
    {
        List<string> ids = [];

        foreach (string line in dump.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            using JsonDocument parsed = JsonDocument.Parse(line);

            if (parsed.RootElement.ValueKind != JsonValueKind.Object ||
                !parsed.RootElement.TryGetProperty("docs", out JsonElement docs) ||
                docs.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            ids.AddRange(
                docs.EnumerateArray()
                    .Where(d => d.TryGetProperty("_id", out _))
                    .Select(d => d.GetProperty("_id").GetString() ?? ""));
        }

        ids.Sort(StringComparer.Ordinal);
        return ids;
    }

    private static string Sha256(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    /// <summary>Keeps one record on one line, so a diff points at the field that moved.</summary>
    private static string Escape(string value) =>
        value.Replace("\r", "", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);

    private static string Invariant(FormattableString value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private static string Rule() => new('=', 70);

    private static string BaselinePath() =>
        Path.Combine(RepoRoot(), "tests", "MausritterTools.Core.Tests", "Golden", "baseline.txt");

    private static string RepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MausritterTools.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate the repository root by walking up from '{AppContext.BaseDirectory}'.");
    }
}
