namespace Jellyfin.Plugin.Oscars.Models;

/// <summary>
/// Internal Oscar metadata model used by the MVP and as the future source of truth for
/// collection/filter presentation work.
/// </summary>
public sealed class OscarAwardInfo
{
    public OscarStatus Status { get; init; } = OscarStatus.None;

    public string? RawAwardsText { get; init; }

    public int OscarWinsCount { get; init; }

    public int OscarNominationsCount { get; init; }

    public DateTimeOffset LastUpdatedUtc { get; init; } = DateTimeOffset.UtcNow;
}
