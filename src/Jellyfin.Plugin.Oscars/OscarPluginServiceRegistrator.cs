using Jellyfin.Plugin.Oscars.Configuration;
using Jellyfin.Plugin.Oscars.Providers;
using Jellyfin.Plugin.Oscars.Services;
using Jellyfin.Plugin.Oscars.Tasks;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Providers;
using Microsoft.Extensions.DependencyInjection;
using MediaBrowser.Model.Tasks;

namespace Jellyfin.Plugin.Oscars;

/// <summary>
/// Registers plugin services and metadata providers with Jellyfin.
/// </summary>
public sealed class OscarPluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<IPluginConfigurationService, PluginConfigurationService>();
        serviceCollection.AddSingleton<HttpClient>();
        serviceCollection.AddSingleton<IAwardsParser, AwardsParser>();
        serviceCollection.AddSingleton<IOmdbClient, OmdbClient>();
        serviceCollection.AddSingleton<IOscarCacheService, OscarCacheService>();
        serviceCollection.AddSingleton<OscarMovieTagService>();
        serviceCollection.AddSingleton<OscarMetadataEnricher>();
        serviceCollection.AddSingleton<IOscarMovieProcessingService, OscarMovieProcessingService>();
        serviceCollection.AddSingleton<ILibraryMovieRepository, JellyfinLibraryMovieRepository>();
        serviceCollection.AddSingleton<IOscarScanStateService, OscarScanStateService>();
        serviceCollection.AddSingleton<IManualOscarScanDispatcher, ManualOscarScanDispatcher>();
        serviceCollection.AddSingleton<IOscarCollectionRepository, JellyfinOscarCollectionRepository>();
        serviceCollection.AddSingleton<IOscarCollectionSyncService, OscarCollectionSyncService>();
        serviceCollection.AddSingleton<IOscarLibraryScanService, OscarLibraryScanService>();
        serviceCollection.AddSingleton<OscarRefreshTask>();
        serviceCollection.AddSingleton<IScheduledTask>(sp => sp.GetRequiredService<OscarRefreshTask>());
        serviceCollection.AddSingleton<OscarMovieMetadataProvider>();
        serviceCollection.AddSingleton<ICustomMetadataProvider<MediaBrowser.Controller.Entities.Movies.Movie>>(sp => sp.GetRequiredService<OscarMovieMetadataProvider>());
        serviceCollection.AddSingleton<IMetadataProvider>(sp => sp.GetRequiredService<OscarMovieMetadataProvider>());
    }
}
