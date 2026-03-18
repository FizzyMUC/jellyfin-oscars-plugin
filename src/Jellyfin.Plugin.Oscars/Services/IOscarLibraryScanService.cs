using Jellyfin.Plugin.Oscars.Models;

namespace Jellyfin.Plugin.Oscars.Services;

/// <summary>
/// Scans the local movie library and applies Oscar enrichment where eligible.
/// </summary>
public interface IOscarLibraryScanService
{
    Task<OscarLibraryScanResult> ScanLibraryAsync(OscarLibraryScanRequest? request = null, CancellationToken cancellationToken = default);
}
