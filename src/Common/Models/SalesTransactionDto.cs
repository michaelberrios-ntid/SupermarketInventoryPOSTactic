namespace Common.Models;

public class SalesTransactionDto
{
    public string Id { get; set; } = "";
    public string TransactionType { get; set; } = "Sale"; // Could be Sale or Refund
    public string ProductId { get; set; } = "";
    public int Quantity { get; set; }
    public double Price { get; set; }
    public string Timestamp { get; set; } = "";
}
