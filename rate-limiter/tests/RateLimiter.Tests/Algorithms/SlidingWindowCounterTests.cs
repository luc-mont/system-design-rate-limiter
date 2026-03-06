using AutoFixture;
using FluentAssertions;
using RateLimiter.Algorithms;

namespace RateLimiter.Tests.Algorithms;

public class SlidingWindowCounterTests
{
    private readonly Fixture _fixture = new();

    [Fact]
    public void Should_allow_requests_within_limit()
    {
        // Arrange
        var time = new FakeTimeProvider();
        var sut = new SlidingWindowCounterAlgorithm(maxRequests: 5, windowSize: TimeSpan.FromSeconds(60), time);
        var clientId = _fixture.Create<string>();

        // Act
        var results = Enumerable.Range(0, 5)
            .Select(_ => sut.TryAcquire(clientId))
            .ToList();

        // Assert
        results.Should().AllSatisfy(r => r.Should().BeTrue());
    }

    [Fact]
    public void Should_reject_when_limit_exceeded()
    {
        // Arrange
        var time = new FakeTimeProvider();
        var sut = new SlidingWindowCounterAlgorithm(maxRequests: 3, windowSize: TimeSpan.FromSeconds(60), time);
        var clientId = _fixture.Create<string>();
        for (int i = 0; i < 3; i++)
            sut.TryAcquire(clientId);

        // Act
        var result = sut.TryAcquire(clientId);

        // Assert
        result.Should().BeFalse("limit of 3 was already reached");
    }

    [Fact]
    public void Should_use_weighted_previous_window_count()
    {
        // Arrange
        var time = new FakeTimeProvider();
        var sut = new SlidingWindowCounterAlgorithm(maxRequests: 7, windowSize: TimeSpan.FromSeconds(60), time);
        var clientId = _fixture.Create<string>();
        for (int i = 0; i < 5; i++)
            sut.TryAcquire(clientId);
        // Move to next window at 30% mark → overlap = 70%.
        // Estimated weight: currentCount + previousCount * 0.7 = 0 + 5 * 0.7 = 3.5
        time.Advance(TimeSpan.FromSeconds(78));

        // Act — with estimate ~3.5, we can fit 7 - 3.5 = 3.5 → 3 more requests.
        var first = sut.TryAcquire(clientId);
        var second = sut.TryAcquire(clientId);
        var third = sut.TryAcquire(clientId);

        // Assert
        first.Should().BeTrue("estimate ~3.5, under 7");
        second.Should().BeTrue("estimate ~4.5, under 7");
        third.Should().BeTrue("estimate ~5.5, under 7");
    }

    [Fact]
    public void Should_reset_fully_after_two_windows()
    {
        // Arrange
        var time = new FakeTimeProvider();
        var sut = new SlidingWindowCounterAlgorithm(maxRequests: 2, windowSize: TimeSpan.FromSeconds(10), time);
        var clientId = _fixture.Create<string>();
        sut.TryAcquire(clientId);
        sut.TryAcquire(clientId);
        // Jump past two full windows — no previous data should remain.
        time.Advance(TimeSpan.FromSeconds(25));

        // Act
        var first = sut.TryAcquire(clientId);
        var second = sut.TryAcquire(clientId);

        // Assert
        first.Should().BeTrue("all data should be stale");
        second.Should().BeTrue("second request also within fresh window");
    }

    [Fact]
    public void Should_isolate_clients()
    {
        // Arrange
        var time = new FakeTimeProvider();
        var sut = new SlidingWindowCounterAlgorithm(maxRequests: 1, windowSize: TimeSpan.FromSeconds(10), time);
        var client1 = _fixture.Create<string>();
        var client2 = _fixture.Create<string>();
        sut.TryAcquire(client1);

        // Act
        var client1Result = sut.TryAcquire(client1);
        var client2Result = sut.TryAcquire(client2);

        // Assert
        client1Result.Should().BeFalse("client1 exceeded limit");
        client2Result.Should().BeTrue("client2 should be unaffected");
    }
}
