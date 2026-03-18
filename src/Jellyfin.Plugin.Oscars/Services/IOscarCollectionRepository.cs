using Jellyfin.Plugin.Oscars.Models;

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

    Task<OscarCollectionArtworkApplyResult> SetPrimaryImageFromPluginResourceAsync(Guid collectionId, string resourceFileName, CancellationToken cancellationToken = default);

    Task<bool> DeleteCollectionIfExistsAsync(string collectionName, CancellationToken cancellationToken = default);
}
