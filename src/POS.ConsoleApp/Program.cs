using Microsoft.Data.Sqlite;

Console.WriteLine("🧾 POS Console App started.");

var conn = new SqliteConnection("Data Source=data/POS_Local.db");
conn.Open();

var cmd = conn.CreateCommand();
cmd.CommandText = @"CREATE TABLE IF NOT EXISTS InventoryEvents (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Type TEXT NOT NULL,
    Timestamp TEXT NOT NULL
);";
cmd.ExecuteNonQuery();

Console.WriteLine("✔️ POS_Local.db initialized.");
