namespace Jellyfin.Plugin.Oscars.Models;

/// <summary>
/// Stable Jellyfin movie library identifier and display name.
/// </summary>
public sealed record OscarLibraryReference(Guid Id, string Name);
