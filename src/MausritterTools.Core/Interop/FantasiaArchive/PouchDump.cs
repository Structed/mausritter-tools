using System.Text;
using System.Text.Json;
using MausritterTools.Core.Randomness;

namespace MausritterTools.Core.Interop.FantasiaArchive;

/// <summary>
/// Reads and writes a Fantasia Archive database dump.
/// </summary>
/// <remarks>
/// <para>
/// A Fantasia Archive project is a folder of these, one per document type, and the file's name is
/// what decides which database it is loaded into. The format is newline-delimited JSON: a header
/// line, one or more lines carrying documents, and a trailing sequence line.
/// </para>
/// <para>
/// Written by hand rather than serialised from a model, because the field values inside a document
/// are polymorphic and the exact shape is the whole contract.
/// </para>
/// </remarks>
public static class PouchDump
{
    /// <summary>The extension every dump in a project folder carries.</summary>
    public const string FileExtension = ".txt";

    /// <summary>The dump format's own version, which is not the app's version.</summary>
    private const string DumpVersion = "0.1.0";

    /// <summary>The file a dump of <paramref name="type"/> must be written to.</summary>
    public static string FileNameFor(string type) => type + FileExtension;

    /// <summary>Writes a set of documents as one dump.</summary>
    public static string Write(
        string databaseName, IReadOnlyList<Document> documents, DateTimeOffset timestamp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);
        ArgumentNullException.ThrowIfNull(documents);

        StringBuilder builder = new();

        builder.Append(WriteLine(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("version", DumpVersion);

            // The app writes its projects out of the browser's IndexedDB. The loader ignores this,
            // but a dump that claims to be something it could not have been is needless noise.
            writer.WriteString("db_type", "idb");
            writer.WriteString("start_time", timestamp.UtcDateTime.ToString("o"));

            writer.WritePropertyName("db_info");
            writer.WriteStartObject();
            writer.WriteNumber("doc_count", documents.Count);
            writer.WriteNumber("update_seq", documents.Count);
            writer.WriteString("db_name", databaseName);
            writer.WriteEndObject();

            writer.WriteEndObject();
        }));

        builder.Append(WriteLine(writer =>
        {
            writer.WriteStartObject();
            writer.WritePropertyName("docs");
            writer.WriteStartArray();
            foreach (Document document in documents)
            {
                document.Write(writer);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }));

        builder.Append(WriteLine(writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("seq", documents.Count);
            writer.WriteEndObject();
        }));

        return builder.ToString();
    }

    /// <summary>
    /// Reads every document out of a dump.
    /// </summary>
    /// <remarks>
    /// Deliberately forgiving. The file may have been written by Fantasia Archive itself, by a
    /// different version of it, or by hand, so a line that is not a document batch is skipped rather
    /// than treated as an error.
    /// </remarks>
    public static IReadOnlyList<JsonElement> ReadDocuments(string dump)
    {
        ArgumentNullException.ThrowIfNull(dump);

        List<JsonElement> documents = [];

        foreach (string line in dump.Split('\n'))
        {
            string trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            JsonDocument parsed;
            try
            {
                parsed = JsonDocument.Parse(trimmed);
            }
            catch (JsonException)
            {
                continue;
            }

            using (parsed)
            {
                if (parsed.RootElement.ValueKind != JsonValueKind.Object ||
                    !parsed.RootElement.TryGetProperty("docs", out JsonElement docs) ||
                    docs.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (JsonElement document in docs.EnumerateArray())
                {
                    if (document.ValueKind == JsonValueKind.Object)
                    {
                        // Cloned so it outlives the JsonDocument it was parsed from.
                        documents.Add(document.Clone());
                    }
                }
            }
        }

        return documents;
    }

    /// <summary>Reads a string-valued entry from a document's field list, if it has one.</summary>
    public static string? FieldString(JsonElement document, string fieldId)
    {
        if (document.ValueKind != JsonValueKind.Object ||
            !document.TryGetProperty("extraFields", out JsonElement fields) ||
            fields.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (JsonElement field in fields.EnumerateArray())
        {
            if (field.ValueKind == JsonValueKind.Object &&
                field.TryGetProperty("id", out JsonElement id) &&
                id.ValueKind == JsonValueKind.String &&
                string.Equals(id.GetString(), fieldId, StringComparison.Ordinal) &&
                field.TryGetProperty("value", out JsonElement value) &&
                value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }

        return null;
    }

    private static string WriteLine(Action<Utf8JsonWriter> write)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = false }))
        {
            write(writer);
        }

        // Newline-delimited, so every line ends with one, including the last.
        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }
}

/// <summary>
/// Mints the identifiers a Fantasia Archive document needs.
/// </summary>
/// <remarks>
/// Derived rather than drawn at random, so exporting the same settlement twice produces the same
/// documents. That makes a second merge a no-op instead of a duplicate set, and it makes the
/// exporter a pure function like everything else here. <c>Guid.NewGuid</c> would give neither.
/// </remarks>
public static class DocumentIdentity
{
    /// <summary>
    /// The id and revision for one document, keyed by a stable path.
    /// </summary>
    /// <param name="seed">The settlement's root seed.</param>
    /// <param name="path">
    /// A path that must identify the document <em>and</em> the exact settlement it belongs to. Two
    /// settlements that share a seed but differ in their settings or locks are different places, and
    /// giving their documents the same identity would make the second one un-importable: the app
    /// loads with <c>new_edits: false</c>, so a document whose id and revision are already present
    /// is quietly discarded rather than applied.
    /// </param>
    public static (string Id, string Revision) For(uint seed, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        Pcg32 random = SeedDerivation.CreateStream(seed, path);

        return (FormatUuid(NextBytes(random)), "1-" + FormatHex(NextBytes(random)));
    }

    private static byte[] NextBytes(Pcg32 random)
    {
        byte[] bytes = new byte[16];
        for (int i = 0; i < bytes.Length; i += 4)
        {
            uint value = random.NextUInt32();
            bytes[i] = (byte)value;
            bytes[i + 1] = (byte)(value >> 8);
            bytes[i + 2] = (byte)(value >> 16);
            bytes[i + 3] = (byte)(value >> 24);
        }

        return bytes;
    }

    /// <summary>
    /// Shapes 16 bytes into a version 4 UUID.
    /// </summary>
    /// <remarks>
    /// The app mints its own ids as version 4, and its repair tooling recognises documents by that
    /// shape, so an id that merely looks random is not enough: the version and variant bits have to
    /// be right. The randomness behind them is this project's, and reproducible.
    /// </remarks>
    private static string FormatUuid(byte[] bytes)
    {
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x40);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

        string hex = FormatHex(bytes);
        return $"{hex[..8]}-{hex[8..12]}-{hex[12..16]}-{hex[16..20]}-{hex[20..]}";
    }

    private static string FormatHex(byte[] bytes) => Convert.ToHexStringLower(bytes);
}
