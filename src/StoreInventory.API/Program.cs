using Common;
using Microsoft.Data.Sqlite;
using Common.Models;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

Database.EnsureStoreSchema();      // Creates tables if needed
Database.SeedStoreInventory();     // ✅ Populates with initial data

app.MapGet("/", () => "🏬 Store Inventory API is live");

app.MapGet("/product/{id}", (string id) =>
{
    using var conn = Database.GetStoreLocalDB();
    conn.Open();

    using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
        SELECT product_id, name, price, quantity 
        FROM Product 
        WHERE product_id = $id;";
    cmd.Parameters.AddWithValue("$id", id);

    using var reader = cmd.ExecuteReader();
    if (reader.Read())
    {
        return Results.Ok(new
        {
            Id = reader.GetString(0),
            Name = reader.GetString(1),
            Price = reader.GetDouble(2),
            Quantity = reader.GetInt32(3)
        });
    }

    return Results.NotFound();
});

app.MapPost("/sync/sales", (List<SalesTransactionDto> transactions) =>
{
    using var conn = Database.GetStoreLocalDB();
    conn.Open();

    using var tx = conn.BeginTransaction();

    foreach (var txItem in transactions)
    {
        // Insert the transaction
        using var insertCmd = conn.CreateCommand();
        insertCmd.CommandText = @"
            INSERT INTO SalesTransaction (id, transaction_type, product_id, quantity, price, timestamp)
            VALUES ($id, $type, $pid, $qty, $price, $ts);";

        insertCmd.Parameters.AddWithValue("$id", txItem.Id);
        insertCmd.Parameters.AddWithValue("$type", txItem.TransactionType);
        insertCmd.Parameters.AddWithValue("$pid", txItem.ProductId);
        insertCmd.Parameters.AddWithValue("$qty", txItem.Quantity);
        insertCmd.Parameters.AddWithValue("$price", txItem.Price);
        insertCmd.Parameters.AddWithValue("$ts", txItem.Timestamp);
        insertCmd.ExecuteNonQuery();

        // Update product quantity (if Sale)
        if (txItem.TransactionType == "Sale")
        {
            using var updateCmd = conn.CreateCommand();
            updateCmd.CommandText = @"
                UPDATE Product
                SET quantity = quantity - $qty
                WHERE product_id = $pid;";
            
            updateCmd.Parameters.AddWithValue("$qty", txItem.Quantity);
            updateCmd.Parameters.AddWithValue("$pid", txItem.ProductId);
            updateCmd.ExecuteNonQuery();
        }
    }

    tx.Commit();
    return Results.Ok("✔️ Sales synced successfully.");
});

app.Run();
