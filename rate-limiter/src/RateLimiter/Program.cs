using RateLimiter.Configuration;
using RateLimiter.Middleware;
using RateLimiter.Rules;

var builder = WebApplication.CreateBuilder(args);

// Bind configuration.
var options = new RateLimiterOptions();
builder.Configuration.GetSection(RateLimiterOptions.SectionName).Bind(options);

// Create the algorithm from configuration.
var algorithm = AlgorithmFactory.Create(options);

var app = builder.Build();

// Register rate limiter middleware — applies to all requests BEFORE routing.
app.UseMiddleware<RateLimiterMiddleware>(algorithm, options.MaxRequests);

// Demo endpoints.
app.MapGet("/api/ping", () => Results.Ok(new { message = "pong", timestamp = DateTime.UtcNow }))
    .WithTags("Demo");

app.MapGet("/api/resource", () =>
{
    // Simulate a protected resource.
    return Results.Ok(new
    {
        data = "This is a rate-limited resource.",
        generatedAt = DateTime.UtcNow
    });
}).WithTags("Demo");

app.MapGet("/api/config", () => Results.Ok(new
{
    algorithm = options.Algorithm.ToString(),
    maxRequests = options.MaxRequests,
    windowSeconds = options.WindowSeconds,
    ratePerSecond = options.RatePerSecond
})).WithTags("Config");

app.Run();

// Make Program accessible for integration tests via WebApplicationFactory.
public partial class Program { }
