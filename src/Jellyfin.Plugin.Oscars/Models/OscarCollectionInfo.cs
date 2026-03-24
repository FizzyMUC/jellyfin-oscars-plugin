namespace Jellyfin.Plugin.Oscars.Models;

/// <summary>
/// Lightweight collection state needed by the Oscar collection sync workflow.
/// </summary>
public sealed class OscarCollectionInfo
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public bool WasCreated { get; init; }

    public bool HasPrimaryImage { get; set; }

    public bool HasThumbImage { get; set; }

    public string? PrimaryImagePath { get; set; }

    public IReadOnlySet<Guid> ItemIds { get; set; } = new HashSet<Guid>();
}
