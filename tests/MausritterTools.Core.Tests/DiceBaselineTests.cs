using System.Globalization;
using System.Text;
using MausritterTools.Core.Dice;
using Structed.Inkwell.Dice;

namespace MausritterTools.Core.Tests;

/// <summary>
/// Pins what the dice actually do, seed by seed.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="MausritterRollTests"/> asserts that the rules are <em>right</em>. This asserts only
/// that they have not <em>changed</em>, which is a different and less obvious guarantee: a seed
/// travels in a shared link and is written beside a roll in the log, so two people reading the same
/// seed code must see the same dice. Upgrading the engine, reordering a preset's arguments or
/// altering a notation would all keep every rule test green while quietly making yesterday's seed
/// mean something else.
/// </para>
/// <para>
/// The preset ids and reading keys are pinned for the same reason field paths are pinned in
/// <see cref="GoldenBaselineTests"/>: a preset id is half of every edge key, a reading key is what a
/// wording file is indexed by, and <see cref="MausritterRolls.PartyAppId"/> is what two browsers
/// have to agree on before they can find each other at all.
/// </para>
/// <para>
/// Set <c>UPDATE_GOLDEN=1</c> to rewrite the baseline after a change that is genuinely intended.
/// Read the diff before you commit it. A diff here is never cosmetic.
/// </para>
/// </remarks>
public sealed class DiceBaselineTests
{
    /// <summary>
    /// Arbitrary but fixed. Several, because one seed reaches one branch of a reading.
    /// </summary>
    private static readonly uint[] Seeds = [1u, 0x5EED_1234, 0x0C17_9000, 0xBEEF_CAFE, 0x0FF1_CE55];

    [Fact]
    public void RolledOutputMatchesTheBaseline()
    {
        string actual = BuildManifest();
        string path = BaselinePath();

        if (Environment.GetEnvironmentVariable("UPDATE_GOLDEN") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, actual);
        }

        Assert.True(File.Exists(path), $"The dice baseline is missing. Expected it at '{path}'.");

        string expected = File.ReadAllText(path).ReplaceLineEndings("\n");

        Assert.Equal(expected, actual.ReplaceLineEndings("\n"));
    }

    private static string BuildManifest()
    {
        StringBuilder manifest = new();

        manifest.Append(
            "Dice baseline. Regenerate with UPDATE_GOLDEN=1 and read the diff before committing.\n");
        manifest.Append($"appId={MausritterRolls.PartyAppId}\n");
        manifest.Append($"edges={string.Join(",", MausritterRolls.Edges)}\n");
        manifest.Append(Rule());

        manifest.Append("\nPresets\n");

        foreach (RollPreset preset in MausritterRolls.All)
        {
            string parameter = preset.Parameter is { } p
                ? Invariant($"{p.Id}[{p.Minimum}..{p.Maximum}]={p.Default}")
                : "none";

            manifest.Append($"  {preset.Id} parameter={parameter} edge={preset.HasEdge}\n");
        }

        manifest.Append(Rule());

        manifest.Append("\nReading keys\n");

        foreach (string key in MausritterRolls.ReadingKeys)
        {
            manifest.Append($"  {key}\n");
        }

        manifest.Append("\nEdge keys\n");

        foreach (string key in MausritterRolls.EdgeKeys)
        {
            manifest.Append($"  {key}\n");
        }

        manifest.Append(Rule());

        foreach (RollPreset preset in MausritterRolls.All)
        {
            manifest.Append($"\n{preset.Id}\n");

            foreach (int parameter in ParametersFor(preset))
            {
                foreach (int edge in EdgesFor(preset))
                {
                    manifest.Append(Invariant($"  {parameter} edge={edge,2} -> {preset.Dice(parameter, edge).Text}\n"));

                    foreach (uint seed in Seeds)
                    {
                        manifest.Append(Line(preset, parameter, edge, seed));
                    }
                }
            }
        }

        return manifest.ToString();
    }

    /// <summary>One roll on one line, so a diff points at the seed that moved.</summary>
    private static string Line(RollPreset preset, int parameter, int edge, uint seed)
    {
        RollOutcome outcome = preset.Roll(parameter, edge, seed);

        string dice = string.Join(
            " ",
            outcome.Dice.Select(die => Invariant($"{die.Face}{(die.IsKept ? "" : "*")}")));

        string reading = preset.Read(outcome, parameter) is { } value
            ? Invariant($"{value.Key}={value.Value}")
            : "-";

        string notes = string.Join(
            " ",
            preset.Note(outcome, parameter).Select(note => Invariant($"{note.Key}={note.Value}")));

        return Invariant(
            $"    seed={seed:x8} code={outcome.SeedCode} dice=[{dice}] total={outcome.Total} read={reading} notes=[{notes}]\n");
    }

    /// <summary>
    /// The ends and the middle of a parameter's range, which is where the branches are.
    /// </summary>
    /// <remarks>
    /// A save at 1 and at 20 sits either side of every reading it can produce; an attack at 2 and at
    /// 12 proves the weapon die is the one being rolled rather than a fixed one. A preset without a
    /// parameter still rolls, and is asked once.
    /// </remarks>
    private static IEnumerable<int> ParametersFor(RollPreset preset) =>
        preset.Parameter is { } parameter
            ? new[] { parameter.Minimum, parameter.Default, parameter.Maximum }.Distinct()
            : [0];

    private static IEnumerable<int> EdgesFor(RollPreset preset) =>
        preset.HasEdge ? MausritterRolls.Edges : [0];

    private static string Invariant(FormattableString value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private static string Rule() => new('=', 70);

    private static string BaselinePath() =>
        Path.Combine(RepoRoot(), "tests", "MausritterTools.Core.Tests", "Golden", "dice-baseline.txt");

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
