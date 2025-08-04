using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.SyncService.Models;

/// <summary>
/// Represents a POS transaction that needs to be synchronized to the central database
/// </summary>
public class PosTransaction
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// External transaction ID from the POS system
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string TransactionId { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(20)]
    public string StoreId { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(50)]
    public string ProductId { get; set; } = string.Empty;
    
    [Required]
    public string TransactionType { get; set; } = string.Empty; // Sale, Purchase, Adjustment, Return, etc.
    
    public int Quantity { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }
    
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Indicates if this transaction has been successfully synchronized to the central database
    /// </summary>
    public bool Synced { get; set; } = false;
    
    /// <summary>
    /// Number of times this record has been attempted for synchronization
    /// </summary>
    public int SyncAttempts { get; set; } = 0;
    
    /// <summary>
    /// Last time this record was attempted for synchronization
    /// </summary>
    public DateTime? LastSyncAttempt { get; set; }
    
    /// <summary>
    /// Last synchronization error message
    /// </summary>
    public string? LastSyncError { get; set; }
    
    /// <summary>
    /// When this record was successfully synchronized
    /// </summary>
    public DateTime? SyncedAt { get; set; }
    
    /// <summary>
    /// Additional metadata for the transaction
    /// </summary>
    public string? Metadata { get; set; }
    
    /// <summary>
    /// Reference number for this transaction (receipt number, etc.)
    /// </summary>
    [MaxLength(100)]
    public string? Reference { get; set; }
    
    /// <summary>
    /// Notes or additional information about the transaction
    /// </summary>
    public string? Notes { get; set; }
}

/// <summary>
/// Represents an inventory update that needs to be synchronized
/// </summary>
public class InventoryUpdate
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    [MaxLength(20)]
    public string StoreId { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(50)]
    public string ProductId { get; set; } = string.Empty;
    
    public int PreviousStock { get; set; }
    
    public int NewStock { get; set; }
    
    public int QuantityChange => NewStock - PreviousStock;
    
    public string UpdateType { get; set; } = string.Empty; // Sale, Restock, Adjustment, etc.
    
    public DateTime UpdateDate { get; set; } = DateTime.UtcNow;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Sync status fields
    /// </summary>
    public bool Synced { get; set; } = false;
    public int SyncAttempts { get; set; } = 0;
    public DateTime? LastSyncAttempt { get; set; }
    public string? LastSyncError { get; set; }
    public DateTime? SyncedAt { get; set; }
    
    /// <summary>
    /// Additional context for the inventory update
    /// </summary>
    public string? Reason { get; set; }
    
    /// <summary>
    /// Reference to the transaction that caused this inventory change
    /// </summary>
    public Guid? TransactionId { get; set; }
}

/// <summary>
/// Retry log for failed synchronization attempts
/// </summary>
public class SyncRetryLog
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public string EntityType { get; set; } = string.Empty; // PosTransaction, InventoryUpdate
    
    [Required]
    public Guid EntityId { get; set; }
    
    [Required]
    [MaxLength(20)]
    public string StoreId { get; set; } = string.Empty;
    
    public int AttemptNumber { get; set; }
    
    public DateTime AttemptTime { get; set; } = DateTime.UtcNow;
    
    [Required]
    public string ErrorMessage { get; set; } = string.Empty;
    
    public string? ErrorDetails { get; set; }
    
    /// <summary>
    /// HTTP status code if the error was related to API call
    /// </summary>
    public int? HttpStatusCode { get; set; }
    
    /// <summary>
    /// Whether this should be retried again
    /// </summary>
    public bool ShouldRetry { get; set; } = true;
    
    /// <summary>
    /// When to attempt the next retry
    /// </summary>
    public DateTime? NextRetryTime { get; set; }
}

/// <summary>
/// Configuration settings for the sync service
/// </summary>
public class SyncConfiguration
{
    [Key]
    public string Key { get; set; } = string.Empty;
    
    public string Value { get; set; } = string.Empty;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    public string? Description { get; set; }
}

/// <summary>
/// Tracks the health and status of the sync service
/// </summary>
public class SyncServiceStatus
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    [MaxLength(20)]
    public string StoreId { get; set; } = string.Empty;
    
    public DateTime LastSyncRun { get; set; }
    
    public int TransactionsSyncedInLastRun { get; set; }
    
    public int InventoryUpdatesSyncedInLastRun { get; set; }
    
    public int FailedSyncsInLastRun { get; set; }
    
    public TimeSpan LastRunDuration { get; set; }
    
    public string Status { get; set; } = "Running"; // Running, Stopped, Error
    
    public string? LastError { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Total number of pending transactions to sync
    /// </summary>
    public int PendingTransactions { get; set; }
    
    /// <summary>
    /// Total number of pending inventory updates to sync
    /// </summary>
    public int PendingInventoryUpdates { get; set; }
}
