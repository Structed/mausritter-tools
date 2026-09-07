using System.Text;
using MausritterTools.Core.Data;
using MausritterTools.Core.Serialization;

namespace MausritterTools.Core.Interop.FantasiaArchive;

/// <summary>
/// Writes the instructions that ship beside an exported project folder.
/// </summary>
/// <remarks>
/// Worth shipping because the import is neither obvious nor forgiving: it is buried under an
/// "Advanced" menu, it takes a folder rather than a file, it cannot be undone, and it will happily
/// swallow anything else left lying in that folder.
/// </remarks>
public static class FantasiaArchiveReadMe
{
    /// <summary>Composes the note for one export.</summary>
    public static string Compose(UiText text, string folderName)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(folderName);

        FantasiaArchiveText prose = text.FantasiaArchive;
        StringBuilder builder = new();

        AppendLine(builder, prose.ReadMeTitle, folderName);
        builder.AppendLine(new string('=', prose.ReadMeTitle.Length));
        builder.AppendLine();

        int step = 1;
        foreach (string line in prose.ReadMeSteps)
        {
            AppendLine(builder, $"{step++}. {line}", folderName);
        }

        builder.AppendLine();
        AppendLine(builder, prose.ReadMeWarning, folderName);
        builder.AppendLine();
        AppendLine(builder, prose.ReadMeNote, folderName);
        builder.AppendLine();
        AppendLine(builder, prose.ReadMeVersionNote, folderName);
        builder.AppendLine();

        builder.AppendLine(new string('-', 78));
        builder.AppendLine(SettlementDocument.AttributionText);

        if (text.Licence.TranslationNote is { Length: > 0 } note)
        {
            builder.AppendLine(note);
        }

        // Windows opens a .txt in Notepad, which historically needed these.
        return builder.ToString().ReplaceLineEndings("\r\n");
    }

    private static void AppendLine(StringBuilder builder, string template, string folderName) =>
        builder.AppendLine(TextTemplate.Format(template, ("folder", folderName)));
}
