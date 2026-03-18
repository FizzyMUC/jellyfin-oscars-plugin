using Jellyfin.Plugin.Oscars.Models;

namespace Jellyfin.Plugin.Oscars.Services;

/// <summary>
/// Plugin-owned cache abstraction for Oscar metadata keyed by IMDb ID.
/// </summary>
public interface IOscarCacheService
{
    Task<OscarAwardInfo?> GetAsync(string imdbId, CancellationToken cancellationToken = default);

    Task SetAsync(string imdbId, OscarAwardInfo awardInfo, CancellationToken cancellationToken = default);

    Task InvalidateAsync(string imdbId, CancellationToken cancellationToken = default);
}
