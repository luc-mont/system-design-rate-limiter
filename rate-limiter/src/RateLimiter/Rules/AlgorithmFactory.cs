using RateLimiter.Algorithms;
using RateLimiter.Configuration;

namespace RateLimiter.Rules;

public static class AlgorithmFactory
{
    public static IRateLimitAlgorithm Create(RateLimiterOptions options, TimeProvider? timeProvider = null)
    {
        return options.Algorithm switch
        {
            AlgorithmType.TokenBucket => new TokenBucketAlgorithm(
                options.MaxRequests,
                options.RatePerSecond,
                timeProvider),

            AlgorithmType.SlidingWindowCounter => new SlidingWindowCounterAlgorithm(
                options.MaxRequests,
                TimeSpan.FromSeconds(options.WindowSeconds),
                timeProvider),

            _ => throw new ArgumentOutOfRangeException(
                nameof(options),
                $"Unknown algorithm type: {options.Algorithm}")
        };
    }
}
