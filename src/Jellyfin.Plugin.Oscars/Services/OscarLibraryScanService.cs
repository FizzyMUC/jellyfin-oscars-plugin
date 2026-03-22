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
    private readonly IOscarScanStateService _scanStateService;
    private readonly ILogger<OscarLibraryScanService> _logger;

    public OscarLibraryScanService(
        IPluginConfigurationService configurationService,
        ILibraryMovieRepository movieRepository,
        IOscarMovieProcessingService movieProcessingService,
        IOscarScanStateService scanStateService,
        ILogger<OscarLibraryScanService> logger)
    {
        _configurationService = configurationService;
        _movieRepository = movieRepository;
        _movieProcessingService = movieProcessingService;
        _scanStateService = scanStateService;
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
            "{Origin} Oscar library scan started. ConfiguredBatchSize={ConfiguredBatchSize}, EffectiveBatchSize={EffectiveBatchSize}, MaxEligibleMoviesToProcess={MaxEligibleMoviesToProcess}.",
            request.Origin,
            Math.Max(1, configuration.RefreshBatchSize),
            request.MaxEligibleMoviesToProcess.GetValueOrDefault(Math.Max(1, configuration.RefreshBatchSize)),
            request.MaxEligibleMoviesToProcess);
        var movies = _movieRepository.GetLocalMovies();
        var scanState = await _scanStateService.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

        var reasonCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var moviesEligible = 0;
        var moviesProcessed = 0;
        var moviesUpdated = 0;
        var moviesNoChange = 0;
        var moviesWithoutOscarData = 0;
        var moviesFailed = 0;
        var omdbRequestsMade = 0;
        var configuredBatchSize = Math.Max(1, configuration.RefreshBatchSize);
        var maxEligibleMoviesToProcess = request.MaxEligibleMoviesToProcess.GetValueOrDefault(configuredBatchSize);
        var eligibleMovies = movies
            .Where(movie => !string.IsNullOrWhiteSpace(movie.Movie.GetProviderId(MetadataProvider.Imdb)))
            .ToList();
        var uncheckedCount = eligibleMovies.Count(movie => !scanState.Items.TryGetValue(movie.Movie.Id, out var itemState) || itemState.LastOscarScanUtc is null);
        var selectionMode = uncheckedCount > 0
            ? "unchecked_first"
            : "oldest_scanned_first";
        var selectedMovies = eligibleMovies
            .OrderBy(movie => scanState.Items.TryGetValue(movie.Movie.Id, out var itemState) && itemState.LastOscarScanUtc.HasValue ? 1 : 0)
            .ThenBy(movie => scanState.Items.TryGetValue(movie.Movie.Id, out var itemState) ? itemState.LastOscarScanUtc ?? DateTimeOffset.MinValue : DateTimeOffset.MinValue)
            .ThenBy(movie => movie.Movie.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(movie => movie.Movie.Id)
            .Take(maxEligibleMoviesToProcess)
            .ToList();
        var uncheckedSelectionsRecorded = 0;
        moviesEligible = eligibleMovies.Count;

        foreach (var movieInfo in movies)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(movieInfo.Movie.GetProviderId(MetadataProvider.Imdb)))
            {
                Increment(reasonCounts, ToReasonKey(OscarMovieProcessOutcome.MissingImdbId));
            }
        }

        var batchLimitCount = Math.Max(0, eligibleMovies.Count - selectedMovies.Count);
        if (batchLimitCount > 0)
        {
            Increment(reasonCounts, "batch_limit_reached", batchLimitCount);
        }

        _logger.LogInformation(
            "{Origin} Oscar scan candidate selection. TotalCandidates={TotalCandidates}, BatchSize={BatchSize}, UncheckedCount={UncheckedCount}, SelectedBatchCount={SelectedBatchCount}, CandidatesNotSelected={CandidatesNotSelected}, SelectionMode={SelectionMode}.",
            request.Origin,
            eligibleMovies.Count,
            maxEligibleMoviesToProcess,
            uncheckedCount,
            selectedMovies.Count,
            batchLimitCount,
            selectionMode);

        foreach (var movieInfo in selectedMovies)
        {
            cancellationToken.ThrowIfCancellationRequested();

            moviesProcessed++;
            var wasUncheckedBeforeSelection = !scanState.Items.TryGetValue(movieInfo.Movie.Id, out var existingScanState) || existingScanState.LastOscarScanUtc is null;
            var result = await _movieProcessingService.ProcessMovieAsync(movieInfo.Movie, cancellationToken).ConfigureAwait(false);
            if (result.OmdbRequestAttempted)
            {
                omdbRequestsMade++;
            }

            if (result.WasUpdated)
            {
                moviesUpdated++;
                await _movieRepository.PersistAsync(movieInfo.Movie, cancellationToken).ConfigureAwait(false);
            }

            if (ShouldRecordCompletedScanAttempt(result))
            {
                await _scanStateService.RecordCompletedScanAttemptAsync(movieInfo.Movie.Id, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
                if (wasUncheckedBeforeSelection)
                {
                    uncheckedSelectionsRecorded++;
                }
            }

            Increment(reasonCounts, ToReasonKey(result.Outcome));
            ClassifyBatchOutcome(result, ref moviesNoChange, ref moviesWithoutOscarData, ref moviesFailed);
        }

        var uncheckedRemaining = Math.Max(0, uncheckedCount - uncheckedSelectionsRecorded);
        var omdbRequestReason = GetOmdbRequestReason(omdbRequestsMade, selectedMovies.Count, moviesNoChange, moviesWithoutOscarData, moviesFailed);
        _logger.LogInformation(
            "{Origin} Oscar scan batch outcome. SelectedBatchCount={SelectedBatchCount}, MoviesProcessed={MoviesProcessed}, OmdbRequestsMade={OmdbRequestsMade}, OmdbRequestReason={OmdbRequestReason}, MoviesUpdated={MoviesUpdated}, MoviesNoChange={MoviesNoChange}, MoviesWithoutOscarData={MoviesWithoutOscarData}, MoviesFailed={MoviesFailed}, UncheckedRemaining={UncheckedRemaining}.",
            request.Origin,
            selectedMovies.Count,
            moviesProcessed,
            omdbRequestsMade,
            omdbRequestReason,
            moviesUpdated,
            moviesNoChange,
            moviesWithoutOscarData,
            moviesFailed,
            uncheckedRemaining);

        var moviesSkipped = movies.Count - moviesUpdated;

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

    private static void Increment(IDictionary<string, int> reasonCounts, string reason, int amount = 1)
    {
        if (reasonCounts.TryGetValue(reason, out var count))
        {
            reasonCounts[reason] = count + amount;
            return;
        }

        reasonCounts[reason] = amount;
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

    private static bool ShouldRecordCompletedScanAttempt(OscarMovieProcessResult result)
    {
        return result.Outcome is not OscarMovieProcessOutcome.OmdbRequestFailure;
    }

    private static void ClassifyBatchOutcome(
        OscarMovieProcessResult result,
        ref int moviesNoChange,
        ref int moviesWithoutOscarData,
        ref int moviesFailed)
    {
        switch (result.Outcome)
        {
            case OscarMovieProcessOutcome.CacheHit:
            case OscarMovieProcessOutcome.AlreadyUpToDate:
                moviesNoChange++;
                break;
            case OscarMovieProcessOutcome.NoOscarRelatedResult:
                moviesWithoutOscarData++;
                break;
            case OscarMovieProcessOutcome.OmdbRequestFailure:
                moviesFailed++;
                break;
        }
    }

    private static string GetOmdbRequestReason(
        int omdbRequestsMade,
        int selectedBatchCount,
        int moviesNoChange,
        int moviesWithoutOscarData,
        int moviesFailed)
    {
        if (omdbRequestsMade > 0)
        {
            return "performed";
        }

        if (selectedBatchCount == 0)
        {
            return "no_selected_candidates";
        }

        if (moviesNoChange == selectedBatchCount)
        {
            return "selected_items_fresh_or_cached";
        }

        if (moviesWithoutOscarData == selectedBatchCount)
        {
            return "no_omdb_lookup_needed";
        }

        if (moviesFailed == selectedBatchCount)
        {
            return "selected_items_failed_before_lookup";
        }

        return "no_omdb_lookup_needed";
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
