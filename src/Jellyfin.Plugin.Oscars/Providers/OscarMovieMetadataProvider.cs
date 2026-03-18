using Jellyfin.Plugin.Oscars.Models;
using Jellyfin.Plugin.Oscars.Services;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Oscars.Providers;

/// <summary>
/// Minimal movie metadata provider that stores derived Oscar state as tags.
/// </summary>
public sealed class OscarMovieMetadataProvider : ICustomMetadataProvider<Movie>
{
    public const string OscarWinnerTag = "Oscar Winner";
    public const string OscarNominatedTag = "Oscar Nominated";

    private readonly IOscarMovieProcessingService _movieProcessingService;
    private readonly ILogger<OscarMovieMetadataProvider> _logger;

    public OscarMovieMetadataProvider(
        IOscarMovieProcessingService movieProcessingService,
        ILogger<OscarMovieMetadataProvider> logger)
    {
        _movieProcessingService = movieProcessingService;
        _logger = logger;
    }

    public string Name => "Oscar Metadata";

    public async Task<ItemUpdateType> FetchAsync(
        Movie item,
        MetadataRefreshOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);

        var result = await _movieProcessingService.ProcessMovieAsync(item, cancellationToken).ConfigureAwait(false);
        if (!result.WasUpdated)
        {
            if (result.Outcome == OscarMovieProcessOutcome.MissingImdbId)
            {
                _logger.LogDebug("Skipping Oscar enrichment for movie {MovieName} because no IMDb ID exists.", item.Name);
            }

            return ItemUpdateType.None;
        }

        return ItemUpdateType.MetadataImport;
    }
}
