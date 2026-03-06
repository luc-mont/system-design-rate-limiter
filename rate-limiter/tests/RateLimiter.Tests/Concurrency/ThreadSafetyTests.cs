using AutoFixture;
using FluentAssertions;
using RateLimiter.Algorithms;

namespace RateLimiter.Tests.Concurrency;

/// <summary>
/// Verifies thread-safety of all algorithm implementations.
/// Spins up many concurrent tasks against a shared algorithm instance
/// and asserts that the total allowed count never exceeds the limit.
/// </summary>
public class ThreadSafetyTests
{
    private readonly Fixture _fixture = new();
    private const int ConcurrentClients = 50;
    private const int RequestsPerClient = 20;

    [Fact]
    public async Task TokenBucket_should_not_exceed_capacity_under_concurrency()
    {
        // Arrange — refillRate=0 means no refill, so exactly 10 tokens available.
        var sut = new TokenBucketAlgorithm(bucketSize: 10, refillRatePerSecond: 0);
        var clientId = _fixture.Create<string>();

        // Act
        var allowed = await FireConcurrentRequests(sut, clientId, ConcurrentClients, RequestsPerClient);

        // Assert
        allowed.Should().Be(10, "only 10 tokens were available with no refill");
    }

    [Fact]
    public async Task SlidingWindowCounter_should_not_exceed_limit_under_concurrency()
    {
        // Arrange
        var sut = new SlidingWindowCounterAlgorithm(maxRequests: 10, windowSize: TimeSpan.FromMinutes(5));
        var clientId = _fixture.Create<string>();

        // Act
        var allowed = await FireConcurrentRequests(sut, clientId, ConcurrentClients, RequestsPerClient);

        // Assert
        allowed.Should().Be(10, "only 10 requests allowed per window");
    }

    /// <summary>
    /// Fires (concurrentTasks × requestsPerTask) requests against the same clientId
    /// and returns the total number of allowed requests.
    /// </summary>
    private static async Task<int> FireConcurrentRequests(
        IRateLimitAlgorithm algorithm,
        string clientId,
        int concurrentTasks,
        int requestsPerTask)
    {
        var totalAllowed = 0;

        var tasks = Enumerable.Range(0, concurrentTasks).Select(_ => Task.Run(() =>
        {
            for (int i = 0; i < requestsPerTask; i++)
            {
                if (algorithm.TryAcquire(clientId))
                    Interlocked.Increment(ref totalAllowed);
            }
        }));

        await Task.WhenAll(tasks);
        return totalAllowed;
    }
}
