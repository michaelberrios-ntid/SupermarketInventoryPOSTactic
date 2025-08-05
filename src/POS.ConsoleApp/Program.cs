using Common;
using Common.Models;
using System.Net.Http.Json;

Console.WriteLine("POS Console App started.");

Database.EnsurePOSSchema();
Console.WriteLine("POS_Local.db schema ensured.");

while (true)
{
    Console.Write("Select an Transaction Type:\n");
    foreach (var type in Enum.GetValues(typeof(Common.Transaction.Types)))
    {
        Console.WriteLine($"  {type}");
    }

    Console.Write("Your choice: ");
    string? input = Console.ReadLine()?.Trim();
    if (string.IsNullOrEmpty(input) || int.TryParse(input, out _))
    {
        Console.WriteLine("Invalid input. Please try again.");
        continue;
    }

    else if (Enum.TryParse<Common.Transaction.Types>(input, true, out var transactionType))
    {
        Console.WriteLine($"You selected: {transactionType}");
        if (transactionType == Common.Transaction.Types.Sale)
        {
            Console.WriteLine("Processing Sale transaction...");
            Console.Write("Scan Product Barcode for Sale: ");
            string? productId = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(productId))
            {
                Console.WriteLine("Invalid barcode.");
                return;
            }

            var client = new HttpClient();
            client.BaseAddress = new Uri("http://store_api:8080"); // container name

            var product = await client.GetFromJsonAsync<ProductDto>($"/product/{productId}");
            if (product == null)
            {
                Console.WriteLine("Product not found.");
                return;
            }

            Console.WriteLine($"Found {product.Name} — Price: ${product.Price}, Stock: {product.Quantity}");
            Console.Write("Enter quantity to purchase: ");
            if (!int.TryParse(Console.ReadLine(), out int quantity) || quantity <= 0)
            {
                Console.WriteLine("Invalid quantity.");
                return;
            }

            if (quantity > product.Quantity)
            {
                Console.WriteLine("Not enough stock available.");
                return;
            }

            // Record transaction
            using var posConn = Database.GetPOSLocalDB();
            posConn.Open();

            using var insertCmd = posConn.CreateCommand();
            insertCmd.CommandText = @"
                INSERT INTO SalesTransaction (id, transaction_type, product_id, quantity, price, timestamp)
                VALUES ($id, 'Sale', $pid, $qty, $price, $ts);";

            insertCmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
            insertCmd.Parameters.AddWithValue("$pid", product.Id);
            insertCmd.Parameters.AddWithValue("$qty", quantity);
            insertCmd.Parameters.AddWithValue("$price", product.Price);
            insertCmd.Parameters.AddWithValue("$ts", DateTime.UtcNow.ToString("o"));

            insertCmd.ExecuteNonQuery();

            Console.WriteLine("Product Sale Successful.");
        }
        else if (transactionType == Common.Transaction.Types.Return)
        {
            Console.WriteLine("Processing Refund transaction...");
            Console.Write("🔍 Scan Product Barcode for Refund: ");
            string? productId = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(productId))
            {
                Console.WriteLine("Invalid barcode. Please try again.");
                return;
            }

            using var http = new HttpClient { BaseAddress = new Uri("http://store_api:8080") };
            var product = await http.GetFromJsonAsync<ProductDto>($"/product/{productId}");

            if (product == null)
            {
                Console.WriteLine("Product not found in inventory.");
                return;
            }

            Console.WriteLine($"{product.Name} — ${product.Price:F2} — Stock: {product.Quantity}");
            Console.Write("Enter quantity to refund: ");
            if (!int.TryParse(Console.ReadLine()?.Trim(), out int quantity) || quantity < 1)
            {
                Console.WriteLine("Invalid quantity.");
                return;
            }

            // Record the refund in POS_Local.db
            using var posConn = Database.GetPOSLocalDB();
            posConn.Open();

            using var insertCmd = posConn.CreateCommand();
            insertCmd.CommandText = @"
                INSERT INTO SalesTransaction (id, transaction_type, product_id, quantity, price, timestamp)
                VALUES ($id, 'Refund', $pid, $qty, $price, $ts);";

            insertCmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
            insertCmd.Parameters.AddWithValue("$pid", productId);
            insertCmd.Parameters.AddWithValue("$qty", quantity);
            insertCmd.Parameters.AddWithValue("$price", product.Price);
            insertCmd.Parameters.AddWithValue("$ts", DateTime.UtcNow.ToString("o"));

            insertCmd.ExecuteNonQuery();

            Console.WriteLine($"Product Refund Successful. Amount refunded: ${quantity * product.Price:F2}");
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