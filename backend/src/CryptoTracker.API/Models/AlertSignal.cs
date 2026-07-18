namespace CryptoTracker.API.Models;

public class AlertSignal
{
    public int Id { get; set; }
    public int AlertId { get; set; }
    public decimal PriceAtTrigger { get; set; }
    public DateTime TriggeredAt { get; set; }

    public PriceAlert Alert { get; set; } = null!;
}
