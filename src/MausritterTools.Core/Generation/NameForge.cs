using System.Text;
using MausritterTools.Core.Data;
using Structed.Inkwell.Data;
using Structed.Inkwell.Generation;
using Structed.Inkwell.Randomness;

namespace MausritterTools.Core.Generation;

/// <summary>
/// Builds names from the SRD's seed tables and this project's mouse name lists.
/// </summary>
/// <remarks>
/// What each name is made of is Mausritter's; how the pieces are put together without showing the
/// seam is <see cref="NameAssembler"/>'s.
/// </remarks>
public static class NameForge
{
    /// <summary>
    /// Rolls a settlement name from the d12 seed columns.
    /// </summary>
    /// <remarks>
    /// The SRD's instruction is "roll d12 twice, choose a start and an end, massage until it sounds
    /// nice", so the raw join is explicitly only a starting point.
    /// <see cref="NameAssembler.Join"/> performs the massaging, which matters because straight
    /// concatenation produces "Stumppond" and "Moonnest".
    /// </remarks>
    public static string SettlementName(DiceRoller dice, NameSeedTable seeds, GrammarText grammar)
    {
        ArgumentNullException.ThrowIfNull(dice);
        ArgumentNullException.ThrowIfNull(seeds);
        ArgumentNullException.ThrowIfNull(grammar);

        IReadOnlyList<string> starts = dice.Roll(2) == 1 ? seeds.StartA : seeds.StartB;
        IReadOnlyList<string> ends = dice.Roll(2) == 1 ? seeds.EndA : seeds.EndB;

        if (starts.Count == 0 || ends.Count == 0)
        {
            return grammar.UnnamedSettlement;
        }

        string start = dice.Pick(starts);
        string end = dice.Pick(ends);

        // "Hillhill" is a legitimate roll but a poor name; try again before settling for it.
        for (int attempt = 0; attempt < 4 && string.Equals(start, end, StringComparison.OrdinalIgnoreCase); attempt++)
        {
            end = dice.Pick(ends);
        }

        return NameAssembler.Join(start, end);
    }

    /// <summary>Rolls a mouse's given and family name.</summary>
    public static (string Given, string Family) MouseName(
        DiceRoller dice, NameTables names, GrammarText grammar)
    {
        ArgumentNullException.ThrowIfNull(dice);
        ArgumentNullException.ThrowIfNull(names);
        ArgumentNullException.ThrowIfNull(grammar);

        string given = names.GivenNames.Count > 0
            ? dice.Pick(names.GivenNames)
            : grammar.DefaultGivenName;

        string family = names.FamilyNames.Count > 0
            ? dice.Pick(names.FamilyNames)
            : grammar.DefaultFamilyName;

        return (given, family);
    }

    /// <summary>
    /// Rolls the tavern's name, e.g. "The Crooked Beetle" or "Zum krummen Käfer".
    /// </summary>
    /// <remarks>
    /// The noun is drawn by position rather than by value so its gender can be read from the
    /// parallel column a translation supplies, which is what decides between "Zum" and "Zur".
    /// </remarks>
    public static string TavernName(DiceRoller dice, TavernTable taverns, GrammarText grammar)
    {
        ArgumentNullException.ThrowIfNull(dice);
        ArgumentNullException.ThrowIfNull(taverns);
        ArgumentNullException.ThrowIfNull(grammar);

        if (taverns.NameA.Count == 0 || taverns.NameB.Count == 0)
        {
            return grammar.DefaultTavernName;
        }

        string adjective = dice.Pick(taverns.NameA);

        int nounIndex = dice.NextIndex(taverns.NameB.Count);
        string noun = taverns.NameB[nounIndex];

        return Assemble(
            grammar.TavernNamePattern,
            ArticleTables.For(grammar.Articles.DativeDefinite, taverns.GenderAt(nounIndex)),
            adjective,
            noun,
            noun,
            "");
    }

    /// <summary>
    /// Rolls the name over a shop's door, using the proprietor's family name where the pattern
    /// calls for it so the sign and the shopkeeper agree.
    /// </summary>
    public static string ShopSign(
        DiceRoller dice,
        ServiceTables services,
        ServiceDefinition service,
        string familyName,
        GrammarText grammar)
    {
        ArgumentNullException.ThrowIfNull(dice);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(grammar);

        if (service.SignNouns.Count == 0)
        {
            return service.Name;
        }

        IReadOnlyList<int> nounIndices = dice.PickDistinctIndices(service.SignNouns.Count, 2);
        int nounIndex = nounIndices[0];
        int noun2Index = nounIndices.Count > 1 ? nounIndices[1] : nounIndex;

        string adjective = services.ShopSignAdjectives.Count > 0
            ? dice.Pick(services.ShopSignAdjectives)
            : "Old";

        SignPattern pattern = services.ShopSignPatterns.Count > 0
            ? dice.PickWeighted(services.ShopSignPatterns, p => p.Weight)
            : new SignPattern { Template = "The {adjective} {noun}" };

        return Assemble(
            pattern.Template,
            ArticleTables.For(grammar.Articles.DativeDefinite, service.SignNounGenderAt(nounIndex)),
            adjective,
            service.SignNouns[nounIndex],
            service.SignNouns[noun2Index],
            familyName);
    }

    /// <summary>Fills one of the sign templates, which all draw on the same handful of slots.</summary>
    private static string Assemble(
        string template, string article, string adjective, string noun, string noun2, string family) =>
        NameAssembler.FromTemplate(
            template,
            ("article", article),
            ("adjective", adjective),
            ("noun", noun),
            ("noun2", noun2),
            ("family", family));
}
