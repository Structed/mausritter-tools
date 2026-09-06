using System.Text.Json;
using System.Text.Json.Serialization;

namespace MausritterTools.Core.Data;

/// <summary>
/// Source-generated serialisation metadata for the data files.
/// </summary>
/// <remarks>
/// Blazor WebAssembly trims unused code on publish, which breaks reflection-based deserialisation.
/// Source generation keeps the models intact and avoids a runtime that only fails once deployed.
/// </remarks>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(SettlementTables))]
[JsonSerializable(typeof(NpcTables))]
[JsonSerializable(typeof(GearTables))]
[JsonSerializable(typeof(HirelingTables))]
[JsonSerializable(typeof(SpellTables))]
[JsonSerializable(typeof(ServiceTables))]
[JsonSerializable(typeof(ServiceDefinition))]
[JsonSerializable(typeof(StockProfile))]
[JsonSerializable(typeof(NameTables))]
[JsonSerializable(typeof(HostTables))]
internal sealed partial class GameDataJsonContext : JsonSerializerContext;
