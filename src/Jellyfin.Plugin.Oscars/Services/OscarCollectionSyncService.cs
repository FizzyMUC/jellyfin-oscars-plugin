using Jellyfin.Plugin.Oscars.Configuration;
using Jellyfin.Plugin.Oscars.Models;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace Jellyfin.Plugin.Oscars.Services;

/// <summary>
/// Shared workflow for syncing Oscar-specific Jellyfin collections from current local movies and Oscar tags.
/// </summary>
public sealed class OscarCollectionSyncService : IOscarCollectionSyncService
{
    public const string OscarWinnersCollectionName = "Oscar Winners";
    public const string OscarNomineesCollectionName = "Oscar Nominees";
    private const string OscarWinnersPosterFileName = "oscar-winners-portrait.png";
    private const string OscarNomineesPosterFileName = "oscar-nominees-portrait.png";
    private const string OscarWinnersLandscapeFileName = "oscar-winners.png";
    private const string OscarNomineesLandscapeFileName = "oscar-nominees.png";

    private readonly IPluginConfigurationService _configurationService;
    private readonly ILibraryMovieRepository _movieRepository;
    private readonly IOscarCollectionRepository _collectionRepository;
    private readonly ILogger<OscarCollectionSyncService> _logger;

    public OscarCollectionSyncService(
        IPluginConfigurationService configurationService,
        ILibraryMovieRepository movieRepository,
        IOscarCollectionRepository collectionRepository,
        ILogger<OscarCollectionSyncService> logger)
    {
        _configurationService = configurationService;
        _movieRepository = movieRepository;
        _collectionRepository = collectionRepository;
        _logger = logger;
    }

