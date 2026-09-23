using System.Text.RegularExpressions;

namespace MausritterTools.Core.Tests;

/// <summary>
/// Guards the line between this repository and the engine it is built on.
/// </summary>
/// <remarks>
/// <para>
/// The dice, the randomness and the browser-to-browser transport all live in
/// <c>Structed.Inkwell</c>, where three tools share them. Only the rules of <em>this</em> game live
/// here. That split is easy to state and easy to erode: the quickest way to fix an engine bug is
/// always to work around it locally, and the second copy is invisible until the two disagree.
/// </para>
/// <para>
/// These are tripwires rather than proofs. They cannot tell whether a new file <em>should</em> have
/// been written here, only that one was, which is enough to make somebody stop and say why in a
/// review.
/// </para>
/// </remarks>
public sealed class EngineBoundaryTests
{
    [Fact]
    public void OnlyOneFileInThisRepositoryKnowsWhatTheDiceMean()
    {
        // The arithmetic is shared; the four rolls Mausritter asks for are not. If a second file
        // has appeared here, the question to answer is whether it is a Mausritter rule or an engine
        // one that got written in the wrong repository.
        string[] files = Directory
            .GetFiles(Path.Combine(CoreRoot, "Dice"), "*.cs", SearchOption.AllDirectories)
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray()!;

        Assert.Equal(["MausritterRolls.cs"], files);
    }

    [Fact]
    public void TheTransportIsNotCopiedIntoThisRepository()
    {
        // Signalling, peer connections and the roster are the engine's problem. A folder by this
        // name would mean somebody had started reimplementing them.
        foreach (string project in new[] { CoreRoot, WebRoot })
        {
            Assert.False(
                Directory.Exists(Path.Combine(project, "Party")),
                $"'{Path.Combine(project, "Party")}' exists. The party transport belongs in Inkwell.");
        }
    }

    [Fact]
    public void ThePartyScriptIsServedByThePackageRatherThanCopiedIn()
    {
        // Party.Blazor ships its own JS under _content/. A copy in wwwroot would keep working right
        // up until the package was upgraded, and then silently talk an older protocol.
        Assert.Empty(Directory.GetFiles(
            Path.Combine(WebRoot, "wwwroot"), "party*.js", SearchOption.AllDirectories));
    }

    [Fact]
    public void RandomnessAlwaysComesFromTheEngine()
    {
        // System.Random's seeded output is not stable across .NET versions, so one use of it
        // anywhere reachable from a seed would invalidate every shared link on the next upgrade.
        List<string> offenders = [];

        foreach (string file in Directory.GetFiles(CoreRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal))
            {
                continue;
            }

            if (Uses.IsMatch(File.ReadAllText(file)))
            {
                offenders.Add(Path.GetFileName(file));
            }
        }

        Assert.Empty(offenders);
    }

    /// <summary>Matches a real use, not the word in a comment explaining why it is banned.</summary>
    private static readonly Regex Uses =
        new(@"new\s+Random\s*\(|Random\.Shared", RegexOptions.Compiled);

    private static string CoreRoot { get; } = ProjectRoot("MausritterTools.Core");

    private static string WebRoot { get; } = ProjectRoot("MausritterTools.Web");

    private static string ProjectRoot(string name)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "src", name);

            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate src/{name} by walking up from '{AppContext.BaseDirectory}'.");
    }
}
