using MausritterTools.Core.Mapping;
using Structed.Inkwell.Data;
using Structed.Inkwell.Mapping;
using Structed.Inkwell.Rendering;
namespace MausritterTools.Core.Rendering;

/// <summary>
/// Renders a settlement's map.
/// </summary>
/// <remarks>
/// The drawing itself is the engine's. What is decided here is how the map introduces itself: the
/// sentence a screen reader hears, and the class the site's stylesheet hooks onto.
/// </remarks>
public static class SettlementMapRenderer
{
    /// <summary>Used when no language has been loaded, which in practice means a test.</summary>
    private const string DefaultAriaLabelPattern = "Map of the settlement, {host}";

    /// <summary>The class <c>settlement.css</c> sizes the map with.</summary>
    private const string CssClass = "settlement-map";

    /// <summary>Renders the map.</summary>
    /// <param name="map">The settlement's map.</param>
    /// <param name="seed">Seeds the pen's wobble, so a redraw is reproducible.</param>
    /// <param name="ariaLabelPattern">
    /// The accessible label, with a <c>{host}</c> placeholder. Optional so that rendering tests
    /// need not load a language.
    /// </param>
    /// <param name="intrinsicSize">
    /// Writes the map's size onto the root element as well as into the <c>viewBox</c>. Needed for
    /// any copy that leaves the page.
    /// </param>
    public static string Render(
        PlaceMap map, uint seed, string? ariaLabelPattern = null, bool intrinsicSize = false)
    {
        ArgumentNullException.ThrowIfNull(map);

        return SvgMapRenderer.Render(map, seed, new MapRenderOptions
        {
            AriaLabel = TextTemplate.Format(
                ariaLabelPattern is { Length: > 0 } pattern ? pattern : DefaultAriaLabelPattern,
                ("host", map.Subject)),
            CssClass = CssClass,
            IntrinsicSize = intrinsicSize
        });
    }
}
