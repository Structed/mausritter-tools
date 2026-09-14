using MausritterTools.Core.Generation;
using Structed.Inkwell.Randomness;

namespace MausritterTools.Core.Tests;

public class NameForgeTests
{
    [Fact]
    public void GeneratedNamesNeverContainATripledLetter()
    {
        for (uint seed = 1; seed <= 500; seed++)
        {
            string name = NameForge.SettlementName(
                new DiceRoller(SeedDerivation.CreateStream(seed, "settlement/name")),
                TestData.Game.Settlement.NameSeeds,
                TestData.Grammar);

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
                TestData.Game.Settlement.NameSeeds,
                TestData.Grammar);

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
                TestData.Game.Settlement.NameSeeds,
                TestData.Grammar))
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
                TestData.Game.Settlement.Taverns,
                TestData.Grammar);

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
                "Thistledown",
                TestData.Grammar);

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
                "Thistledown",
                TestData.Grammar).Contains("Thistledown", StringComparison.Ordinal));

        Assert.True(sawFamilyName, "Family-name sign patterns should be reachable.");
    }

    [Fact]
    public void MouseNamesCombineGivenAndFamilyNames()
    {
        (string given, string family) = NameForge.MouseName(
            new DiceRoller(SeedDerivation.CreateStream(1, "mouse")),
                TestData.Game.Names,
                TestData.Grammar);

        Assert.Contains(given, TestData.Game.Names.GivenNames);
        Assert.Contains(family, TestData.Game.Names.FamilyNames);
    }
}
