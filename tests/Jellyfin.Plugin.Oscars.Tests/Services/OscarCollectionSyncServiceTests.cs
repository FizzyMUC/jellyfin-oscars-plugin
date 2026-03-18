using Jellyfin.Plugin.Oscars.Configuration;
using Jellyfin.Plugin.Oscars.Models;
using Jellyfin.Plugin.Oscars.Services;
using MediaBrowser.Controller.Entities.Movies;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.Oscars.Tests.Services;

public sealed class OscarCollectionSyncServiceTests
{
    [Fact]
    public async Task SyncCollectionsAsync_Fails_WhenBothCollectionTogglesAreDisabled()
    {
        var service = new OscarCollectionSyncService(
            new StubPluginConfigurationService(new PluginConfiguration()),
            new StubLibraryMovieRepository([]),
            new StubOscarCollectionRepository(),
            NullLogger<OscarCollectionSyncService>.Instance);

        var result = await service.SyncCollectionsAsync();

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task SyncCollectionsAsync_SyncsEnabledCollections_AndRespectsWinnerInNomineeSetting()
    {
        var winnerMovie = CreateMovieWithTags("Winner", "Oscar Winner");
        var nominatedMovie = CreateMovieWithTags("Nominated", "Oscar Nominated");
        var plainMovie = new Movie { Name = "Plain" };

        var collectionRepository = new StubOscarCollectionRepository
        {
            Collections =
            {
                [OscarCollectionSyncService.OscarWinnersCollectionName] = new OscarCollectionInfo
                {
                    Id = Guid.NewGuid(),
                    Name = OscarCollectionSyncService.OscarWinnersCollectionName,
                    ItemIds = new HashSet<Guid> { plainMovie.Id }
                },
                [OscarCollectionSyncService.OscarNomineesCollectionName] = new OscarCollectionInfo
                {
                    Id = Guid.NewGuid(),
                    Name = OscarCollectionSyncService.OscarNomineesCollectionName,
                    ItemIds = new HashSet<Guid>()
                }
            }
        };

        var service = new OscarCollectionSyncService(
            new StubPluginConfigurationService(new PluginConfiguration
            {
                CreateOscarWinnersCollection = true,
                CreateOscarNomineesCollection = true,
                IncludeWinnersInNomineesCollection = true
            }),
            new StubLibraryMovieRepository([winnerMovie, nominatedMovie, plainMovie]),
            collectionRepository,
            NullLogger<OscarCollectionSyncService>.Instance);

        var result = await service.SyncCollectionsAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.CollectionsProcessed);
        Assert.Equal(3, result.MoviesAdded);
        Assert.Equal(1, result.MoviesRemoved);
        Assert.Equal(1, result.MoviesSkipped);

        Assert.Single(collectionRepository.Added[OscarCollectionSyncService.OscarWinnersCollectionName], winnerMovie.Id);
        Assert.Equal(
            new[] { winnerMovie.Id, nominatedMovie.Id }.OrderBy(id => id),
            collectionRepository.Added[OscarCollectionSyncService.OscarNomineesCollectionName].OrderBy(id => id));
        Assert.Single(collectionRepository.Removed[OscarCollectionSyncService.OscarWinnersCollectionName], plainMovie.Id);
    }

    [Fact]
    public async Task SyncCollectionsAsync_DoesNotPutWinnersInNominatedCollection_WhenOptionIsDisabled()
    {
        var winnerMovie = CreateMovieWithTags("Winner", "Oscar Winner");
        var nominatedMovie = CreateMovieWithTags("Nominated", "Oscar Nominated");

        var collectionRepository = new StubOscarCollectionRepository();
        var service = new OscarCollectionSyncService(
            new StubPluginConfigurationService(new PluginConfiguration
            {
                CreateOscarNomineesCollection = true,
                IncludeWinnersInNomineesCollection = false
            }),
            new StubLibraryMovieRepository([winnerMovie, nominatedMovie]),
            collectionRepository,
            NullLogger<OscarCollectionSyncService>.Instance);

        var result = await service.SyncCollectionsAsync();

        Assert.True(result.IsSuccess);
        Assert.Single(collectionRepository.Added[OscarCollectionSyncService.OscarNomineesCollectionName], nominatedMovie.Id);
        Assert.DoesNotContain(winnerMovie.Id, collectionRepository.Added[OscarCollectionSyncService.OscarNomineesCollectionName]);
    }

