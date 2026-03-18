using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Oscars.Models;

/// <summary>
/// Minimal OMDb DTO for the MVP.
/// </summary>
public sealed class OmdbMovieResponse
{
    [JsonPropertyName("imdbID")]
    public string? ImdbId { get; init; }

    [JsonPropertyName("Title")]
    public string? Title { get; init; }

    [JsonPropertyName("Awards")]
    public string? Awards { get; init; }

    [JsonPropertyName("Response")]
    public string? Response { get; init; }

    [JsonPropertyName("Error")]
    public string? Error { get; init; }

    [JsonIgnore]
    public bool IsSuccess => string.Equals(Response, "True", StringComparison.OrdinalIgnoreCase);
}
