using Structed.Inkwell.Dice;

namespace MausritterTools.Core.Dice;

/// <summary>
/// The four rolls Mausritter asks for again and again, what the dice mean when they land, and the
/// name this app answers to on the wire.
/// </summary>
/// <remarks>
/// <para>
/// The arithmetic lives in <c>Structed.Inkwell</c>, where it is shared with the other tools built
/// on the same engine. This is the file that stayed behind: the one that knows which game it is
/// playing. Nothing else in this repository is allowed to, and a test says so.
/// </para>
/// <para>
/// Every preset here implements a rule printed in the Mausritter SRD, which the Creative Commons
/// licence permits. Three rolls that FLAIL! offers are deliberately absent, and their absence is the
/// point: Mausritter has no natural-1 or natural-20 critical, no morale roll and no reaction roll.
/// Adding one would be a house rule, and a house rule that arrived without a label is exactly what
/// this repository is careful not to ship. The notation box covers the rest.
/// </para>
/// <para>
/// Nothing here decides what a result <em>does</em>. A miscast is counted, not applied; a usage dot
/// is announced, not filled in. The player has the sheet, and keeping the rules engine out of the
/// dice tool is what lets a table house-rule any of this without the tool arguing.
/// </para>
/// </remarks>
public static class MausritterRolls
{
    /// <summary>The name this app answers to when two browsers look for each other.</summary>
    /// <remarks>
    /// <para>
    /// Load-bearing, and it does not look it. The channel namespaces its signalling by this string,
    /// so two browsers only meet if their app ids match exactly. Every table code anybody has
    /// written down was handed out under this one; change it and those codes stop finding their
    /// tables, with no error at either end — the far side simply never arrives.
    /// </para>
    /// <para>
    /// It reads like configuration and belongs with the rest of the transport, which is exactly why
    /// it is kept here instead: this is the file that is allowed to know which game this is, and the
    /// engine that does the connecting deliberately does not.
    /// </para>
    /// </remarks>
    public const string PartyAppId = "structed-mausritter-tools-dice";

    /// <summary>Identifies the save preset.</summary>
    public const string SaveId = "save";

    /// <summary>Identifies the attack preset.</summary>
    public const string AttackId = "attack";

    /// <summary>Identifies the spell preset.</summary>
    public const string SpellId = "spell";

    /// <summary>Identifies the mouse attribute preset.</summary>
    public const string MouseId = "mouse";

    /// <summary>The die an impaired attack rolls, whatever the weapon.</summary>
    private const int ImpairedSides = 4;

    /// <summary>The die an enhanced attack rolls, whatever the weapon.</summary>
    private const int EnhancedSides = 12;

    /// <summary>The lowest face that marks a usage dot when a spell is cast.</summary>
    private const int UsageFace = 4;

    /// <summary>The face that miscasts.</summary>
    private const int MiscastFace = 6;

    /// <summary>
    /// How far the odds can be bent, in the steps this game actually defines.
    /// </summary>
    /// <remarks>
    /// The engine allows two steps either way, because advantages stack in some games. Mausritter is
    /// not one of them: it defines exactly one step, and says nothing about what a second would do.
    /// Offering a control for a rule the book does not have would be inventing one, so the row shows
    /// three buttons. The notation lambdas still treat anything positive and anything negative alike,
    /// so a stray step arriving from a stale link cannot produce dice this game has no name for.
    /// </remarks>
    public static IReadOnlyList<int> Edges { get; } = [-1, 0, 1];

    /// <summary>
    /// A d20 rolled at or under an attribute.
    /// </summary>
    /// <remarks>
    /// <para>
    /// At or under, not strictly under: Mausritter says a roll equal to the attribute succeeds. The
    /// distinction is one number wide and decides a twentieth of every save in the game, so it is
    /// worth writing down beside the code that depends on it.
    /// </para>
    /// <para>
    /// Advantage is not a bonus but a second d20 with the <em>lowest</em> kept, because low is good
    /// here. That inversion is the single easiest thing in this file to get backwards.
    /// </para>
    /// </remarks>
    public static RollPreset Save { get; } = new()
    {
        Id = SaveId,
        Parameter = new RollParameter("score", 1, 20, 10),
        HasEdge = true,

        Notation = (_, edge) => edge switch
        {
            0 => Pool(1, 20),
            > 0 => Keep(2, 20, KeepRule.Lowest),
            _ => Keep(2, 20, KeepRule.Highest)
        },

        // Read from the die that counted rather than from the total, so the die that advantage threw
        // away cannot decide the save.
        Reading = (outcome, score) =>
            new RollReading(Kept(outcome) <= score ? "save/pass" : "save/fail", score)
    };

    /// <summary>
    /// The weapon's die, which is the whole of an attack.
    /// </summary>
    /// <remarks>
    /// <para>
    /// There is no roll to hit in Mausritter. An attack always lands, and the only question is the
    /// damage, from which the defender's armour is then subtracted. So this preset has no success
    /// and no failure to report — only a number — and the armour is left off here, because the tool
    /// does not know who is being hit.
    /// </para>
    /// <para>
    /// Impaired and enhanced <em>replace</em> the weapon's die rather than adjusting it: a d4 and a
    /// d12 respectively, however large or small the weapon. A mouse swinging a d10 blade in a cramped
    /// tunnel rolls d4, and the parameter is ignored on purpose.
    /// </para>
    /// <para>
    /// The weapon parameter starts at 4 rather than at the engine's floor of 2. Mausritter's
    /// smallest die is the d4 an improvised or impaired attack rolls, and there is no d2 or d3 in
    /// the game or in most dice bags. Offering them would be inventing equipment; the notation box
    /// is there for anyone who really does want to roll one.
    /// </para>
    /// </remarks>
    public static RollPreset Attack { get; } = new()
    {
        Id = AttackId,
        Parameter = new RollParameter("weapon", ImpairedSides, EnhancedSides, 6),
        HasEdge = true,

        Notation = (weapon, edge) => edge switch
        {
            0 => Pool(1, weapon),
            > 0 => Pool(1, EnhancedSides),
            _ => Pool(1, ImpairedSides)
        },

        Reading = (outcome, _) => new RollReading("attack/damage", outcome.Total)
    };

