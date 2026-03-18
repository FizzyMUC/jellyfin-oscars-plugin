namespace Jellyfin.Plugin.Oscars.Models;

/// <summary>
/// Normalized OMDb movie data used inside the plugin.
/// </summary>
public sealed class OmdbMovieData
{
    public string ImdbId { get; init; } = string.Empty;

    public string? Title { get; init; }

    public string? AwardsText { get; init; }
}
