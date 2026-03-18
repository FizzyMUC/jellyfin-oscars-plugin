namespace Jellyfin.Plugin.Oscars.Models;

/// <summary>
/// High-level result of looking up Oscar metadata for a single IMDb ID.
/// </summary>
public enum OscarEnrichmentLookupOutcome
{
    Success,
    EnrichmentDisabled,
    MissingApiKey,
    OmdbLookupFailed
}
