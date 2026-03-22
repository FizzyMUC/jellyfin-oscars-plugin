using Jellyfin.Plugin.Oscars.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Oscars.Services;

/// <summary>
/// Runs manual scans in the background so the initiating HTTP request can return immediately.
/// </summary>
public sealed class ManualOscarScanDispatcher : IManualOscarScanDispatcher
{
    private static readonly TimeSpan DuplicateRequestWindow = TimeSpan.FromSeconds(2);

    private readonly IPluginConfigurationService _configurationService;
    private readonly IOscarLibraryScanService _libraryScanService;
    private readonly ILogger<ManualOscarScanDispatcher> _logger;
    private readonly Lock _stateLock = new();
    private int _isRunning;
    private DateTimeOffset _startedAtUtc;

    public ManualOscarScanDispatcher(
        IPluginConfigurationService configurationService,
        IOscarLibraryScanService libraryScanService,
        ILogger<ManualOscarScanDispatcher> logger)
    {
        _configurationService = configurationService;
        _libraryScanService = libraryScanService;
        _logger = logger;
    }

    public ManualOscarScanStartResult StartManualScan()
    {
        _logger.LogInformation("Manual Oscar library scan requested.");
        var startedAtUtc = DateTimeOffset.UtcNow;
        lock (_stateLock)
        {
            if (_isRunning != 0 || startedAtUtc - _startedAtUtc < DuplicateRequestWindow)
            {
                _logger.LogInformation("Manual Oscar library scan request ignored because a scan is already running.");
                return new ManualOscarScanStartResult
                {
                    Started = false,
                    Message = "Oscar library scan is already running. Please wait a moment."
                };
            }

            _isRunning = 1;
            _startedAtUtc = startedAtUtc;
        }

        _ = Task.Run(RunManualScanAsync);
        _logger.LogInformation("Manual Oscar library scan start accepted.");
        return new ManualOscarScanStartResult
        {
            Started = true,
            Message = "Oscar library scan started in the background. Check the server logs for progress and results."
        };
    }

    private async Task RunManualScanAsync()
    {
        var configuration = _configurationService.GetCurrent();
        var configuredBatchSize = Math.Max(1, configuration.RefreshBatchSize);
        _logger.LogInformation(
            "Manual Oscar library scan started. ConfiguredBatchSize={ConfiguredBatchSize}, EffectiveBatchSize={EffectiveBatchSize}.",
            configuredBatchSize,
            configuredBatchSize);

        try
        {
            var result = await _libraryScanService.ScanLibraryAsync(
                new OscarLibraryScanRequest
                {
                    Origin = "manual_background",
                    MaxEligibleMoviesToProcess = configuredBatchSize
                },
                CancellationToken.None).ConfigureAwait(false);

            if (result.IsSuccess)
            {
                _logger.LogInformation(
                    "Manual Oscar library scan completed. MoviesProcessed={MoviesProcessed}, MoviesUpdated={MoviesUpdated}, MoviesNoChange={MoviesNoChange}, MoviesWithoutOscarData={MoviesWithoutOscarData}, MoviesFailed={MoviesFailed}, OmdbRequestsMade={OmdbRequestsMade}.",
                    result.MoviesProcessed,
                    result.MoviesUpdated,
                    GetReasonCount(result, "cache_hit") + GetReasonCount(result, "already_up_to_date"),
                    GetReasonCount(result, "no_oscar_related_result"),
                    GetReasonCount(result, "omdb_request_failure"),
                    result.OmdbRequestsMade);
            }
            else
            {
                _logger.LogWarning("Manual Oscar library scan failed. Message={Message}.", result.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Manual Oscar library scan failed with an exception.");
        }
        finally
        {
            var duplicateGuardDelay = GetRemainingDuplicateGuardDelay();
            if (duplicateGuardDelay > TimeSpan.Zero)
            {
                await Task.Delay(duplicateGuardDelay).ConfigureAwait(false);
            }

            lock (_stateLock)
            {
                _isRunning = 0;
            }
        }
    }

    private TimeSpan GetRemainingDuplicateGuardDelay()
    {
        lock (_stateLock)
        {
            var remainingDelay = DuplicateRequestWindow - (DateTimeOffset.UtcNow - _startedAtUtc);
            return remainingDelay > TimeSpan.Zero
                ? remainingDelay
                : TimeSpan.Zero;
        }
    }

    private static int GetReasonCount(OscarLibraryScanResult result, string reason)
    {
        return result.ReasonCounts.TryGetValue(reason, out var count)
            ? count
            : 0;
    }
}
