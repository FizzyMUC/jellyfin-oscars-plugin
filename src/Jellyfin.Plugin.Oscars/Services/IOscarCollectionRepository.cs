using Jellyfin.Plugin.Oscars.Models;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.Oscars.Services;

/// <summary>
/// Jellyfin collection persistence boundary for Oscar collection syncing.
/// </summary>
public interface IOscarCollectionRepository
{
    Task<OscarCollectionInfo?> GetCollectionAsync(string collectionName, CancellationToken cancellationToken = default);

    Task<OscarCollectionInfo> GetOrCreateCollectionAsync(string collectionName, CancellationToken cancellationToken = default);

    Task AddItemsAsync(Guid collectionId, IEnumerable<Guid> itemIds, CancellationToken cancellationToken = default);

    Task RemoveItemsAsync(Guid collectionId, IEnumerable<Guid> itemIds, CancellationToken cancellationToken = default);

    Task<OscarCollectionArtworkApplyResult> SetImageFromPluginResourceAsync(Guid collectionId, string resourceFileName, ImageType imageType, CancellationToken cancellationToken = default);

    Task<bool> DeleteCollectionIfExistsAsync(string collectionName, CancellationToken cancellationToken = default);
}
