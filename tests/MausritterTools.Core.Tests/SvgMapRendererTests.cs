using System.Xml.Linq;
using MausritterTools.Core.Generation;
using MausritterTools.Core.Mapping;
using MausritterTools.Core.Model;
using MausritterTools.Core.Rendering;

namespace MausritterTools.Core.Tests;

public class SvgMapRendererTests
{
    private static (Settlement Settlement, SettlementMap Map, string Svg) Render(uint seed, int? size = 5)
    {
        Settlement settlement = new SettlementGenerator(TestData.Game).Generate(new GenerationOptions
        {
            Seed = seed,
            Size = size,
            NearHumanTown = true
        });

        SettlementMap map = MapGenerator.Generate(settlement, seed);

        return (settlement, map, SvgMapRenderer.Render(map, seed));
    }

    [Fact]
    public void OutputIsWellFormedXml()
    {
        for (uint seed = 1; seed <= 30; seed++)
        {
            (_, _, string svg) = Render(seed, 6);

            // Parsing is the real check: a malformed path or an unescaped name would throw here.
            XDocument document = XDocument.Parse(svg);

            Assert.Equal("svg", document.Root!.Name.LocalName);
        }
    }

    [Fact]
    public void OutputCarriesAViewBoxSoItScales()
    {
        (_, SettlementMap map, string svg) = Render(1234);
        XDocument document = XDocument.Parse(svg);

        string viewBox = document.Root!.Attribute("viewBox")!.Value;

        Assert.Equal($"0 0 {map.Width} {map.Height}", viewBox);
    }

    [Fact]
    public void OutputIsAccessible()
    {
        (_, SettlementMap map, string svg) = Render(555);
        XDocument document = XDocument.Parse(svg);

        Assert.Equal("img", document.Root!.Attribute("role")!.Value);
        Assert.Contains(map.HostName, document.Root.Attribute("aria-label")!.Value, StringComparison.Ordinal);
    }

    /// <summary>
    /// The page wants a map that fills its frame, which a bare <c>viewBox</c> gives. A copy that
    /// leaves the page has to carry its own size instead: an image with no intrinsic dimensions is
    /// drawn at the default object size of 300×150, so the map would arrive as a thumbnail.
    /// </summary>
    [Fact]
    public void ASizeIsWrittenOnlyWhenItIsAskedFor()
    {
        (_, SettlementMap map, string onPage) = Render(2468);

        Assert.Null(XDocument.Parse(onPage).Root!.Attribute("width"));
        Assert.Null(XDocument.Parse(onPage).Root!.Attribute("height"));

        XElement standalone =
            XDocument.Parse(SvgMapRenderer.Render(map, 2468, intrinsicSize: true)).Root!;

        Assert.Equal(map.Width.ToString(System.Globalization.CultureInfo.InvariantCulture),
            standalone.Attribute("width")!.Value);
        Assert.Equal(map.Height.ToString(System.Globalization.CultureInfo.InvariantCulture),
            standalone.Attribute("height")!.Value);

        // The viewBox is unchanged, so the drawing inside is identical either way.
        Assert.Equal(
            XDocument.Parse(onPage).Root!.Attribute("viewBox")!.Value,
            standalone.Attribute("viewBox")!.Value);
    }

    [Fact]
    public void EveryRoadAndBuildingIsDrawn()
    {
        (_, SettlementMap map, string svg) = Render(4321, 6);
        XDocument document = XDocument.Parse(svg);

        XNamespace svgNs = "http://www.w3.org/2000/svg";

        int roadPaths = document.Descendants(svgNs + "g")
            .Where(g => (string?)g.Attribute("class") == "map-roads")
            .Sum(g => g.Elements(svgNs + "path").Count());

        // Each road is stroked twice: a dark casing and a lighter fill.
        Assert.Equal(map.Roads.Count * 2, roadPaths);

        int buildingPaths = document.Descendants(svgNs + "g")
            .Where(g => (string?)g.Attribute("class") == "map-buildings")
            .Sum(g => g.Elements(svgNs + "path").Count());

        // Each building is an outline plus a roof ridge.
        Assert.Equal(map.Buildings.Count * 2, buildingPaths);
    }

    [Fact]
    public void KeyedBuildingsGetNumberedMarkers()
    {
        (_, SettlementMap map, string svg) = Render(31337, 6);
        XDocument document = XDocument.Parse(svg);

        XNamespace svgNs = "http://www.w3.org/2000/svg";

        string[] labels =
        [
            .. document.Descendants(svgNs + "g")
                .Where(g => (string?)g.Attribute("class") == "map-keys")
                .SelectMany(g => g.Elements(svgNs + "text"))
                .Select(t => t.Value)
        ];

        int keyed = map.Buildings.Count(b => b.IsKeyed);

        Assert.Equal(keyed, labels.Length);
        Assert.Equal(
            Enumerable.Range(1, keyed).Select(i => i.ToString()).Order(),
            labels.Order());
    }

    [Fact]
    public void ShopNamesAreEscapedRatherThanInjected()
    {
        // Shop signs are generated, but a hand-edited name could contain markup.
        Settlement settlement = new SettlementGenerator(TestData.Game).Generate(
            new GenerationOptions { Seed = 8, Size = 4 }
                .WithPin("tavern/name", "The <script>alert(1)</script> & Bell"));

        SettlementMap map = MapGenerator.Generate(settlement, 8);
        string svg = SvgMapRenderer.Render(map, 8);

        XDocument document = XDocument.Parse(svg);

        Assert.DoesNotContain("<script>", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            document.Descendants().Select(e => e.Value),
            value => value.Contains("alert(1)", StringComparison.Ordinal));
    }

    [Fact]
    public void PathsUseInvariantNumberFormatting()
    {
        // A comma decimal separator under a European culture would corrupt every path.
        (_, _, string svg) = Render(2024, 6);

        Assert.DoesNotContain(",,", svg, StringComparison.Ordinal);
        Assert.Matches(@"d=""M \d", svg);
    }

    [Fact]
    public void WaterIsDrawnWhenPresent()
    {
        for (uint seed = 1; seed <= 120; seed++)
        {
            (_, SettlementMap map, string svg) = Render(seed);

            if (map.Water is not null)
            {
                Assert.Contains("map-water", svg, StringComparison.Ordinal);
                return;
            }
        }

        Assert.Fail("Never generated a map with water.");
    }

    [Fact]
    public void RenderingIsDeterministic()
    {
        (_, SettlementMap map, string first) = Render(6161, 6);

        Assert.Equal(first, SvgMapRenderer.Render(map, 6161));
    }

    [Fact]
    public void OutputStaysAReasonableSize()
    {
        // A city map that ran to megabytes would make the page and the print view unusable.
        (_, _, string svg) = Render(11, 6);

        Assert.InRange(svg.Length, 2_000, 400_000);
    }
}