    /// <summary>
    /// A d6 for each point of power a spell is cast with.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The reading carries <c>[DICE]</c> — the number of dice invested — because <c>[SUM]</c> is the
    /// total already shown beside the roll, and a spell's effect is written in terms of both. Saying
    /// the same number twice would be the less useful of the two choices.
    /// </para>
    /// <para>
    /// The usage marks and the miscasts are worked out again from the faces rather than taken on
    /// trust, which is what <see cref="RollPreset.Notes"/> is handed a bare list of numbers for. A
    /// browser across the table receives the dice and re-reads them itself, so a peer cannot announce
    /// a clean cast it did not roll.
    /// </para>
    /// </remarks>
    public static RollPreset Spell { get; } = new()
    {
        Id = SpellId,
        Parameter = new RollParameter("power", 1, 6, 1),

        Notation = (power, _) => Pool(power, 6),

        Reading = (_, power) => new RollReading("spell/cast", power),

        Notes = (faces, _) =>
        {
            // Always said, even at zero: the usage dots are the price of the spell, and a cast that
            // costs nothing is the most remarkable result on the table. Leaving it out would make
            // that indistinguishable from a reading that simply forgot to mention it.
            List<RollReading> notes =
                [new RollReading("spell/usage", faces.Count(face => face >= UsageFace))];

            int miscasts = faces.Count(face => face == MiscastFace);

            if (miscasts > 0)
            {
                notes.Add(new RollReading("spell/miscast", miscasts));
            }

            return notes;
        }
    };

    /// <summary>
    /// Three d6, keeping the best two, for one of a new mouse's attributes.
    /// </summary>
    /// <remarks>
    /// One attribute per roll, not a whole mouse: STR, DEX and WIL are three separate rolls of the
    /// same dice, and rolling them together would have to decide which was which. The dropped die is
    /// still reported by the engine, which is most of the pleasure of rolling this way.
    /// </remarks>
    public static RollPreset Mouse { get; } = new()
    {
        Id = MouseId,

        Notation = (_, _) => Keep(3, 6, KeepRule.Highest, keep: 2),

        Reading = (outcome, _) => new RollReading("mouse/attribute", outcome.Total)
    };

    /// <summary>The presets, in the order they are offered.</summary>
    public static IReadOnlyList<RollPreset> All { get; } = [Save, Attack, Spell, Mouse];

    /// <summary>
    /// Every reading and note key a roll can produce, named for the wording file.
    /// </summary>
    /// <remarks>
    /// Listed rather than discovered, because the only way to find them all by rolling would be to
    /// roll until each had come up. A missing key does not throw; it renders as an empty line in the
    /// middle of the log, which is precisely the kind of fault that survives review. A test walks
    /// this list against every language.
    /// </remarks>
    public static IReadOnlyList<string> ReadingKeys { get; } =
    [
        "save/pass",
        "save/fail",
        "attack/damage",
        "spell/cast",
        "spell/usage",
        "spell/miscast",
        "mouse/attribute"
    ];

    /// <summary>
    /// Names the wording for one step of the edge control on one preset.
    /// </summary>
    /// <remarks>
    /// Per preset, because Mausritter uses two different pairs of words for the same idea: a save is
    /// made with advantage or disadvantage, an attack is enhanced or impaired. One shared label would
    /// have to be wrong about one of them.
    /// </remarks>
    public static string EdgeKey(string presetId, int step) =>
        $"{presetId}/edge/{(step < 0 ? "down" : step > 0 ? "up" : "level")}";

    /// <summary>Every edge label the page can ask for, for the same reason as the reading keys.</summary>
    public static IReadOnlyList<string> EdgeKeys { get; } =
        [.. All.Where(preset => preset.HasEdge)
               .SelectMany(preset => Edges.Select(step => EdgeKey(preset.Id, step)))];

    /// <summary>The face on the die that decided the roll.</summary>
    private static int Kept(RollOutcome outcome) =>
        outcome.Dice.FirstOrDefault(die => die.IsKept).Face;

    /// <summary>
    /// A plain pool, which the engine refuses only for counts and sides no preset here asks for.
    /// </summary>
    private static DiceNotation Pool(int count, int sides) =>
        DiceNotation.Pool(count, sides)
            ?? throw new InvalidOperationException($"{count}d{sides} is not a pool the engine accepts.");

    /// <summary>A handful of dice of which only some count.</summary>
    private static DiceNotation Keep(int count, int sides, KeepRule rule, int keep = 1)
    {
        if (!DiceNotation.TryParse(
                $"{count}d{sides}k{(rule is KeepRule.Lowest ? "l" : "h")}{keep}",
                out DiceNotation notation))
        {
            throw new InvalidOperationException(
                $"{count}d{sides} keeping {keep} is not a roll the engine accepts.");
        }

        return notation;
    }
}
