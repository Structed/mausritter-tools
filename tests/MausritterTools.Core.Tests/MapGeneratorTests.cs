using MausritterTools.Core.Generation;
using MausritterTools.Core.Mapping;
using MausritterTools.Core.Model;
using MausritterTools.Core.Rendering;

namespace MausritterTools.Core.Tests;

public class MapGeneratorTests
{
    private static Settlement Settle(uint seed, int? size = null, bool nearHumanTown = true) =>
        new SettlementGenerator(TestData.Game).Generate(new GenerationOptions
        {
            Seed = seed,
            Size = size,
            NearHumanTown = nearHumanTown
        });

    private static SettlementMap Map(uint seed, int? size = null) =>
        MapGenerator.Generate(Settle(seed, size), seed);

    [Fact]
    public void SameSeedProducesAnIdenticalMap()
    {
        // A map that drifted between renders would make a shared settlement link a lie.
        SettlementMap first = Map(4242, 5);
        SettlementMap second = Map(4242, 5);

        Assert.Equal(first.Roads.Count, second.Roads.Count);
        Assert.Equal(first.Buildings.Count, second.Buildings.Count);
        Assert.Equal(
            first.Roads.Select(r => (r.From, r.To, r.Depth)),
            second.Roads.Select(r => (r.From, r.To, r.Depth)));
        Assert.Equal(
            first.Buildings.Select(b => (b.Centre, b.Width, b.Depth)),
            second.Buildings.Select(b => (b.Centre, b.Width, b.Depth)));
        Assert.Equal(first.Boundary.Points, second.Boundary.Points);
    }

    [Fact]
    public void DifferentSeedsProduceDifferentMaps()
    {
        int[] roadCounts = [.. Enumerable.Range(1, 25).Select(i => Map((uint)i * 37, 5).Roads.Count)];

        Assert.True(roadCounts.Distinct().Count() > 3, "Maps should vary between seeds.");
    }

    [Fact]
    public void EverySizeProducesAUsableMap()
    {
        for (int size = 1; size <= 6; size++)
        {
            for (uint seed = 1; seed <= 25; seed++)
            {
                SettlementMap map = Map(seed, size);

                Assert.NotNull(map.Boundary);
                Assert.True(map.Boundary.Points.Count >= 3);
                Assert.True(map.Width > 0 && map.Height > 0);
                Assert.NotEmpty(map.Roads);
            }
        }
    }

    [Fact]
    public void BiggerSettlementsGetMoreRoadsAndBuildings()
    {
        double AverageRoads(int size) => Enumerable.Range(1, 20).Average(i => Map((uint)i, size).Roads.Count);
        double AverageBuildings(int size) => Enumerable.Range(1, 20).Average(i => Map((uint)i, size).Buildings.Count);

        Assert.True(AverageRoads(6) > AverageRoads(3));
        Assert.True(AverageRoads(3) > AverageRoads(1));
        Assert.True(AverageBuildings(6) > AverageBuildings(3));
        Assert.True(AverageBuildings(3) > AverageBuildings(1));
    }

    [Fact]
    public void EverythingStaysInsideTheHostObject()
    {
        // The settlement occupies the host; a building outside it would break the whole conceit.
        for (int size = 1; size <= 6; size++)
        {
            for (uint seed = 1; seed <= 20; seed++)
            {
                SettlementMap map = Map(seed, size);

                foreach (RoadSegment road in map.Roads)
                {
                    Assert.True(map.Boundary.Contains(road.From), $"road start outside host (seed {seed}, size {size})");
                    Assert.True(map.Boundary.Contains(road.To), $"road end outside host (seed {seed}, size {size})");
                }

                foreach (MapBuilding building in map.Buildings)
                {
                    Assert.True(
                        map.Boundary.Contains(building.Centre),
                        $"building outside host (seed {seed}, size {size})");
                }
            }
        }
    }

    [Fact]
    public void MapStaysWithinItsCanvas()
    {
        for (uint seed = 1; seed <= 30; seed++)
        {
            SettlementMap map = Map(seed, 6);
            MapBounds bounds = map.Boundary.Bounds;

            Assert.InRange(bounds.MinX, 0, map.Width);
            Assert.InRange(bounds.MinY, 0, map.Height);
            Assert.InRange(bounds.MaxX, 0, map.Width);
            Assert.InRange(bounds.MaxY, 0, map.Height);
        }
    }

    [Fact]
    public void MapKeepsAMarginSoTheInkedOutlineIsNotClipped()
    {
        // The outline is drawn with a wobble, so a host flush against the viewBox would lose
        // strokes at the edge.
        for (int size = 1; size <= 6; size++)
        {
            for (uint seed = 1; seed <= 20; seed++)
            {
                SettlementMap map = Map(seed, size);
                MapBounds bounds = map.Boundary.Bounds;

                Assert.True(bounds.MinX >= 8, $"host too close to the left edge (seed {seed}, size {size})");
                Assert.True(bounds.MinY >= 8, $"host too close to the top edge (seed {seed}, size {size})");
                Assert.True(bounds.MaxX <= map.Width - 8, $"host too close to the right edge (seed {seed}, size {size})");
                Assert.True(bounds.MaxY <= map.Height - 8, $"host too close to the bottom edge (seed {seed}, size {size})");
            }
        }
    }

