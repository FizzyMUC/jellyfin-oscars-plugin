namespace Jellyfin.Plugin.Oscars.Models;

/// <summary>
/// Structured lookup result for Oscar enrichment.
/// </summary>
public sealed class OscarEnrichmentLookupResult
{
    public OscarEnrichmentLookupOutcome Outcome { get; init; }

    public OscarAwardInfo? AwardInfo { get; init; }

    public bool UsedCache { get; init; }

    public bool OmdbRequestAttempted { get; init; }
}
