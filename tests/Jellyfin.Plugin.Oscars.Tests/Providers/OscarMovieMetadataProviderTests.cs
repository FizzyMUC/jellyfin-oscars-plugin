using Jellyfin.Plugin.Oscars.Models;
using Jellyfin.Plugin.Oscars.Providers;
using Jellyfin.Plugin.Oscars.Services;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.Oscars.Tests.Providers;

public sealed class OscarMovieMetadataProviderTests
{
    [Fact]
    public async Task FetchAsync_AddsWinnerTag_WhenMovieWinsOscar()
    {
        var provider = CreateProvider(new OscarAwardInfo { Status = OscarStatus.Winner });
        var movie = CreateMovie("tt0111161");

        var result = await provider.FetchAsync(movie, new MetadataRefreshOptions(new DirectoryService()), CancellationToken.None);

        Assert.Equal(ItemUpdateType.MetadataImport, result);
        Assert.Contains(OscarMovieMetadataProvider.OscarWinnerTag, movie.Tags);
        Assert.DoesNotContain(OscarMovieMetadataProvider.OscarNominatedTag, movie.Tags);
    }

    [Fact]
    public async Task FetchAsync_AddsNominatedTag_WhenMovieIsOscarNominated()
    {
        var provider = CreateProvider(new OscarAwardInfo { Status = OscarStatus.Nominated });
        var movie = CreateMovie("tt0137523");

        var result = await provider.FetchAsync(movie, new MetadataRefreshOptions(new DirectoryService()), CancellationToken.None);

        Assert.Equal(ItemUpdateType.MetadataImport, result);
        Assert.Contains(OscarMovieMetadataProvider.OscarNominatedTag, movie.Tags);
        Assert.DoesNotContain(OscarMovieMetadataProvider.OscarWinnerTag, movie.Tags);
    }

    [Fact]
    public async Task FetchAsync_RemovesOscarTags_WhenNoOscarDataIsAvailable()
    {
        var provider = CreateProvider(null);
        var movie = CreateMovie("tt0068646");
        movie.Tags = [OscarMovieMetadataProvider.OscarWinnerTag, "Crime"];

        var result = await provider.FetchAsync(movie, new MetadataRefreshOptions(new DirectoryService()), CancellationToken.None);

        Assert.Equal(ItemUpdateType.MetadataImport, result);
        Assert.DoesNotContain(OscarMovieMetadataProvider.OscarWinnerTag, movie.Tags);
        Assert.Contains("Crime", movie.Tags);
    }

    [Fact]
    public async Task FetchAsync_ReturnsNone_WhenMovieHasNoImdbId()
    {
        var provider = CreateProvider(new OscarAwardInfo { Status = OscarStatus.Winner });
        var movie = new Movie();

        var result = await provider.FetchAsync(movie, new MetadataRefreshOptions(new DirectoryService()), CancellationToken.None);

        Assert.Equal(ItemUpdateType.None, result);
    }

    private static OscarMovieMetadataProvider CreateProvider(OscarAwardInfo? awardInfo)
    {
        var omdbClient = new StubOmdbClient(awardInfo switch
        {
            { Status: OscarStatus.Winner } => "Won 1 Oscar.",
            { Status: OscarStatus.Nominated } => "Nominated for 1 Oscar.",
            _ => string.Empty
        });

        var processingService = new OscarMovieProcessingService(
            new OscarMetadataEnricher(
            omdbClient,
            new AwardsParser(),
            new StubOscarCacheService(),
            new StubPluginConfigurationService(new Jellyfin.Plugin.Oscars.Configuration.PluginConfiguration
            {
                OmdbApiKey = "test-key",
                EnableOscarEnrichment = true
            }),
            NullLogger<OscarMetadataEnricher>.Instance),
            new OscarMovieTagService(),
            NullLogger<OscarMovieProcessingService>.Instance);

        return new OscarMovieMetadataProvider(
            processingService,
            NullLogger<OscarMovieMetadataProvider>.Instance);
    }

    private static Movie CreateMovie(string imdbId)
    {
        var movie = new Movie();
        movie.SetProviderId(MetadataProvider.Imdb, imdbId);
        return movie;
    }

    private sealed class DirectoryService : IDirectoryService
    {
        public MediaBrowser.Model.IO.FileSystemMetadata[] GetFileSystemEntries(string path) => [];
        public List<MediaBrowser.Model.IO.FileSystemMetadata> GetDirectories(string path) => [];
        public List<MediaBrowser.Model.IO.FileSystemMetadata> GetFiles(string path) => [];
        public MediaBrowser.Model.IO.FileSystemMetadata GetFile(string path) => throw new FileNotFoundException(path);
        public MediaBrowser.Model.IO.FileSystemMetadata GetDirectory(string path) => throw new DirectoryNotFoundException(path);
        public MediaBrowser.Model.IO.FileSystemMetadata GetFileSystemEntry(string path) => throw new FileNotFoundException(path);
        public IReadOnlyList<string> GetFilePaths(string path) => [];
        public IReadOnlyList<string> GetFilePaths(string path, bool clearCache, bool recursive) => [];
        public bool IsAccessible(string path) => false;
    }

    private sealed class StubOmdbClient : IOmdbClient
    {
        private readonly string? _awardsText;

        public StubOmdbClient(string? awardsText)
        {
            _awardsText = awardsText;
        }

        public Task<OmdbMovieData?> GetByImdbIdAsync(string imdbId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<OmdbMovieData?>(_awardsText is null
                ? null
                : new OmdbMovieData
                {
                    ImdbId = imdbId,
                    AwardsText = _awardsText
                });
        }

        public Task<OmdbConnectionTestResult> TestConnectionAsync(string? apiKeyOverride = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(OmdbConnectionTestResult.Success("ok"));
        }
    }

    private sealed class StubOscarCacheService : IOscarCacheService
    {
        public Task<OscarAwardInfo?> GetAsync(string imdbId, CancellationToken cancellationToken = default) => Task.FromResult<OscarAwardInfo?>(null);
        public Task SetAsync(string imdbId, OscarAwardInfo awardInfo, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task InvalidateAsync(string imdbId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubPluginConfigurationService : IPluginConfigurationService
    {
        private readonly Jellyfin.Plugin.Oscars.Configuration.PluginConfiguration _configuration;

        public StubPluginConfigurationService(Jellyfin.Plugin.Oscars.Configuration.PluginConfiguration configuration)
        {
            _configuration = configuration;
        }

        public Jellyfin.Plugin.Oscars.Configuration.PluginConfiguration GetCurrent() => _configuration;

        public Jellyfin.Plugin.Oscars.Configuration.PluginConfiguration Save(Jellyfin.Plugin.Oscars.Configuration.PluginConfiguration configuration)
        {
            throw new NotSupportedException();
        }
    }
}
