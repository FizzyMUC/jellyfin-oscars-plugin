using Jellyfin.Plugin.Oscars.Models;
using Jellyfin.Plugin.Oscars.Providers;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Oscars.Services;

/// <summary>
/// Shared movie-level Oscar enrichment workflow used by refresh and manual scan paths.
/// </summary>
public sealed class OscarMovieProcessingService : IOscarMovieProcessingService
{
    private readonly OscarMetadataEnricher _enricher;
    private readonly OscarMovieTagService _tagService;
    private readonly ILogger<OscarMovieProcessingService> _logger;

    public OscarMovieProcessingService(
        OscarMetadataEnricher enricher,
        OscarMovieTagService tagService,
        ILogger<OscarMovieProcessingService> logger)
    {
        _enricher = enricher;
        _tagService = tagService;
        _logger = logger;
    }

    public async Task<OscarMovieProcessResult> ProcessMovieAsync(Movie item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        var imdbId = item.GetProviderId(MetadataProvider.Imdb);
        if (string.IsNullOrWhiteSpace(imdbId))
        {
            _logger.LogDebug("Skipping movie {MovieName} because no IMDb ID exists.", item.Name);
            return OscarMovieProcessResult.Create(OscarMovieProcessOutcome.MissingImdbId);
        }

        var lookup = await _enricher.EnrichDetailedAsync(imdbId, cancellationToken).ConfigureAwait(false);
        switch (lookup.Outcome)
        {
            case OscarEnrichmentLookupOutcome.EnrichmentDisabled:
                return OscarMovieProcessResult.Create(OscarMovieProcessOutcome.EnrichmentDisabled);
            case OscarEnrichmentLookupOutcome.MissingApiKey:
                return OscarMovieProcessResult.Create(OscarMovieProcessOutcome.MissingApiKey);
            case OscarEnrichmentLookupOutcome.OmdbLookupFailed:
                return OscarMovieProcessResult.Create(
                    OscarMovieProcessOutcome.OmdbRequestFailure,
                    isEligible: true,
                    omdbRequestAttempted: lookup.OmdbRequestAttempted);
        }

        var awardInfo = lookup.AwardInfo ?? new OscarAwardInfo { Status = OscarStatus.None };
        var wasUpdated = _tagService.ApplyOscarTags(item, awardInfo.Status);
        if (wasUpdated)
        {
            return OscarMovieProcessResult.Create(
                OscarMovieProcessOutcome.Updated,
                isEligible: true,
                wasUpdated: true,
                omdbRequestAttempted: lookup.OmdbRequestAttempted);
        }

        if (lookup.UsedCache)
        {
            return OscarMovieProcessResult.Create(
                OscarMovieProcessOutcome.CacheHit,
                isEligible: true);
        }

        if (awardInfo.Status == OscarStatus.None)
        {
            return OscarMovieProcessResult.Create(
                OscarMovieProcessOutcome.NoOscarRelatedResult,
                isEligible: true,
                omdbRequestAttempted: lookup.OmdbRequestAttempted);
        }

        return OscarMovieProcessResult.Create(
            OscarMovieProcessOutcome.AlreadyUpToDate,
            isEligible: true,
            omdbRequestAttempted: lookup.OmdbRequestAttempted);
    }
}
