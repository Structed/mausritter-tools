using System.Text.Json;
using MausritterTools.Core.Serialization;
using Structed.Inkwell.Interop.FantasiaArchive;
using Structed.Inkwell.Serialization;

namespace MausritterTools.Core.Interop.FantasiaArchive;

/// <summary>
/// The payload this tool hides on a settlement's Fantasia Archive document.
/// </summary>
/// <remarks>
/// A thin naming of <see cref="HiddenState"/>: what is Mausritter's about it is only which format
/// id marks the payload as ours and what the settlement is nested under, both of which are frozen
/// by every file already written.
/// </remarks>
public static class FantasiaArchiveState
{
    /// <summary>Wraps an exported settlement document for the journey.</summary>
    /// <param name="documentId">The Fantasia Archive document this was written onto.</param>
    /// <param name="settlementJson">This project's own export format, verbatim.</param>
    public static string Write(string documentId, string settlementJson) => HiddenState.Write(
        MausritterArchiveKeys.StateFormatId,
        documentId,
        MausritterArchiveKeys.StatePayloadName,
        settlementJson);

    /// <summary>
    /// Unwraps a payload, returning the settlement export inside it.
    /// </summary>
    /// <returns>The embedded document's JSON, or <c>null</c> if this is not one of ours.</returns>
    public static string? Read(string? state) => HiddenState.Read(
        MausritterArchiveKeys.StateFormatId,
        MausritterArchiveKeys.StatePayloadName,
        state);
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
    /// <exception cref="DocumentFormatException">No settlement of ours was in there.</exception>
    public static SettlementImport Read(IEnumerable<string> dumps)
    {
        ArgumentNullException.ThrowIfNull(dumps);

        foreach (string dump in dumps)
        {
            foreach (JsonElement document in PouchDump.ReadDocuments(dump))
            {
                string? state = PouchDump.FieldString(document, MausritterArchiveKeys.StateField);
                if (FantasiaArchiveState.Read(state) is { } settlementJson)
                {
                    return SettlementSerializer.Read(settlementJson);
                }
            }
        }

        throw new DocumentFormatException(
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
            .Select(document => PouchDump.FieldString(document, MausritterArchiveKeys.StateField))
            .Count(state => FantasiaArchiveState.Read(state) is not null);
    }
}
