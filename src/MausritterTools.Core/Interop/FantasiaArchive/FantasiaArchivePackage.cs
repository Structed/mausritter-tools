using System.IO.Compression;
using System.Text;

namespace MausritterTools.Core.Interop.FantasiaArchive;

/// <summary>
/// Packs an export into a ZIP, and unpacks one again.
/// </summary>
/// <remarks>
/// <para>
/// Fantasia Archive imports a <em>folder</em>, and a page running in a browser cannot hand one over.
/// A ZIP holding that folder is the nearest thing: the reader unpacks it and points the app at what
/// falls out.
/// </para>
/// <para>
/// The instructions sit outside the project folder, not in it, and that placement is load-bearing.
/// The app's merge reads every file in the folder it is given and feeds each one to its database
/// loader, so a readme sitting alongside the dumps would be parsed as a database and break the
/// import.
/// </para>
/// </remarks>
public static class FantasiaArchivePackage
{
    /// <summary>The instructions, which deliberately live beside the folder rather than inside it.</summary>
    public const string ReadMeName = "HOW-TO-IMPORT.txt";

    /// <summary>Builds the downloadable archive.</summary>
    public static byte[] Create(FantasiaArchiveExport export, string readMe)
    {
        ArgumentNullException.ThrowIfNull(export);
        ArgumentNullException.ThrowIfNull(readMe);

        using MemoryStream stream = new();

        using (ZipArchive archive = new(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (ExportedFile file in export.Files)
            {
                Write(archive, $"{export.FolderName}/{file.Name}", file.Content);
            }

            Write(archive, ReadMeName, readMe);
        }

        return stream.ToArray();
    }

    /// <summary>The archive's file name, e.g. <c>owlmill-c21p6-fantasia-archive.zip</c>.</summary>
    public static string FileNameFor(FantasiaArchiveExport export)
    {
        ArgumentNullException.ThrowIfNull(export);

        return $"{export.FolderName}-fantasia-archive.zip";
    }

    /// <summary>
    /// Pulls the database dumps out of an archive.
    /// </summary>
    /// <remarks>
    /// Takes anything that looks like a dump wherever it sits in the archive, so a project the
    /// reader has rezipped themselves, folder and all, still opens.
    /// </remarks>
    public static IReadOnlyList<string> ExtractDumps(Stream zip)
    {
        ArgumentNullException.ThrowIfNull(zip);

        List<string> dumps = [];

        using ZipArchive archive = new(zip, ZipArchiveMode.Read);

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            if (!entry.FullName.EndsWith(PouchDump.FileExtension, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(entry.Name, ReadMeName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            using Stream content = entry.Open();
            using StreamReader reader = new(content, Encoding.UTF8);
            dumps.Add(reader.ReadToEnd());
        }

        return dumps;
    }

    private static void Write(ZipArchive archive, string path, string content)
    {
        ZipArchiveEntry entry = archive.CreateEntry(path, CompressionLevel.Optimal);

        using Stream stream = entry.Open();
        using StreamWriter writer = new(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
    }
}
