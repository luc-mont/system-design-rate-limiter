using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RateLimiter.Tests.Middleware;

/// <summary>
/// Integration tests for the rate limiter middleware using an in-memory test server.
/// Each test uses a unique X-Forwarded-For to simulate independent clients.
/// </summary>
public class RateLimiterMiddlewareTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public RateLimiterMiddlewareTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Allowed_request_returns_200_with_rate_limit_header()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/ping");
        request.Headers.Add("X-Forwarded-For", "10.0.0.1");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey("X-Ratelimit-Limit");
    }

    [Fact]
    public async Task Exceeding_limit_returns_429_with_headers_and_error_body()
    {
        // Arrange — exhaust the bucket (default: TokenBucket with 10 max requests).
        var client = _factory.CreateClient();
        for (int i = 0; i < 15; i++)
        {
            var req = new HttpRequestMessage(HttpMethod.Get, "/api/ping");
            req.Headers.Add("X-Forwarded-For", "10.0.0.2");
            await client.SendAsync(req);
        }
        var finalRequest = new HttpRequestMessage(HttpMethod.Get, "/api/ping");
        finalRequest.Headers.Add("X-Forwarded-For", "10.0.0.2");

        // Act
        var response = await client.SendAsync(finalRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        response.Headers.Should().ContainKey("X-Ratelimit-Retry-After");
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Rate limit exceeded");
    }

    [Fact]
    public async Task Config_endpoint_returns_active_algorithm()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/config");
        request.Headers.Add("X-Forwarded-For", "10.0.0.3");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("TokenBucket");
    }
}
