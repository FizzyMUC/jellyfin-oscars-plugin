using Jellyfin.Plugin.Oscars.Models;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Oscars.Services;

/// <summary>
/// Manual library-wide Oscar scan workflow for existing local movies.
/// </summary>
public sealed class OscarLibraryScanService : IOscarLibraryScanService
{
    private readonly IPluginConfigurationService _configurationService;
    private readonly ILibraryMovieRepository _movieRepository;
    private readonly IOscarMovieProcessingService _movieProcessingService;
    private readonly ILogger<OscarLibraryScanService> _logger;

    public OscarLibraryScanService(
        IPluginConfigurationService configurationService,
        ILibraryMovieRepository movieRepository,
        IOscarMovieProcessingService movieProcessingService,
        ILogger<OscarLibraryScanService> logger)
    {
        _configurationService = configurationService;
        _movieRepository = movieRepository;
        _movieProcessingService = movieProcessingService;
        _logger = logger;
    }

    public async Task<OscarLibraryScanResult> ScanLibraryAsync(OscarLibraryScanRequest? request = null, CancellationToken cancellationToken = default)
    {
        request ??= new OscarLibraryScanRequest();
        var configuration = _configurationService.GetCurrent();
        if (!configuration.EnableOscarEnrichment)
        {
            const string message = "Oscar enrichment is disabled. Enable it before running a library scan.";
            _logger.LogWarning("{Origin} Oscar library scan aborted because enrichment is disabled.", request.Origin);
            return Failure(message, "enrichment_disabled");
        }

        if (string.IsNullOrWhiteSpace(configuration.OmdbApiKey))
        {
            const string message = "OMDb API key is missing. Enter your key before running a library scan.";
            _logger.LogWarning("{Origin} Oscar library scan aborted because the OMDb API key is missing.", request.Origin);
            return Failure(message, "missing_api_key");
        }

        if (request.RequireScheduledRefreshEnabled && !configuration.EnableScheduledRefresh)
        {
            const string message = "Scheduled refresh is disabled. Enable it before running the Oscar metadata refresh task.";
            _logger.LogInformation("{Origin} Oscar library scan skipped because scheduled refresh is disabled.", request.Origin);
            return Failure(message, "scheduled_refresh_disabled");
        }

        _logger.LogInformation(
            "{Origin} Oscar library scan started. MaxEligibleMoviesToProcess={MaxEligibleMoviesToProcess}.",
            request.Origin,
            request.MaxEligibleMoviesToProcess);
        var movies = _movieRepository.GetLocalMovies();

        var reasonCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var moviesEligible = 0;
        var moviesProcessed = 0;
        var moviesUpdated = 0;
        var omdbRequestsMade = 0;
        var maxEligibleMoviesToProcess = request.MaxEligibleMoviesToProcess.GetValueOrDefault(int.MaxValue);

        foreach (var movie in movies)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(movie.GetProviderId(MetadataProvider.Imdb)))
            {
                Increment(reasonCounts, ToReasonKey(OscarMovieProcessOutcome.MissingImdbId));
                continue;
            }

            moviesEligible++;
            if (moviesProcessed >= maxEligibleMoviesToProcess)
            {
                Increment(reasonCounts, "batch_limit_reached");
                continue;
            }

            moviesProcessed++;
            var result = await _movieProcessingService.ProcessMovieAsync(movie, cancellationToken).ConfigureAwait(false);
            if (result.OmdbRequestAttempted)
            {
                omdbRequestsMade++;
            }

            if (result.WasUpdated)
            {
                moviesUpdated++;
                await _movieRepository.PersistAsync(movie, cancellationToken).ConfigureAwait(false);
                continue;
            }

            Increment(reasonCounts, ToReasonKey(result.Outcome));
        }

        var moviesSkipped = movies.Count - moviesUpdated;
        _logger.LogInformation(
            "{Origin} Oscar library scan completed. TotalMoviesFound={TotalMoviesFound}, MoviesEligible={MoviesEligible}, MoviesProcessed={MoviesProcessed}, MoviesUpdated={MoviesUpdated}, MoviesSkipped={MoviesSkipped}, OmdbRequestsMade={OmdbRequestsMade}.",
            request.Origin,
            movies.Count,
            moviesEligible,
            moviesProcessed,
            moviesUpdated,
            moviesSkipped,
            omdbRequestsMade);

        return new OscarLibraryScanResult
        {
            IsSuccess = true,
            Message = BuildSuccessMessage(movies.Count, moviesEligible, moviesProcessed, moviesUpdated, moviesSkipped, omdbRequestsMade, reasonCounts),
            TotalMoviesFound = movies.Count,
            MoviesEligible = moviesEligible,
            MoviesProcessed = moviesProcessed,
            MoviesUpdated = moviesUpdated,
            MoviesSkipped = moviesSkipped,
            OmdbRequestsMade = omdbRequestsMade,
            ReasonCounts = new Dictionary<string, int>(reasonCounts, StringComparer.OrdinalIgnoreCase)
        };
    }

    private static OscarLibraryScanResult Failure(string message, string reason)
    {
        return new OscarLibraryScanResult
        {
            IsSuccess = false,
            Message = message,
            MoviesSkipped = 0,
            ReasonCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                [reason] = 1
            }
        };
    }

    private static void Increment(IDictionary<string, int> reasonCounts, string reason)
    {
        if (reasonCounts.TryGetValue(reason, out var count))
        {
            reasonCounts[reason] = count + 1;
            return;
        }

        reasonCounts[reason] = 1;
    }

    private static string ToReasonKey(OscarMovieProcessOutcome outcome)
    {
        return outcome switch
        {
            OscarMovieProcessOutcome.MissingImdbId => "missing_imdb_id",
            OscarMovieProcessOutcome.CacheHit => "cache_hit",
            OscarMovieProcessOutcome.NoOscarRelatedResult => "no_oscar_related_result",
            OscarMovieProcessOutcome.OmdbRequestFailure => "omdb_request_failure",
            OscarMovieProcessOutcome.AlreadyUpToDate => "already_up_to_date",
            OscarMovieProcessOutcome.EnrichmentDisabled => "enrichment_disabled",
            OscarMovieProcessOutcome.MissingApiKey => "missing_api_key",
            _ => "other"
        };
    }

    private static string BuildSuccessMessage(
        int totalMoviesFound,
        int moviesEligible,
        int moviesProcessed,
        int moviesUpdated,
        int moviesSkipped,
        int omdbRequestsMade,
        IReadOnlyDictionary<string, int> reasonCounts)
    {
        var summary = $"Scan completed. Found {totalMoviesFound} movies; eligible {moviesEligible}; processed {moviesProcessed}; updated {moviesUpdated}; skipped {moviesSkipped}; OMDb requests {omdbRequestsMade}.";
        if (reasonCounts.Count == 0)
        {
            return summary;
        }

        var reasons = string.Join(
            ", ",
            reasonCounts
                .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
                .Select(entry => $"{entry.Key.Replace('_', ' ')}: {entry.Value}"));

        return $"{summary} Reasons: {reasons}.";
    }
}
