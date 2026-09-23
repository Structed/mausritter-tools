using MausritterTools.Core.Dice;
using Structed.Inkwell.Dice;
using Structed.Inkwell.Party;

namespace MausritterTools.Core.Tests;

/// <summary>
/// Pins what one browser sends and another has to understand.
/// </summary>
/// <remarks>
/// <para>
/// Everything here belongs to the engine, so nothing in this repository can change it. That is the
/// reason to pin it: these values are the compatibility contract between two copies of this site
/// that were loaded at different times, possibly weeks apart, and the version this repository
/// upgrades to is the one that decides whether they can still talk. An engine bump that altered a
/// JSON property name or shortened a table code would break every table in progress and no other
/// test in this repository would notice.
/// </para>
/// <para>
/// A failure here is not a bug in this file. It means the engine changed the wire format, and the
/// question to answer before updating these numbers is whether that change was meant to be a
/// breaking one.
/// </para>
/// </remarks>
public sealed class WireFormatTests
{
    /// <summary>Fixed, so a timestamp cannot make the pinned JSON drift.</summary>
    private static readonly DateTimeOffset At = new(2024, 6, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TheAppIdIsTheOneEveryWrittenDownTableCodeWasHandedOutUnder()
    {
        // Two browsers only meet if this matches exactly. Changing it does not raise an error at
        // either end; the far side simply never arrives.
        Assert.Equal("structed-mausritter-tools-dice", MausritterRolls.PartyAppId);
    }

    [Fact]
    public void ATableCodeIsTwelveCharactersOfAnUnambiguousAlphabet()
    {
        // No i, l, o or u: the code gets read aloud across a table, and the first three are the
        // pairs that get misheard. Shortening the code or widening the alphabet would make every
        // code already written on somebody's notes the wrong length.
        Assert.Equal("0123456789abcdefghjkmnpqrstvwxyz", TableCode.Alphabet);
        Assert.Equal(12, TableCode.Length);

        string code = TableCode.Create();

        Assert.Equal(12, code.Length);
        Assert.All(code, character => Assert.Contains(character, TableCode.Alphabet));
        Assert.True(TableCode.TryParse(code, out string parsed));
        Assert.Equal(code, parsed);
    }

    [Fact]
    public void ARollTravelsAsExactlyTheseBytes()
    {
        RollOutcome outcome = MausritterRolls.Save.Roll(10, 0, 0x5EED_1234);
        RollReading reading = Assert.IsType<RollReading>(MausritterRolls.Save.Read(outcome, 10));

        RollMessage message = RollMessage.From(
            "abc123", "Wilhelmina", outcome, reading, secret: false, At, MausritterRolls.SaveId);

        // Note what is not here: no "secret" property. The serializer omits it at its default, so an
        // ordinary roll does not pay for the flag, and a receiver reads its absence as false.
        Assert.Equal(
            """
            {"version":1,"id":"abc123","player":"Wilhelmina","notation":"1d20","faces":[3],"kept":[true],"total":3,"seed":1592594996,"readingKey":"save/pass","readingValue":10,"preset":"save","at":1717243200000}
            """,
            message.Write());
    }

    [Fact]
    public void TheSeedShownBesideARollIsTheOneThatReproducesIt()
    {
        // The log prints this code and the notation, and that pair is the whole of the claim that
        // the roll was not invented. If the seed travelled altered, the code would be decoration.
        RollOutcome outcome = MausritterRolls.Save.Roll(10, 0, 0x5EED_1234);

        Assert.Equal(0x5EED_1234u, outcome.Seed);

        RollMessage message = RollMessage.From(
            "abc123", "Wilhelmina", outcome, new RollReading("save/pass", 10),
            secret: false, At, MausritterRolls.SaveId);

        Assert.True(RollMessage.TryRead(message.Write(), out RollMessage received));

        RollOutcome again = MausritterRolls.Save.Roll(10, 0, received.Seed);

        Assert.Equal(outcome.Faces, again.Faces);
        Assert.Equal(outcome.Total, again.Total);
        Assert.Equal(outcome.SeedCode, again.SeedCode);
    }

    [Fact]
    public void ARollSurvivesTheRoundTrip()
    {
        RollOutcome outcome = MausritterRolls.Mouse.Roll(null, 0, 0xBEEF_CAFE);
        RollReading reading = Assert.IsType<RollReading>(MausritterRolls.Mouse.Read(outcome, null));

        RollMessage sent = RollMessage.From(
            "x", "Hex", outcome, reading, secret: false, At, MausritterRolls.MouseId);

        Assert.True(RollMessage.TryRead(sent.Write(), out RollMessage received));

        // The dropped die has to survive, because watching it is most of the point of rolling three
        // and keeping two, and the receiving browser re-reads the faces rather than trusting the
        // total it was sent.
        Assert.Equal([1, 6, 6], received.Faces);
        Assert.Equal([false, true, true], received.Kept);
        Assert.Equal(12, received.Total);
        Assert.Equal("mouse/attribute", received.ReadingKey);
        Assert.Equal(MausritterRolls.MouseId, received.Preset);
        Assert.Equal(outcome.Seed, received.Seed);
    }

    [Fact]
    public void APrivateRollArrivesWithoutItsDice()
    {
        // The whole promise of a private roll: the numbers never leave this browser. A ghost says
        // that somebody rolled, and nothing else.
        RollOutcome outcome = MausritterRolls.Attack.Roll(6, 0, 1u);
        RollReading reading = Assert.IsType<RollReading>(MausritterRolls.Attack.Read(outcome, 6));

        RollMessage ghost = RollMessage
            .From("y", "Hex", outcome, reading, secret: true, At, MausritterRolls.AttackId)
            .Ghost();

        Assert.True(ghost.Secret);
        Assert.Empty(ghost.Faces);
        Assert.Empty(ghost.Kept);
        Assert.Equal(0, ghost.Total);
        Assert.Equal(0u, ghost.Seed);
        Assert.Equal("", ghost.Notation);
        Assert.Equal("", ghost.ReadingKey);

        // Not even in the bytes, which is the only version of this claim that matters.
        string json = ghost.Write();

        Assert.DoesNotContain("attack/damage", json, StringComparison.Ordinal);
        Assert.DoesNotContain("1d6", json, StringComparison.Ordinal);
    }

    [Fact]
    public void AHailTravelsAsExactlyTheseBytes()
    {
        Assert.Equal(
            """
            {"version":1,"player":"Wilhelmina","at":1717243200000}
            """,
            Hail.From("Wilhelmina", At).Write());
    }

    [Fact]
    public void TheLimitsThatKeepAPeerFromFloodingTheTableAreStillInPlace()
    {
        // Every one of these is a bound on something a hostile or broken peer controls. They are
        // the engine's, but this site is what gets flooded if one of them disappears.
        Assert.Equal(1, RollMessage.CurrentVersion);
        Assert.Equal(4096, RollMessage.MaximumBytes);
        Assert.Equal(24, RollMessage.MaximumNameLength);
        Assert.Equal(100, RollMessage.MaximumDice);
        Assert.Equal(1, Hail.CurrentVersion);
        Assert.Equal(512, Hail.MaximumBytes);
        Assert.Equal(60, RollHistory.MaximumCount);
        Assert.Equal(65536, RollHistory.MaximumBytes);
        Assert.Equal(32, Roster.MaximumPlayers);
        Assert.Equal(200, RollLog.Capacity);
    }

    [Fact]
    public void EveryPresetIdFitsTheWireAndIsStable()
    {
        // A preset id is sent with every roll and is half of every edge key. Renaming one orphans
        // the wording and makes an older browser's rolls unreadable.
        Assert.Equal(
            ["save", "attack", "spell", "mouse"],
            MausritterRolls.All.Select(preset => preset.Id));

        foreach (RollPreset preset in MausritterRolls.All)
        {
            Assert.True(preset.Id.Length <= RollMessage.MaximumTextLength);
        }

        foreach (string key in MausritterRolls.ReadingKeys)
        {
            Assert.True(key.Length <= RollMessage.MaximumTextLength, key);
        }
    }
}
