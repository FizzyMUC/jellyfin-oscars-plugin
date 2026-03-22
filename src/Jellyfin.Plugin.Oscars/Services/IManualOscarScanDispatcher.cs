using Jellyfin.Plugin.Oscars.Models;

namespace Jellyfin.Plugin.Oscars.Services;

/// <summary>
/// Starts non-blocking manual Oscar library scans.
/// </summary>
public interface IManualOscarScanDispatcher
{
    ManualOscarScanStartResult StartManualScan();
}