    [Fact]
    public void HostFillsAReasonableShareOfTheCanvas()
    {
        // Fitting must not shrink a host so far that the map becomes a small island of ink.
        for (uint seed = 1; seed <= 30; seed++)
        {
            SettlementMap map = Map(seed, 5);
            MapBounds bounds = map.Boundary.Bounds;

            Assert.True(
                bounds.Width >= map.Width * 0.55,
                $"host only spans {bounds.Width:F0} of {map.Width} on seed {seed}");
        }
    }

    [Fact]
    public void BuildingsDoNotOverlapEachOther()
    {
        for (uint seed = 1; seed <= 25; seed++)
        {
            IReadOnlyList<MapBuilding> buildings = Map(seed, 6).Buildings;

            for (int i = 0; i < buildings.Count; i++)
            {
                for (int j = i + 1; j < buildings.Count; j++)
                {
                    double gap = buildings[i].Centre.DistanceTo(buildings[j].Centre);
                    double minimum = buildings[i].Radius + buildings[j].Radius;

                    Assert.True(gap >= minimum, $"buildings {i} and {j} overlap on seed {seed}");
                }
            }
        }
    }

    [Fact]
    public void BuildingsDoNotSitOnRoads()
    {
        for (uint seed = 1; seed <= 20; seed++)
        {
            SettlementMap map = Map(seed, 5);

            foreach (MapBuilding building in map.Buildings)
            {
                foreach (RoadSegment road in map.Roads)
                {
                    double distance = MapPolygon.DistanceToSegment(building.Centre, road.From, road.To);

                    Assert.True(
                        distance >= road.Width / 2,
                        $"building sits on a road on seed {seed}");
                }
            }
        }
    }

    [Fact]
    public void RoadsDoNotCrossOneAnother()
    {
        // Roads may meet at junctions, but a crossing partway along reads as a mistake.
        for (uint seed = 1; seed <= 25; seed++)
        {
            IReadOnlyList<RoadSegment> roads = Map(seed, 6).Roads;

            for (int i = 0; i < roads.Count; i++)
            {
                for (int j = i + 1; j < roads.Count; j++)
                {
                    Assert.False(
                        MapPolygon.SegmentsCross(roads[i].From, roads[i].To, roads[j].From, roads[j].To),
                        $"roads {i} and {j} cross on seed {seed}");
                }
            }
        }
    }

    [Fact]
    public void RoadNetworkIsConnected()
    {
        // Every road must be reachable from the first one, or part of the settlement is stranded.
        for (uint seed = 1; seed <= 20; seed++)
        {
            IReadOnlyList<RoadSegment> roads = Map(seed, 5).Roads;
            if (roads.Count < 2)
            {
                continue;
            }

            HashSet<int> reached = [0];
            Queue<int> pending = new([0]);

            while (pending.Count > 0)
            {
                int current = pending.Dequeue();

                for (int i = 0; i < roads.Count; i++)
                {
                    if (reached.Contains(i) || !SharesEndpoint(roads[current], roads[i]))
                    {
                        continue;
                    }

                    reached.Add(i);
                    pending.Enqueue(i);
                }
            }

            Assert.Equal(roads.Count, reached.Count);
        }
    }

    private static bool SharesEndpoint(RoadSegment a, RoadSegment b)
    {
        const double epsilon = 0.5;

        return a.From.DistanceTo(b.From) < epsilon || a.From.DistanceTo(b.To) < epsilon ||
               a.To.DistanceTo(b.From) < epsilon || a.To.DistanceTo(b.To) < epsilon;
    }

    [Fact]
    public void ShopsAndTavernAreKeyedToBuildings()
    {
        Settlement settlement = Settle(31337, 6);
        SettlementMap map = MapGenerator.Generate(settlement, 31337);

        int expected = settlement.Shops.Count + (settlement.Tavern is null ? 0 : 1);
        int keyed = map.Buildings.Count(b => b.IsKeyed);

        Assert.True(keyed > 0, "Nothing was keyed to the map.");
        Assert.Equal(keyed, map.Legend.Count);
        Assert.Equal(Math.Min(expected, map.Buildings.Count), keyed);
    }

