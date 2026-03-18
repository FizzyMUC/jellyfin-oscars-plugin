using Jellyfin.Plugin.Oscars.Models;
using Jellyfin.Plugin.Oscars.Providers;
using MediaBrowser.Controller.Entities.Movies;

namespace Jellyfin.Plugin.Oscars.Services;

/// <summary>
/// Applies the MVP Oscar tags to a movie.
/// </summary>
public sealed class OscarMovieTagService
{
    public bool ApplyOscarTags(Movie item, OscarStatus status)
    {
        ArgumentNullException.ThrowIfNull(item);

        var tags = item.Tags ?? [];
        var updatedTags = tags
            .Where(tag => !string.Equals(tag, OscarMovieMetadataProvider.OscarWinnerTag, StringComparison.OrdinalIgnoreCase))
            .Where(tag => !string.Equals(tag, OscarMovieMetadataProvider.OscarNominatedTag, StringComparison.OrdinalIgnoreCase))
            .ToList();

        switch (status)
        {
            case OscarStatus.Winner:
                updatedTags.Add(OscarMovieMetadataProvider.OscarWinnerTag);
                break;
            case OscarStatus.Nominated:
                updatedTags.Add(OscarMovieMetadataProvider.OscarNominatedTag);
                break;
        }

        var normalizedTags = updatedTags
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (tags.SequenceEqual(normalizedTags, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        item.Tags = normalizedTags;
        return true;
    }
}
