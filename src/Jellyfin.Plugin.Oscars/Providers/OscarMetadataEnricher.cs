using Jellyfin.Plugin.Oscars.Configuration;
using Jellyfin.Plugin.Oscars.Models;
using Jellyfin.Plugin.Oscars.Services;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Oscars.Providers;

/// <summary>
/// Coordinates Oscar lookup/parsing for a single IMDb ID.
/// </summary>
public sealed class OscarMetadataEnricher
{
    private readonly IOmdbClient _omdbClient;
    private readonly IAwardsParser _awardsParser;
    private readonly IOscarCacheService _cacheService;
    private readonly IPluginConfigurationService _configurationService;
    private readonly ILogger<OscarMetadataEnricher> _logger;

    public OscarMetadataEnricher(
        IOmdbClient omdbClient,
        IAwardsParser awardsParser,
        IOscarCacheService cacheService,
        IPluginConfigurationService configurationService,
        ILogger<OscarMetadataEnricher> logger)
    {
        _omdbClient = omdbClient;
        _awardsParser = awardsParser;
        _cacheService = cacheService;
        _configurationService = configurationService;
        _logger = logger;
    }

    public async Task<OscarAwardInfo?> EnrichAsync(string imdbId, CancellationToken cancellationToken = default)
    {
        var result = await EnrichDetailedAsync(imdbId, cancellationToken).ConfigureAwait(false);
        return result.Outcome == OscarEnrichmentLookupOutcome.Success
            ? result.AwardInfo
            : null;
    }

    public async Task<OscarEnrichmentLookupResult> EnrichDetailedAsync(string imdbId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imdbId);
        var normalizedImdbId = imdbId.Trim();
        var configuration = _configurationService.GetCurrent();

        if (!configuration.EnableOscarEnrichment)
        {
            _logger.LogDebug("Oscar enrichment is disabled. Skipping IMDb ID {ImdbId}.", normalizedImdbId);
            return new OscarEnrichmentLookupResult
            {
                Outcome = OscarEnrichmentLookupOutcome.EnrichmentDisabled
            };
        }

        if (string.IsNullOrWhiteSpace(configuration.OmdbApiKey))
        {
            _logger.LogDebug("OMDb API key is missing. Skipping IMDb ID {ImdbId}.", normalizedImdbId);
            return new OscarEnrichmentLookupResult
            {
                Outcome = OscarEnrichmentLookupOutcome.MissingApiKey
            };
        }

        var cached = await _cacheService.GetAsync(normalizedImdbId, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            _logger.LogDebug("Using cached Oscar metadata for IMDb ID {ImdbId}.", normalizedImdbId);
            return new OscarEnrichmentLookupResult
            {
                Outcome = OscarEnrichmentLookupOutcome.Success,
                AwardInfo = cached,
                UsedCache = true
            };
        }

        _logger.LogDebug("Fetching OMDb Oscar metadata for IMDb ID {ImdbId}.", normalizedImdbId);
        var movie = await _omdbClient.GetByImdbIdAsync(normalizedImdbId, cancellationToken).ConfigureAwait(false);
        if (movie is null)
        {
            _logger.LogWarning("OMDb lookup failed or returned no usable payload for IMDb ID {ImdbId}.", normalizedImdbId);
            return new OscarEnrichmentLookupResult
            {
                Outcome = OscarEnrichmentLookupOutcome.OmdbLookupFailed,
                OmdbRequestAttempted = true
            };
        }

        var parsed = string.IsNullOrWhiteSpace(movie.AwardsText)
            ? new OscarAwardInfo
            {
                Status = OscarStatus.None,
                RawAwardsText = movie.AwardsText,
                LastUpdatedUtc = DateTimeOffset.UtcNow
            }
            : _awardsParser.Parse(movie.AwardsText);

        await _cacheService.SetAsync(normalizedImdbId, parsed, cancellationToken).ConfigureAwait(false);
        _logger.LogDebug(
            "Oscar enrichment completed for IMDb ID {ImdbId} with status {Status}.",
            normalizedImdbId,
            parsed.Status);
        return new OscarEnrichmentLookupResult
        {
            Outcome = OscarEnrichmentLookupOutcome.Success,
            AwardInfo = parsed,
            OmdbRequestAttempted = true
        };
    }

    // TODO: Revisit whether tags remain the best MVP storage once plugin-owned persistent metadata is introduced.
    // TODO: Respect cache expiration settings once persistent cache storage is introduced.
}
