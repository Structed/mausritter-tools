namespace MausritterTools.Core.Mapping;

/// <summary>A stretch of road or tunnel.</summary>
public sealed record RoadSegment(MapPoint From, MapPoint To, int Depth)
{
    /// <summary>Depth 0 is the main thoroughfare; deeper segments are progressively smaller lanes.</summary>
    public double Width => Depth switch
    {
        0 => 5.0,
        1 => 3.6,
        2 => 2.6,
        _ => 2.0
    };

    public double Length => From.DistanceTo(To);

    public MapPoint Direction => (To - From).Normalised();

    public MapPoint Midpoint => From.Lerp(To, 0.5);
}

/// <summary>A mouse dwelling, workshop or shopfront on the map.</summary>
public sealed record MapBuilding
{
    public MapPoint Centre { get; init; }

    public double Width { get; init; }

    public double Depth { get; init; }

    /// <summary>Rotation in radians. Buildings sit square to the road they face.</summary>
    public double Angle { get; init; }

    /// <summary>
    /// The depth of the road this building faces. Zero is the main thoroughfare, which is where
    /// the shops that matter tend to sit.
    /// </summary>
    public int RoadDepth { get; init; }

    /// <summary>The map key number, set only for buildings tied to a shop or the tavern.</summary>
    public int? Key { get; init; }

    /// <summary>What the key refers to, shown in the legend.</summary>
    public string? Label { get; init; }

    public bool IsKeyed => Key is not null;

    /// <summary>The four corners, in order.</summary>
    public IReadOnlyList<MapPoint> Corners()
    {
        double cos = Math.Cos(Angle);
        double sin = Math.Sin(Angle);

        double halfWidth = Width / 2;
        double halfDepth = Depth / 2;

        MapPoint Corner(double dx, double dy) => new(
            Centre.X + ((dx * cos) - (dy * sin)),
            Centre.Y + ((dx * sin) + (dy * cos)));

        return
        [
            Corner(-halfWidth, -halfDepth),
            Corner(halfWidth, -halfDepth),
            Corner(halfWidth, halfDepth),
            Corner(-halfWidth, halfDepth)
        ];
    }

    /// <summary>The radius of a circle that would enclose the building, used for spacing checks.</summary>
    public double Radius => Math.Sqrt((Width * Width) + (Depth * Depth)) / 2;
}

/// <summary>A patch of greenery, drawn as scatter to break up empty ground.</summary>
public sealed record MapScatter(MapPoint Position, double Size, ScatterKind Kind);

/// <summary>The kinds of decorative scatter the renderer knows how to draw.</summary>
public enum ScatterKind
{
    Tree,
    Shrub,
    Rock
}

/// <summary>An entry in the map legend.</summary>
public sealed record MapLegendEntry(int Key, string Name, string Detail);

/// <summary>
/// A generated settlement map.
/// </summary>
/// <remarks>
/// Laid out inside the silhouette of the host object rather than on open ground, because a
/// Mausritter settlement is a human-scale object annotated at mouse scale: an oak hollow, a
/// farmhouse wall, a cow skull, a boot.
/// </remarks>
public sealed record SettlementMap
{
    /// <summary>The outline of the host object the settlement occupies.</summary>
    public MapPolygon Boundary { get; init; } = null!;

    public string HostName { get; init; } = "";

    /// <summary>The layout archetype used, e.g. hollow, linear, vessel.</summary>
    public string Shape { get; init; } = "";

    public IReadOnlyList<RoadSegment> Roads { get; init; } = [];

    public IReadOnlyList<MapBuilding> Buildings { get; init; } = [];

    public IReadOnlyList<MapScatter> Scatter { get; init; } = [];

    /// <summary>Present when the settlement's trade or features imply water.</summary>
    public MapPolygon? Water { get; init; }

    public IReadOnlyList<MapLegendEntry> Legend { get; init; } = [];

    /// <summary>Canvas width in map units.</summary>
    public double Width { get; init; }

    /// <summary>Canvas height in map units.</summary>
    public double Height { get; init; }
}
