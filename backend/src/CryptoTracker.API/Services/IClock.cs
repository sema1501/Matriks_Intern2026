namespace CryptoTracker.API.Services;

/// <summary>Abstracts UTC "now" so alert cadence logic can be unit-tested without Thread.Sleep.</summary>
public interface IClock
{
    DateTime UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
