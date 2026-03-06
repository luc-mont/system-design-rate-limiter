using RateLimiter.Algorithms;

namespace RateLimiter.Middleware;

public sealed class RateLimiterMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IRateLimitAlgorithm _algorithm;
    private readonly int _limit;

    public RateLimiterMiddleware(RequestDelegate next, IRateLimitAlgorithm algorithm, int limit)
    {
        _next = next;
        _algorithm = algorithm;
        _limit = limit;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var clientId = ExtractClientId(context);

        if (!_algorithm.TryAcquire(clientId))
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers["X-Ratelimit-Limit"] = _limit.ToString();
            context.Response.Headers["X-Ratelimit-Remaining"] = "0";
            context.Response.Headers["X-Ratelimit-Retry-After"] = "1";
            context.Response.ContentType = "application/json";

            await context.Response.WriteAsync("""{"error":"Rate limit exceeded. Try again later."}""");
            return;
        }

        context.Response.Headers["X-Ratelimit-Limit"] = _limit.ToString();
        await _next(context);
    }

    private static string ExtractClientId(HttpContext context)
    {
        // Prefer X-Forwarded-For for reverse proxy setups, fall back to remote IP.
        var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwarded))
            return forwarded.Split(',')[0].Trim();

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
