using Microsoft.Data.Sqlite;
using Common;

Console.WriteLine("🔄 POS Sync Service running...");

// Open both DBs
Database.EnsurePOSSchema();
using var posConn = Database.GetPOSLocalDB();
posConn.Open();

Database.EnsureStoreSchema();
using var storeConn = Database.GetStoreLocalDB();
storeConn.Open();

// Read transactions from POS DB
using var readCmd = posConn.CreateCommand();
readCmd.CommandText = @"
    SELECT id, transaction_type, product_id, quantity, price, timestamp 
    FROM SalesTransaction;";

using var reader = readCmd.ExecuteReader();

var hasRows = false;
var insertCmd = storeConn.CreateCommand();
var insertTxn = storeConn.BeginTransaction();

while (reader.Read())
{
    hasRows = true;

    insertCmd.CommandText = @"
        INSERT INTO SalesTransaction (id, transaction_type, product_id, quantity, price, timestamp)
        VALUES ($id, $type, $pid, $qty, $price, $ts);";

    insertCmd.Parameters.Clear();
    insertCmd.Parameters.AddWithValue("$id", reader.GetString(0));
    insertCmd.Parameters.AddWithValue("$type", reader.GetString(1));
    insertCmd.Parameters.AddWithValue("$pid", reader.GetString(2));
    insertCmd.Parameters.AddWithValue("$qty", reader.GetInt32(3));
    insertCmd.Parameters.AddWithValue("$price", reader.GetDouble(4));
    insertCmd.Parameters.AddWithValue("$ts", reader.GetString(5));

    insertCmd.ExecuteNonQuery();
}

if (hasRows)
{
    insertTxn.Commit();

    using var deleteCmd = posConn.CreateCommand();
    deleteCmd.CommandText = "DELETE FROM SalesTransaction;";
    deleteCmd.ExecuteNonQuery();

    Console.WriteLine("✅ Transactions synced and cleared from POS DB.");
}
else
{
    Console.WriteLine("🕊️ No transactions to sync.");
}
