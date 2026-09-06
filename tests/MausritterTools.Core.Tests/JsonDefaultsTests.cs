using System.Text.Json;
using MausritterTools.Core.Data;

namespace MausritterTools.Core.Tests;

/// <summary>
/// Guards against a subtle System.Text.Json trap.
/// </summary>
/// <remarks>
/// The source generator, which the app must use because Blazor WebAssembly trims on publish,
/// <em>discards property initialisers</em>. A property absent from a JSON file therefore arrives as
/// null no matter what <c>= ""</c> or <c>= []</c> default is written on it, and the first optional
/// property anyone adds to a data file turns into a NullReferenceException deep inside generation.
/// The models defend against this with null-coercing getters; these tests make sure that defence
/// stays in place.
/// </remarks>
public class JsonDefaultsTests
{
    [Fact]
    public void AbsentCollectionsBecomeEmptyRatherThanNull()
    {
        StockProfile profile = Deserialize("{\"minItems\":1}", GameDataJsonContext.Default.StockProfile);

        Assert.Equal(1, profile.MinItems);
        Assert.NotNull(profile.Categories);
        Assert.Empty(profile.Categories);
        Assert.NotNull(profile.ItemNames);
        Assert.Empty(profile.ItemNames);
    }

    [Fact]
    public void AbsentStringsBecomeEmptyRatherThanNull()
    {
        ServiceDefinition service = Deserialize("{\"minSize\":2}", GameDataJsonContext.Default.ServiceDefinition);

        Assert.Equal("", service.Id);
        Assert.Equal("", service.Name);
        Assert.Equal("", service.KeeperTitle);
        Assert.Equal("", service.SrdBasis);
        Assert.Equal("", service.Blurb);
    }

    [Fact]
    public void AbsentNestedRecordsAreConstructedRatherThanNull()
    {
        ServiceDefinition service = Deserialize("{\"id\":\"x\"}", GameDataJsonContext.Default.ServiceDefinition);

        Assert.NotNull(service.Stock);
        Assert.Empty(service.Stock.Categories);
        Assert.Empty(service.SignNouns);
    }

    [Fact]
    public void StringsWithNonEmptyDefaultsKeepThoseDefaults()
    {
        HostTables hosts = Deserialize("{\"hosts\":[{\"id\":\"x\",\"name\":\"A boot\"}]}",
            GameDataJsonContext.Default.HostTables);

        HostObject host = Assert.Single(hosts.Hosts);

        Assert.Equal("in", host.Preposition);
        Assert.Equal("hollow", host.Shape);
        Assert.Empty(host.Terrain);
    }

    [Fact]
    public void AnEmptyDocumentProducesUsableEmptyTables()
    {
        // The safety net: even a completely empty file must not produce nulls that blow up later.
        SettlementTables settlement = Deserialize("{}", GameDataJsonContext.Default.SettlementTables);

        Assert.Empty(settlement.Sizes);
        Assert.Empty(settlement.Governance);
        Assert.Empty(settlement.Inhabitants);
        Assert.NotNull(settlement.NameSeeds);
        Assert.Empty(settlement.NameSeeds.StartA);
        Assert.NotNull(settlement.Taverns);
        Assert.NotNull(settlement.Source);
        Assert.Equal("", settlement.Source.Describes);
    }

    [Fact]
    public void ExplicitNullsAreAlsoCoerced()
    {
        // A hand-edited data file may write null rather than omitting the property.
        StockProfile profile = Deserialize(
            "{\"categories\":null,\"itemNames\":null}", GameDataJsonContext.Default.StockProfile);

        Assert.Empty(profile.Categories);
        Assert.Empty(profile.ItemNames);
    }

    [Fact]
    public void AbsentDictionariesBecomeEmptyRatherThanNull()
    {
        HostTables hosts = Deserialize("{}", GameDataJsonContext.Default.HostTables);

        Assert.NotNull(hosts.Shapes);
        Assert.Empty(hosts.Shapes);
    }

    private static T Deserialize<T>(string json, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
    {
        T? value = JsonSerializer.Deserialize(json, typeInfo);
        Assert.NotNull(value);
        return value!;
    }
}
