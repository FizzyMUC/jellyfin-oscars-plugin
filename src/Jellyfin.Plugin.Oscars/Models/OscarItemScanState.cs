namespace Jellyfin.Plugin.Oscars.Models;

/// <summary>
/// Persistent per-item Oscar scan progress.
/// </summary>
public sealed class OscarItemScanState
{
    public DateTimeOffset? LastOscarScanUtc { get; set; }
}
