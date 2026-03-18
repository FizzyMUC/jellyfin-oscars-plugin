using Jellyfin.Plugin.Oscars.Models;
using Jellyfin.Plugin.Oscars.Services;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Oscars.Tasks;

/// <summary>
/// Scheduled task that refreshes Oscar metadata for existing library movies.
/// </summary>
public sealed class OscarRefreshTask : IScheduledTask, IConfigurableScheduledTask
{
    private readonly IPluginConfigurationService _configurationService;
    private readonly IOscarCollectionSyncService _collectionSyncService;
    private readonly IOscarLibraryScanService _libraryScanService;
    private readonly ILogger<OscarRefreshTask> _logger;

    public OscarRefreshTask(
        IPluginConfigurationService configurationService,
        IOscarCollectionSyncService collectionSyncService,
        IOscarLibraryScanService libraryScanService,
        ILogger<OscarRefreshTask> logger)
    {
        _configurationService = configurationService;
        _collectionSyncService = collectionSyncService;
        _libraryScanService = libraryScanService;
        _logger = logger;
    }

    public string Name => "Oscar Metadata Refresh";

    public string Key => "OscarMetadataRefresh";

    public string Description => "Refreshes Oscar tags for existing movies in the background using OMDb and the configured cache window.";

    public string Category => "Library";

    public bool IsHidden => false;

    public bool IsEnabled => _configurationService.GetCurrent().EnableScheduledRefresh;

    public bool IsLogged => true;

    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var configuration = _configurationService.GetCurrent();
        _logger.LogInformation(
            "Oscar metadata refresh task started. ScheduledRefreshEnabled={ScheduledRefreshEnabled}, EnrichmentEnabled={EnrichmentEnabled}, BatchSize={BatchSize}, CacheDurationHours={CacheDurationHours}, HasApiKey={HasApiKey}, WinnersCollectionEnabled={WinnersCollectionEnabled}, NomineesCollectionEnabled={NomineesCollectionEnabled}, IncludeWinnersInNominees={IncludeWinnersInNominees}.",
            configuration.EnableScheduledRefresh,
            configuration.EnableOscarEnrichment,
            configuration.RefreshBatchSize,
            configuration.CacheDurationHours,
            !string.IsNullOrWhiteSpace(configuration.OmdbApiKey),
            configuration.CreateOscarWinnersCollection,
            configuration.CreateOscarNomineesCollection,
            configuration.IncludeWinnersInNomineesCollection);

        progress.Report(0);

        var result = await _libraryScanService.ScanLibraryAsync(
            new OscarLibraryScanRequest
            {
                Origin = "scheduled_task",
                MaxEligibleMoviesToProcess = Math.Max(1, configuration.RefreshBatchSize),
                RequireScheduledRefreshEnabled = true
            },
            cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Oscar metadata refresh task completed. Success={IsSuccess}, TotalMoviesFound={TotalMoviesFound}, MoviesEligible={MoviesEligible}, MoviesProcessed={MoviesProcessed}, MoviesUpdated={MoviesUpdated}, MoviesSkipped={MoviesSkipped}, OmdbRequestsMade={OmdbRequestsMade}.",
            result.IsSuccess,
            result.TotalMoviesFound,
            result.MoviesEligible,
            result.MoviesProcessed,
            result.MoviesUpdated,
            result.MoviesSkipped,
            result.OmdbRequestsMade);

        progress.Report(75);
        var collectionResult = await _collectionSyncService.SyncCollectionsAsync("scheduled_task", cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "Oscar metadata refresh task collection sync completed. Success={IsSuccess}, CollectionsProcessed={CollectionsProcessed}, MoviesAdded={MoviesAdded}, MoviesRemoved={MoviesRemoved}, MoviesSkipped={MoviesSkipped}, ErrorCount={ErrorCount}.",
            collectionResult.IsSuccess,
            collectionResult.CollectionsProcessed,
            collectionResult.MoviesAdded,
            collectionResult.MoviesRemoved,
            collectionResult.MoviesSkipped,
            collectionResult.ErrorCount);

        progress.Report(100);
    }

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return
        [
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfoType.DailyTrigger,
                TimeOfDayTicks = TimeSpan.FromHours(3).Ticks
            }
        ];
    }
}
