using Microsoft.EntityFrameworkCore;
using POS.SyncService.Data;
using POS.SyncService.Models;

namespace POS.SyncService.Services;

/// <summary>
/// Service for seeding sample data for testing purposes
/// </summary>
public interface IDataSeeder
{
    /// <summary>
    /// Seed sample transactions and inventory updates
    /// </summary>
    Task SeedSampleDataAsync(int transactionCount = 10, int inventoryUpdateCount = 5);
    
    /// <summary>
    /// Create a sample transaction
    /// </summary>
    Task<PosTransaction> CreateSampleTransactionAsync(string storeId, string productId = "PROD001");
    
    /// <summary>
    /// Create a sample inventory update
    /// </summary>
    Task<InventoryUpdate> CreateSampleInventoryUpdateAsync(string storeId, string productId = "PROD001");
}

public class DataSeeder : IDataSeeder
{
    private readonly PosDbContext _dbContext;
    private readonly ILogger<DataSeeder> _logger;
    private readonly Random _random = new();

    public DataSeeder(PosDbContext dbContext, ILogger<DataSeeder> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task SeedSampleDataAsync(int transactionCount = 10, int inventoryUpdateCount = 5)
    {
        _logger.LogInformation("🌱 Seeding {TransactionCount} transactions and {InventoryCount} inventory updates", 
            transactionCount, inventoryUpdateCount);

        var storeIds = new[] { "STORE001", "STORE002", "STORE003" };
        var productIds = new[] { "PROD001", "PROD002", "PROD003", "PROD004", "PROD005" };
        var transactionTypes = new[] { "Sale", "Return", "Adjustment" };
        var inventoryTypes = new[] { "Sale", "Restock", "Adjustment", "Damage" };

        // Create sample transactions
        for (int i = 0; i < transactionCount; i++)
        {
            var transaction = new PosTransaction
            {
                Id = Guid.NewGuid(),
                StoreId = storeIds[_random.Next(storeIds.Length)],
                ProductId = productIds[_random.Next(productIds.Length)],
                TransactionType = transactionTypes[_random.Next(transactionTypes.Length)],
                Quantity = _random.Next(1, 10),
                UnitPrice = (decimal)(_random.NextDouble() * 50 + 5), // $5-$55
                TransactionDate = DateTime.UtcNow.AddMinutes(-_random.Next(0, 1440)), // Last 24 hours
                Reference = $"REF{DateTime.UtcNow.Ticks}{i:D3}",
                Notes = $"Sample transaction {i + 1}",
                Synced = false // These will need to be synced
            };

            transaction.TotalAmount = transaction.Quantity * transaction.UnitPrice;
            _dbContext.PosTransactions.Add(transaction);
        }

        // Create sample inventory updates
        for (int i = 0; i < inventoryUpdateCount; i++)
        {
            var previousStock = _random.Next(0, 100);
            var stockChange = _random.Next(-20, 50);
            var newStock = Math.Max(0, previousStock + stockChange);

            var inventoryUpdate = new InventoryUpdate
            {
                Id = Guid.NewGuid(),
                StoreId = storeIds[_random.Next(storeIds.Length)],
                ProductId = productIds[_random.Next(productIds.Length)],
                PreviousStock = previousStock,
                NewStock = newStock,
                UpdateType = inventoryTypes[_random.Next(inventoryTypes.Length)],
                UpdateDate = DateTime.UtcNow.AddMinutes(-_random.Next(0, 1440)), // Last 24 hours
                Reason = $"Sample inventory update {i + 1}",
                Synced = false // These will need to be synced
            };

            _dbContext.InventoryUpdates.Add(inventoryUpdate);
        }

        await _dbContext.SaveChangesAsync();
        
        _logger.LogInformation("✅ Seeded {TransactionCount} transactions and {InventoryCount} inventory updates", 
            transactionCount, inventoryUpdateCount);
    }

    public async Task<PosTransaction> CreateSampleTransactionAsync(string storeId, string productId = "PROD001")
    {
        var transactionTypes = new[] { "Sale", "Return", "Adjustment" };
        
        var transaction = new PosTransaction
        {
            Id = Guid.NewGuid(),
            StoreId = storeId,
            ProductId = productId,
            TransactionType = transactionTypes[_random.Next(transactionTypes.Length)],
            Quantity = _random.Next(1, 10),
            UnitPrice = (decimal)(_random.NextDouble() * 50 + 5),
            TransactionDate = DateTime.UtcNow,
            Reference = $"REF{DateTime.UtcNow.Ticks}",
            Notes = "Programmatically created transaction",
            Synced = false
        };

        transaction.TotalAmount = transaction.Quantity * transaction.UnitPrice;
        
        _dbContext.PosTransactions.Add(transaction);
        await _dbContext.SaveChangesAsync();
        
        _logger.LogInformation("📝 Created sample transaction {TransactionId} for store {StoreId}", 
            transaction.Id, storeId);
        
        return transaction;
    }

    public async Task<InventoryUpdate> CreateSampleInventoryUpdateAsync(string storeId, string productId = "PROD001")
    {
        var inventoryTypes = new[] { "Sale", "Restock", "Adjustment", "Damage" };
        var previousStock = _random.Next(0, 100);
        var stockChange = _random.Next(-20, 50);
        var newStock = Math.Max(0, previousStock + stockChange);

        var inventoryUpdate = new InventoryUpdate
        {
            Id = Guid.NewGuid(),
            StoreId = storeId,
            ProductId = productId,
            PreviousStock = previousStock,
            NewStock = newStock,
            UpdateType = inventoryTypes[_random.Next(inventoryTypes.Length)],
            UpdateDate = DateTime.UtcNow,
            Reason = "Programmatically created inventory update",
            Synced = false
        };

        _dbContext.InventoryUpdates.Add(inventoryUpdate);
        await _dbContext.SaveChangesAsync();
        
        _logger.LogInformation("📦 Created sample inventory update {UpdateId} for store {StoreId}", 
            inventoryUpdate.Id, storeId);
        
        return inventoryUpdate;
    }
}
