using Jellyfin.Plugin.Oscars.Models;

namespace Jellyfin.Plugin.Oscars.Services;

/// <summary>
/// Parses raw awards text into Oscar-specific metadata.
/// </summary>
public interface IAwardsParser
{
    OscarAwardInfo Parse(string? rawAwardsText, DateTimeOffset? lastUpdatedUtc = null);
}
