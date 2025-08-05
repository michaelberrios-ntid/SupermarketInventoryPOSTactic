using System.Net.Http.Json;
using Common;
using Common.Models;
using Microsoft.Data.Sqlite;

Console.WriteLine("POS Sync Service running...");

Database.EnsurePOSSchema();

while (true)
{
    try
    {
        using var posConn = Database.GetPOSLocalDB();
        posConn.Open();

        // Read unsynced transactions
        using var readCmd = posConn.CreateCommand();
        readCmd.CommandText = @"
            SELECT id, transaction_type, product_id, quantity, price, timestamp 
            FROM SalesTransaction;";

        using var reader = readCmd.ExecuteReader();

        var transactions = new List<SalesTransactionDto>();

        while (reader.Read())
        {
            transactions.Add(new SalesTransactionDto
            {
                Id = reader.GetString(0),
                TransactionType = reader.GetString(1),
                ProductId = reader.GetString(2),
                Quantity = reader.GetInt32(3),
                Price = reader.GetDouble(4),
                Timestamp = reader.GetString(5)
            });
        }

        if (transactions.Count == 0)
        {
            Console.WriteLine("No new transactions to sync.");
        }
        else
        {
            using var client = new HttpClient { BaseAddress = new Uri("http://store_api:8080") };

            var sales = transactions.Where(t => t.TransactionType == "Sale").ToList();
            var refunds = transactions.Where(t => t.TransactionType == "Refund").ToList();

            bool success = true;

            if (sales.Any())
            {
                var res = await client.PostAsJsonAsync("/sync/sales", sales);
                success &= res.IsSuccessStatusCode;
                Console.WriteLine(res.IsSuccessStatusCode
                    ? $"Synced {sales.Count} sale(s)."
                    : $"Failed to sync sales: {res.StatusCode}");
            }

            if (refunds.Any())
            {
                var res = await client.PostAsJsonAsync("/sync/return", refunds);
                success &= res.IsSuccessStatusCode;
                Console.WriteLine(res.IsSuccessStatusCode
                    ? $"Synced {refunds.Count} refund(s)."
                    : $"Failed to sync refunds: {res.StatusCode}");
            }

            if (success)
            {
                using var deleteCmd = posConn.CreateCommand();
                deleteCmd.CommandText = "DELETE FROM SalesTransaction;";
                deleteCmd.ExecuteNonQuery();

                Console.WriteLine($"Cleared local synced transactions.");
            }
        }

    }
    catch (Exception ex)
    {
        Console.WriteLine($"Sync error: {ex.Message}");
    }

    Console.WriteLine("🕒 Waiting 60 seconds...\n");
    await Task.Delay(TimeSpan.FromMinutes(1));
}