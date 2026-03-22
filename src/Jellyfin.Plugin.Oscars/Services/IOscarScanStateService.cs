using Jellyfin.Plugin.Oscars.Models;

namespace Jellyfin.Plugin.Oscars.Services;

/// <summary>
/// Persistent per-item Oscar scan progress store.
/// </summary>
public interface IOscarScanStateService
{
    Task<OscarScanStateSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);

    Task RecordCompletedScanAttemptAsync(Guid itemId, DateTimeOffset completedAtUtc, CancellationToken cancellationToken = default);
}
