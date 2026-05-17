using System.Text.Json;
using Jellyfin.Plugin.Oscars.Models;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Oscars.Services;

/// <summary>
/// JSON-backed persistent scan progress for Oscar library refresh ordering.
/// </summary>
public sealed class OscarScanStateService : IOscarScanStateService
{
    private const int MoveAttemptCount = 3;
    private static readonly TimeSpan MoveRetryDelay = TimeSpan.FromMilliseconds(75);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly ILogger<OscarScanStateService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _statePath;

    public OscarScanStateService(IApplicationPaths applicationPaths, ILogger<OscarScanStateService> logger)
    {
        _logger = logger;
        _statePath = Path.Combine(applicationPaths.DataPath, "plugins", "Jellyfin.Oscars", "scanstate.json");
    }

    public async Task<OscarScanStateSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await LoadSnapshotUnsafeAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RecordCompletedScanAttemptAsync(Guid itemId, DateTimeOffset completedAtUtc, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var snapshot = await LoadSnapshotUnsafeAsync(cancellationToken).ConfigureAwait(false);
            snapshot.Items[itemId] = new OscarItemScanState
            {
                LastOscarScanUtc = completedAtUtc
            };

            await SaveSnapshotUnsafeAsync(snapshot, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<OscarScanStateSnapshot> LoadSnapshotUnsafeAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_statePath))
        {
            return new OscarScanStateSnapshot();
        }

        if (new FileInfo(_statePath).Length == 0)
        {
            _logger.LogWarning("Oscar scan state file at {StatePath} was empty. Resetting to a default snapshot.", _statePath);
            var emptySnapshot = new OscarScanStateSnapshot();
            await SaveSnapshotUnsafeAsync(emptySnapshot, cancellationToken).ConfigureAwait(false);
            return emptySnapshot;
        }

        try
        {
            await using var stream = File.OpenRead(_statePath);
            var snapshot = await JsonSerializer.DeserializeAsync<OscarScanStateSnapshot>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
            if (snapshot is not null)
            {
                return snapshot;
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Oscar scan state file at {StatePath} contained invalid JSON. Resetting to a default snapshot.", _statePath);
        }

        var defaultSnapshot = new OscarScanStateSnapshot();
        await SaveSnapshotUnsafeAsync(defaultSnapshot, cancellationToken).ConfigureAwait(false);
        return defaultSnapshot;
    }

    private async Task SaveSnapshotUnsafeAsync(OscarScanStateSnapshot snapshot, CancellationToken cancellationToken)
    {
        var directoryPath = Path.GetDirectoryName(_statePath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        var tempPath = CreateTempPath(directoryPath);
        try
        {
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, snapshot, JsonOptions, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            await MoveSnapshotIntoPlaceAsync(tempPath, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            TryDeleteTempFile(tempPath);
            throw;
        }

        _logger.LogDebug("Persisted Oscar scan state for {ItemCount} items.", snapshot.Items.Count);
    }

    private string CreateTempPath(string? directoryPath)
    {
        var tempFileName = $"{Path.GetFileName(_statePath)}.{Guid.NewGuid():N}.tmp";
        return string.IsNullOrWhiteSpace(directoryPath)
            ? tempFileName
            : Path.Combine(directoryPath, tempFileName);
    }

    private async Task MoveSnapshotIntoPlaceAsync(string tempPath, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MoveAttemptCount; attempt++)
        {
            try
            {
                File.Move(tempPath, _statePath, overwrite: true);
                return;
            }
            catch (IOException) when (attempt < MoveAttemptCount)
            {
                _logger.LogDebug(
                    "Oscar scan state replace attempt {Attempt} of {MaxAttempts} failed because the file was unavailable. Retrying.",
                    attempt,
                    MoveAttemptCount);
                await Task.Delay(MoveRetryDelay, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Oscar scan state could not be replaced at {StatePath} after {AttemptCount} attempts.",
                    _statePath,
                    MoveAttemptCount);
                throw;
            }
        }
    }

    private void TryDeleteTempFile(string tempPath)
    {
        try
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
        catch (IOException ex)
        {
            _logger.LogDebug(ex, "Could not delete temporary Oscar scan state file at {TempPath}.", tempPath);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogDebug(ex, "Could not delete temporary Oscar scan state file at {TempPath}.", tempPath);
        }
    }
}
