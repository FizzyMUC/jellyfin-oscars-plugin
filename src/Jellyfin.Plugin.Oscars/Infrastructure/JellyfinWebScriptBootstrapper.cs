using System.Text;

namespace Jellyfin.Plugin.Oscars.Infrastructure;

/// <summary>
/// Ensures the Oscar detail badge script is loaded by Jellyfin Web during normal app usage.
/// </summary>
public static class JellyfinWebScriptBootstrapper
{
    private const string BadgeScriptPath = "/plugins/Jellyfin.Oscars/scripts/oscarDetailBadge.js";
    private const string BadgeScriptTag = "<script defer=\"defer\" src=\"/plugins/Jellyfin.Oscars/scripts/oscarDetailBadge.js\"></script>";
    private const string HeadClosingTag = "</head>";

    public static void EnsureOscarDetailBadgeScriptIsLoaded(string? webPath)
    {
        if (string.IsNullOrWhiteSpace(webPath))
        {
            return;
        }

        var indexPath = Path.Combine(webPath, "index.html");
        if (!File.Exists(indexPath))
        {
            return;
        }

        var indexContents = File.ReadAllText(indexPath);
        if (indexContents.Contains(BadgeScriptPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var headClosingTagIndex = indexContents.IndexOf(HeadClosingTag, StringComparison.OrdinalIgnoreCase);
        if (headClosingTagIndex < 0)
        {
            return;
        }

        var updatedIndexContents = new StringBuilder(indexContents.Length + BadgeScriptTag.Length + Environment.NewLine.Length)
            .Append(indexContents.AsSpan(0, headClosingTagIndex))
            .Append(BadgeScriptTag)
            .Append(Environment.NewLine)
            .Append(indexContents.AsSpan(headClosingTagIndex))
            .ToString();

        File.WriteAllText(indexPath, updatedIndexContents);
    }
}
