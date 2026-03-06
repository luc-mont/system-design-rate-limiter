using System.Collections.Concurrent;

namespace RateLimiter.Algorithms;

// Token Bucket: each client gets a bucket that refills at a fixed rate.
// Allows short bursts up to bucket capacity, unlike Sliding Window Counter which smooths traffic.
// Trade-off: simple and O(1) memory, but the two parameters (size + refill rate) need tuning.
public sealed class TokenBucketAlgorithm : IRateLimitAlgorithm
{
    private readonly int _bucketSize;
    private readonly double _refillRatePerSecond;
    private readonly ConcurrentDictionary<string, Bucket> _buckets = new();
    private readonly TimeProvider _timeProvider;

    public TokenBucketAlgorithm(int bucketSize, double refillRatePerSecond, TimeProvider? timeProvider = null)
    {
        if (bucketSize <= 0) throw new ArgumentOutOfRangeException(nameof(bucketSize));
        if (refillRatePerSecond < 0) throw new ArgumentOutOfRangeException(nameof(refillRatePerSecond));

        _bucketSize = bucketSize;
        _refillRatePerSecond = refillRatePerSecond;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public bool TryAcquire(string clientId)
    {
        var bucket = _buckets.GetOrAdd(clientId, _ => new Bucket(_bucketSize, _timeProvider.GetUtcNow()));
        return bucket.TryConsume(_bucketSize, _refillRatePerSecond, _timeProvider.GetUtcNow());
    }

    // Per-client state. Lock is per-client so different clients never contend.
    private sealed class Bucket
    {
        private double _tokens;
        private DateTimeOffset _lastRefill;
        private readonly object _lock = new();

        public Bucket(int initialTokens, DateTimeOffset now)
        {
            _tokens = initialTokens;
            _lastRefill = now;
        }

        public bool TryConsume(int bucketSize, double refillRate, DateTimeOffset now)
        {
            lock (_lock)
            {
                // Refill on access instead of using a background timer — avoids task/thread leaks.
                Refill(bucketSize, refillRate, now);

                if (_tokens < 1)
                    return false;

                _tokens -= 1;
                return true;
            }
        }

        private void Refill(int bucketSize, double refillRate, DateTimeOffset now)
        {
            var elapsed = (now - _lastRefill).TotalSeconds;
            if (elapsed <= 0) return;

            _tokens = Math.Min(bucketSize, _tokens + elapsed * refillRate);
            _lastRefill = now;
        }
    }
}
