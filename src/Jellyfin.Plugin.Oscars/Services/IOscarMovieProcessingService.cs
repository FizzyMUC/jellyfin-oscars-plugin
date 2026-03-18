using Jellyfin.Plugin.Oscars.Models;
using MediaBrowser.Controller.Entities.Movies;

namespace Jellyfin.Plugin.Oscars.Services;

/// <summary>
/// Applies the shared Oscar enrichment workflow to a single movie.
/// </summary>
public interface IOscarMovieProcessingService
{
    Task<OscarMovieProcessResult> ProcessMovieAsync(Movie item, CancellationToken cancellationToken = default);
}
