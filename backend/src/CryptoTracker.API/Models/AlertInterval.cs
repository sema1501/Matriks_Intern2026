namespace CryptoTracker.API.Models;

/// <summary>
/// How often an active alert is evaluated by the background monitor.
/// Only <see cref="Minute"/> is supported this week; Hourly/Daily are reserved.
/// </summary>
public enum AlertInterval
{
    Minute = 0,
    Hourly = 1,
    Daily = 2
}
