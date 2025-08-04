using Microsoft.EntityFrameworkCore;
using POS.SyncService.Models;

namespace POS.SyncService.Data;

/// <summary>
/// Entity Framework DbContext for local SQLite POS database
/// </summary>
public class PosDbContext : DbContext
{
    public PosDbContext(DbContextOptions<PosDbContext> options) : base(options)
    {
    }

    public DbSet<PosTransaction> PosTransactions { get; set; }
    public DbSet<InventoryUpdate> InventoryUpdates { get; set; }
    public DbSet<SyncRetryLog> SyncRetryLogs { get; set; }
    public DbSet<SyncConfiguration> SyncConfigurations { get; set; }
    public DbSet<SyncServiceStatus> SyncServiceStatuses { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure PosTransaction
        modelBuilder.Entity<PosTransaction>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.StoreId, e.Synced })
                  .HasDatabaseName("IX_PosTransactions_StoreId_Synced");
            entity.HasIndex(e => e.TransactionDate)
                  .HasDatabaseName("IX_PosTransactions_TransactionDate");
            entity.HasIndex(e => new { e.StoreId, e.ProductId, e.TransactionDate })
                  .HasDatabaseName("IX_PosTransactions_Store_Product_Date");
            
            entity.Property(e => e.UnitPrice).HasPrecision(18, 2);
            entity.Property(e => e.TotalAmount).HasPrecision(18, 2);
        });

        // Configure InventoryUpdate
        modelBuilder.Entity<InventoryUpdate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.StoreId, e.Synced })
                  .HasDatabaseName("IX_InventoryUpdates_StoreId_Synced");
            entity.HasIndex(e => e.UpdateDate)
                  .HasDatabaseName("IX_InventoryUpdates_UpdateDate");
            entity.HasIndex(e => new { e.StoreId, e.ProductId, e.UpdateDate })
                  .HasDatabaseName("IX_InventoryUpdates_Store_Product_Date");
        });

        // Configure SyncRetryLog
        modelBuilder.Entity<SyncRetryLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.EntityType, e.EntityId })
                  .HasDatabaseName("IX_SyncRetryLogs_Entity");
            entity.HasIndex(e => e.AttemptTime)
                  .HasDatabaseName("IX_SyncRetryLogs_AttemptTime");
            entity.HasIndex(e => new { e.ShouldRetry, e.NextRetryTime })
                  .HasDatabaseName("IX_SyncRetryLogs_Retry");
        });

        // Configure SyncConfiguration
        modelBuilder.Entity<SyncConfiguration>(entity =>
        {
            entity.HasKey(e => e.Key);
            entity.HasIndex(e => e.Key).IsUnique();
        });

        // Configure SyncServiceStatus
        modelBuilder.Entity<SyncServiceStatus>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.StoreId)
                  .HasDatabaseName("IX_SyncServiceStatus_StoreId");
            entity.HasIndex(e => e.LastSyncRun)
                  .HasDatabaseName("IX_SyncServiceStatus_LastSyncRun");
        });

        // Seed default configuration
        modelBuilder.Entity<SyncConfiguration>().HasData(
            new SyncConfiguration { Key = "MaxRetryAttempts", Value = "3", Description = "Maximum number of retry attempts for failed syncs" },
            new SyncConfiguration { Key = "SyncIntervalSeconds", Value = "10", Description = "Interval between sync runs in seconds" },
            new SyncConfiguration { Key = "BatchSize", Value = "50", Description = "Number of records to process in each batch" },
            new SyncConfiguration { Key = "RetryDelayMinutes", Value = "5", Description = "Delay before retrying failed syncs in minutes" },
            new SyncConfiguration { Key = "HealthCheckIntervalMinutes", Value = "1", Description = "Interval for health checks in minutes" }
        );
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            // Fallback configuration if not configured via DI
            optionsBuilder.UseSqlite("Data Source=data/POS_Local.db");
        }
        
        // Enable detailed logging in development
        if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
        {
            optionsBuilder.EnableSensitiveDataLogging();
            optionsBuilder.EnableDetailedErrors();
        }
    }

    /// <summary>
    /// Override SaveChanges to automatically update timestamps
    /// </summary>
    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    /// <summary>
    /// Override SaveChangesAsync to automatically update timestamps
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return await base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Update timestamps for entities that have timestamp properties
    /// </summary>
    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            if (entry.Entity is PosTransaction transaction)
            {
                if (entry.State == EntityState.Added)
                {
                    transaction.CreatedAt = DateTime.UtcNow;
                }
            }
            else if (entry.Entity is InventoryUpdate inventoryUpdate)
            {
                if (entry.State == EntityState.Added)
                {
                    inventoryUpdate.CreatedAt = DateTime.UtcNow;
                }
            }
            else if (entry.Entity is SyncConfiguration config)
            {
                config.UpdatedAt = DateTime.UtcNow;
            }
            else if (entry.Entity is SyncServiceStatus status)
            {
                if (entry.State == EntityState.Added)
                {
                    status.CreatedAt = DateTime.UtcNow;
                }
            }
        }
    }
}
