using MausritterTools.Core.Dice;
using Structed.Inkwell.Dice;

namespace MausritterTools.Core.Tests;

/// <summary>
/// The rules themselves, as printed in the Mausritter SRD.
/// </summary>
/// <remarks>
/// <para>
/// Everything here is a rule somebody could get wrong by one number and never notice: a save that
/// failed on equal, an impaired attack that still rolled the weapon's die, advantage that kept the
/// wrong end of two d20s. None of those would throw, and all of them would quietly change the game.
/// </para>
/// <para>
/// Rolls are made with fixed seeds where a specific result is needed, and swept across a range of
/// seeds where the claim is about every possible result. The engine's generator is stable across
/// .NET versions, so both are reproducible.
/// </para>
/// </remarks>
public class MausritterRollTests
{
    /// <summary>Enough rolls to see every face of a d20 many times over.</summary>
    private const int Sweep = 500;

    [Fact]
    public void ThePresetsAreOfferedInAKnownOrder()
    {
        Assert.Equal(
            ["save", "attack", "spell", "mouse"],
            MausritterRolls.All.Select(preset => preset.Id));
    }

    [Fact]
    public void TheAppIdIsTheOneEveryTableCodeWasHandedOutUnder()
    {
        // Pinned rather than described. If this ever needs changing, the change is a compatibility
        // break and ought to be argued for in a diff, not slipped in.
        Assert.Equal("structed-mausritter-tools-dice", MausritterRolls.PartyAppId);
    }

    [Fact]
    public void OnlyOneStepOfAdvantageIsOffered()
    {
        Assert.Equal([-1, 0, 1], MausritterRolls.Edges);
    }

    // -- saves ------------------------------------------------------------------------------

    [Fact]
    public void APlainSaveRollsOneD20()
    {
        Assert.Equal("1d20", MausritterRolls.Save.Dice(10, 0).Text);
    }

    [Fact]
    public void AdvantageKeepsTheLowestOfTwoD20s()
    {
        // Low is good on a Mausritter save, so advantage keeps the lowest. Getting this backwards
        // is the single easiest mistake in the file it guards.
        Assert.Equal("2d20kl1", MausritterRolls.Save.Dice(10, 1).Text);
    }

    [Fact]
    public void DisadvantageKeepsTheHighestOfTwoD20s()
    {
        Assert.Equal("2d20kh1", MausritterRolls.Save.Dice(10, -1).Text);
    }

    [Fact]
    public void AStrayExtraStepOfAdvantageStillRollsTwoDice()
    {
        // The engine permits two steps; Mausritter defines one. A stale link carrying ±2 must not
        // produce dice this game has no name for.
        Assert.Equal("2d20kl1", MausritterRolls.Save.Dice(10, 2).Text);
        Assert.Equal("2d20kh1", MausritterRolls.Save.Dice(10, -2).Text);
    }

    [Fact]
    public void ASaveEqualToTheScorePasses()
    {
        // At or under, not strictly under. One number wide, and a twentieth of every save.
        RollReading? reading = ReadSaveOf(12, score: 12);

        Assert.Equal("save/pass", reading?.Key);
    }

    [Fact]
    public void ASaveOneOverTheScoreFails()
    {
        Assert.Equal("save/fail", ReadSaveOf(13, score: 12)?.Key);
    }

