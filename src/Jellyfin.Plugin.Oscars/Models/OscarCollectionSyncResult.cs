namespace Jellyfin.Plugin.Oscars.Models;

/// <summary>
/// Summary result for syncing Oscar collections.
/// </summary>
public sealed class OscarCollectionSyncResult
{
    public bool IsSuccess { get; init; }

    public string Message { get; init; } = string.Empty;

    public int CollectionsProcessed { get; init; }

    public int MoviesAdded { get; init; }

    public int MoviesRemoved { get; init; }

    public int MoviesSkipped { get; init; }

    public int ErrorCount { get; init; }

    public IReadOnlyDictionary<string, int> ReasonCounts { get; init; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> Errors { get; init; } = [];
}
