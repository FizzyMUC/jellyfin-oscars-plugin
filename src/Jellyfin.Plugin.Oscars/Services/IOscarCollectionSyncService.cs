using Jellyfin.Plugin.Oscars.Models;

namespace Jellyfin.Plugin.Oscars.Services;

/// <summary>
/// Syncs Jellyfin Oscar collections from existing Oscar-tagged local movies.
/// </summary>
public interface IOscarCollectionSyncService
{
    Task<OscarCollectionSyncResult> SyncCollectionsAsync(string origin = "manual_rebuild", CancellationToken cancellationToken = default);
}
