using System.Text.Json;
using System.Text.Json.Serialization;
using MausritterTools.Core.Generation;
using MausritterTools.Core.Model;
using Structed.Inkwell.Randomness;
using Structed.Inkwell.Serialization;

namespace MausritterTools.Core.Serialization;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(SettlementDocument))]
internal sealed partial class SettlementDocumentJsonContext : JsonSerializerContext;

/// <summary>What an imported file yielded: how to rebuild it, and the language it was read in.</summary>
/// <param name="Options">Everything needed to regenerate the settlement.</param>
/// <param name="Locale">The language code recorded in the file, if it carried one.</param>
public sealed record SettlementImport(GenerationOptions Options, string? Locale);

/// <summary>Reads and writes exported settlement files.</summary>
public static class SettlementSerializer
{
    /// <summary>Serialises a settlement and the options that produced it.</summary>
    public static string ToJson(
        Settlement settlement,
        GenerationOptions options,
        DateTimeOffset? timestamp = null,
        string? locale = null)
    {
        ArgumentNullException.ThrowIfNull(settlement);
        ArgumentNullException.ThrowIfNull(options);

        SettlementDocument document = new()
        {
            GeneratedUtc = (timestamp ?? DateTimeOffset.UtcNow).UtcDateTime.ToString("o"),
            Locale = locale,
            Options = SettlementDocumentOptions.From(options),
            Settlement = SettlementSnapshot.From(settlement)
        };

        return JsonSerializer.Serialize(document, SettlementDocumentJsonContext.Default.SettlementDocument);
    }

    /// <summary>
    /// Reads an exported file and returns the options needed to rebuild it.
    /// </summary>
    /// <remarks>
    /// Only the options are honoured. Regenerating from them rather than trusting the snapshot
    /// keeps the imported settlement fully editable and re-rollable, and means a settlement shared
    /// before a table update is rebuilt against the current tables.
    /// </remarks>
    /// <exception cref="DocumentFormatException">The file is not a settlement export.</exception>
    public static GenerationOptions FromJson(string json) => Read(json).Options;

    /// <summary>
    /// Reads an exported file, including the language it was written in.
    /// </summary>
    /// <exception cref="DocumentFormatException">The file is not a settlement export.</exception>
    public static SettlementImport Read(string json)
    {
        SettlementDocument document = DocumentEnvelope.Read(
            json,
            SettlementDocument.FormatId,
            SettlementDocument.CurrentVersion,
            SettlementDocumentJsonContext.Default.SettlementDocument);

        return new SettlementImport(document.Options.ToGenerationOptions(), document.Locale);
    }

    /// <summary>Whether some text is one of this tool's own settlement exports.</summary>
    public static bool IsSettlementJson(string? text) =>
        DocumentEnvelope.Matches(text, SettlementDocument.FormatId);

    /// <summary>A filename-safe name for the exported file, e.g. <c>owlmill-c21p6.json</c>.</summary>
    public static string SuggestFileName(Settlement settlement)
    {
        ArgumentNullException.ThrowIfNull(settlement);

        return $"{DocumentEnvelope.Slug(settlement.Name, "settlement")}-{SeedCodec.Encode(settlement.Seed)}.json";
    }
}
