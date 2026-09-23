using MausritterTools.Core.Data;
using MausritterTools.Core.Dice;
using Structed.Inkwell.Data;
using Structed.Inkwell.Dice;

namespace MausritterTools.Core.Tests;

/// <summary>
/// Ties the ids <c>MausritterRolls</c> mints to the wording the page looks them up in.
/// </summary>
/// <remarks>
/// <para>
/// The join between the two is four dictionaries keyed by string, which means nothing checks it at
/// compile time and nothing throws at run time. A preset whose label is missing renders as a button
/// with no text on it; a reading whose line is missing renders as a gap in the middle of the roll
/// log. Both look like a styling problem and survive review, and both are one forgotten key away
/// at all times.
/// </para>
/// <para>
/// Every language is checked, not just English, because the overlay is deep-merged: a key the
/// translation simply omits falls back to English and reads as a missed translation rather than a
/// fault. A key the translation <em>misspells</em> is the dangerous one, and that is what the
/// canonical-key comparison below catches.
/// </para>
/// </remarks>
public class DiceWordingTests
{
    public static TheoryData<string> AllLocales =>
        [.. MausritterLocales.All.Select(locale => locale.Code)];

    [Theory]
    [MemberData(nameof(AllLocales))]
    public void EveryPresetHasALabel(string code)
    {
        DiceText dice = Dice(code);

        foreach (RollPreset preset in MausritterRolls.All)
        {
            AssertSaid(dice.Presets, preset.Id, $"the button that rolls '{preset.Id}'", code);
        }
    }

    [Theory]
    [MemberData(nameof(AllLocales))]
    public void EveryParameterHasALabel(string code)
    {
        DiceText dice = Dice(code);

        foreach (RollParameter parameter in
            MausritterRolls.All.Select(preset => preset.Parameter).OfType<RollParameter>())
        {
            AssertSaid(dice.Parameters, parameter.Id, $"the box asking for '{parameter.Id}'", code);
        }
    }

    [Theory]
    [MemberData(nameof(AllLocales))]
    public void EveryReadingHasALine(string code)
    {
        DiceText dice = Dice(code);

        foreach (string key in MausritterRolls.ReadingKeys)
        {
            AssertSaid(dice.Readings, key, $"what '{key}' means", code);
        }
    }

    [Theory]
    [MemberData(nameof(AllLocales))]
    public void EveryEdgeStepHasALabel(string code)
    {
        DiceText dice = Dice(code);

        foreach (string key in MausritterRolls.EdgeKeys)
        {
            AssertSaid(dice.Edges, key, $"the edge step '{key}'", code);
        }
    }

    [Theory]
    [MemberData(nameof(AllLocales))]
    public void NothingIsWordedThatNoRollCanProduce(string code)
    {
        // The other direction, which is how a renamed id leaves its old wording behind to rot. A
        // line nothing can ever show is not harmless: it is the line somebody will later translate,
        // and the one they will edit when the real line is wrong.
        DiceText dice = Dice(code);

        Assert.Empty(dice.Presets.Keys.Except(MausritterRolls.All.Select(preset => preset.Id)));
        Assert.Empty(dice.Readings.Keys.Except(MausritterRolls.ReadingKeys));
        Assert.Empty(dice.Edges.Keys.Except(MausritterRolls.EdgeKeys));
        Assert.Empty(dice.Parameters.Keys.Except(
            MausritterRolls.All.Select(preset => preset.Parameter?.Id).OfType<string>()));
    }

