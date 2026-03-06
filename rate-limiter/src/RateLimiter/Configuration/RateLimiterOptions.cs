using RateLimiter.Algorithms;

namespace RateLimiter.Configuration;

public sealed class RateLimiterOptions
{
    public const string SectionName = "RateLimiter";

    public AlgorithmType Algorithm { get; set; } = AlgorithmType.TokenBucket;
    public int MaxRequests { get; set; } = 10;
    public int WindowSeconds { get; set; } = 60;

    // Tokens/sec for Token Bucket. Not used by Sliding Window Counter.
    public double RatePerSecond { get; set; } = 1.0;
}
