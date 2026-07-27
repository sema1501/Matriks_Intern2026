namespace CryptoTracker.API.Models;

public class PortfolioHolding
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal AvgBuyPrice { get; set; }

    public User User { get; set; } = null!;
}