using MausritterTools.Core.Generation;
using MausritterTools.Core.Randomness;

namespace MausritterTools.Core.Tests;

public class NameForgeTests
{
    [Theory]
    // A doubled letter at the seam is collapsed: Oaks + stand would read "Oaksstand".
    [InlineData("Oaks", "stand", "Oakstand")]
    [InlineData("Moon", "nest", "Moonest")]
    [InlineData("Stump", "pond", "Stumpond")]
    [InlineData("Fig", "grove", "Figrove")]
    // A trailing 'e' before a vowel is dropped: Rose + ashe would read "Roseashe".
    [InlineData("Rose", "ashe", "Rosashe")]
    [InlineData("Stone", "ashe", "Stonashe")]
    // Clean joins are left untouched.
    [InlineData("Willow", "ville", "Willowville")]
    [InlineData("Black", "creek", "Blackcreek")]
    [InlineData("Berry", "mill", "Berrymill")]
    public void JoinSmoothsTheSeam(string start, string end, string expected) =>
        Assert.Equal(expected, NameForge.Join(start, end));

    [Fact]
    public void JoinAlwaysCapitalises() =>
        Assert.Equal("Oakstand", NameForge.Join("oaks", "stand"));

    [Theory]
    [InlineData("", "thorpe", "Thorpe")]
    [InlineData("Oaks", "", "Oaks")]
    public void JoinHandlesMissingHalves(string start, string end, string expected) =>
        Assert.Equal(expected, NameForge.Join(start, end));

    [Fact]
    public void GeneratedNamesNeverContainATripledLetter()
    {
        for (uint seed = 1; seed <= 500; seed++)
        {
            string name = NameForge.SettlementName(
                new DiceRoller(SeedDerivation.CreateStream(seed, "settlement/name")),
                TestData.Game.Settlement.NameSeeds);

            for (int i = 2; i < name.Length; i++)
            {
                bool tripled =
                    char.ToLowerInvariant(name[i]) == char.ToLowerInvariant(name[i - 1]) &&
                    char.ToLowerInvariant(name[i]) == char.ToLowerInvariant(name[i - 2]);

                Assert.False(tripled, $"'{name}' contains a tripled letter.");
            }
        }
    }

    [Fact]
    public void GeneratedNamesAreNeverEmptyOrDoubledWords()
    {
        for (uint seed = 1; seed <= 500; seed++)
        {
            string name = NameForge.SettlementName(
                new DiceRoller(SeedDerivation.CreateStream(seed, "settlement/name")),
                TestData.Game.Settlement.NameSeeds);

            Assert.False(string.IsNullOrWhiteSpace(name));
            Assert.True(char.IsUpper(name[0]), $"'{name}' should be capitalised.");
            Assert.DoesNotContain("hillhill", name, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void NamesShowGoodVariety()
    {
        string[] names =
        [
            .. Enumerable.Range(1, 300).Select(i => NameForge.SettlementName(
                new DiceRoller(SeedDerivation.CreateStream((uint)i, "settlement/name")),
                TestData.Game.Settlement.NameSeeds))
        ];

        Assert.True(names.Distinct().Count() > 150, $"Only {names.Distinct().Count()} distinct names in 300 rolls.");
    }

    [Fact]
    public void TavernNamesReadLikeSigns()
    {
        for (uint seed = 1; seed <= 50; seed++)
        {
            string name = NameForge.TavernName(
                new DiceRoller(SeedDerivation.CreateStream(seed, "tavern")),
                TestData.Game.Settlement.Taverns);

            Assert.StartsWith("The ", name, StringComparison.Ordinal);
            Assert.Equal(3, name.Split(' ').Length);
        }
    }

    [Fact]
    public void ShopSignsExpandEveryPlaceholder()
    {
        Data.ServiceDefinition service = TestData.Game.Services.Services.First(s => s.SignNouns.Count > 1);

        for (uint seed = 1; seed <= 200; seed++)
        {
            string sign = NameForge.ShopSign(
                new DiceRoller(SeedDerivation.CreateStream(seed, "sign")),
                TestData.Game.Services,
                service,
                "Thistledown");

            Assert.False(string.IsNullOrWhiteSpace(sign));
            Assert.DoesNotContain('{', sign);
            Assert.DoesNotContain('}', sign);
        }
    }

    [Fact]
    public void ShopSignsUseTheKeepersFamilyNameWhenThePatternCallsForIt()
    {
        Data.ServiceDefinition service = TestData.Game.Services.Services.First(s => s.SignNouns.Count > 1);

        bool sawFamilyName = Enumerable.Range(1, 200).Any(i => NameForge.ShopSign(
            new DiceRoller(SeedDerivation.CreateStream((uint)i, "sign")),
            TestData.Game.Services,
            service,
            "Thistledown").Contains("Thistledown", StringComparison.Ordinal));

        Assert.True(sawFamilyName, "Family-name sign patterns should be reachable.");
    }

    [Fact]
    public void MouseNamesCombineGivenAndFamilyNames()
    {
        (string given, string family) = NameForge.MouseName(
            new DiceRoller(SeedDerivation.CreateStream(1, "mouse")),
            TestData.Game.Names);

        Assert.Contains(given, TestData.Game.Names.GivenNames);
        Assert.Contains(family, TestData.Game.Names.FamilyNames);
    }
}
