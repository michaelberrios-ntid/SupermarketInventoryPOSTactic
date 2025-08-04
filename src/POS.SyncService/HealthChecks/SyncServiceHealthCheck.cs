using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using POS.SyncService.Configuration;
using POS.SyncService.Services;

namespace POS.SyncService;

/// <summary>
/// Health check for the sync service
/// </summary>
public class SyncServiceHealthCheck : IHealthCheck
{
    private readonly ISyncService _syncService;
    private readonly SyncOptions _options;
    private readonly ILogger<SyncServiceHealthCheck> _logger;

    public SyncServiceHealthCheck(
        ISyncService syncService,
        IOptions<SyncOptions> options,
        ILogger<SyncServiceHealthCheck> logger)
    {
        _syncService = syncService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var data = new Dictionary<string, object>();
            var healthyComponents = 0;
            var totalComponents = 0;
            var warnings = new List<string>();

            // Check sync service health
            totalComponents++;
            var syncHealthy = await _syncService.IsHealthyAsync();
            data["sync_service_healthy"] = syncHealthy;
            if (syncHealthy) healthyComponents++;
            else warnings.Add("Sync service is unhealthy");

            // Get sync statistics
            var stats = await _syncService.GetSyncStatisticsAsync();
            data["pending_transactions"] = stats.PendingTransactions;
            data["pending_inventory_updates"] = stats.PendingInventoryUpdates;
            data["failed_syncs_today"] = stats.FailedSyncsToday;
            data["last_successful_sync"] = stats.LastSuccessfulSync?.ToString("O") ?? "never";
            data["service_status"] = stats.ServiceStatus;

            // Determine overall health
            if (healthyComponents == totalComponents)
            {
                if (stats.PendingTransactions > 100 || stats.PendingInventoryUpdates > 100)
                {
                    warnings.Add($"High pending items: {stats.PendingTransactions} transactions, {stats.PendingInventoryUpdates} inventory updates");
                    return HealthCheckResult.Degraded("Service is healthy but has high pending items", null, data);
                }

                if (stats.FailedSyncsToday > 10)
                {
                    warnings.Add($"High number of failed syncs today: {stats.FailedSyncsToday}");
                    return HealthCheckResult.Degraded("Service is healthy but has many failed syncs", null, data);
                }

                return HealthCheckResult.Healthy("All components are healthy", data);
            }
            else if (healthyComponents > 0)
            {
                var warningMessage = string.Join("; ", warnings);
                return HealthCheckResult.Degraded($"Some components are unhealthy: {warningMessage}", null, data);
            }
            else
            {
                var errorMessage = string.Join("; ", warnings);
                return HealthCheckResult.Unhealthy($"All components are unhealthy: {errorMessage}", null, data);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed with exception");
            return HealthCheckResult.Unhealthy("Health check failed with exception", ex);
        }
    }
}
