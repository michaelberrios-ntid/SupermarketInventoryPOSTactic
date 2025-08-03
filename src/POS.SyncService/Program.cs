using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Data.Sqlite;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services => { })
    .ConfigureLogging(logging => logging.AddConsole())
    .Build();

Console.WriteLine("🔄 POS Sync Service running...");

var conn = new SqliteConnection("Data Source=data/POS_Local.db");
conn.Open();

var cmd = conn.CreateCommand();
cmd.CommandText = @"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='InventoryEvents';";
var tableExists = Convert.ToInt32(cmd.ExecuteScalar()) == 1;

Console.WriteLine(tableExists
    ? "📦 Found InventoryEvents table in POS_Local.db"
    : "❌ InventoryEvents table not found.");

await host.RunAsync();
