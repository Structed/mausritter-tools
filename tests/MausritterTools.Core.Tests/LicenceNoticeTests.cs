using System.Text.RegularExpressions;
using MausritterTools.Core.Data;
using Structed.Inkwell.Data;

namespace MausritterTools.Core.Tests;

/// <summary>
/// Guards the notices that have to be on the site itself rather than only in the repository.
/// </summary>
/// <remarks>
/// <para>
/// CC BY 4.0 and the Mausritter Third Party Licence both require their text to be visible where the
/// work is published, which makes "every page carries them" a licensing requirement rather than a
/// layout preference. A page that forgets is not a rendering bug that anybody notices.
/// </para>
/// <para>
/// So the footer is rendered once, by the layout every route is wrapped in, and these tests keep it
/// that way: a new tool cannot ship without the notices because it never had to ask for them.
/// </para>
/// </remarks>
public sealed class LicenceNoticeTests
{
    /// <summary>Every language the app ships, including the one the data files are written in.</summary>
    public static TheoryData<string> AllLocales =>
        [.. MausritterLocales.All.Select(locale => locale.Code)];

    [Fact]
    public void OnlyTheLayoutRendersTheLicenceFooter()
    {
        // A second renderer means either a duplicated footer or, far more likely, a page that has
        // gone back to deciding for itself whether the notices appear.
        string[] renderers = [.. RazorFiles()
            .Where(file => Renders.IsMatch(File.ReadAllText(file)))
            .Select(RelativeName)
            .OrderBy(name => name, StringComparer.Ordinal)];

        Assert.Equal(["Layout/MainLayout.razor"], renderers);
    }

    [Fact]
    public void EveryRoutedPageIsWrappedInThatLayout()
    {
        Assert.Matches(@"DefaultLayout=""@typeof\(MainLayout\)""", File.ReadAllText(AppShell));

        foreach (string file in RazorFiles().Where(file => Routes.IsMatch(File.ReadAllText(file))))
        {
            Match layout = Layout.Match(File.ReadAllText(file));

            // Anything else is a page the footer would not reach, whether or not it is the one the
            // router falls back to.
            Assert.True(
                !layout.Success || layout.Groups[1].Value == "MainLayout",
                $"{RelativeName(file)} routes but uses layout '{layout.Groups[1].Value}'.");
        }
    }

    [Fact]
    public void NoPageCanSwitchANoticeOff()
    {
        // The house-rule note used to be optional per page. One footer for the whole site means one
        // statement for the whole site, and no parameter to get wrong.
        string[] offenders = [.. RazorFiles()
            .Where(file => File.ReadAllText(file)
                .Contains("ShowHouseRuleNote", StringComparison.Ordinal))
            .Select(RelativeName)];

        Assert.Empty(offenders);
    }

    [Fact]
    public void TheFooterStatesEveryNotice()
    {
        string source = File.ReadAllText(Path.Combine(WebRoot, "Components", "LicenceFooter.razor"));

        foreach (string notice in new[]
                 {
                     "BasedOnHtml", "IndependentHtml", "CopyrightHtml", "HouseRuleNote",
                     // Only a translation has one, but CC BY 4.0 requires a modification to be
                     // declared and a translation is one, so the footer must still ask for it.
                     "TranslationNote"
                 })
        {
            Assert.Contains($"Text.Licence.{notice}", source, StringComparison.Ordinal);
        }
    }

    [Theory]
    [MemberData(nameof(AllLocales))]
    public void EveryLanguageSaysTheNoticesTheFooterAsksFor(string code)
    {
        Locale locale = MausritterLocales.FromCode(code);
        LicenceText licence = TestData.In(locale).Text.Licence;

        Assert.NotEmpty(licence.BasedOnHtml.Trim());
        Assert.NotEmpty(licence.IndependentHtml.Trim());
        Assert.NotEmpty(licence.CopyrightHtml.Trim());

        // Shown on every page now, so a blank one is a blank paragraph on every page.
        Assert.NotEmpty(licence.HouseRuleNote.Trim());

        // A translation must declare itself as one; the canonical text has nothing to declare.
        Assert.Equal(!locale.IsCanonical, licence.TranslationNote is { Length: > 0 });
    }

    [Fact]
    public void PrintingASettlementKeepsTheNotices()
    {
        string css = File.ReadAllText(
            Path.Combine(WebRoot, "wwwroot", "css", "settlement.css"));
        int print = css.IndexOf("@media print", StringComparison.Ordinal);

        Assert.True(print >= 0, "settlement.css no longer has a print block.");
        Assert.DoesNotContain(".site-footer", css[print..], StringComparison.Ordinal);
        Assert.DoesNotContain(
            "no-print",
            File.ReadAllText(Path.Combine(WebRoot, "Components", "LicenceFooter.razor")),
            StringComparison.Ordinal);
    }

    private static readonly Regex Renders = new(@"<LicenceFooter\b", RegexOptions.Compiled);

    private static readonly Regex Routes =
        new(@"^@page\s", RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex Layout =
        new(@"^@layout\s+(\S+)", RegexOptions.Compiled | RegexOptions.Multiline);

    private static string WebRoot { get; } =
        Path.GetFullPath(Path.Combine(TestData.DataRoot, "..", ".."));

    private static string AppShell => Path.Combine(WebRoot, "App.razor");

    private static IEnumerable<string> RazorFiles() =>
        Directory.EnumerateFiles(WebRoot, "*.razor", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                               StringComparison.Ordinal)
                && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal));

    private static string RelativeName(string file) =>
        Path.GetRelativePath(WebRoot, file).Replace('\\', '/');
}
