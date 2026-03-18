using Jellyfin.Plugin.Oscars.Configuration;

namespace Jellyfin.Plugin.Oscars.Services;

/// <summary>
/// Provides access to the current plugin configuration and persists updates.
/// </summary>
public interface IPluginConfigurationService
{
    PluginConfiguration GetCurrent();

    PluginConfiguration Save(PluginConfiguration configuration);
}