    [Fact]
    public void LegendNumbersAreContiguousAndMatchTheMap()
    {
        for (uint seed = 1; seed <= 20; seed++)
        {
            Settlement settlement = Settle(seed, 6);
            SettlementMap map = MapGenerator.Generate(settlement, seed);

            int[] legendKeys = [.. map.Legend.Select(l => l.Key).Order()];
            int[] buildingKeys = [.. map.Buildings.Where(b => b.IsKeyed).Select(b => b.Key!.Value).Order()];

            Assert.Equal(legendKeys, buildingKeys);
            Assert.Equal(Enumerable.Range(1, legendKeys.Length), legendKeys);

            foreach (MapLegendEntry entry in map.Legend)
            {
                Assert.False(string.IsNullOrWhiteSpace(entry.Name));
                Assert.False(string.IsNullOrWhiteSpace(entry.Detail));
            }
        }
    }

    [Fact]
    public void KeyedBuildingsCarryTheirName()
    {
        SettlementMap map = Map(999, 6);

        foreach (MapBuilding building in map.Buildings.Where(b => b.IsKeyed))
        {
            Assert.False(string.IsNullOrWhiteSpace(building.Label));
        }
    }

    [Fact]
    public void WaterAppearsWhenTheSettlementImpliesIt()
    {
        // Fishermice, a water-wheel mill or a riverboat dock should put water on the map.
        bool sawWater = false;
        bool sawDryLand = false;

        for (uint seed = 1; seed <= 120; seed++)
        {
            Settlement settlement = Settle(seed, 5);
            SettlementMap map = MapGenerator.Generate(settlement, seed);

            bool implied = settlement.Industries
                .Concat(settlement.NotableFeatures)
                .Any(t =>
                    t.Contains("fishermice", StringComparison.OrdinalIgnoreCase) ||
                    t.Contains("water-wheel", StringComparison.OrdinalIgnoreCase) ||
                    t.Contains("riverboat", StringComparison.OrdinalIgnoreCase));

            if (implied)
            {
                Assert.NotNull(map.Water);
                sawWater = true;
            }
            else if (map.Water is null)
            {
                sawDryLand = true;
            }
        }

        Assert.True(sawWater, "Never generated a settlement whose trade implies water.");
        Assert.True(sawDryLand, "Every settlement had water, which cannot be right.");
    }

    [Fact]
    public void ScatterAvoidsRoadsAndBuildings()
    {
        for (uint seed = 1; seed <= 15; seed++)
        {
            SettlementMap map = Map(seed, 5);

            foreach (MapScatter item in map.Scatter)
            {
                Assert.True(map.Boundary.Contains(item.Position));

                foreach (RoadSegment road in map.Roads)
                {
                    Assert.True(MapPolygon.DistanceToSegment(item.Position, road.From, road.To) >= 10);
                }

                foreach (MapBuilding building in map.Buildings)
                {
                    Assert.True(item.Position.DistanceTo(building.Centre) >= building.Radius);
                }
            }
        }
    }

    [Fact]
    public void HostShapeIsCarriedThrough()
    {
        Settlement settlement = Settle(77, 4);
        SettlementMap map = MapGenerator.Generate(settlement, 77);

        Assert.Equal(settlement.Host.Shape, map.Shape);
        Assert.Equal(settlement.Host.Name, map.HostName);
    }

    [Theory]
    [InlineData("linear")]
    [InlineData("hollow")]
    [InlineData("boxy")]
    [InlineData("vessel")]
    [InlineData("warren")]
    [InlineData("sprawl")]
    public void NoHostShapeStarvesALargeSettlement(string shape)
    {
        // A city drawn inside a farmhouse wall once came out smaller than a hamlet in a tree
        // stump, because the narrow archetype rejected most of the growth. Narrow hosts now open
        // up as the settlement grows.
        double AverageBuildings(int size)
        {
            List<int> counts = [];

            for (uint seed = 1; seed <= 400 && counts.Count < 10; seed++)
            {
                Settlement settlement = Settle(seed, size);
                if (settlement.Host.Shape != shape)
                {
                    continue;
                }

                counts.Add(MapGenerator.Generate(settlement, seed).Buildings.Count);
            }

            Assert.NotEmpty(counts);
            return counts.Average();
        }

        double hamlet = AverageBuildings(3);
        double city = AverageBuildings(6);

        Assert.True(
            city > hamlet * 1.5,
            $"A city in a '{shape}' host averaged {city:F1} buildings against a hamlet's {hamlet:F1}.");
    }

    [Fact]
    public void EveryHostShapeSupportsAReadableCity()
    {
        // Guards the weakest archetype in absolute terms, not just relative to a hamlet.
        Dictionary<string, List<int>> byShape = [];

        for (uint seed = 1; seed <= 300; seed++)
        {
            Settlement settlement = Settle(seed, 6);
            SettlementMap map = MapGenerator.Generate(settlement, seed);

            if (!byShape.TryGetValue(map.Shape, out List<int>? counts))
            {
                counts = [];
                byShape[map.Shape] = counts;
            }

            counts.Add(map.Buildings.Count);
        }

        Assert.NotEmpty(byShape);

        foreach ((string shape, List<int> counts) in byShape)
        {
            Assert.True(
                counts.Average() >= 20,
                $"Cities in a '{shape}' host averaged only {counts.Average():F1} buildings.");
        }
    }
}
