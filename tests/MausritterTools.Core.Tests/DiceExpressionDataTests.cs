using Structed.Inkwell.Generation;
using Structed.Inkwell.Randomness;

namespace MausritterTools.Core.Tests;

/// <summary>
/// Checks that every dice expression the Mausritter tables actually contain can be read.
/// </summary>
/// <remarks>
/// The parser itself is covered by the engine's own tests. What is left here is the part that
/// depends on this game's data: an expression the parser cannot read does not throw, it quietly
/// leaves a shopkeeper with an empty purse.
/// </remarks>
public class DiceExpressionDataTests
{
    private static DiceRoller Roller(uint seed = 1) => new(new Pcg32(seed));

    [Fact]
    public void EverySocialPositionPaymentIsParsable()
    {
        DiceRoller dice = Roller();

        foreach (Data.SocialPosition position in TestData.Game.Npc.SocialPositions)
        {
            Assert.NotNull(DiceExpression.TryRoll(dice, position.Payment));
        }
    }

    [Fact]
    public void EveryHirelingNumberIsParsable()
    {
        DiceRoller dice = Roller();

        foreach (Data.Hireling hireling in TestData.Game.Hirelings.Hirelings)
        {
            Assert.NotNull(DiceExpression.TryRoll(dice, hireling.Number));
        }
    }
}
