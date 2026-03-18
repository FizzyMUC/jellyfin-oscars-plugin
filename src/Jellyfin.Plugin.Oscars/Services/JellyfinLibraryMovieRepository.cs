using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.Oscars.Services;

/// <summary>
/// Jellyfin-backed movie enumeration and persistence for library-wide Oscar scans.
/// </summary>
public sealed class JellyfinLibraryMovieRepository : ILibraryMovieRepository
{
    private readonly ILibraryManager _libraryManager;

    public JellyfinLibraryMovieRepository(ILibraryManager libraryManager)
    {
        _libraryManager = libraryManager;
    }

    public IReadOnlyList<Movie> GetLocalMovies()
    {
        var movies = _libraryManager.RootFolder.RecursiveChildren
            .OfType<Movie>()
            .Where(movie => !movie.IsVirtualItem)
            .ToList();

        return movies;
    }

    public Task PersistAsync(Movie movie, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(movie);
        return _libraryManager.UpdateItemAsync(movie, null!, ItemUpdateType.MetadataImport, cancellationToken);
    }
}
