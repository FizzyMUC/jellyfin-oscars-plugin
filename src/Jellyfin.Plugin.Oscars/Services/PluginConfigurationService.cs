using Jellyfin.Plugin.Oscars.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Oscars.Services;

/// <summary>
/// Wraps plugin configuration access so runtime services always see current values.
/// </summary>
public sealed class PluginConfigurationService : IPluginConfigurationService
{
    private readonly ILogger<PluginConfigurationService> _logger;

    public PluginConfigurationService(ILogger<PluginConfigurationService> logger)
    {
        _logger = logger;
    }

    public PluginConfiguration GetCurrent()
    {
        return Plugin.Instance?.Configuration ?? new PluginConfiguration();
    }

    public PluginConfiguration Save(PluginConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var plugin = Plugin.Instance ?? throw new InvalidOperationException("Plugin instance is not available.");
        Normalize(configuration);
        plugin.SaveConfiguration(configuration);

        _logger.LogInformation(
            "Saved plugin configuration. EnrichmentEnabled={EnableOscarEnrichment}, HasApiKey={HasApiKey}, CacheDurationHours={CacheDurationHours}, ScheduledRefreshEnabled={EnableScheduledRefresh}, RefreshBatchSize={RefreshBatchSize}, WinnersCollectionEnabled={CreateOscarWinnersCollection}, NomineesCollectionEnabled={CreateOscarNomineesCollection}, IncludeWinnersInNominees={IncludeWinnersInNomineesCollection}, DefaultCollectionArtworkEnabled={SetDefaultArtworkForOscarCollections}.",
            plugin.Configuration.EnableOscarEnrichment,
            !string.IsNullOrWhiteSpace(plugin.Configuration.OmdbApiKey),
            plugin.Configuration.CacheDurationHours,
            plugin.Configuration.EnableScheduledRefresh,
            plugin.Configuration.RefreshBatchSize,
            plugin.Configuration.CreateOscarWinnersCollection,
            plugin.Configuration.CreateOscarNomineesCollection,
            plugin.Configuration.IncludeWinnersInNomineesCollection,
            plugin.Configuration.SetDefaultArtworkForOscarCollections);

        return plugin.Configuration;
    }

    private static void Normalize(PluginConfiguration configuration)
    {
        configuration.OmdbApiKey = configuration.OmdbApiKey.Trim();
        configuration.CacheDurationHours = Math.Max(1, configuration.CacheDurationHours);
        configuration.RefreshBatchSize = Math.Max(1, configuration.RefreshBatchSize);
    }
}
