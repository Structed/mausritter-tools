using System.Text.Json.Serialization;

namespace MausritterTools.Core.Data;

/// <summary>
/// Where a data file came from and under what licence.
/// </summary>
/// <remarks>
/// Carried in the data rather than hardcoded in the UI so the attribution shown to users cannot
/// drift away from the content it describes. Both licences that apply to this project require
/// their notices to be displayed, and the Mausritter Third Party Licence specifically requires it
/// on the website itself, not merely in the source repository.
/// </remarks>
public sealed record DataProvenance
{
    public string Describes { get; init; } = "";

    /// <summary>Set on house-rule files to mark them as unofficial.</summary>
    public string? Status { get; init; }

    /// <summary>Why this file exists in the form it does.</summary>
    public string? Rationale { get; init; }

    public string? Work { get; init; }

    public string? Version { get; init; }

    public string? Url { get; init; }

    public string? Licence { get; init; }

    public string? LicenceUrl { get; init; }

    public string? Attribution { get; init; }

    public IReadOnlyList<string> SourceFiles { get; init; } = [];

    /// <summary><c>true</c> when the file is imported from the Mausritter SRD.</summary>
    [JsonIgnore]
    public bool IsSrdDerived => Work is not null;
}
