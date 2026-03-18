namespace Jellyfin.Plugin.Oscars.Models;

/// <summary>
/// Outcome of processing a single movie during enrichment.
/// </summary>
public enum OscarMovieProcessOutcome
{
    Updated,
    MissingImdbId,
    CacheHit,
    NoOscarRelatedResult,
    OmdbRequestFailure,
    AlreadyUpToDate,
    EnrichmentDisabled,
    MissingApiKey
}
