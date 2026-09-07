using System.Text;
using System.Text.Json;
using MausritterTools.Core.Serialization;

namespace MausritterTools.Core.Interop.FantasiaArchive;

/// <summary>
/// The payload this tool hides on a settlement's Fantasia Archive document.
/// </summary>
/// <remarks>
/// Fantasia Archive has nowhere to keep a seed, and a settlement cannot be recovered from its prose:
/// the generator is not invertible. So the state that regenerates it travels with the document, in a
/// field the app has no blueprint for and therefore never shows, edits or discards.
/// </remarks>
public static class FantasiaArchiveState
{
    /// <summary>Marks the payload as ours, so a field collision is not read as a settlement.</summary>
    private const string FormatId = FantasiaArchiveExporter.StateFormatId;

    /// <summary>Wraps an exported settlement document for the journey.</summary>
    /// <param name="documentId">The Fantasia Archive document this was written onto.</param>
    /// <param name="settlementJson">This project's own export format, verbatim.</param>
    public static string Write(string documentId, string settlementJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(settlementJson);

        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteString("format", FormatId);

            // Recorded so a document duplicated inside Fantasia Archive can be told from the
            // original, which would otherwise leave two documents claiming the same settlement.
            writer.WriteString("documentId", documentId);

            // Nested as an object rather than as an escaped string, so anyone who does go looking
            // finds something readable.
            writer.WritePropertyName("settlement");
            writer.WriteRawValue(settlementJson);

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// Unwraps a payload, returning the settlement export inside it.
    /// </summary>
    /// <returns>The embedded document's JSON, or <c>null</c> if this is not one of ours.</returns>
    public static string? Read(string? state)
    {
        if (state is not { Length: > 0 })
        {
            return null;
        }

        try
        {
            using JsonDocument parsed = JsonDocument.Parse(state);

            if (parsed.RootElement.ValueKind != JsonValueKind.Object ||
                !parsed.RootElement.TryGetProperty("format", out JsonElement format) ||
                format.ValueKind != JsonValueKind.String ||
                !string.Equals(format.GetString(), FormatId, StringComparison.Ordinal) ||
                !parsed.RootElement.TryGetProperty("settlement", out JsonElement settlement) ||
                settlement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            return settlement.GetRawText();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>
/// Reads a Fantasia Archive project back into a settlement.
/// </summary>
/// <remarks>
/// Only a project this tool wrote can be read. A settlement is a pure function of its seed, and that
/// function does not run backwards, so a place someone wrote by hand in Fantasia Archive has nothing
/// here to rebuild from. That fails plainly rather than producing a half-restored sheet.
/// </remarks>
public static class FantasiaArchiveImporter
{
    /// <summary>
    /// Finds the settlement in a set of database dumps.
    /// </summary>
    /// <param name="dumps">
    /// The contents of the project folder's files. Files that are not dumps, and dumps holding no
    /// settlement of ours, are skipped.
    /// </param>
    /// <exception cref="SettlementFormatException">No settlement of ours was in there.</exception>
    public static SettlementImport Read(IEnumerable<string> dumps)
    {
        ArgumentNullException.ThrowIfNull(dumps);

        foreach (string dump in dumps)
        {
            foreach (JsonElement document in PouchDump.ReadDocuments(dump))
            {
                string? state = PouchDump.FieldString(document, FantasiaArchiveBlueprints.StateField);
                if (FantasiaArchiveState.Read(state) is { } settlementJson)
                {
                    return SettlementSerializer.Read(settlementJson);
                }
            }
        }

        throw new SettlementFormatException(
            "That Fantasia Archive project has no settlement written by this tool. Only a " +
            "settlement exported from here carries the seed needed to rebuild it.");
    }

    /// <summary>How many settlements of ours a set of dumps holds.</summary>
    /// <remarks>
    /// A project merged into over time can accumulate several. The page reads the first and says so
    /// rather than guessing which one was meant.
    /// </remarks>
    public static int Count(IEnumerable<string> dumps)
    {
        ArgumentNullException.ThrowIfNull(dumps);

        return dumps
            .SelectMany(PouchDump.ReadDocuments)
            .Select(document => PouchDump.FieldString(document, FantasiaArchiveBlueprints.StateField))
            .Count(state => FantasiaArchiveState.Read(state) is not null);
    }
}