    public async Task<OscarCollectionSyncResult> SyncCollectionsAsync(string origin = "manual_rebuild", CancellationToken cancellationToken = default)
    {
        var configuration = _configurationService.GetCurrent();
        var winnersEnabled = configuration.CreateOscarWinnersCollection;
        var nomineesEnabled = configuration.CreateOscarNomineesCollection;
        var includeWinnersInNominees = configuration.IncludeWinnersInNomineesCollection;
        var defaultArtworkEnabled = configuration.SetDefaultArtworkForOscarCollections;
        var excludedLibraryIds = configuration.ExcludedOscarCollectionLibraryIds
            .Select(value => Guid.TryParse(value, out var libraryId) ? libraryId : Guid.Empty)
            .Where(value => value != Guid.Empty)
            .ToHashSet();

        _logger.LogInformation(
            "{Origin} Oscar collection sync started. WinnersEnabled={WinnersEnabled}, NomineesEnabled={NomineesEnabled}, IncludeWinnersInNominees={IncludeWinnersInNominees}, DefaultArtworkEnabled={DefaultArtworkEnabled}.",
            origin,
            winnersEnabled,
            nomineesEnabled,
            includeWinnersInNominees,
            defaultArtworkEnabled);

        var movies = _movieRepository.GetLocalMovies();
        var excludedLibraryNames = movies
            .SelectMany(movie => movie.Libraries)
            .Where(library => excludedLibraryIds.Contains(library.Id))
            .DistinctBy(library => library.Id)
            .OrderBy(library => library.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (excludedLibraryIds.Count > 0)
        {
            _logger.LogInformation(
                "{Origin} Oscar collection sync exclusions. ExcludedLibraryIds={ExcludedLibraryIds}, ExcludedLibraryNames={ExcludedLibraryNames}.",
                origin,
                string.Join(", ", excludedLibraryIds.OrderBy(id => id)),
                string.Join(", ", excludedLibraryNames.Select(library => library.Name)));
        }

        var winnerMovies = movies.Where(movie => IsOscarWinner(movie.Movie)).ToList();
        var nominatedMovies = movies.Where(movie => IsOscarNominated(movie.Movie)).ToList();
        var excludedWinnerMovieIds = winnerMovies
            .Where(movie => IsExcludedFromCollections(movie, excludedLibraryIds))
            .Select(movie => movie.Movie.Id);
        var excludedNomineeMovieIds = nominatedMovies
            .Where(movie => IsExcludedFromCollections(movie, excludedLibraryIds))
            .Select(movie => movie.Movie.Id);
        var excludedMovieIds = excludedWinnerMovieIds
            .Concat(excludedNomineeMovieIds)
            .ToHashSet();
        var excludedWinnerCount = winnerMovies.Count(movie => IsExcludedFromCollections(movie, excludedLibraryIds));
        var excludedNomineeCount = nominatedMovies.Count(movie => IsExcludedFromCollections(movie, excludedLibraryIds));
        winnerMovies = winnerMovies.Where(movie => !IsExcludedFromCollections(movie, excludedLibraryIds)).ToList();
        nominatedMovies = nominatedMovies.Where(movie => !IsExcludedFromCollections(movie, excludedLibraryIds)).ToList();
        var nomineesMembership = includeWinnersInNominees
            ? nominatedMovies
                .Concat(winnerMovies)
                .DistinctBy(movie => movie.Movie.Id)
                .ToList()
            : nominatedMovies;

        var reasonCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();
        var collectionsProcessed = 0;
        var moviesAdded = 0;
        var moviesRemoved = 0;
        var collectionExcludedMovieCount = excludedMovieIds.Count;
        if (collectionExcludedMovieCount > 0)
        {
            reasonCounts["collection_library_excluded"] = collectionExcludedMovieCount;
        }
        _logger.LogInformation("{Origin} Oscar collection sync skipped {SkippedCount} tagged candidate items due to collection library exclusions.", origin, collectionExcludedMovieCount);

        if (winnersEnabled)
        {
            var outcome = await SyncOneCollectionAsync(
                OscarWinnersCollectionName,
                winnerMovies.Select(movie => movie.Movie.Id).ToHashSet(),
                defaultArtworkEnabled,
                origin,
                cancellationToken).ConfigureAwait(false);
            collectionsProcessed += outcome.CollectionsProcessed;
            moviesAdded += outcome.MoviesAdded;
            moviesRemoved += outcome.MoviesRemoved;
            Merge(reasonCounts, outcome.ReasonCounts);
            errors.AddRange(outcome.Errors);
        }
        else
        {
            var outcome = await DeleteDisabledCollectionAsync(OscarWinnersCollectionName, origin, cancellationToken).ConfigureAwait(false);
            collectionsProcessed += outcome.CollectionsProcessed;
            moviesRemoved += outcome.MoviesRemoved;
            Merge(reasonCounts, outcome.ReasonCounts);
            errors.AddRange(outcome.Errors);
        }

        if (nomineesEnabled)
        {
            var outcome = await SyncOneCollectionAsync(
                OscarNomineesCollectionName,
                nomineesMembership.Select(movie => movie.Movie.Id).ToHashSet(),
                defaultArtworkEnabled,
                origin,
                cancellationToken).ConfigureAwait(false);
            collectionsProcessed += outcome.CollectionsProcessed;
            moviesAdded += outcome.MoviesAdded;
            moviesRemoved += outcome.MoviesRemoved;
            Merge(reasonCounts, outcome.ReasonCounts);
            errors.AddRange(outcome.Errors);
        }
        else
        {
            var outcome = await DeleteDisabledCollectionAsync(OscarNomineesCollectionName, origin, cancellationToken).ConfigureAwait(false);
            collectionsProcessed += outcome.CollectionsProcessed;
            moviesRemoved += outcome.MoviesRemoved;
            Merge(reasonCounts, outcome.ReasonCounts);
            errors.AddRange(outcome.Errors);
        }

        var matchedMovieIds = new HashSet<Guid>();
        matchedMovieIds.UnionWith(winnerMovies.Select(movie => movie.Movie.Id));
        matchedMovieIds.UnionWith(nomineesMembership.Select(movie => movie.Movie.Id));
        var moviesSkipped = Math.Max(0, movies.Count - matchedMovieIds.Count);
        if (moviesSkipped > 0)
        {
            reasonCounts["no_matching_oscar_tags"] = moviesSkipped;
        }

        var result = new OscarCollectionSyncResult
        {
            IsSuccess = errors.Count == 0,
            Message = BuildSuccessMessage(collectionsProcessed, moviesAdded, moviesRemoved, moviesSkipped, errors),
            CollectionsProcessed = collectionsProcessed,
            MoviesAdded = moviesAdded,
            MoviesRemoved = moviesRemoved,
            MoviesSkipped = moviesSkipped,
            ErrorCount = errors.Count,
            ReasonCounts = new Dictionary<string, int>(reasonCounts, StringComparer.OrdinalIgnoreCase),
            Errors = errors
        };

        _logger.LogInformation(
            "{Origin} Oscar collection sync completed. Success={IsSuccess}, CollectionsProcessed={CollectionsProcessed}, MoviesAdded={MoviesAdded}, MoviesRemoved={MoviesRemoved}, MoviesSkipped={MoviesSkipped}, ErrorCount={ErrorCount}.",
            origin,
            result.IsSuccess,
            result.CollectionsProcessed,
            result.MoviesAdded,
            result.MoviesRemoved,
            result.MoviesSkipped,
            result.ErrorCount);

        return result;
    }

    private async Task<OscarCollectionSyncResult> DeleteDisabledCollectionAsync(
        string collectionName,
        string origin,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("{Origin} Deleting {CollectionName} collection because setting disabled.", origin, collectionName);
            var deleted = await _collectionRepository.DeleteCollectionIfExistsAsync(collectionName, cancellationToken).ConfigureAwait(false);
            if (!deleted)
            {
                _logger.LogInformation("{Origin} {CollectionName}: collection not found, nothing to delete.", origin, collectionName);
                return new OscarCollectionSyncResult
                {
                    IsSuccess = true,
                    ReasonCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                    {
                        [ToReasonKey(collectionName, "disabled_not_found")] = 1
                    }
                };
            }

            return new OscarCollectionSyncResult
            {
                IsSuccess = true,
                CollectionsProcessed = 1,
                MoviesRemoved = 0,
                ReasonCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    [ToReasonKey(collectionName, "deleted_disabled")] = 1
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Origin} Failed to delete disabled Oscar collection {CollectionName}.", origin, collectionName);
            return new OscarCollectionSyncResult
            {
                IsSuccess = false,
                ErrorCount = 1,
                Errors = [$"{collectionName}: {ex.Message}"],
                ReasonCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    [ToReasonKey(collectionName, "delete_error")] = 1
                }
            };
        }
    }

    private async Task<OscarCollectionSyncResult> SyncOneCollectionAsync(
        string collectionName,
        IReadOnlySet<Guid> desiredItemIds,
        bool defaultArtworkEnabled,
        string origin,
        CancellationToken cancellationToken)
    {
        try
        {
            var collection = await _collectionRepository.GetCollectionAsync(collectionName, cancellationToken).ConfigureAwait(false);
            if (collection is null)
            {
                _logger.LogInformation("{Origin} {CollectionName} collection is enabled but missing. Recreating it now.", origin, collectionName);
                collection = await _collectionRepository.GetOrCreateCollectionAsync(collectionName, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("{Origin} Creating {CollectionName} collection.", origin, collectionName);
            }
            else
            {
                _logger.LogDebug("{Origin} Found existing {CollectionName} collection.", origin, collectionName);
            }

            await EnsureDefaultArtworkAsync(collection, defaultArtworkEnabled, origin, cancellationToken).ConfigureAwait(false);

            var itemsToAdd = desiredItemIds.Except(collection.ItemIds).ToArray();
            var itemsToRemove = collection.ItemIds.Except(desiredItemIds).ToArray();

            if (itemsToAdd.Length > 0)
            {
                await _collectionRepository.AddItemsAsync(collection.Id, itemsToAdd, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("{Origin} Oscar collection {CollectionName}: added {AddedCount} items.", origin, collectionName, itemsToAdd.Length);
            }

            if (itemsToRemove.Length > 0)
            {
                await _collectionRepository.RemoveItemsAsync(collection.Id, itemsToRemove, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("{Origin} Oscar collection {CollectionName}: removed {RemovedCount} items.", origin, collectionName, itemsToRemove.Length);
            }

            if (itemsToAdd.Length == 0 && itemsToRemove.Length == 0)
            {
                _logger.LogDebug("{Origin} Oscar collection {CollectionName} already up to date.", origin, collectionName);
            }

            var reasons = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                [ToReasonKey(collectionName, "desired")] = desiredItemIds.Count
            };

            return new OscarCollectionSyncResult
            {
                IsSuccess = true,
                CollectionsProcessed = 1,
                MoviesAdded = itemsToAdd.Length,
                MoviesRemoved = itemsToRemove.Length,
                ReasonCounts = reasons
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Origin} Oscar collection sync failed for collection {CollectionName}.", origin, collectionName);
            return new OscarCollectionSyncResult
            {
                IsSuccess = false,
                CollectionsProcessed = 1,
                ErrorCount = 1,
                Errors = [$"{collectionName}: {ex.Message}"],
                ReasonCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    [ToReasonKey(collectionName, "error")] = 1
                }
            };
        }
    }

    private async Task EnsureDefaultArtworkAsync(
        OscarCollectionInfo collection,
        bool defaultArtworkEnabled,
        string origin,
        CancellationToken cancellationToken)
    {
        if (!defaultArtworkEnabled)
        {
            _logger.LogDebug("{Origin} Oscar collection {CollectionName}: skipping artwork because default artwork is disabled.", origin, collection.Name);
            return;
        }

        var artworkFiles = GetArtworkFiles(collection.Name);
        if (artworkFiles is null)
        {
            return;
        }

        var shouldReplaceLegacyLandscapePrimary = await ShouldReplaceLegacyLandscapePrimaryAsync(collection, artworkFiles.Value.LandscapeFileName, cancellationToken).ConfigureAwait(false);
        if (!collection.HasPrimaryImage || shouldReplaceLegacyLandscapePrimary)
        {
            await ApplyDefaultArtworkAsync(
                collection,
                artworkFiles.Value.PosterFileName,
                ImageType.Primary,
                origin,
                cancellationToken,
                overwriteExisting: shouldReplaceLegacyLandscapePrimary).ConfigureAwait(false);
        }
        else
        {
            _logger.LogDebug("{Origin} Oscar collection {CollectionName}: keeping existing primary artwork.", origin, collection.Name);
        }

        if (!collection.HasThumbImage)
        {
            await ApplyDefaultArtworkAsync(collection, artworkFiles.Value.LandscapeFileName, ImageType.Thumb, origin, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        else
        {
            _logger.LogDebug("{Origin} Oscar collection {CollectionName}: keeping existing thumb artwork.", origin, collection.Name);
        }
    }

    private async Task ApplyDefaultArtworkAsync(
        OscarCollectionInfo collection,
        string resourceFileName,
        ImageType imageType,
        string origin,
        CancellationToken cancellationToken,
        bool overwriteExisting = false)
    {
        _logger.LogInformation(
            "{Origin} Applying default {ImageType} artwork to {CollectionName} collection from {ResourceFileName}.",
            origin,
            imageType,
            collection.Name,
            resourceFileName);

        var outcome = await _collectionRepository
            .SetImageFromPluginResourceAsync(collection.Id, resourceFileName, imageType, overwriteExisting, cancellationToken)
            .ConfigureAwait(false);

        if (outcome.Applied)
        {
            if (imageType == ImageType.Primary)
            {
                collection.HasPrimaryImage = true;
                collection.PrimaryImagePath = null;
            }

            if (imageType == ImageType.Thumb)
            {
                collection.HasThumbImage = true;
            }

            return;
        }

        if (outcome.AlreadyPresent)
        {
            _logger.LogDebug("{Origin} Oscar collection {CollectionName}: skipping {ImageType} artwork (already present).", origin, collection.Name, imageType);
            return;
        }

        if (outcome.FileMissing)
        {
            _logger.LogWarning("{Origin} Oscar collection {CollectionName}: {ImageType} artwork file {ResourceFileName} not found.", origin, collection.Name, imageType, resourceFileName);
            return;
        }

        if (outcome.CollectionMissing)
        {
            _logger.LogWarning("{Origin} Oscar collection {CollectionName}: collection not found when applying {ImageType} artwork.", origin, collection.Name, imageType);
        }
    }

    private async Task<bool> ShouldReplaceLegacyLandscapePrimaryAsync(
        OscarCollectionInfo collection,
        string legacyLandscapeFileName,
        CancellationToken cancellationToken)
    {
        if (!collection.HasPrimaryImage || string.IsNullOrWhiteSpace(collection.PrimaryImagePath) || !File.Exists(collection.PrimaryImagePath))
        {
            return false;
        }

        var pluginDirectory = Path.GetDirectoryName(typeof(Plugin).Assembly.Location);
        if (string.IsNullOrWhiteSpace(pluginDirectory))
        {
            return false;
        }

        var legacyLandscapePath = Path.Combine(pluginDirectory, "Resources", legacyLandscapeFileName);
        if (!File.Exists(legacyLandscapePath))
        {
            return false;
        }

        var currentPrimaryHash = await ComputeSha256Async(collection.PrimaryImagePath, cancellationToken).ConfigureAwait(false);
        var legacyLandscapeHash = await ComputeSha256Async(legacyLandscapePath, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(currentPrimaryHash, legacyLandscapeHash, StringComparison.Ordinal))
        {
            return false;
        }

        _logger.LogInformation(
            "Oscar collection {CollectionName}: replacing legacy landscape primary artwork with portrait artwork.",
            collection.Name);

        return true;
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        using var sha256 = SHA256.Create();
        var hash = await sha256.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    private static (string PosterFileName, string LandscapeFileName)? GetArtworkFiles(string collectionName)
        => collectionName switch
        {
            OscarWinnersCollectionName => (OscarWinnersPosterFileName, OscarWinnersLandscapeFileName),
            OscarNomineesCollectionName => (OscarNomineesPosterFileName, OscarNomineesLandscapeFileName),
            _ => null
        };

    private static bool IsOscarWinner(Movie movie)
        => HasTag(movie, "Oscar Winner");

    private static bool IsOscarNominated(Movie movie)
        => HasTag(movie, "Oscar Nominated");

    private static bool HasTag(Movie movie, string tag)
        => movie.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase);

    private static bool IsExcludedFromCollections(OscarLibraryMovieInfo movie, IReadOnlySet<Guid> excludedLibraryIds)
        => excludedLibraryIds.Count > 0 && movie.Libraries.Any(library => excludedLibraryIds.Contains(library.Id));

    private static void Merge(IDictionary<string, int> target, IReadOnlyDictionary<string, int> source)
    {
        foreach (var entry in source)
        {
            Increment(target, entry.Key, entry.Value);
        }
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

    private static string ToReasonKey(string collectionName, string suffix)
        => collectionName.Replace(' ', '_').ToLowerInvariant() + "_" + suffix;

    private static string BuildSuccessMessage(int collectionsProcessed, int moviesAdded, int moviesRemoved, int moviesSkipped, IReadOnlyList<string> errors)
    {
        var summary = $"Collection rebuild completed. Collections processed {collectionsProcessed}; movies added {moviesAdded}; movies removed {moviesRemoved}; movies skipped {moviesSkipped}; errors {errors.Count}.";
        if (errors.Count == 0)
        {
            return summary;
        }

        return summary + " Error details: " + string.Join(" | ", errors) + ".";
    }
}
