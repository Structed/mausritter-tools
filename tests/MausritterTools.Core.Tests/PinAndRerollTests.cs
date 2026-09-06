using MausritterTools.Core.Generation;

namespace MausritterTools.Core.Tests;

/// <summary>
/// Covers the "lock the bits you like, re-roll the rest" behaviour, which is the reason values are
/// drawn from per-field streams rather than one shared sequence.
/// </summary>
public class PinAndRerollTests
{
    private static SettlementGenerator Generator() => new(TestData.Game);

    [Fact]
    public void PinnedValueSurvivesACompleteReroll()
    {
        GenerationOptions options = new() { Seed = 1000, Size = 4 };
        Model.Settlement original = Generator().Generate(options);

        // Lock the name, then re-roll everything by changing the root seed.
        GenerationOptions rerolled = options
            .WithPin("settlement/name", original.Name)
            with
        { Seed = 2000 };

        Model.Settlement after = Generator().Generate(rerolled);

        Assert.Equal(original.Name, after.Name);
        Assert.NotEqual(original.Event, after.Event);
    }

    [Fact]
    public void PinnedValueCanBeAHandEdit()
    {
        GenerationOptions options = new GenerationOptions { Seed = 55, Size = 3 }
            .WithPin("settlement/name", "Nibblewick");

        Assert.Equal("Nibblewick", Generator().Generate(options).Name);
    }

    [Fact]
    public void RemovingAPinRestoresTheRolledValue()
    {
        GenerationOptions options = new() { Seed = 77, Size = 3 };
        string rolled = Generator().Generate(options).Name;

        GenerationOptions pinned = options.WithPin("settlement/name", "Something Else");
        Assert.Equal("Something Else", Generator().Generate(pinned).Name);

        Assert.Equal(rolled, Generator().Generate(pinned.WithoutPin("settlement/name")).Name);
    }

    [Fact]
    public void RerollingOneFieldLeavesEveryOtherFieldAlone()
    {
        // This is the payoff of per-field streams. With a single shared sequence, re-rolling the
        // event would shift every value drawn after it.
        GenerationOptions options = new() { Seed = 4321, Size = 5 };
        Model.Settlement before = Generator().Generate(options);

        Model.Settlement after = Generator().Generate(options.WithReroll("settlement/event"));

        Assert.NotEqual(before.Event, after.Event);
        Assert.Equal(before.Name, after.Name);
        Assert.Equal(before.Size.Name, after.Size.Name);
        Assert.Equal(before.Governance, after.Governance);
        Assert.Equal(before.Inhabitants, after.Inhabitants);
        Assert.Equal(before.Industries, after.Industries);
        Assert.Equal(before.Host.Id, after.Host.Id);
        Assert.Equal(
            before.Shops.Select(s => s.SignName),
            after.Shops.Select(s => s.SignName));
    }

    [Fact]
    public void RerollingOneShopLeavesTheOthersAlone()
    {
        GenerationOptions options = new() { Seed = 8080, Size = 6, NearHumanTown = true };
        Model.Settlement before = Generator().Generate(options);

        Assert.True(before.Shops.Count >= 3, "Need a few shops for this to be meaningful.");

        Model.Settlement after = Generator().Generate(options.WithReroll("shop/1/keeper/name"));

        Assert.NotEqual(before.Shops[1].Keeper.FullName, after.Shops[1].Keeper.FullName);
        Assert.Equal(before.Shops[0].Keeper.FullName, after.Shops[0].Keeper.FullName);
        Assert.Equal(before.Shops[2].Keeper.FullName, after.Shops[2].Keeper.FullName);
    }

    [Fact]
    public void RepeatedRerollsKeepProducingFreshValues()
    {
        GenerationOptions options = new() { Seed = 9, Size = 4 };
        List<string> events = [];

        for (int i = 0; i < 6; i++)
        {
            events.Add(Generator().Generate(options).Event);
            options = options.WithReroll("settlement/event");
        }

        Assert.True(events.Distinct().Count() >= 4, $"Only {events.Distinct().Count()} distinct events in 6 re-rolls.");
    }

    [Fact]
    public void RerollingReleasesAnExistingPin()
    {
        // A pinned field would otherwise ignore its new roll, leaving the button apparently dead.
        GenerationOptions options = new GenerationOptions { Seed = 12, Size = 4 }
            .WithPin("settlement/event", "Pinned event");

        Assert.Equal("Pinned event", Generator().Generate(options).Event);

        GenerationOptions rerolled = options.WithReroll("settlement/event");

        Assert.False(rerolled.IsPinned("settlement/event"));
        Assert.NotEqual("Pinned event", Generator().Generate(rerolled).Event);
    }

    [Fact]
    public void MultiValuedFieldsPinAsAUnit()
    {
        GenerationOptions options = new() { Seed = 5150, Size = 6 };
        Model.Settlement original = Generator().Generate(options);

        Assert.Equal(2, original.Industries.Count);

        GenerationOptions pinned = options
            .WithPin("settlement/industries", string.Join('\n', original.Industries))
            with
        { Seed = 6161 };

        Assert.Equal(original.Industries, Generator().Generate(pinned).Industries);
    }

    [Fact]
    public void PinsCanBeClearedByPrefix()
    {
        GenerationOptions options = new GenerationOptions { Seed = 1 }
            .WithPin("shop/0/sign", "A")
            .WithPin("shop/0/quirk", "B")
            .WithPin("shop/1/sign", "C");

        GenerationOptions cleared = options.WithoutPinsUnder("shop/0/");

        Assert.False(cleared.IsPinned("shop/0/sign"));
        Assert.False(cleared.IsPinned("shop/0/quirk"));
        Assert.True(cleared.IsPinned("shop/1/sign"));
    }

    [Fact]
    public void PinnedSizeChangesWhatTheSettlementSupports()
    {
        GenerationOptions options = new GenerationOptions { Seed = 31 }
            .WithPin("settlement/size", "City");

        Model.Settlement settlement = Generator().Generate(options);

        Assert.Equal("City", settlement.Size.Name);
        Assert.NotNull(settlement.Tavern);
        Assert.Equal(2, settlement.NotableFeatures.Count);
    }

    [Fact]
    public void OptionHelpersDoNotMutateTheOriginal()
    {
        GenerationOptions original = new() { Seed = 1 };

        _ = original.WithPin("a", "1");
        _ = original.WithReroll("b");

        Assert.Empty(original.Pins);
        Assert.Empty(original.Rerolls);
    }
}
