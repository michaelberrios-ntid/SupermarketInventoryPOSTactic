using Microsoft.Data.Sqlite;

Console.WriteLine("🧾 POS Console App started.");

while (true)
{
    Console.Write("Select an Transaction Type:\n");
    int n = 0;
    foreach (var type in Enum.GetValues(typeof(TransactionType)))
    {
        Console.WriteLine($"  {type}");
    }

    string? input = Console.ReadLine()?.Trim();
    if (string.IsNullOrEmpty(input) || int.TryParse(input, out _))
    {
        Console.WriteLine("Invalid input. Please try again.");
        continue;
    }

    else if (Enum.TryParse<TransactionType>(input, true, out var transactionType))
    {
        Console.WriteLine($"You selected: {transactionType}");
        if (transactionType == TransactionType.Sale)
        {
            Console.WriteLine("Processing Sale transaction...");
            Console.Write("Scan Product Barcode for Sale: ");
            string? barcode = Console.ReadLine()?.Trim();

            //  Add the sale transaction to the POS Local database
            if (!string.IsNullOrEmpty(barcode))
            {
                Console.WriteLine($"Product Sale Successful.");
            }
            else
            {
                Console.WriteLine("Invalid barcode. Please try again.");
            }
        }
        else if (transactionType == TransactionType.Refund)
        {
            Console.WriteLine("Processing Refund transaction...");
            Console.Write("Scan Product Barcode for Refund: ");
            string? barcode = Console.ReadLine()?.Trim();

            // Add the refund transaction to the POS Local database
            if (!string.IsNullOrEmpty(barcode))
            {
                Console.WriteLine($"Product Refund Successful (TODO: Show amount refunded).");
            }
            else
            {
                Console.WriteLine("Invalid barcode. Please try again.");
            }
        }
        else
        {
            Console.WriteLine("Invalid choice. Please try again.");
        }
    }
    else
    {
        Console.WriteLine("Invalid transaction type. Please try again.");
    }

    Console.WriteLine();
}

enum TransactionType
{
    Sale,
    Refund
}

// var conn = new SqliteConnection("Data Source=data/POS_Local.db");
// conn.Open();

// var cmd = conn.CreateCommand();
// cmd.CommandText = @"CREATE TABLE IF NOT EXISTS InventoryEvents (
//     Id INTEGER PRIMARY KEY AUTOINCREMENT,
//     Type TEXT NOT NULL,
//     Timestamp TEXT NOT NULL
// );";
// cmd.ExecuteNonQuery();

// Console.WriteLine("✔️ POS_Local.db initialized.");
