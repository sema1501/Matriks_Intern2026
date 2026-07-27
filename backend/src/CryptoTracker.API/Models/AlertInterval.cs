namespace CryptoTracker.API.Models;

/// <summary>
/// How often an active alert is evaluated by the background monitor.
/// </summary>
public enum AlertInterval
{
    Minute = 0,
    Hourly = 1,
    Daily = 2
}
