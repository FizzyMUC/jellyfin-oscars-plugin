using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Oscars.Configuration;

/// <summary>
/// Plugin configuration for the Oscar enrichment MVP.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    public string OmdbApiKey { get; set; } = string.Empty;

    public int CacheDurationHours { get; set; } = 168;

    public bool EnableOscarEnrichment { get; set; } = true;

    public bool EnableScheduledRefresh { get; set; }

    public int RefreshBatchSize { get; set; } = 100;

    public bool CreateOscarWinnersCollection { get; set; }

    public bool CreateOscarNomineesCollection { get; set; }

    public bool IncludeWinnersInNomineesCollection { get; set; }

    public bool SetDefaultArtworkForOscarCollections { get; set; } = true;
}
