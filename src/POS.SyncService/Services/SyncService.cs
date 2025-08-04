using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POS.SyncService.Data;
using POS.SyncService.Models;
using POS.SyncService.Configuration;
using System.Diagnostics;
using System.Text.Json;
using Polly;
using Polly.Retry;

namespace POS.SyncService.Services;

/// <summary>
/// Main implementation of the sync service with modifiability and availability tactics
/// </summary>
public class SyncService : ISyncService
{
    private readonly PosDbContext _dbContext;
    private readonly ILogger<SyncService> _logger;
    private readonly HttpClient _httpClient;
    private readonly SyncOptions _options;
    private readonly ResiliencePipeline<HttpResponseMessage> _retryPolicy;

    public SyncService(
        PosDbContext dbContext,
        ILogger<SyncService> logger,
        HttpClient httpClient,
        IOptions<SyncOptions> options)
    {
        _dbContext = dbContext;
        _logger = logger;
        _httpClient = httpClient;
        _options = options.Value;
        
        // Configure retry policy with exponential backoff using Polly v8 syntax
        _retryPolicy = new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = _options.MaxRetryAttempts,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromSeconds(1),
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .HandleResult(response => !response.IsSuccessStatusCode)
                    .Handle<HttpRequestException>(),
                OnRetry = args =>
                {
                    _logger.LogWarning("Retry {RetryCount} in {Delay}ms", 
                        args.AttemptNumber, args.RetryDelay.TotalMilliseconds);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    public async Task<SyncResult> SynchronizeAllAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new SyncResult
        {
            StartTime = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation("Starting full synchronization for store {StoreId}", _options.StoreId);

            // Synchronize transactions first
            var transactionResult = await SynchronizeTransactionsAsync(cancellationToken);
            result.TransactionsSynced = transactionResult.TransactionsSynced;
            result.FailedSyncs += transactionResult.FailedSyncs;
            result.Warnings.AddRange(transactionResult.Warnings);

            // Then synchronize inventory updates
            var inventoryResult = await SynchronizeInventoryUpdatesAsync(cancellationToken);
            result.InventoryUpdatesSynced = inventoryResult.InventoryUpdatesSynced;
            result.FailedSyncs += inventoryResult.FailedSyncs;
            result.Warnings.AddRange(inventoryResult.Warnings);

            result.Success = transactionResult.Success && inventoryResult.Success;
            
            // Update service status
            await UpdateServiceStatusAsync(result);

            _logger.LogInformation("Synchronization completed. Transactions: {Transactions}, Inventory: {Inventory}, Failed: {Failed}", 
                result.TransactionsSynced, result.InventoryUpdatesSynced, result.FailedSyncs);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Error during synchronization");
        }
        finally
        {
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
            result.EndTime = DateTime.UtcNow;
        }

        return result;
    }

    public async Task<SyncResult> SynchronizeTransactionsAsync(CancellationToken cancellationToken = default)
    {
        var result = new SyncResult { StartTime = DateTime.UtcNow };
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Get pending transactions
            var pendingTransactions = await _dbContext.PosTransactions
                .Where(t => !t.Synced && t.SyncAttempts < _options.MaxRetryAttempts)
                .OrderBy(t => t.CreatedAt)
                .Take(_options.BatchSize)
                .ToListAsync(cancellationToken);

            _logger.LogInformation("Found {Count} pending transactions to sync", pendingTransactions.Count);

            foreach (var transaction in pendingTransactions)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var syncSuccess = await SyncTransactionAsync(transaction, cancellationToken);
                if (syncSuccess)
                {
                    result.TransactionsSynced++;
                }
                else
                {
                    result.FailedSyncs++;
                }
            }

            result.Success = result.FailedSyncs == 0;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Error synchronizing transactions");
        }
        finally
        {
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
            result.EndTime = DateTime.UtcNow;
        }

