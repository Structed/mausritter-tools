using MausritterTools.Core.Data;
using MausritterTools.Core.Model;
using MausritterTools.Core.Randomness;

namespace MausritterTools.Core.Generation;

/// <summary>
/// Rolls up a non-player mouse from the SRD's non-player mice tables.
/// </summary>
public static class NpcGenerator
{
    /// <summary>
    /// Generates one mouse.
    /// </summary>
    /// <param name="data">The loaded tables.</param>
    /// <param name="context">Resolves each field against the seed and any pins.</param>
    /// <param name="basePath">Path prefix for this mouse's fields, e.g. <c>shop/2/keeper</c>.</param>
    public static MouseNpc Generate(GameData data, RollContext context, string basePath)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(basePath);

        NpcTables tables = data.Npc;

        string given;
        string family;
        if (context.TryGetPin($"{basePath}/name", out string pinnedName))
        {
            (given, family) = SplitName(pinnedName);
        }
        else
        {
            (given, family) = NameForge.MouseName(context.Dice($"{basePath}/name"), data.Names);
        }

        SocialPosition position = PickPosition(tables, context, $"{basePath}/position");
        Birthsign birthsign = PickBirthsign(tables, context, $"{basePath}/birthsign");

        // The social position table carries the fee a mouse of that station commands, which
        // doubles nicely as how many pips they have on them.
        int purse = DiceExpression.Roll(context.Dice($"{basePath}/purse"), position.Payment);

        return new MouseNpc
        {
            GivenName = given,
            FamilyName = family,
            Appearance = context.Text($"{basePath}/appearance", tables.Appearance),
            Quirk = context.Text($"{basePath}/quirk", tables.Quirk),
            Wants = context.Text($"{basePath}/wants", tables.Wants),
            Position = position,
            Purse = purse,
            Birthsign = birthsign
        };
    }

    private static SocialPosition PickPosition(NpcTables tables, RollContext context, string path)
    {
        if (tables.SocialPositions.Count == 0)
        {
            return new SocialPosition();
        }

        if (context.TryGetPin(path, out string pinned))
        {
            SocialPosition? match = tables.SocialPositions
                .FirstOrDefault(p => string.Equals(p.Name, pinned, StringComparison.OrdinalIgnoreCase));

            if (match is not null)
            {
                return match;
            }
        }

        // Rolled on the table rather than picked uniformly, because "Common" occupies two of the
        // six rows and so should come up twice as often.
        DiceRoller dice = context.Dice(path);
        int roll = dice.Roll(tables.SocialPositions.Count);

        return tables.SocialPositions.FirstOrDefault(p => p.Roll == roll)
               ?? tables.SocialPositions[roll - 1];
    }

    private static Birthsign PickBirthsign(NpcTables tables, RollContext context, string path)
    {
        if (tables.Birthsigns.Count == 0)
        {
            return new Birthsign();
        }

        if (context.TryGetPin(path, out string pinned))
        {
            Birthsign? match = tables.Birthsigns
                .FirstOrDefault(b => string.Equals(b.Name, pinned, StringComparison.OrdinalIgnoreCase));

            if (match is not null)
            {
                return match;
            }
        }

        return context.Dice(path).Pick(tables.Birthsigns);
    }

    /// <summary>Splits a hand-edited full name back into its parts.</summary>
    private static (string Given, string Family) SplitName(string fullName)
    {
        string trimmed = fullName.Trim();
        int split = trimmed.IndexOf(' ');

        return split < 0
            ? (trimmed, "")
            : (trimmed[..split], trimmed[(split + 1)..].Trim());
    }
}
