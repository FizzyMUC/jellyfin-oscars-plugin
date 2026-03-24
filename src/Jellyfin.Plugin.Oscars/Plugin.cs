using Jellyfin.Plugin.Oscars.Configuration;
using Jellyfin.Plugin.Oscars.Infrastructure;
using Jellyfin.Plugin.Oscars.Models;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.Oscars;

/// <summary>
/// Main plugin entry point.
/// </summary>
public sealed class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    private static readonly Guid PluginId = Guid.Parse("c531afa3-de20-4055-aca5-a7cc43adf783");

    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        FrontendBadgeIntegrationStatus = JavaScriptInjectorRegistration.TryRegisterOscarBadgeScript(AssemblyFilePath, PluginId, Name, Version);
        Instance = this;
    }

    public static Plugin? Instance { get; private set; }

    public static FrontendBadgeIntegrationStatus FrontendBadgeIntegrationStatus { get; private set; } =
        new()
        {
            State = FrontendBadgeIntegrationState.RegistrationFailed,
            Message = "Inactive: Frontend badge integration has not completed initialization yet."
        };

    public override string Name => "Jellyfin Oscars";

    public override Guid Id => PluginId;

    public IEnumerable<PluginPageInfo> GetPages()
    {
        return
        [
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.configPage.html"
            }
        ];
    }
}