        return result;
    }

    public async Task<SyncResult> SynchronizeInventoryUpdatesAsync(CancellationToken cancellationToken = default)
    {
        var result = new SyncResult { StartTime = DateTime.UtcNow };
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Get pending inventory updates
            var pendingUpdates = await _dbContext.InventoryUpdates
                .Where(u => !u.Synced && u.SyncAttempts < _options.MaxRetryAttempts)
                .OrderBy(u => u.CreatedAt)
                .Take(_options.BatchSize)
                .ToListAsync(cancellationToken);

            _logger.LogInformation("Found {Count} pending inventory updates to sync", pendingUpdates.Count);

            foreach (var update in pendingUpdates)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var syncSuccess = await SyncInventoryUpdateAsync(update, cancellationToken);
                if (syncSuccess)
                {
                    result.InventoryUpdatesSynced++;
                }
                else
                {
                    result.FailedSyncs++;
                }
            }

            result.Success = result.FailedSyncs == 0;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Error synchronizing inventory updates");
        }
        finally
        {
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
            result.EndTime = DateTime.UtcNow;
        }

        return result;
    }

    private async Task<bool> SyncTransactionAsync(PosTransaction transaction, CancellationToken cancellationToken)
    {
        try
        {
            var payload = new
            {
                transactionId = transaction.TransactionId,
                storeId = transaction.StoreId,
                type = transaction.TransactionType,
                timestamp = transaction.TransactionDate,
                productId = transaction.ProductId,
                quantity = transaction.Quantity,
                price = transaction.UnitPrice
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            // Send to central API with retry policy
            var response = await _retryPolicy.ExecuteAsync(async (cancellationToken) =>
            {
                return await _httpClient.PostAsync(_options.CentralApiEndpoint, content, cancellationToken);
            }, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                // Mark as synced
                transaction.Synced = true;
                transaction.SyncedAt = DateTime.UtcNow;
                transaction.LastSyncError = null;

                await _dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogDebug("Successfully synced transaction {TransactionId}", transaction.Id);
                return true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                transaction.LastSyncError = $"HTTP {response.StatusCode}: {errorContent}";
                
                await LogSyncRetryAsync("PosTransaction", transaction.Id, transaction.SyncAttempts, 
                    transaction.LastSyncError, (int)response.StatusCode, cancellationToken);
                
                await _dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogWarning("Failed to sync transaction {TransactionId}: {Error}", 
                    transaction.Id, transaction.LastSyncError);
                return false;
            }
        }
        catch (Exception ex)
        {
            transaction.LastSyncError = ex.Message;
            
            await LogSyncRetryAsync("PosTransaction", transaction.Id, transaction.SyncAttempts, 
                ex.Message, null, cancellationToken);
            
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogError(ex, "Exception while syncing transaction {TransactionId}", transaction.Id);
            return false;
        }
    }

    private async Task<bool> SyncInventoryUpdateAsync(InventoryUpdate update, CancellationToken cancellationToken)
    {
        try
        {
            update.SyncAttempts++;
            update.LastSyncAttempt = DateTime.UtcNow;

            // Create the payload for the central API
            var payload = new
            {
                storeId = update.StoreId,
                type = update.UpdateType,
                timestamp = update.UpdateDate,
                productId = update.ProductId,
                quantity = update.QuantityChange,
                price = 0m // Inventory updates might not have price
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            // Send to central API with retry policy
            var response = await _retryPolicy.ExecuteAsync(async (cancellationToken) =>
            {
                return await _httpClient.PostAsync(_options.CentralApiEndpoint, content, cancellationToken);
            }, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                // Mark as synced
                update.Synced = true;
                update.SyncedAt = DateTime.UtcNow;
                update.LastSyncError = null;

                await _dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogDebug("Successfully synced inventory update {UpdateId}", update.Id);
                return true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                update.LastSyncError = $"HTTP {response.StatusCode}: {errorContent}";
                
                await LogSyncRetryAsync("InventoryUpdate", update.Id, update.SyncAttempts, 
                    update.LastSyncError, (int)response.StatusCode, cancellationToken);
                
                await _dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogWarning("Failed to sync inventory update {UpdateId}: {Error}", 
                    update.Id, update.LastSyncError);
                return false;
            }
        }
        catch (Exception ex)
        {
            update.LastSyncError = ex.Message;
            
            await LogSyncRetryAsync("InventoryUpdate", update.Id, update.SyncAttempts, 
                ex.Message, null, cancellationToken);
            
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogError(ex, "Exception while syncing inventory update {UpdateId}", update.Id);
            return false;
        }
    }

    private async Task LogSyncRetryAsync(string entityType, Guid entityId, int attemptNumber, 
        string errorMessage, int? httpStatusCode, CancellationToken cancellationToken)
    {
        var retryLog = new SyncRetryLog
        {
            EntityType = entityType,
            EntityId = entityId,
            StoreId = _options.StoreId,
            AttemptNumber = attemptNumber,
            ErrorMessage = errorMessage,
            HttpStatusCode = httpStatusCode,
            ShouldRetry = attemptNumber < _options.MaxRetryAttempts,
            NextRetryTime = attemptNumber < _options.MaxRetryAttempts 
                ? DateTime.UtcNow.AddMinutes(_options.RetryDelayMinutes) 
                : null
        };

        _dbContext.SyncRetryLogs.Add(retryLog);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task UpdateServiceStatusAsync(SyncResult result)
    {
        var status = new SyncServiceStatus
        {
            StoreId = _options.StoreId,
            LastSyncRun = result.EndTime,
            TransactionsSyncedInLastRun = result.TransactionsSynced,
            InventoryUpdatesSyncedInLastRun = result.InventoryUpdatesSynced,
            FailedSyncsInLastRun = result.FailedSyncs,
            LastRunDuration = result.Duration,
            Status = result.Success ? "Running" : "Error",
            LastError = result.ErrorMessage,
            PendingTransactions = await _dbContext.PosTransactions.CountAsync(t => !t.Synced),
            PendingInventoryUpdates = await _dbContext.InventoryUpdates.CountAsync(u => !u.Synced)
        };

        _dbContext.SyncServiceStatuses.Add(status);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<SyncStatistics> GetSyncStatisticsAsync()
    {
        var today = DateTime.UtcNow.Date;
        
        return new SyncStatistics
        {
            PendingTransactions = await _dbContext.PosTransactions.CountAsync(t => !t.Synced),
            PendingInventoryUpdates = await _dbContext.InventoryUpdates.CountAsync(u => !u.Synced),
            TotalRetryLogEntries = await _dbContext.SyncRetryLogs.CountAsync(),
            LastSuccessfulSync = await _dbContext.SyncServiceStatuses
                .Where(s => s.StoreId == _options.StoreId && s.Status == "Running")
                .OrderByDescending(s => s.LastSyncRun)
                .Select(s => s.LastSyncRun)
                .FirstOrDefaultAsync(),
            LastSyncAttempt = await _dbContext.SyncServiceStatuses
                .Where(s => s.StoreId == _options.StoreId)
                .OrderByDescending(s => s.LastSyncRun)
                .Select(s => s.LastSyncRun)
                .FirstOrDefaultAsync(),
            ServiceStatus = "Running",
            TotalTransactionsSyncedToday = await _dbContext.PosTransactions
                .CountAsync(t => t.Synced && t.SyncedAt >= today),
            TotalInventoryUpdatesSyncedToday = await _dbContext.InventoryUpdates
                .CountAsync(u => u.Synced && u.SyncedAt >= today),
            FailedSyncsToday = await _dbContext.SyncRetryLogs
                .CountAsync(r => r.AttemptTime >= today)
        };
    }

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            // Check database connectivity
            await _dbContext.Database.CanConnectAsync();
            
            // Check if central API is reachable
            var response = await _httpClient.GetAsync(_options.HealthCheckEndpoint);
            
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return false;
        }
    }

    public async Task CleanupOldDataAsync(int daysToKeep = 30)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-daysToKeep);
        
        // Clean up old retry logs
        var oldRetryLogs = await _dbContext.SyncRetryLogs
            .Where(r => r.AttemptTime < cutoffDate)
            .ToListAsync();
        
        _dbContext.SyncRetryLogs.RemoveRange(oldRetryLogs);
        
        // Clean up old service status records
        var oldStatuses = await _dbContext.SyncServiceStatuses
            .Where(s => s.CreatedAt < cutoffDate)
            .ToListAsync();
        
        _dbContext.SyncServiceStatuses.RemoveRange(oldStatuses);
        
        await _dbContext.SaveChangesAsync();
        
        _logger.LogInformation("Cleaned up {RetryLogs} old retry logs and {Statuses} old status records", 
            oldRetryLogs.Count, oldStatuses.Count);
    }
}
