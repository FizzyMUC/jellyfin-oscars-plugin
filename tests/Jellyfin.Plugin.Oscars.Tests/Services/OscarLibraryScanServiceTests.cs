using Jellyfin.Plugin.Oscars.Configuration;
using Jellyfin.Plugin.Oscars.Models;
using Jellyfin.Plugin.Oscars.Services;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.Oscars.Tests.Services;

public sealed class OscarLibraryScanServiceTests
{
    [Fact]
    public async Task ScanLibraryAsync_Fails_WhenEnrichmentIsDisabled()
    {
        var service = new OscarLibraryScanService(
            new StubPluginConfigurationService(new PluginConfiguration
            {
                EnableOscarEnrichment = false,
                OmdbApiKey = "test-key"
            }),
            new StubLibraryMovieRepository([]),
            new StubMovieProcessingService(),
            NullLogger<OscarLibraryScanService>.Instance);

        var result = await service.ScanLibraryAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal("Oscar enrichment is disabled. Enable it before running a library scan.", result.Message);
        Assert.Equal(1, result.ReasonCounts["enrichment_disabled"]);
    }

    [Fact]
    public async Task ScanLibraryAsync_Fails_WhenApiKeyIsMissing()
    {
        var service = new OscarLibraryScanService(
            new StubPluginConfigurationService(new PluginConfiguration
            {
                EnableOscarEnrichment = true,
                OmdbApiKey = string.Empty
            }),
            new StubLibraryMovieRepository([]),
            new StubMovieProcessingService(),
            NullLogger<OscarLibraryScanService>.Instance);

        var result = await service.ScanLibraryAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal("OMDb API key is missing. Enter your key before running a library scan.", result.Message);
        Assert.Equal(1, result.ReasonCounts["missing_api_key"]);
    }

