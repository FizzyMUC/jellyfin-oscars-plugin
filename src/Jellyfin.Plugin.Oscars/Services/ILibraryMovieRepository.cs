using MediaBrowser.Controller.Entities.Movies;

namespace Jellyfin.Plugin.Oscars.Services;

/// <summary>
/// Jellyfin-specific access to local library movies for Oscar enrichment.
/// </summary>
public interface ILibraryMovieRepository
{
    IReadOnlyList<Movie> GetLocalMovies();

    Task PersistAsync(Movie movie, CancellationToken cancellationToken = default);
}
