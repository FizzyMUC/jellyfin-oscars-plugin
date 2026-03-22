namespace Jellyfin.Plugin.Oscars.Models;

/// <summary>
/// Result returned when a manual scan is queued for background execution.
/// </summary>
public sealed class ManualOscarScanStartResult
{
    public bool Started { get; init; }

    public string Message { get; init; } = string.Empty;
}
