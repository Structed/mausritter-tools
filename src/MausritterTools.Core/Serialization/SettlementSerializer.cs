using System.Text.Json;
using System.Text.Json.Serialization;
using MausritterTools.Core.Generation;
using MausritterTools.Core.Model;

namespace MausritterTools.Core.Serialization;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(SettlementDocument))]
internal sealed partial class SettlementDocumentJsonContext : JsonSerializerContext;

/// <summary>Reads and writes exported settlement files.</summary>
public static class SettlementSerializer
{
    /// <summary>Serialises a settlement and the options that produced it.</summary>
    public static string ToJson(Settlement settlement, GenerationOptions options, DateTimeOffset? timestamp = null)
    {
        ArgumentNullException.ThrowIfNull(settlement);
        ArgumentNullException.ThrowIfNull(options);

        SettlementDocument document = new()
        {
            GeneratedUtc = (timestamp ?? DateTimeOffset.UtcNow).UtcDateTime.ToString("o"),
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
    /// <exception cref="SettlementFormatException">The file is not a settlement export.</exception>
    public static GenerationOptions FromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        SettlementDocument? document;
        try
        {
            document = JsonSerializer.Deserialize(json, SettlementDocumentJsonContext.Default.SettlementDocument);
        }
        catch (JsonException ex)
        {
            throw new SettlementFormatException($"That does not look like valid JSON: {ex.Message}", ex);
        }

        if (document is null)
        {
            throw new SettlementFormatException("The file was empty.");
        }

        if (!string.Equals(document.Format, SettlementDocument.FormatId, StringComparison.Ordinal))
        {
            throw new SettlementFormatException(
                $"Expected a '{SettlementDocument.FormatId}' file but found '{document.Format}'.");
        }

        if (document.Version > SettlementDocument.CurrentVersion)
        {
            throw new SettlementFormatException(
                $"That file was written by a newer version of this tool (format version {document.Version}).");
        }

        return document.Options.ToGenerationOptions();
    }

    /// <summary>A filename-safe name for the exported file, e.g. <c>owlmill-c21p6.json</c>.</summary>
    public static string SuggestFileName(Settlement settlement)
    {
        ArgumentNullException.ThrowIfNull(settlement);

        string slug = new(
        [
            .. settlement.Name
                .ToLowerInvariant()
                .Select(c => char.IsLetterOrDigit(c) ? c : '-')
        ]);

        slug = string.Join('-', slug.Split('-', StringSplitOptions.RemoveEmptyEntries));
        if (slug.Length == 0)
        {
            slug = "settlement";
        }

        return $"{slug}-{Randomness.SeedCodec.Encode(settlement.Seed)}.json";
    }
}

/// <summary>Thrown when an imported file is not a usable settlement export.</summary>
public sealed class SettlementFormatException(string message, Exception? innerException = null)
    : Exception(message, innerException);
