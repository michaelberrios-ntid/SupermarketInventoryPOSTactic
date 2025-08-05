using Common;
using Microsoft.Data.Sqlite;

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

app.Run();
