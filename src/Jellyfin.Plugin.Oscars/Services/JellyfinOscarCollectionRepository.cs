using Jellyfin.Plugin.Oscars.Models;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.Oscars.Services;

/// <summary>
/// Jellyfin-backed Oscar collection persistence using the server collection manager.
/// </summary>
public sealed class JellyfinOscarCollectionRepository : IOscarCollectionRepository
{
    private readonly ICollectionManager _collectionManager;
    private readonly ILibraryManager _libraryManager;
    private readonly IProviderManager _providerManager;

    public JellyfinOscarCollectionRepository(ICollectionManager collectionManager, ILibraryManager libraryManager, IProviderManager providerManager)
    {
        _collectionManager = collectionManager;
        _libraryManager = libraryManager;
        _providerManager = providerManager;
    }

    public Task<OscarCollectionInfo?> GetCollectionAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionName);
        cancellationToken.ThrowIfCancellationRequested();

        var collection = FindCollection(collectionName);
        return Task.FromResult(collection is null ? null : CreateInfo(collection, wasCreated: false));
    }

    public async Task<OscarCollectionInfo> GetOrCreateCollectionAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionName);
        cancellationToken.ThrowIfCancellationRequested();

        var collection = FindCollection(collectionName);
        if (collection is not null)
        {
            return CreateInfo(collection, wasCreated: false);
        }

        var createdCollection = await _collectionManager.CreateCollectionAsync(new CollectionCreationOptions
        {
            Name = collectionName,
            ItemIdList = []
        }).ConfigureAwait(false);

        return CreateInfo(createdCollection, wasCreated: true);
    }

    public Task AddItemsAsync(Guid collectionId, IEnumerable<Guid> itemIds, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _collectionManager.AddToCollectionAsync(collectionId, itemIds);
    }

    public Task RemoveItemsAsync(Guid collectionId, IEnumerable<Guid> itemIds, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _collectionManager.RemoveFromCollectionAsync(collectionId, itemIds);
    }

    public Task<bool> DeleteCollectionIfExistsAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionName);
        cancellationToken.ThrowIfCancellationRequested();

        var collection = FindCollection(collectionName);
        if (collection is null)
        {
            return Task.FromResult(false);
        }

        _libraryManager.DeleteItem(collection, new DeleteOptions
        {
            DeleteFileLocation = false
        });

        return Task.FromResult(true);
    }

    public async Task<OscarCollectionArtworkApplyResult> SetImageFromPluginResourceAsync(Guid collectionId, string resourceFileName, ImageType imageType, bool overwriteExisting = false, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceFileName);
        cancellationToken.ThrowIfCancellationRequested();

        if (_libraryManager.GetItemById(collectionId) is not BoxSet collection)
        {
            return new OscarCollectionArtworkApplyResult
            {
                CollectionMissing = true
            };
        }

        if (!overwriteExisting && HasImage(collection, imageType))
        {
            return new OscarCollectionArtworkApplyResult
            {
                AlreadyPresent = true
            };
        }

        var pluginDirectory = Path.GetDirectoryName(typeof(Plugin).Assembly.Location);
        if (string.IsNullOrWhiteSpace(pluginDirectory))
        {
            return new OscarCollectionArtworkApplyResult
            {
                FileMissing = true
            };
        }

        var resourcePath = Path.Combine(pluginDirectory, "Resources", resourceFileName);
        if (!File.Exists(resourcePath))
        {
            return new OscarCollectionArtworkApplyResult
            {
                FileMissing = true
            };
        }

        await using var stream = File.OpenRead(resourcePath);
        await _providerManager.SaveImage(collection, stream, "image/png", imageType, null, cancellationToken).ConfigureAwait(false);
        return new OscarCollectionArtworkApplyResult
        {
            Applied = true
        };
    }

    private BoxSet? FindCollection(string collectionName)
    {
        var collection = _libraryManager.RootFolder.RecursiveChildren
            .OfType<BoxSet>()
            .FirstOrDefault(collection => string.Equals(collection.Name, collectionName, StringComparison.OrdinalIgnoreCase));

        if (collection is null)
        {
            return null;
        }

        return _libraryManager.GetItemById<BoxSet>(collection.Id);
    }

    private static OscarCollectionInfo CreateInfo(BoxSet collection, bool wasCreated)
    {
        var itemIds = collection.LinkedChildren
            .Where(item => item.ItemId.HasValue)
            .Select(item => item.ItemId)
            .Select(itemId => itemId!.Value)
            .ToHashSet();

        return new OscarCollectionInfo
        {
            Id = collection.Id,
            Name = collection.Name,
            WasCreated = wasCreated,
            HasPrimaryImage = collection.HasImage(ImageType.Primary, 0) || !string.IsNullOrWhiteSpace(collection.PrimaryImagePath),
            HasThumbImage = collection.HasImage(ImageType.Thumb, 0),
            PrimaryImagePath = collection.PrimaryImagePath,
            ItemIds = itemIds
        };
    }

    private static bool HasImage(BoxSet collection, ImageType imageType)
    {
        return imageType switch
        {
            ImageType.Primary => collection.HasImage(ImageType.Primary, 0) || !string.IsNullOrWhiteSpace(collection.PrimaryImagePath),
            ImageType.Thumb => collection.HasImage(ImageType.Thumb, 0),
            ImageType.Backdrop => collection.HasImage(ImageType.Backdrop, 0),
            _ => collection.HasImage(imageType, 0)
        };
    }
}
