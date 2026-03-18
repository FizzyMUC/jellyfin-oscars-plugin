namespace Jellyfin.Plugin.Oscars.Models;

/// <summary>
/// Result of processing a single movie for Oscar enrichment.
/// </summary>
public sealed class OscarMovieProcessResult
{
    public OscarMovieProcessOutcome Outcome { get; init; }

    public bool IsEligible { get; init; }

    public bool WasUpdated { get; init; }

    public bool OmdbRequestAttempted { get; init; }

    public static OscarMovieProcessResult Create(
        OscarMovieProcessOutcome outcome,
        bool isEligible = false,
        bool wasUpdated = false,
        bool omdbRequestAttempted = false)
    {
        return new OscarMovieProcessResult
        {
            Outcome = outcome,
            IsEligible = isEligible,
            WasUpdated = wasUpdated,
            OmdbRequestAttempted = omdbRequestAttempted
        };
    }
}