    [Fact]
    public async Task ScanLibraryAsync_ReturnsSummaryCounts()
    {
        var movies = new[]
        {
            CreateMovie("tt1"),
            new Movie { Name = "Missing IMDb" },
            CreateMovie("tt2"),
            CreateMovie("tt3")
        };

        var processingService = new StubMovieProcessingService(
            OscarMovieProcessResult.Create(OscarMovieProcessOutcome.Updated, isEligible: true, wasUpdated: true, omdbRequestAttempted: true),
            OscarMovieProcessResult.Create(OscarMovieProcessOutcome.CacheHit, isEligible: true),
            OscarMovieProcessResult.Create(OscarMovieProcessOutcome.OmdbRequestFailure, isEligible: true, omdbRequestAttempted: true));

        var repository = new StubLibraryMovieRepository(movies);
        var service = new OscarLibraryScanService(
            new StubPluginConfigurationService(new PluginConfiguration
            {
                EnableOscarEnrichment = true,
                OmdbApiKey = "test-key"
            }),
            repository,
            processingService,
            NullLogger<OscarLibraryScanService>.Instance);

        var result = await service.ScanLibraryAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.TotalMoviesFound);
        Assert.Equal(3, result.MoviesEligible);
        Assert.Equal(3, result.MoviesProcessed);
        Assert.Equal(1, result.MoviesUpdated);
        Assert.Equal(3, result.MoviesSkipped);
        Assert.Equal(2, result.OmdbRequestsMade);
        Assert.Equal(1, result.ReasonCounts["missing_imdb_id"]);
        Assert.Equal(1, result.ReasonCounts["cache_hit"]);
        Assert.Equal(1, result.ReasonCounts["omdb_request_failure"]);
        Assert.Single(repository.PersistedMovies);
        Assert.Contains("Scan completed.", result.Message);
    }

    [Fact]
    public async Task ScanLibraryAsync_RespectsMaxEligibleMoviesToProcess()
    {
        var movies = new[]
        {
            CreateMovie("tt1"),
            CreateMovie("tt2"),
            CreateMovie("tt3")
        };

        var processingService = new StubMovieProcessingService(
            OscarMovieProcessResult.Create(OscarMovieProcessOutcome.CacheHit, isEligible: true),
            OscarMovieProcessResult.Create(OscarMovieProcessOutcome.Updated, isEligible: true, wasUpdated: true, omdbRequestAttempted: true));

        var repository = new StubLibraryMovieRepository(movies);
        var service = new OscarLibraryScanService(
            new StubPluginConfigurationService(new PluginConfiguration
            {
                EnableOscarEnrichment = true,
                OmdbApiKey = "test-key",
                RefreshBatchSize = 2
            }),
            repository,
            processingService,
            NullLogger<OscarLibraryScanService>.Instance);

        var result = await service.ScanLibraryAsync(new OscarLibraryScanRequest
        {
            Origin = "scheduled_task",
            MaxEligibleMoviesToProcess = 2
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.TotalMoviesFound);
        Assert.Equal(3, result.MoviesEligible);
        Assert.Equal(2, result.MoviesProcessed);
        Assert.Equal(1, result.MoviesUpdated);
        Assert.Equal(2, result.MoviesSkipped);
        Assert.Equal(1, result.ReasonCounts["cache_hit"]);
        Assert.Equal(1, result.ReasonCounts["batch_limit_reached"]);
        Assert.Single(repository.PersistedMovies);
    }

    [Fact]
    public async Task ScanLibraryAsync_Fails_WhenScheduledRefreshIsRequiredButDisabled()
    {
        var service = new OscarLibraryScanService(
            new StubPluginConfigurationService(new PluginConfiguration
            {
                EnableOscarEnrichment = true,
                EnableScheduledRefresh = false,
                OmdbApiKey = "test-key"
            }),
            new StubLibraryMovieRepository([]),
            new StubMovieProcessingService(),
            NullLogger<OscarLibraryScanService>.Instance);

        var result = await service.ScanLibraryAsync(new OscarLibraryScanRequest
        {
            Origin = "scheduled_task",
            RequireScheduledRefreshEnabled = true
        });

        Assert.False(result.IsSuccess);
        Assert.Equal("Scheduled refresh is disabled. Enable it before running the Oscar metadata refresh task.", result.Message);
        Assert.Equal(1, result.ReasonCounts["scheduled_refresh_disabled"]);
    }

    private static Movie CreateMovie(string imdbId)
    {
        var movie = new Movie();
        movie.SetProviderId(MetadataProvider.Imdb, imdbId);
        return movie;
    }

    private sealed class StubPluginConfigurationService : IPluginConfigurationService
    {
        private readonly PluginConfiguration _configuration;

        public StubPluginConfigurationService(PluginConfiguration configuration)
        {
            _configuration = configuration;
        }

        public PluginConfiguration GetCurrent() => _configuration;

        public PluginConfiguration Save(PluginConfiguration configuration)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubLibraryMovieRepository : ILibraryMovieRepository
    {
        private readonly IReadOnlyList<Movie> _movies;

        public StubLibraryMovieRepository(IReadOnlyList<Movie> movies)
        {
            _movies = movies;
        }

        public List<Movie> PersistedMovies { get; } = [];

        public IReadOnlyList<Movie> GetLocalMovies() => _movies;

        public Task PersistAsync(Movie movie, CancellationToken cancellationToken = default)
        {
            PersistedMovies.Add(movie);
            return Task.CompletedTask;
        }
    }

    private sealed class StubMovieProcessingService : IOscarMovieProcessingService
    {
        private readonly Queue<OscarMovieProcessResult> _results;

        public StubMovieProcessingService(params OscarMovieProcessResult[] results)
        {
            _results = new Queue<OscarMovieProcessResult>(results);
        }

        public Task<OscarMovieProcessResult> ProcessMovieAsync(Movie item, CancellationToken cancellationToken = default)
        {
            if (_results.Count == 0)
            {
                return Task.FromResult(OscarMovieProcessResult.Create(OscarMovieProcessOutcome.AlreadyUpToDate));
            }

            return Task.FromResult(_results.Dequeue());
        }
    }
}
