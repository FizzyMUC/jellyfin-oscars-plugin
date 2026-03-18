using Jellyfin.Plugin.Oscars.Configuration;
using Jellyfin.Plugin.Oscars.Models;
using Jellyfin.Plugin.Oscars.Services;
using Jellyfin.Plugin.Oscars.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.Oscars.Tests.Tasks;

public sealed class OscarRefreshTaskTests
{
    [Fact]
    public void IsEnabled_ReflectsPluginConfiguration()
    {
        var task = new OscarRefreshTask(
            new StubPluginConfigurationService(new PluginConfiguration
            {
                EnableScheduledRefresh = true
            }),
            new StubOscarCollectionSyncService(),
            new StubOscarLibraryScanService(),
            NullLogger<OscarRefreshTask>.Instance);

        Assert.True(task.IsEnabled);
    }

    [Fact]
    public async Task ExecuteAsync_UsesConfiguredBatchSizeAndScheduledOrigin()
    {
        var scanService = new StubOscarLibraryScanService();
        var task = new OscarRefreshTask(
            new StubPluginConfigurationService(new PluginConfiguration
            {
                EnableScheduledRefresh = true,
                EnableOscarEnrichment = true,
                OmdbApiKey = "test-key",
                RefreshBatchSize = 7,
                CacheDurationHours = 24
            }),
            new StubOscarCollectionSyncService(),
            scanService,
            NullLogger<OscarRefreshTask>.Instance);

        await task.ExecuteAsync(new Progress<double>(), CancellationToken.None);

        Assert.NotNull(scanService.LastRequest);
        Assert.Equal("scheduled_task", scanService.LastRequest!.Origin);
        Assert.True(scanService.LastRequest.RequireScheduledRefreshEnabled);
        Assert.Equal(7, scanService.LastRequest.MaxEligibleMoviesToProcess);
    }

    [Fact]
    public void GetDefaultTriggers_ReturnsDailyTrigger()
    {
        var task = new OscarRefreshTask(
            new StubPluginConfigurationService(new PluginConfiguration()),
            new StubOscarCollectionSyncService(),
            new StubOscarLibraryScanService(),
            NullLogger<OscarRefreshTask>.Instance);

        var trigger = Assert.Single(task.GetDefaultTriggers());
        Assert.Equal(MediaBrowser.Model.Tasks.TaskTriggerInfoType.DailyTrigger, trigger.Type);
        Assert.Equal(TimeSpan.FromHours(3).Ticks, trigger.TimeOfDayTicks);
    }

    [Fact]
    public async Task ExecuteAsync_RunsCollectionSync_WhenCollectionSettingsAreEnabled()
    {
        var collectionSyncService = new StubOscarCollectionSyncService();
        var task = new OscarRefreshTask(
            new StubPluginConfigurationService(new PluginConfiguration
            {
                EnableScheduledRefresh = true,
                CreateOscarNomineesCollection = true
            }),
            collectionSyncService,
            new StubOscarLibraryScanService(),
            NullLogger<OscarRefreshTask>.Instance);

        await task.ExecuteAsync(new Progress<double>(), CancellationToken.None);

        Assert.Equal("scheduled_task", collectionSyncService.LastOrigin);
    }

    private sealed class StubPluginConfigurationService : IPluginConfigurationService
    {
        private readonly PluginConfiguration _configuration;

        public StubPluginConfigurationService(PluginConfiguration configuration)
        {
            _configuration = configuration;
        }

        public PluginConfiguration GetCurrent() => _configuration;

        public PluginConfiguration Save(PluginConfiguration configuration)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubOscarLibraryScanService : IOscarLibraryScanService
    {
        public OscarLibraryScanRequest? LastRequest { get; private set; }

        public Task<OscarLibraryScanResult> ScanLibraryAsync(OscarLibraryScanRequest? request = null, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new OscarLibraryScanResult
            {
                IsSuccess = true,
                Message = "ok"
            });
        }
    }

    private sealed class StubOscarCollectionSyncService : IOscarCollectionSyncService
    {
        public string? LastOrigin { get; private set; }

        public Task<OscarCollectionSyncResult> SyncCollectionsAsync(string origin = "manual_rebuild", CancellationToken cancellationToken = default)
        {
            LastOrigin = origin;
            return Task.FromResult(new OscarCollectionSyncResult
            {
                IsSuccess = true,
                Message = "ok"
            });
        }
    }
}