    [Fact]
    public void ASaveReadingCarriesTheScoreItWasTestedAgainst()
    {
        Assert.Equal(12, ReadSaveOf(5, score: 12)?.Value);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void ASaveWithAnEdgeIsReadFromTheDieThatCounted(int edge)
    {
        for (uint seed = 0; seed < Sweep; seed++)
        {
            RollOutcome outcome = MausritterRolls.Save.Roll(10, edge, seed);
            RollReading? reading = MausritterRolls.Save.Read(outcome, 10);

            int kept = outcome.Dice.Single(die => die.IsKept).Face;
            int dropped = outcome.Dice.Single(die => !die.IsKept).Face;

            Assert.Equal(kept <= 10 ? "save/pass" : "save/fail", reading?.Key);

            // The discarded die is the whole point of the keep rule: it must not be able to decide
            // the save, in either direction.
            if (edge > 0)
            {
                Assert.True(kept <= dropped);
            }
            else
            {
                Assert.True(kept >= dropped);
            }
        }
    }

    [Fact]
    public void ASaveScoreIsClampedToSomethingAD20CanTest()
    {
        Assert.Equal(1, MausritterRolls.Save.Settle(-4));
        Assert.Equal(20, MausritterRolls.Save.Settle(99));
        Assert.Equal(10, MausritterRolls.Save.Settle(null));
    }

    // -- attacks ----------------------------------------------------------------------------

    [Theory]
    [InlineData(4, "1d4")]
    [InlineData(6, "1d6")]
    [InlineData(8, "1d8")]
    [InlineData(10, "1d10")]
    [InlineData(12, "1d12")]
    public void AnAttackRollsTheWeaponsDie(int weapon, string expected)
    {
        Assert.Equal(expected, MausritterRolls.Attack.Dice(weapon, 0).Text);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(10)]
    [InlineData(12)]
    public void AnImpairedAttackRollsAD4WhateverTheWeapon(int weapon)
    {
        // Impaired replaces the die rather than adjusting it. A mouse with a d10 blade in a cramped
        // tunnel rolls d4, and the parameter is ignored on purpose.
        Assert.Equal("1d4", MausritterRolls.Attack.Dice(weapon, -1).Text);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(12)]
    public void AnEnhancedAttackRollsAD12WhateverTheWeapon(int weapon)
    {
        Assert.Equal("1d12", MausritterRolls.Attack.Dice(weapon, 1).Text);
    }

    [Fact]
    public void TheWeaponIsOnlyEverADieTheGameHas()
    {
        // The engine would allow a d2 and a d3. Mausritter names neither, and neither is in a
        // normal dice bag; d4 is the smallest it asks for and d12 the largest it can reach.
        RollParameter weapon = Assert.IsType<RollParameter>(MausritterRolls.Attack.Parameter);

        Assert.Equal(4, weapon.Minimum);
        Assert.Equal(12, weapon.Maximum);
        Assert.Equal(6, weapon.Default);
    }

    [Fact]
    public void AnAttackAlwaysLandsAndOnlyEverReportsDamage()
    {
        // There is no roll to hit in Mausritter, so there is no failure for this preset to report.
        for (uint seed = 0; seed < Sweep; seed++)
        {
            RollOutcome outcome = MausritterRolls.Attack.Roll(6, 0, seed);
            RollReading? reading = MausritterRolls.Attack.Read(outcome, 6);

            Assert.Equal("attack/damage", reading?.Key);
            Assert.Equal(outcome.Total, reading?.Value);
            Assert.InRange(reading!.Value, 1, 6);
        }
    }

    // -- spells -----------------------------------------------------------------------------

    [Theory]
    [InlineData(1, "1d6")]
    [InlineData(3, "3d6")]
    [InlineData(6, "6d6")]
    public void ASpellRollsOneD6PerPointOfPower(int power, string expected)
    {
        Assert.Equal(expected, MausritterRolls.Spell.Dice(power, 0).Text);
    }

    [Fact]
    public void ASpellHasNoEdgeControlBecauseTheBookDefinesNone()
    {
        Assert.False(MausritterRolls.Spell.HasEdge);
    }

    [Fact]
    public void ASpellReadingCarriesTheDiceInvestedRatherThanTheirSum()
    {
        // [SUM] is already the total shown beside the roll; [DICE] is the other half of how a
        // spell's effect is written, and the half that would otherwise go unsaid.
        RollOutcome outcome = MausritterRolls.Spell.Roll(3, 0, seed: 12345);
        RollReading? reading = MausritterRolls.Spell.Read(outcome, 3);

        Assert.Equal("spell/cast", reading?.Key);
        Assert.Equal(3, reading?.Value);
    }

    [Fact]
    public void EveryFourFiveOrSixMarksOneUsage()
    {
        IReadOnlyList<RollReading> notes = MausritterRolls.Spell.Note([1, 4, 5, 6, 3], 5);

        Assert.Equal(3, Note(notes, "spell/usage"));
    }

    [Fact]
    public void ACastThatMarksNoUsageStillSaysSo()
    {
        // A free cast is the most remarkable result on the table. Silence would be indistinguishable
        // from a reading that simply forgot to mention it.
        IReadOnlyList<RollReading> notes = MausritterRolls.Spell.Note([1, 2, 3], 3);

        Assert.Equal(0, Note(notes, "spell/usage"));
    }

    [Fact]
    public void EverySixIsAMiscast()
    {
        IReadOnlyList<RollReading> notes = MausritterRolls.Spell.Note([6, 6, 2], 3);

        Assert.Equal(2, Note(notes, "spell/miscast"));
        Assert.Equal(2, Note(notes, "spell/usage"));
    }

    [Fact]
    public void ACastWithoutASixIsNotAMiscastAtAll()
    {
        IReadOnlyList<RollReading> notes = MausritterRolls.Spell.Note([4, 5, 1], 3);

        Assert.DoesNotContain(notes, note => note.Key == "spell/miscast");
    }

    [Fact]
    public void TheNotesOnASpellAreWorkedOutAgainFromTheFaces()
    {
        // The overload taking bare faces is what a receiving browser uses to re-read dice it was
        // sent, so it must agree exactly with the one that reads a whole outcome.
        for (uint seed = 0; seed < Sweep; seed++)
        {
            RollOutcome outcome = MausritterRolls.Spell.Roll(4, 0, seed);

            Assert.Equal(
                MausritterRolls.Spell.Note(outcome, 4),
                MausritterRolls.Spell.Note([.. outcome.Faces], 4));
        }
    }

    // -- mice -------------------------------------------------------------------------------

    [Fact]
    public void AMouseAttributeIsThreeD6KeepingTheBestTwo()
    {
        Assert.Equal("3d6kh2", MausritterRolls.Mouse.Dice(null, 0).Text);
    }

    [Fact]
    public void AMouseAttributeAsksForNothingAndOffersNoEdge()
    {
        Assert.Null(MausritterRolls.Mouse.Parameter);
        Assert.False(MausritterRolls.Mouse.HasEdge);
    }

    [Fact]
    public void AMouseAttributeLandsBetweenTwoAndTwelveAndDropsItsWorstDie()
    {
        for (uint seed = 0; seed < Sweep; seed++)
        {
            RollOutcome outcome = MausritterRolls.Mouse.Roll(null, 0, seed);
            RollReading? reading = MausritterRolls.Mouse.Read(outcome, null);

            Assert.Equal(3, outcome.Dice.Count);
            Assert.Equal(2, outcome.Dice.Count(die => die.IsKept));
            Assert.InRange(outcome.Total, 2, 12);
            Assert.Equal("mouse/attribute", reading?.Key);
            Assert.Equal(outcome.Total, reading?.Value);

            int dropped = outcome.Dice.Single(die => !die.IsKept).Face;

            Assert.All(
                outcome.Dice.Where(die => die.IsKept),
                die => Assert.True(die.Face >= dropped));
        }
    }

    // -- wiring -----------------------------------------------------------------------------

    [Fact]
    public void EveryKeyAPresetCanProduceIsListed()
    {
        // Rolled rather than trusted: sweep every preset until the readings stop being new, and
        // check nothing turned up that the list does not admit to.
        HashSet<string> seen = [];

        foreach (RollPreset preset in MausritterRolls.All)
        {
            foreach (int edge in MausritterRolls.Edges)
            {
                for (uint seed = 0; seed < Sweep; seed++)
                {
                    int? value = preset.Parameter?.Maximum;
                    RollOutcome outcome = preset.Roll(value, edge, seed);

                    if (preset.Read(outcome, value) is { } reading)
                    {
                        seen.Add(reading.Key);
                    }

                    foreach (RollReading note in preset.Note(outcome, value))
                    {
                        seen.Add(note.Key);
                    }
                }
            }
        }

        Assert.Empty(seen.Except(MausritterRolls.ReadingKeys));
    }

    [Fact]
    public void EachPresetWithAnEdgeNamesItsOwnThreeSteps()
    {
        // A save is made with advantage; an attack is enhanced. One shared label would have to be
        // wrong about one of them.
        Assert.Equal(
            [
                "save/edge/down",
                "save/edge/level",
                "save/edge/up",
                "attack/edge/down",
                "attack/edge/level",
                "attack/edge/up"
            ],
            MausritterRolls.EdgeKeys);
    }

    [Fact]
    public void ThePresetsWithoutAnEdgeContributeNoEdgeLabels()
    {
        Assert.DoesNotContain(MausritterRolls.EdgeKeys, key => key.StartsWith("spell/", StringComparison.Ordinal));
        Assert.DoesNotContain(MausritterRolls.EdgeKeys, key => key.StartsWith("mouse/", StringComparison.Ordinal));
    }

    /// <summary>Rolls a save until the kept die shows <paramref name="face"/>, then reads it.</summary>
    private static RollReading? ReadSaveOf(int face, int score)
    {
        for (uint seed = 0; seed < 10_000; seed++)
        {
            RollOutcome outcome = MausritterRolls.Save.Roll(score, 0, seed);

            if (outcome.Dice[0].Face == face)
            {
                return MausritterRolls.Save.Read(outcome, score);
            }
        }

        throw new InvalidOperationException($"No seed below 10,000 rolled a {face} on a d20.");
    }

    private static int Note(IReadOnlyList<RollReading> notes, string key) =>
        notes.Single(note => note.Key == key).Value;
}
