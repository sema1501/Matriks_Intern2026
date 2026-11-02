using System.Net;
using System.Text.Json;
using CryptoTracker.API.Services;

namespace CryptoTracker.API.Middleware;

public class ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (NotImplementedException ex)
        {
            logger.LogWarning("Not implemented: {Message}", ex.Message);
            context.Response.StatusCode  = (int)HttpStatusCode.NotImplemented;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = ex.Message }));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            logger.LogWarning("Validation/Business rule violation: {Message}", ex.Message);
            context.Response.StatusCode  = (int)HttpStatusCode.BadRequest;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = ex.Message }));
        }
        catch (RefreshTokenAlreadyRotatedException ex)
        {
            // Normal 401 ile aynı, ama frontend'in diğer sekmenin yeni token'ını beklemesi için kodu var.
            logger.LogInformation("Unauthorized: {Message}", ex.Message);
            context.Response.StatusCode  = (int)HttpStatusCode.Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = ex.Message, code = RefreshTokenAlreadyRotatedException.ErrorCode }));
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning("Unauthorized: {Message}", ex.Message);
            context.Response.StatusCode  = (int)HttpStatusCode.Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = ex.Message }));
        }
        catch (KeyNotFoundException ex)
        {
            logger.LogWarning("Resource not found: {Message}", ex.Message);
            context.Response.StatusCode  = (int)HttpStatusCode.NotFound;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = ex.Message }));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
            context.Response.StatusCode  = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = "Internal server error." }));
        }
    }
}
