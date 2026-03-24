namespace Jellyfin.Plugin.Oscars.Models;

/// <summary>
/// Describes the current frontend badge integration status for the plugin.
/// </summary>
public sealed class FrontendBadgeIntegrationStatus
{
    public required FrontendBadgeIntegrationState State { get; init; }

    public required string Message { get; init; }

    public bool IsActive => State == FrontendBadgeIntegrationState.Active;
}
