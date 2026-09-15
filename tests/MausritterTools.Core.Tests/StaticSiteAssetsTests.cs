using System.Buffers.Binary;
using System.Xml.Linq;
using MausritterTools.Core.Data;

namespace MausritterTools.Core.Tests;

public class StaticSiteAssetsTests
{
    private const string SiteUrl = "https://structed.github.io/mausritter-tools/";
    private const string CardUrl = SiteUrl + "images/open-graph.png";
    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

    private static string WebRoot => Path.GetFullPath(Path.Combine(TestData.DataRoot, ".."));

    public static TheoryData<string> SvgAssets =>
        ["favicon.svg", Path.Combine("images", "open-graph.svg")];

    [Fact]
    public void InitialHtmlContainsTheSharedEnglishSocialPreview()
    {
        XElement head = ReadHead();
        AppText copy = TestData.Game.Text.App;

        Assert.Equal(copy.Title, Assert.Single(head.Elements("title")).Value);
        Assert.Equal(copy.Description, Meta(head, "name", "description"));

        Dictionary<string, string> openGraph = new()
        {
            ["og:type"] = "website",
            ["og:site_name"] = copy.Title,
            ["og:title"] = copy.Title,
            ["og:description"] = copy.Description,
            ["og:url"] = SiteUrl,
            ["og:image"] = CardUrl,
            ["og:image:type"] = "image/png",
            ["og:image:width"] = "1200",
            ["og:image:height"] = "630",
            ["og:image:alt"] = copy.SocialImageAlt
        };
        foreach ((string key, string value) in openGraph)
        {
            Assert.Equal(value, Meta(head, "property", key));
        }

        Dictionary<string, string> twitter = new()
        {
            ["twitter:card"] = "summary_large_image",
            ["twitter:title"] = copy.Title,
            ["twitter:description"] = copy.Description,
            ["twitter:image"] = CardUrl,
            ["twitter:image:alt"] = copy.SocialImageAlt
        };
        foreach ((string key, string value) in twitter)
        {
            Assert.Equal(value, Meta(head, "name", key));
        }
    }

    [Fact]
    public void FaviconLinksWorkWithTheDevelopmentAndProjectBaseHrefs()
    {
        XElement head = ReadHead();
        XElement[] links = [.. head.Elements("link").Where(link => (string?)link.Attribute("rel") == "icon")];

        Assert.Equal(2, links.Length);
        Assert.Equal("favicon.png", (string?)links[0].Attribute("href"));
        Assert.Equal("image/png", (string?)links[0].Attribute("type"));
        Assert.Equal("32x32", (string?)links[0].Attribute("sizes"));
        Assert.Equal("favicon.svg", (string?)links[1].Attribute("href"));
        Assert.Equal("image/svg+xml", (string?)links[1].Attribute("type"));
        Assert.Equal("any", (string?)links[1].Attribute("sizes"));

        foreach (string baseUrl in new[] { "http://localhost:5023/", SiteUrl })
        {
            Uri baseUri = new(baseUrl);
            foreach (XElement link in links)
            {
                string href = Assert.IsType<XAttribute>(link.Attribute("href")).Value;
                Assert.Equal(baseUrl + href, new Uri(baseUri, href).AbsoluteUri);
                Assert.True(File.Exists(Path.Combine(WebRoot, href)));
            }
        }
    }

    [Fact]
    public void RasterAssetsHaveTheAdvertisedDimensions()
    {
        AssertPng("favicon.png", 32, 32);
        AssertPng(Path.Combine("images", "open-graph.png"), 1200, 630);
    }

    [Fact]
    public void ArtworkUsesCanonicalCopyAndOneSharedMouseEmblem()
    {
        XElement card = ReadSvg(Path.Combine("images", "open-graph.svg"));
        XElement favicon = ReadSvg("favicon.svg");
        AppText copy = TestData.Game.Text.App;

        Assert.Equal("0 0 1200 630", (string?)card.Attribute("viewBox"));
        Assert.Equal("0 0 64 64", (string?)favicon.Attribute("viewBox"));
        Assert.Equal(copy.Title, ElementById(card, "card-accessible-title").Value);
        Assert.Equal(copy.SocialImageAlt, ElementById(card, "card-alt").Value);
        Assert.Equal(copy.Title, ElementById(favicon, "favicon-title").Value);
        Assert.Equal(copy.Title, WrappedText(card, "card-title"));
        Assert.Equal(copy.Description, WrappedText(card, "card-description"));
        Assert.True(XNode.DeepEquals(
            new XElement(Svg + "g", ElementById(card, "site-mark").Elements()),
            new XElement(Svg + "g", ElementById(favicon, "site-mark").Elements())));
    }

    [Theory]
    [MemberData(nameof(SvgAssets))]
    public void ArtworkDoesNotLoadExternalImagesOrResources(string path)
    {
        XElement svg = ReadSvg(path);
        foreach (XAttribute reference in svg.DescendantsAndSelf().Attributes()
                     .Where(attribute => attribute.Name.LocalName == "href"))
        {
            Assert.StartsWith("#", reference.Value);
        }
        Assert.Empty(svg.Descendants(Svg + "script"));
        Assert.Empty(svg.Descendants(Svg + "foreignObject"));
    }

    [Theory]
    [MemberData(nameof(TranslationTests.TranslatedLocales), MemberType = typeof(TranslationTests))]
    public void SocialImageAltTextLoadsFromEachTranslation(string code)
    {
        AppText translated = TestData.In(MausritterLocales.FromCode(code)).Text.App;

        Assert.NotEmpty(translated.SocialImageAlt.Trim());
        Assert.NotEqual(TestData.Game.Text.App.SocialImageAlt, translated.SocialImageAlt);
    }

    private static XElement ReadHead() =>
        Assert.Single(XDocument.Load(Path.Combine(WebRoot, "index.html")).Descendants("head"));

    private static XElement ReadSvg(string path) =>
        Assert.Single(XDocument.Load(Path.Combine(WebRoot, path)).Elements(Svg + "svg"));

    private static XElement ElementById(XElement svg, string id) =>
        Assert.Single(svg.Descendants(), element => (string?)element.Attribute("id") == id);

    private static string WrappedText(XElement svg, string id) =>
        string.Join(" ", ElementById(svg, id).Elements(Svg + "tspan").Select(span => span.Value));

    private static string Meta(XElement head, string attribute, string key)
    {
        XElement meta = Assert.Single(head.Elements("meta"),
            element => (string?)element.Attribute(attribute) == key);
        return Assert.IsType<XAttribute>(meta.Attribute("content")).Value;
    }

    private static void AssertPng(string path, int width, int height)
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine(WebRoot, path));

        Assert.InRange(bytes.Length, 24, 5 * 1024 * 1024);
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, bytes[..8]);
        Assert.Equal("IHDR"u8.ToArray(), bytes[12..16]);
        Assert.Equal(width, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)));
        Assert.Equal(height, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4)));
    }
}