    [Fact]
    public async Task SyncCollectionsAsync_AppliesDefaultArtwork_WhenEnabled_AndImageIsMissing()
    {
        var winnerMovie = CreateMovieWithTags("Winner", "Oscar Winner");
        var collectionRepository = new StubOscarCollectionRepository
        {
            Collections =
            {
                [OscarCollectionSyncService.OscarWinnersCollectionName] = new OscarCollectionInfo
                {
                    Id = Guid.NewGuid(),
                    Name = OscarCollectionSyncService.OscarWinnersCollectionName,
                    HasPrimaryImage = false,
                    ItemIds = new HashSet<Guid>()
                }
            }
        };

        var service = new OscarCollectionSyncService(
            new StubPluginConfigurationService(new PluginConfiguration
            {
                CreateOscarWinnersCollection = true,
                SetDefaultArtworkForOscarCollections = true
            }),
            new StubLibraryMovieRepository([winnerMovie]),
            collectionRepository,
            NullLogger<OscarCollectionSyncService>.Instance);

        var result = await service.SyncCollectionsAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(["winners.png"], collectionRepository.AppliedArtwork[OscarCollectionSyncService.OscarWinnersCollectionName]);
    }

    [Fact]
    public async Task SyncCollectionsAsync_DeletesDisabledCollections_WhenPresent()
    {
        var collectionRepository = new StubOscarCollectionRepository
        {
            Collections =
            {
                [OscarCollectionSyncService.OscarWinnersCollectionName] = new OscarCollectionInfo
                {
                    Id = Guid.NewGuid(),
                    Name = OscarCollectionSyncService.OscarWinnersCollectionName,
                    ItemIds = new HashSet<Guid>()
                },
                [OscarCollectionSyncService.OscarNomineesCollectionName] = new OscarCollectionInfo
                {
                    Id = Guid.NewGuid(),
                    Name = OscarCollectionSyncService.OscarNomineesCollectionName,
                    ItemIds = new HashSet<Guid>()
                }
            }
        };

        var service = new OscarCollectionSyncService(
            new StubPluginConfigurationService(new PluginConfiguration()),
            new StubLibraryMovieRepository([]),
            collectionRepository,
            NullLogger<OscarCollectionSyncService>.Instance);

        var result = await service.SyncCollectionsAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(
            new[]
            {
                OscarCollectionSyncService.OscarNomineesCollectionName,
                OscarCollectionSyncService.OscarWinnersCollectionName
            }.OrderBy(name => name),
            collectionRepository.Deleted.OrderBy(name => name));
    }

    [Fact]
    public async Task SyncCollectionsAsync_RecreatesEnabledCollection_AfterItWasDeletedWhileDisabled()
    {
        var winnerMovie = CreateMovieWithTags("Winner", "Oscar Winner");
        var configuration = new PluginConfiguration
        {
            CreateOscarWinnersCollection = true
        };
        var configurationService = new StubPluginConfigurationService(configuration);
        var collectionRepository = new StubOscarCollectionRepository();
        var service = new OscarCollectionSyncService(
            configurationService,
            new StubLibraryMovieRepository([winnerMovie]),
            collectionRepository,
            NullLogger<OscarCollectionSyncService>.Instance);

        var initialResult = await service.SyncCollectionsAsync();
        Assert.True(initialResult.IsSuccess);
        Assert.Contains(OscarCollectionSyncService.OscarWinnersCollectionName, collectionRepository.Collections.Keys);

        configuration.CreateOscarWinnersCollection = false;
        var deleteResult = await service.SyncCollectionsAsync();
        Assert.True(deleteResult.IsSuccess);
        Assert.DoesNotContain(OscarCollectionSyncService.OscarWinnersCollectionName, collectionRepository.Collections.Keys);

        configuration.CreateOscarWinnersCollection = true;
        var recreateResult = await service.SyncCollectionsAsync();
        Assert.True(recreateResult.IsSuccess);
        Assert.Contains(OscarCollectionSyncService.OscarWinnersCollectionName, collectionRepository.Collections.Keys);
        Assert.Equal(
            new HashSet<Guid> { winnerMovie.Id },
            collectionRepository.Collections[OscarCollectionSyncService.OscarWinnersCollectionName].ItemIds);
    }

