using System.Text.RegularExpressions;
using Jellyfin.Plugin.Oscars.Models;

namespace Jellyfin.Plugin.Oscars.Services;

/// <summary>
/// First-pass parser for common OMDb Oscar phrases.
/// </summary>
public sealed partial class AwardsParser : IAwardsParser
{
    public OscarAwardInfo Parse(string? rawAwardsText, DateTimeOffset? lastUpdatedUtc = null)
    {
        var normalizedText = string.IsNullOrWhiteSpace(rawAwardsText)
            ? null
            : rawAwardsText.Trim();

        var wins = ExtractHighestCount(WonOscarRegex(), normalizedText);
        var explicitNominations = ExtractHighestCount(NominatedForOscarRegex(), normalizedText);
        var nominations = Math.Max(explicitNominations, wins);

        var status = wins > 0
            ? OscarStatus.Winner
            : nominations > 0
                ? OscarStatus.Nominated
                : OscarStatus.None;

        return new OscarAwardInfo
        {
            Status = status,
            RawAwardsText = normalizedText,
            OscarWinsCount = wins,
            OscarNominationsCount = nominations,
            LastUpdatedUtc = lastUpdatedUtc ?? DateTimeOffset.UtcNow
        };
    }

    private static int ExtractHighestCount(Regex regex, string? rawAwardsText)
    {
        if (string.IsNullOrWhiteSpace(rawAwardsText))
        {
            return 0;
        }

        var highest = 0;
        foreach (Match match in regex.Matches(rawAwardsText))
        {
            if (!match.Success)
            {
                continue;
            }

            if (int.TryParse(match.Groups["count"].Value, out var count))
            {
                highest = Math.Max(highest, count);
            }
        }

        return highest;
    }

    // Limitations:
    // - This only handles common OMDb phrases such as "Won X Oscar(s)" and
    //   "Nominated for X Oscar(s)".
    // - A win implies at least that many nominations, so nominations are normalized
    //   to be at least the number of wins.
    // - It does not infer Oscar state from more ambiguous wording or non-numeric phrasing.
    // - Additional parser coverage belongs in Phase 3 with dedicated tests.
    [GeneratedRegex(@"Won\s+(?<count>\d+)\s+Oscar[s]?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WonOscarRegex();

    [GeneratedRegex(@"Nominated\s+for\s+(?<count>\d+)\s+Oscar[s]?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NominatedForOscarRegex();
}
