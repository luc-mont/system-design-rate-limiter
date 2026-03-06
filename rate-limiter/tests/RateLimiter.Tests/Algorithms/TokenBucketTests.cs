using AutoFixture;
using FluentAssertions;
using RateLimiter.Algorithms;

namespace RateLimiter.Tests.Algorithms;

public class TokenBucketTests
{
    private readonly Fixture _fixture = new();

    [Fact]
    public void Should_allow_requests_within_bucket_capacity()
    {
        // Arrange
        var time = new FakeTimeProvider();
        var sut = new TokenBucketAlgorithm(bucketSize: 5, refillRatePerSecond: 1, time);
        var clientId = _fixture.Create<string>();

        // Act
        var results = Enumerable.Range(0, 5)
            .Select(_ => sut.TryAcquire(clientId))
            .ToList();

        // Assert
        results.Should().AllSatisfy(r => r.Should().BeTrue());
    }

    [Fact]
    public void Should_reject_when_bucket_is_empty()
    {
        // Arrange
        var time = new FakeTimeProvider();
        var sut = new TokenBucketAlgorithm(bucketSize: 3, refillRatePerSecond: 1, time);
        var clientId = _fixture.Create<string>();
        for (int i = 0; i < 3; i++)
            sut.TryAcquire(clientId);

        // Act
        var result = sut.TryAcquire(clientId);

        // Assert
        result.Should().BeFalse("bucket should be empty");
    }

    [Fact]
    public void Should_refill_tokens_over_time()
    {
        // Arrange
        var time = new FakeTimeProvider();
        var sut = new TokenBucketAlgorithm(bucketSize: 3, refillRatePerSecond: 1, time);
        var clientId = _fixture.Create<string>();
        for (int i = 0; i < 3; i++)
            sut.TryAcquire(clientId);
        time.Advance(TimeSpan.FromSeconds(2));

        // Act
        var first = sut.TryAcquire(clientId);
        var second = sut.TryAcquire(clientId);
        var third = sut.TryAcquire(clientId);

        // Assert
        first.Should().BeTrue("1 token should have refilled");
        second.Should().BeTrue("2nd token should have refilled");
        third.Should().BeFalse("only 2 tokens refilled in 2 seconds");
    }

    [Fact]
    public void Should_not_exceed_bucket_capacity_on_refill()
    {
        // Arrange
        var time = new FakeTimeProvider();
        var sut = new TokenBucketAlgorithm(bucketSize: 3, refillRatePerSecond: 10, time);
        var clientId = _fixture.Create<string>();
        sut.TryAcquire(clientId);
        time.Advance(TimeSpan.FromSeconds(100));

        // Act
        var results = Enumerable.Range(0, 3)
            .Select(_ => sut.TryAcquire(clientId))
            .ToList();
        var overflow = sut.TryAcquire(clientId);

        // Assert
        results.Should().AllSatisfy(r => r.Should().BeTrue());
        overflow.Should().BeFalse("bucket should cap at bucket size");
    }

    [Fact]
    public void Should_isolate_clients()
    {
        // Arrange
        var time = new FakeTimeProvider();
        var sut = new TokenBucketAlgorithm(bucketSize: 2, refillRatePerSecond: 1, time);
        var client1 = _fixture.Create<string>();
        var client2 = _fixture.Create<string>();
        sut.TryAcquire(client1);
        sut.TryAcquire(client1);

        // Act
        var client1Result = sut.TryAcquire(client1);
        var client2Result = sut.TryAcquire(client2);

        // Assert
        client1Result.Should().BeFalse("client1 bucket is exhausted");
        client2Result.Should().BeTrue("client2 should be unaffected");
    }
}
