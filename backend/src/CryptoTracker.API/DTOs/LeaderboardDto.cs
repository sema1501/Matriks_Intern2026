namespace CryptoTracker.API.DTOs
{
    public class LeaderboardDto
    {
        public string Username { get; set; } = string.Empty;
        public decimal ProfitLossPercentage { get; set; }
    }
}