    [Fact]
    public async Task SyncCollectionsAsync_DoesNotOverwriteArtwork_WhenPrimaryImageAlreadyExists()
    {
        var winnerMovie = CreateMovieWithTags("Winner", "Oscar Winner");
        var collectionRepository = new StubOscarCollectionRepository
        {
            Collections =
            {
                [OscarCollectionSyncService.OscarWinnersCollectionName] = new OscarCollectionInfo
                {
                    Id = Guid.NewGuid(),
                    Name = OscarCollectionSyncService.OscarWinnersCollectionName,
                    HasPrimaryImage = true,
                    ItemIds = new HashSet<Guid>()
                }
            }
        };

        var service = new OscarCollectionSyncService(
            new StubPluginConfigurationService(new PluginConfiguration
            {
                CreateOscarWinnersCollection = true,
                SetDefaultArtworkForOscarCollections = true
            }),
            new StubLibraryMovieRepository([winnerMovie]),
            collectionRepository,
            NullLogger<OscarCollectionSyncService>.Instance);

        var result = await service.SyncCollectionsAsync();

        Assert.True(result.IsSuccess);
        Assert.False(collectionRepository.AppliedArtwork.ContainsKey(OscarCollectionSyncService.OscarWinnersCollectionName));
    }

    private static Movie CreateMovieWithTags(string name, params string[] tags)
    {
        var movie = new Movie
        {
            Id = Guid.NewGuid(),
            Name = name,
            Tags = tags.ToArray()
        };

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

        public IReadOnlyList<Movie> GetLocalMovies() => _movies;

        public Task PersistAsync(Movie movie, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubOscarCollectionRepository : IOscarCollectionRepository
    {
        public Dictionary<string, OscarCollectionInfo> Collections { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, List<Guid>> Added { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, List<Guid>> Removed { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, List<string>> AppliedArtwork { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<string> Deleted { get; } = [];

        public Task<OscarCollectionInfo?> GetCollectionAsync(string collectionName, CancellationToken cancellationToken = default)
        {
            Collections.TryGetValue(collectionName, out var collection);
            return Task.FromResult(collection);
        }

        public Task<OscarCollectionInfo> GetOrCreateCollectionAsync(string collectionName, CancellationToken cancellationToken = default)
        {
            if (!Collections.TryGetValue(collectionName, out var collection))
            {
                collection = new OscarCollectionInfo
                {
                    Id = Guid.NewGuid(),
                    Name = collectionName,
                    WasCreated = true,
                    ItemIds = new HashSet<Guid>()
                };
                Collections[collectionName] = collection;
            }

            return Task.FromResult(collection);
        }

        public Task AddItemsAsync(Guid collectionId, IEnumerable<Guid> itemIds, CancellationToken cancellationToken = default)
        {
            var collection = Collections.Values.Single(value => value.Id == collectionId);
            if (!Added.TryGetValue(collection.Name, out var added))
            {
                added = [];
                Added[collection.Name] = added;
            }

            var itemIdList = itemIds.ToList();
            added.AddRange(itemIdList);
            collection.ItemIds = collection.ItemIds.Concat(itemIdList).ToHashSet();
            return Task.CompletedTask;
        }

        public Task RemoveItemsAsync(Guid collectionId, IEnumerable<Guid> itemIds, CancellationToken cancellationToken = default)
        {
            var collection = Collections.Values.Single(value => value.Id == collectionId);
            if (!Removed.TryGetValue(collection.Name, out var removed))
            {
                removed = [];
                Removed[collection.Name] = removed;
            }

            var itemIdList = itemIds.ToList();
            removed.AddRange(itemIdList);
            collection.ItemIds = collection.ItemIds.Except(itemIdList).ToHashSet();
            return Task.CompletedTask;
        }

        public Task<OscarCollectionArtworkApplyResult> SetPrimaryImageFromPluginResourceAsync(Guid collectionId, string resourceFileName, CancellationToken cancellationToken = default)
        {
            var collection = Collections.Values.Single(value => value.Id == collectionId);
            if (!AppliedArtwork.TryGetValue(collection.Name, out var resources))
            {
                resources = [];
                AppliedArtwork[collection.Name] = resources;
            }

            resources.Add(resourceFileName);
            collection.HasPrimaryImage = true;
            return Task.FromResult(new OscarCollectionArtworkApplyResult
            {
                Applied = true
            });
        }

        public Task<bool> DeleteCollectionIfExistsAsync(string collectionName, CancellationToken cancellationToken = default)
        {
            if (!Collections.Remove(collectionName))
            {
                return Task.FromResult(false);
            }

            Deleted.Add(collectionName);
            return Task.FromResult(true);
        }
    }
}
