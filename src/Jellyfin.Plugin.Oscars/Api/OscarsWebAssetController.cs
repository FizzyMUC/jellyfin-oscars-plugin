using System.Globalization;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Oscars.Api;

/// <summary>
/// Serves plugin frontend assets under a stable plugin path for Jellyfin Web.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("plugins/Jellyfin.Oscars")]
public sealed class OscarsWebAssetController : ControllerBase
{
    private static readonly IReadOnlyDictionary<string, string> ContentTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".css"] = "text/css; charset=utf-8",
            [".js"] = "application/javascript; charset=utf-8",
            [".png"] = "image/png",
            [".svg"] = "image/svg+xml"
        };

    private readonly ILogger<OscarsWebAssetController> _logger;

    public OscarsWebAssetController(ILogger<OscarsWebAssetController> logger)
    {
        _logger = logger;
    }

    [HttpGet("{**assetPath}")]
    public IActionResult GetAsset([FromRoute] string? assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return NotFound();
        }

        var pluginDirectory = Path.GetDirectoryName(typeof(Plugin).Assembly.Location);
        if (string.IsNullOrWhiteSpace(pluginDirectory))
        {
            _logger.LogWarning("Plugin asset request failed because the plugin directory could not be determined. AssetPath={AssetPath}.", assetPath);
            return NotFound();
        }

        var wwwrootDirectory = Path.GetFullPath(Path.Combine(pluginDirectory, "wwwroot"));
        var normalizedRelativePath = assetPath.Replace('/', Path.DirectorySeparatorChar);
        var candidatePath = Path.GetFullPath(Path.Combine(wwwrootDirectory, normalizedRelativePath));

        if (!candidatePath.StartsWith(wwwrootDirectory, StringComparison.Ordinal))
        {
            _logger.LogWarning("Rejected plugin asset request outside wwwroot. AssetPath={AssetPath}.", assetPath);
            return NotFound();
        }

        if (!System.IO.File.Exists(candidatePath))
        {
            _logger.LogDebug("Plugin asset not found. AssetPath={AssetPath}, CandidatePath={CandidatePath}.", assetPath, candidatePath);
            return NotFound();
        }

        var extension = Path.GetExtension(candidatePath);
        var contentType = ContentTypes.TryGetValue(extension, out var mappedContentType)
            ? mappedContentType
            : "application/octet-stream";

        Response.Headers.CacheControl = string.Create(
            CultureInfo.InvariantCulture,
            $"public,max-age={TimeSpan.FromHours(1).TotalSeconds:0}");

        _logger.LogDebug("Serving plugin asset {AssetPath} from {CandidatePath}.", assetPath, candidatePath);
        return PhysicalFile(candidatePath, contentType);
    }
}
