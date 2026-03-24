namespace Jellyfin.Plugin.Oscars.Models;

/// <summary>
/// Represents the current Jellyfin Web badge integration state.
/// </summary>
public enum FrontendBadgeIntegrationState
{
    Active = 0,
    MissingDependency = 1,
    RegistrationFailed = 2
}