    [Theory]
    [MemberData(nameof(AllLocales))]
    public void TheFixedProseIsThereToo(string code)
    {
        // A spot check on the typed half. These are the strings that would leave a button blank or
        // a status line empty, which is the same class of fault as a missing key.
        DiceText dice = Dice(code);

        Assert.NotEqual("", dice.Heading);
        Assert.NotEqual("", dice.PlayerLabel);
        Assert.NotEqual("", dice.TableLabel);
        Assert.NotEqual("", dice.StartTable);
        Assert.NotEqual("", dice.JoinTable);
        Assert.NotEqual("", dice.LeaveTable);
        Assert.NotEqual("", dice.Roll);
        Assert.NotEqual("", dice.RollPrivately);
        Assert.NotEqual("", dice.EmptyLog);
        Assert.NotEqual("", dice.NameNeeded);
        Assert.NotEqual("", dice.NotationBad);
        Assert.NotEqual("", dice.TableBad);
        Assert.NotEqual("", dice.Anonymous);
        Assert.NotEqual("", dice.StatusAway);
        Assert.NotEqual("", dice.StatusOpen);
        Assert.NotEqual("", dice.StatusFailed);
    }

    [Fact]
    public void ACountedReadingKeepsItsSlotInEveryLanguage()
    {
        // The number is placed by the sentence, not appended to it, so a translation that drops
        // {count} loses the number entirely — and loses it quietly, because a template with no slot
        // formats perfectly well. These are the readings whose whole point is the number.
        string[] counted = ["save/pass", "save/fail", "spell/cast", "spell/usage", "spell/miscast"];

        foreach (Locale locale in MausritterLocales.All)
        {
            DiceText dice = Dice(locale.Code);

            foreach (string key in counted)
            {
                Assert.Contains(
                    "{count}",
                    dice.Readings[key],
                    StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void AnUncountedReadingDoesNotPretendToHaveASlot()
    {
        // The other half of the rule. These two report a number that is already printed beside the
        // dice, so a slot here would say it twice — and an unfilled {count} would be worse still.
        foreach (Locale locale in MausritterLocales.All)
        {
            DiceText dice = Dice(locale.Code);

            foreach (string key in new[] { "attack/damage", "mouse/attribute" })
            {
                Assert.DoesNotContain("{count}", dice.Readings[key], StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void NoReadingLeavesAnUnfilledSlotBehind()
    {
        // A slot the renderer does not know about survives formatting and reaches the screen with
        // its braces still on, which is the one failure mode a spelling mistake here produces.
        foreach (Locale locale in MausritterLocales.All)
        {
            DiceText dice = Dice(locale.Code);

            foreach ((string key, string said) in dice.Readings)
            {
                string filled = TextTemplate.Format(said, ("count", "7"));

                Assert.False(
                    filled.Contains('{', StringComparison.Ordinal),
                    $"The {locale.Code} reading '{key}' still reads '{filled}' after formatting.");
            }
        }
    }

    [Fact]
    public void TheCodeAndNotationSamplesAreNotTranslated()
    {
        // Both are examples of a syntax, not sentences. A translated table code would advertise
        // letters the alphabet does not contain, and a translated notation would not parse.
        foreach (Locale locale in MausritterLocales.All)
        {
            DiceText dice = Dice(locale.Code);

            Assert.Equal(Dice(MausritterLocales.English.Code).TablePlaceholder, dice.TablePlaceholder);

            Assert.True(
                DiceNotation.TryParse(dice.NotationPlaceholder, out _),
                $"The {locale.EnglishName} notation example '{dice.NotationPlaceholder}' does not parse.");
        }
    }

    [Theory]
    [MemberData(nameof(AllLocales))]
    public void TheNavigationAndLandingPageMentionTheTool(string code)
    {
        UiText text = TestData.In(MausritterLocales.FromCode(code)).Text;

        Assert.NotEqual("", text.Nav.Dice);
        Assert.Contains("href=\"dice\"", text.Home.DiceItemHtml, StringComparison.Ordinal);
        Assert.NotEqual("", text.About.DiceTable.Heading);
        Assert.NotEmpty(text.About.DiceTable.ParagraphsHtml);
    }

    private static DiceText Dice(string code) =>
        TestData.In(MausritterLocales.FromCode(code)).Text.Dice;

    private static void AssertSaid(
        IReadOnlyDictionary<string, string> wording, string key, string what, string code)
    {
        Assert.True(
            wording.TryGetValue(key, out string? said) && said.Length > 0,
            $"The {code} wording does not say {what}. It would render as an empty space.");
    }
}
