namespace CryptoTracker.API.Services;

/// <summary>
/// Gates DEVELOPMENT / DEBUG ONLY endpoints. Production implementations require IsDevelopment().
/// </summary>
public interface IDebugEndpointAccess
{
    bool AllowDebugExecute { get; }
}

public sealed class DevelopmentOnlyDebugEndpointAccess(IWebHostEnvironment environment)
    : IDebugEndpointAccess
{
    public bool AllowDebugExecute => environment.IsDevelopment();
}
