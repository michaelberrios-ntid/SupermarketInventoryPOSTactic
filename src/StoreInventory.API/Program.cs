using Common;
using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

Database.EnsureStoreSchema();      // Creates tables if needed
Database.SeedStoreInventory();     // ✅ Populates with initial data

app.MapGet("/", () => "🏬 Store Inventory API is live");
app.Run();
