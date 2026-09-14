using System.Xml.Linq;
using MausritterTools.Core.Data;
using MausritterTools.Core.Generation;
using MausritterTools.Core.Mapping;
using MausritterTools.Core.Model;
using MausritterTools.Core.Rendering;
using Structed.Inkwell.Mapping;

namespace MausritterTools.Core.Tests;

/// <summary>
/// Covers how a settlement's map introduces itself.
/// </summary>
/// <remarks>
/// The SVG itself is the engine's and is tested there. What is left here is the wording and the
/// stylesheet hook, which are this site's.
/// </remarks>
public class SettlementMapRendererTests
{
    private static (Settlement Settlement, PlaceMap Map) Draw(uint seed, int? size = 5)
    {
        Settlement settlement = new SettlementGenerator(TestData.Game).Generate(new GenerationOptions
        {
            Seed = seed,
            Size = size,
            NearHumanTown = true
        });

        return (settlement, SettlementMapper.Generate(settlement, seed, TestData.Grammar));
    }

    [Fact]
    public void TheMapCarriesTheClassTheStylesheetSizesItWith()
    {
        (_, PlaceMap map) = Draw(1234);

        XElement root = XDocument.Parse(SettlementMapRenderer.Render(map, 1234)).Root!;

        Assert.Equal("settlement-map", root.Attribute("class")!.Value);
    }

    [Fact]
    public void TheAccessibleLabelNamesTheHostObject()
    {
        (_, PlaceMap map) = Draw(555);

        string label = XDocument
            .Parse(SettlementMapRenderer.Render(map, 555, TestData.Game.Text.Settlement.Map.AriaLabel))
            .Root!
            .Attribute("aria-label")!
            .Value;

        Assert.Contains(map.Subject, label, StringComparison.Ordinal);
        Assert.DoesNotContain("{host}", label, StringComparison.Ordinal);
    }

    [Fact]
    public void TheAccessibleLabelFollowsTheLanguage()
    {
        (_, PlaceMap map) = Draw(555);
        GameData german = TestData.In(MausritterLocales.German);

        string label = XDocument
            .Parse(SettlementMapRenderer.Render(map, 555, german.Text.Settlement.Map.AriaLabel))
            .Root!
            .Attribute("aria-label")!
            .Value;

        Assert.StartsWith("Karte der Siedlung", label, StringComparison.Ordinal);
        Assert.Contains(map.Subject, label, StringComparison.Ordinal);
    }

    [Fact]
    public void ALabelIsStillWrittenWhenNoLanguageIsLoaded()
    {
        (_, PlaceMap map) = Draw(555);

        string label = XDocument
            .Parse(SettlementMapRenderer.Render(map, 555))
            .Root!
            .Attribute("aria-label")!
            .Value;

        Assert.Contains(map.Subject, label, StringComparison.Ordinal);
        Assert.DoesNotContain("{host}", label, StringComparison.Ordinal);
    }

    [Fact]
    public void ShopNamesAreEscapedRatherThanInjected()
    {
        // Shop signs are generated, but a hand-edited name could contain markup.
        Settlement settlement = new SettlementGenerator(TestData.Game).Generate(
            new GenerationOptions { Seed = 8, Size = 4 }
                .WithPin("tavern/name", "The <script>alert(1)</script> & Bell"));

        string svg = SettlementMapRenderer.Render(SettlementMapper.Generate(settlement, 8), 8);

        Assert.DoesNotContain("<script>", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            XDocument.Parse(svg).Descendants().Select(e => e.Value),
            value => value.Contains("alert(1)", StringComparison.Ordinal));
    }
}
