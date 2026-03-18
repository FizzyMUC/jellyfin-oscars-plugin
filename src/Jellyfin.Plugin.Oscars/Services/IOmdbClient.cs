using Jellyfin.Plugin.Oscars.Models;

namespace Jellyfin.Plugin.Oscars.Services;

/// <summary>
/// Retrieves OMDb movie data by IMDb ID.
/// </summary>
public interface IOmdbClient
{
    Task<OmdbMovieData?> GetByImdbIdAsync(string imdbId, CancellationToken cancellationToken = default);

    Task<OmdbConnectionTestResult> TestConnectionAsync(string? apiKeyOverride = null, CancellationToken cancellationToken = default);
}
