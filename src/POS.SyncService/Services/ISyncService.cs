using POS.SyncService.Models;

namespace POS.SyncService.Services;

/// <summary>
/// Interface for synchronizing local POS data to central database
/// </summary>
public interface ISyncService
{
    /// <summary>
    /// Synchronize all pending transactions and inventory updates
    /// </summary>
    Task<SyncResult> SynchronizeAllAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Synchronize pending transactions only
    /// </summary>
    Task<SyncResult> SynchronizeTransactionsAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Synchronize pending inventory updates only
    /// </summary>
    Task<SyncResult> SynchronizeInventoryUpdatesAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get current sync statistics
    /// </summary>
    Task<SyncStatistics> GetSyncStatisticsAsync();
    
    /// <summary>
    /// Health check for sync service
    /// </summary>
    Task<bool> IsHealthyAsync();
    
    /// <summary>
    /// Clean up old retry logs and completed transactions
    /// </summary>
    Task CleanupOldDataAsync(int daysToKeep = 30);
}

/// <summary>
/// Result of a synchronization operation
/// </summary>
public class SyncResult
{
    public bool Success { get; set; }
    public int TransactionsSynced { get; set; }
    public int InventoryUpdatesSynced { get; set; }
    public int FailedSyncs { get; set; }
    public TimeSpan Duration { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> Warnings { get; set; } = new();
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
}

/// <summary>
/// Current synchronization statistics
/// </summary>
public class SyncStatistics
{
    public int PendingTransactions { get; set; }
    public int PendingInventoryUpdates { get; set; }
    public int TotalRetryLogEntries { get; set; }
    public DateTime? LastSuccessfulSync { get; set; }
    public DateTime? LastSyncAttempt { get; set; }
    public string ServiceStatus { get; set; } = "Unknown";
    public int TotalTransactionsSyncedToday { get; set; }
    public int TotalInventoryUpdatesSyncedToday { get; set; }
    public int FailedSyncsToday { get; set; }
}
