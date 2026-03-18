using System.Collections.Concurrent;
using Jellyfin.Plugin.Oscars.Configuration;
using Jellyfin.Plugin.Oscars.Models;

namespace Jellyfin.Plugin.Oscars.Services;

/// <summary>
/// Minimal in-memory cache scaffold.
/// </summary>
public sealed class OscarCacheService : IOscarCacheService
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly IPluginConfigurationService _configurationService;

    public OscarCacheService(IPluginConfigurationService configurationService)
    {
        _configurationService = configurationService;
    }

    public Task<OscarAwardInfo?> GetAsync(string imdbId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imdbId);

        if (!_cache.TryGetValue(imdbId, out var entry))
        {
            return Task.FromResult<OscarAwardInfo?>(null);
        }

        if (entry.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            _cache.TryRemove(imdbId, out _);
            return Task.FromResult<OscarAwardInfo?>(null);
        }

        return Task.FromResult<OscarAwardInfo?>(entry.AwardInfo);
    }

    public Task SetAsync(string imdbId, OscarAwardInfo awardInfo, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imdbId);
        ArgumentNullException.ThrowIfNull(awardInfo);

        var configuration = _configurationService.GetCurrent();
        _cache[imdbId] = new CacheEntry(
            awardInfo,
            DateTimeOffset.UtcNow.AddHours(Math.Max(1, configuration.CacheDurationHours)));
        return Task.CompletedTask;
    }

    public Task InvalidateAsync(string imdbId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imdbId);

        _cache.TryRemove(imdbId, out _);
        return Task.CompletedTask;
    }

    private sealed record CacheEntry(OscarAwardInfo AwardInfo, DateTimeOffset ExpiresAtUtc);
}
