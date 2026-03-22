using Jellyfin.Plugin.Oscars.Models;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;

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

    public IReadOnlyList<OscarLibraryMovieInfo> GetLocalMovies()
    {
        var movies = _libraryManager.RootFolder.RecursiveChildren
            .OfType<Movie>()
            .Where(movie => !movie.IsVirtualItem)
            .Select(movie => new OscarLibraryMovieInfo(movie, GetLibraryReferences(movie)))
            .ToList();

        return movies;
    }

    public IReadOnlyList<OscarLibraryReference> GetMovieLibraries()
    {
        var libraries = _libraryManager.GetVirtualFolders()
            .Where(folder => folder.CollectionType == CollectionTypeOptions.movies)
            .Select(folder => new
            {
                folder.Name,
                LibraryId = Guid.TryParse(folder.ItemId, out var libraryId) ? libraryId : Guid.Empty
            })
            .Where(folder => folder.LibraryId != Guid.Empty)
            .Select(folder => new OscarLibraryReference(folder.LibraryId, folder.Name))
            .OrderBy(folder => folder.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return libraries;
    }

    public Task PersistAsync(Movie movie, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(movie);
        return _libraryManager.UpdateItemAsync(movie, null!, ItemUpdateType.MetadataImport, cancellationToken);
    }

    private IReadOnlyList<OscarLibraryReference> GetLibraryReferences(Movie movie)
    {
        return _libraryManager.GetCollectionFolders(movie)
            .Where(folder => folder is not null)
            .Select(folder => new OscarLibraryReference(folder.Id, folder.Name))
            .DistinctBy(folder => folder.Id)
            .ToList();
    }
}
