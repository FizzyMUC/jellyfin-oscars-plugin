namespace Jellyfin.Plugin.Oscars.Models;

/// <summary>
/// Outcome for applying default artwork to an Oscar collection.
/// </summary>
public sealed class OscarCollectionArtworkApplyResult
{
    public bool Applied { get; init; }

    public bool AlreadyPresent { get; init; }

    public bool FileMissing { get; init; }

    public bool CollectionMissing { get; init; }
}
