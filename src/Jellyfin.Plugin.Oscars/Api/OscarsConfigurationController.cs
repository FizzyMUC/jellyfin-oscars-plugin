using System.ComponentModel.DataAnnotations;
using Jellyfin.Plugin.Oscars.Services;
using MediaBrowser.Common.Api;
using MediaBrowser.Model.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Oscars.Api;

/// <summary>
/// Admin-only configuration endpoints for the Jellyfin Oscars plugin.
/// </summary>
[ApiController]
[Authorize(Policy = Policies.RequiresElevation)]
[Produces("application/json")]
[Route("Plugins/Jellyfin.Oscars")]
public sealed class OscarsConfigurationController : ControllerBase
{
    private readonly IOscarCollectionSyncService _collectionSyncService;
    private readonly IManualOscarScanDispatcher _manualOscarScanDispatcher;
    private readonly ILibraryMovieRepository _libraryMovieRepository;
    private readonly IOmdbClient _omdbClient;
    private readonly ILogger<OscarsConfigurationController> _logger;

    public OscarsConfigurationController(
        IOscarCollectionSyncService collectionSyncService,
        IManualOscarScanDispatcher manualOscarScanDispatcher,
        ILibraryMovieRepository libraryMovieRepository,
        IOmdbClient omdbClient,
        ILogger<OscarsConfigurationController> logger)
    {
        _collectionSyncService = collectionSyncService;
        _manualOscarScanDispatcher = manualOscarScanDispatcher;
        _libraryMovieRepository = libraryMovieRepository;
        _omdbClient = omdbClient;
        _logger = logger;
    }

    [HttpGet("TestOmdbConnection")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<OmdbConnectionTestResultDto>> TestOmdbConnection(
        [FromQuery] string? omdbApiKey,
        CancellationToken cancellationToken)
    {
        var hasQueryValue = Request.Query.ContainsKey("omdbApiKey");
        var requestedApiKey = hasQueryValue
            ? Request.Query["omdbApiKey"].ToString()
            : omdbApiKey;
        _logger.LogInformation("Executing OMDb connection test.");

        var result = await _omdbClient.TestConnectionAsync(requestedApiKey, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            _logger.LogInformation("OMDb connection test succeeded.");
        }
        else
        {
            _logger.LogWarning("OMDb connection test failed. ErrorCode={ErrorCode}, Message={Message}", result.ErrorCode, result.Message);
        }

        var payload = new OmdbConnectionTestResultDto(result.IsSuccess, result.Message, result.ErrorCode);
        return Ok(payload);
    }

    [HttpPost("ScanLibrary")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<LibraryScanResultDto> ScanLibrary()
    {
        _logger.LogInformation("Manual Oscar library scan endpoint invoked.");
        var result = _manualOscarScanDispatcher.StartManualScan();
        var payload = new LibraryScanResultDto(
            result.Started,
            result.Message,
            0,
            0,
            0,
            0,
            0,
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));

        if (!result.Started)
        {
            _logger.LogInformation("Manual Oscar library scan endpoint ignored because a scan is already in progress.");
            return Conflict(payload);
        }

        _logger.LogInformation("Manual Oscar library scan endpoint accepted and returned immediately.");
        return Accepted(payload);
    }

    [HttpPost("RebuildCollections")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<CollectionSyncResultDto>> RebuildCollections(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Manual Oscar collection rebuild endpoint invoked.");
        var result = await _collectionSyncService.SyncCollectionsAsync("manual_rebuild", cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "Manual Oscar collection rebuild completed. Success={IsSuccess}, CollectionsProcessed={CollectionsProcessed}, MoviesAdded={MoviesAdded}, MoviesRemoved={MoviesRemoved}, MoviesSkipped={MoviesSkipped}, ErrorCount={ErrorCount}.",
            result.IsSuccess,
            result.CollectionsProcessed,
            result.MoviesAdded,
            result.MoviesRemoved,
            result.MoviesSkipped,
            result.ErrorCount);

        return Ok(new CollectionSyncResultDto(
            result.IsSuccess,
            result.Message,
            result.CollectionsProcessed,
            result.MoviesAdded,
            result.MoviesRemoved,
            result.MoviesSkipped,
            result.ErrorCount,
            result.Errors.ToArray(),
            new Dictionary<string, int>(result.ReasonCounts, StringComparer.OrdinalIgnoreCase)));
    }

    [HttpGet("Libraries")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<MovieLibraryDto>> GetLibraries()
    {
        var libraries = _libraryMovieRepository.GetMovieLibraries()
            .Select(library => new MovieLibraryDto(library.Id, library.Name))
            .ToArray();
        return Ok(libraries);
    }

    [HttpPost("ConfigurationLog")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult LogConfigurationEvent([FromBody][Required] ConfigurationLogRequest request)
    {
        _logger.LogDebug(
            "Configuration page event {EventName}. HasApiKey={HasApiKey}, EnrichmentEnabled={EnableOscarEnrichment}, CacheDurationHours={CacheDurationHours}, ScheduledRefreshEnabled={EnableScheduledRefresh}, RefreshBatchSize={RefreshBatchSize}, WinnersCollectionEnabled={CreateOscarWinnersCollection}, NomineesCollectionEnabled={CreateOscarNomineesCollection}, IncludeWinnersInNominees={IncludeWinnersInNomineesCollection}, DefaultCollectionArtworkEnabled={SetDefaultArtworkForOscarCollections}, ExcludedCollectionLibraryCount={ExcludedCollectionLibraryCount}.",
            request.EventName,
            request.HasApiKey,
            request.EnableOscarEnrichment,
            request.CacheDurationHours,
            request.EnableScheduledRefresh,
            request.RefreshBatchSize,
            request.CreateOscarWinnersCollection,
            request.CreateOscarNomineesCollection,
            request.IncludeWinnersInNomineesCollection,
            request.SetDefaultArtworkForOscarCollections,
            request.ExcludedCollectionLibraryCount);

        return Ok();
    }

    public sealed record ConfigurationLogRequest(
        string EventName,
        bool HasApiKey,
        bool EnableOscarEnrichment,
        int CacheDurationHours,
        bool EnableScheduledRefresh,
        int RefreshBatchSize,
        bool CreateOscarWinnersCollection,
        bool CreateOscarNomineesCollection,
        bool IncludeWinnersInNomineesCollection,
        bool SetDefaultArtworkForOscarCollections,
        int ExcludedCollectionLibraryCount);

    public sealed record MovieLibraryDto(Guid Id, string Name);

    public sealed record OmdbConnectionTestResultDto(bool IsSuccess, string Message, string? ErrorCode);

    public sealed record LibraryScanResultDto(
        bool IsSuccess,
        string Message,
        int TotalMoviesFound,
        int MoviesEligible,
        int MoviesUpdated,
        int MoviesSkipped,
        int OmdbRequestsMade,
        IReadOnlyDictionary<string, int> ReasonCounts);

    public sealed record CollectionSyncResultDto(
        bool IsSuccess,
        string Message,
        int CollectionsProcessed,
        int MoviesAdded,
        int MoviesRemoved,
        int MoviesSkipped,
        int ErrorCount,
        IReadOnlyList<string> Errors,
        IReadOnlyDictionary<string, int> ReasonCounts);
}
