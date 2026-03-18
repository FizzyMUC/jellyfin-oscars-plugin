namespace Jellyfin.Plugin.Oscars.Models;

/// <summary>
/// Result of validating OMDb connectivity.
/// </summary>
public sealed record OmdbConnectionTestResult(bool IsSuccess, string Message, string? ErrorCode = null)
{
    public static OmdbConnectionTestResult Success(string message) => new(true, message);

    public static OmdbConnectionTestResult Failure(string message, string? errorCode = null) => new(false, message, errorCode);
}
