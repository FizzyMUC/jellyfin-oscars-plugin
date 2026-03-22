namespace Jellyfin.Plugin.Oscars.Models;

/// <summary>
/// Persistent Oscar scan state keyed by Jellyfin item id.
/// </summary>
public sealed class OscarScanStateSnapshot
{
    public Dictionary<Guid, OscarItemScanState> Items { get; init; } = [];
}
