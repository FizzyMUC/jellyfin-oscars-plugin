namespace Jellyfin.Plugin.Oscars.Models;

/// <summary>
/// Summary result for a manual library-wide Oscar scan.
/// </summary>
public sealed class OscarLibraryScanResult
{
    public bool IsSuccess { get; init; }

    public string Message { get; init; } = string.Empty;

    public int TotalMoviesFound { get; init; }

    public int MoviesEligible { get; init; }

    public int MoviesUpdated { get; init; }

    public int MoviesSkipped { get; init; }

    public int MoviesProcessed { get; init; }

    public int OmdbRequestsMade { get; init; }

    public IReadOnlyDictionary<string, int> ReasonCounts { get; init; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
}
