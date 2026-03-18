namespace Jellyfin.Plugin.Oscars.Models;

/// <summary>
/// Options for running a library-wide Oscar scan.
/// </summary>
public sealed class OscarLibraryScanRequest
{
    public string Origin { get; init; } = "manual";

    public int? MaxEligibleMoviesToProcess { get; init; }

    public bool RequireScheduledRefreshEnabled { get; init; }
}
