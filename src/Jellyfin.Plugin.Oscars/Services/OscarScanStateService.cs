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

        await using var stream = File.OpenRead(_statePath);
        var snapshot = await JsonSerializer.DeserializeAsync<OscarScanStateSnapshot>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        return snapshot ?? new OscarScanStateSnapshot();
    }

    private async Task SaveSnapshotUnsafeAsync(OscarScanStateSnapshot snapshot, CancellationToken cancellationToken)
    {
        var directoryPath = Path.GetDirectoryName(_statePath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        await using var stream = File.Create(_statePath);
        await JsonSerializer.SerializeAsync(stream, snapshot, JsonOptions, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("Persisted Oscar scan state for {ItemCount} items.", snapshot.Items.Count);
    }
}
