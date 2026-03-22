using MediaBrowser.Controller.Entities.Movies;

namespace Jellyfin.Plugin.Oscars.Models;

/// <summary>
/// Local movie plus its containing Jellyfin libraries.
/// </summary>
public sealed record OscarLibraryMovieInfo(Movie Movie, IReadOnlyList<OscarLibraryReference> Libraries);
