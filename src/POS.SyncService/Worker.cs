using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using POS.SyncService.Configuration;
using POS.SyncService.Services;

namespace POS.SyncService;

/// <summary>
/// Background worker that orchestrates the synchronization process
/// Implements modifiability and availability tactics for robust operation
/// </summary>
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly SyncOptions _options;
    private readonly IServiceProvider _serviceProvider;
    private DateTime _lastCleanupTime = DateTime.UtcNow;

    public Worker(
        ILogger<Worker> logger, 
        IOptions<SyncOptions> options,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _options = options.Value;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🔄 POS Sync Service starting for store {StoreId}", _options.StoreId);
        _logger.LogInformation("📊 Sync interval: {Interval}s, Batch size: {BatchSize}, Max retries: {MaxRetries}", 
            _options.SyncIntervalSeconds, _options.BatchSize, _options.MaxRetryAttempts);

        // Wait a bit for services to stabilize
        await Task.Delay(2000, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Run synchronization
                await RunSyncCycleAsync(stoppingToken);

                // Run periodic cleanup if needed
                await RunPeriodicCleanupAsync(stoppingToken);

                // Run health checks if enabled
                if (_options.EnableHealthChecks)
                {
                    await RunHealthChecksAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // This is expected when stopping the service
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Unhandled error in sync worker. Service will continue.");
            }

            // Wait for the next sync interval
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_options.SyncIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("🛑 POS Sync Service stopped for store {StoreId}", _options.StoreId);
    }

    private async Task RunSyncCycleAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var syncService = scope.ServiceProvider.GetRequiredService<ISyncService>();
        
        try
        {
            if (_options.EnableDetailedLogging)
            {
                _logger.LogDebug("🔄 Starting sync cycle for store {StoreId}", _options.StoreId);
            }

            var result = await syncService.SynchronizeAllAsync(cancellationToken);

            if (result.Success)
            {
                if (result.TransactionsSynced > 0 || result.InventoryUpdatesSynced > 0)
                {
                    _logger.LogInformation("✅ Sync completed: {Transactions} transactions, {Inventory} inventory updates in {Duration}ms", 
                        result.TransactionsSynced, result.InventoryUpdatesSynced, result.Duration.TotalMilliseconds);
                }
                else if (_options.EnableDetailedLogging)
                {
                    _logger.LogDebug("🔄 Sync cycle completed - no pending items");
                }
            }
            else
            {
                _logger.LogWarning("⚠️ Sync cycle completed with errors: {Error}. Failed syncs: {Failed}", 
                    result.ErrorMessage, result.FailedSyncs);
            }

            // Log warnings if any
            foreach (var warning in result.Warnings)
            {
                _logger.LogWarning("⚠️ Sync warning: {Warning}", warning);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error during sync cycle");
        }
    }

    private async Task RunPeriodicCleanupAsync(CancellationToken cancellationToken)
    {
        if (DateTime.UtcNow - _lastCleanupTime < TimeSpan.FromHours(_options.CleanupIntervalHours))
        {
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var syncService = scope.ServiceProvider.GetRequiredService<ISyncService>();

        try
        {
            _logger.LogInformation("🧹 Running periodic cleanup...");
            
            await syncService.CleanupOldDataAsync(_options.DataRetentionDays);
            
            _lastCleanupTime = DateTime.UtcNow;
            
            _logger.LogInformation("✅ Periodic cleanup completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error during periodic cleanup");
        }
    }

    private async Task RunHealthChecksAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var syncService = scope.ServiceProvider.GetRequiredService<ISyncService>();
        
        try
        {
            var isHealthy = await syncService.IsHealthyAsync();
            
            if (!isHealthy)
            {
                _logger.LogWarning("⚠️ Health check failed - service may be experiencing issues");
            }
            else if (_options.EnableDetailedLogging)
            {
                _logger.LogDebug("💚 Health check passed");
            }

            // Get and log statistics periodically
            var stats = await syncService.GetSyncStatisticsAsync();
            
            if (_options.EnableDetailedLogging || stats.PendingTransactions > 0 || stats.PendingInventoryUpdates > 0)
            {
                _logger.LogInformation("📊 Stats: {PendingTx} pending transactions, {PendingInv} pending inventory updates, {FailedToday} failed today", 
                    stats.PendingTransactions, stats.PendingInventoryUpdates, stats.FailedSyncsToday);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error during health checks");
        }
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("🚀 Initializing POS Sync Service for store {StoreId}...", _options.StoreId);
        
        try
        {
            // Initialize database if needed
            await InitializeDatabaseAsync();
            
            // Seed sample data if database is empty
            await SeedInitialDataIfNeededAsync();
            
            _logger.LogInformation("✅ POS Sync Service initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to initialize POS Sync Service");
            throw;
        }

        await base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("🛑 Stopping POS Sync Service for store {StoreId}...", _options.StoreId);
        
        await base.StopAsync(cancellationToken);
        
        _logger.LogInformation("✅ POS Sync Service stopped gracefully");
    }

    private async Task InitializeDatabaseAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<POS.SyncService.Data.PosDbContext>();
            
            // Log the connection string for debugging
            var connectionString = dbContext.Database.GetConnectionString();
            _logger.LogInformation("🔗 Using database connection: {ConnectionString}", connectionString);
            
            // Ensure the data directory exists
            if (connectionString?.Contains("Data Source=") == true)
            {
                var dbPath = connectionString.Replace("Data Source=", "");
                var dbDirectory = Path.GetDirectoryName(dbPath);
                if (!string.IsNullOrEmpty(dbDirectory) && !Directory.Exists(dbDirectory))
                {
                    Directory.CreateDirectory(dbDirectory);
                    _logger.LogInformation("📁 Created database directory: {Directory}", dbDirectory);
                }
            }
            
        if (_options.AutoCreateDatabase)
        {
            _logger.LogInformation("🔧 Creating database tables if not exists...");
            
            // First ensure the database file exists
            await dbContext.Database.EnsureCreatedAsync();
            
            // Verify tables exist by checking the schema
            var tables = await dbContext.Database.SqlQueryRaw<string>(
                "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'").ToListAsync();
            
            if (!tables.Contains("PosTransactions") || !tables.Contains("InventoryUpdates"))
            {
                _logger.LogWarning("⚠️ Tables missing, forcing database recreation...");
                await dbContext.Database.EnsureDeletedAsync();
                await dbContext.Database.EnsureCreatedAsync();
                
                // Verify tables were created
                tables = await dbContext.Database.SqlQueryRaw<string>(
                    "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'").ToListAsync();
                _logger.LogInformation("📋 Created tables: {Tables}", string.Join(", ", tables));
            }
            
            _logger.LogInformation("📄 Database initialization completed");
        }            // Verify database connectivity
            await dbContext.Database.CanConnectAsync();
            _logger.LogInformation("🔌 Database connectivity verified");
            
            // Check if tables exist and log their status
            try
            {
                var transactionCount = await dbContext.PosTransactions.CountAsync();
                var inventoryCount = await dbContext.InventoryUpdates.CountAsync();
                _logger.LogInformation("📊 Database status: {TransactionCount} transactions, {InventoryCount} inventory updates", 
                    transactionCount, inventoryCount);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("⚠️ Could not query tables (they may not exist yet): {Error}", ex.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Database initialization failed");
            throw;
        }
    }

    private async Task SeedInitialDataIfNeededAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<POS.SyncService.Data.PosDbContext>();
            var dataSeeder = scope.ServiceProvider.GetRequiredService<IDataSeeder>();
            
            // Check if we already have data
            var transactionCount = await dbContext.PosTransactions.CountAsync();
            var inventoryCount = await dbContext.InventoryUpdates.CountAsync();
            
            if (transactionCount == 0 && inventoryCount == 0)
            {
                _logger.LogInformation("🌱 Database is empty, seeding initial test data...");
                await dataSeeder.SeedSampleDataAsync(transactionCount: 5, inventoryUpdateCount: 3);
                _logger.LogInformation("✅ Initial test data seeded successfully");
            }
            else
            {
                _logger.LogInformation("📊 Database already contains data: {Transactions} transactions, {Inventory} inventory updates", 
                    transactionCount, inventoryCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Failed to seed initial data - continuing without sample data");
        }
    }
}
