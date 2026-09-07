using MausritterTools.Core.Generation;
using MausritterTools.Core.Randomness;

namespace MausritterTools.Core.Tests;

public class DiceExpressionTests
{
    private static DiceRoller Roller(uint seed = 1) => new(new Pcg32(seed));

    [Theory]
    // The exact forms used by the SRD's social position table.
    [InlineData("d6p", 1, 6)]
    [InlineData("d6 x 10p", 10, 60)]
    [InlineData("d6 x 50p", 50, 300)]
    [InlineData("d4 x 100p", 100, 400)]
    [InlineData("d4 x 1000p", 1000, 4000)]
    // Hireling availability, which has no pip suffix.
    [InlineData("d2", 1, 2)]
    [InlineData("d6", 1, 6)]
    [InlineData("2d6", 2, 12)]
    public void RollsStayWithinTheExpressionsRange(string expression, int min, int max)
    {
        DiceRoller dice = Roller();

        for (int i = 0; i < 500; i++)
        {
            int? value = DiceExpression.TryRoll(dice, expression);

            Assert.NotNull(value);
            Assert.InRange(value!.Value, min, max);
        }
    }

    [Fact]
    public void MultipliedExpressionsLandOnMultiples()
    {
        DiceRoller dice = Roller();

        for (int i = 0; i < 200; i++)
        {
            Assert.Equal(0, DiceExpression.TryRoll(dice, "d6 x 10p")!.Value % 10);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("free")]
    [InlineData("10%")]
    [InlineData("x10p")]
    [InlineData("d0")]
    public void UnparsableExpressionsReturnNull(string? expression) =>
        Assert.Null(DiceExpression.TryRoll(Roller(), expression));

    [Fact]
    public void RollFallsBackWhenTheExpressionIsUnusable() =>
        Assert.Equal(7, DiceExpression.Roll(Roller(), "not dice", fallback: 7));

    [Fact]
    public void EverySocialPositionPaymentIsParsable()
    {
        // A payment the parser cannot read would silently leave a shopkeeper with an empty purse.
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
