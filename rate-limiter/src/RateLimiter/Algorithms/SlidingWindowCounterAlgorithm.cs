using System.Collections.Concurrent;

namespace RateLimiter.Algorithms;

// Sliding Window Counter: estimates the request count in a rolling window using a weighted sum
// from the current and previous fixed windows. More accurate than a plain fixed window counter
// (smooths boundary spikes) while using O(1) memory per client.
// The assumption that previous-window requests are evenly distributed introduces only ~0.003%
// error according to Cloudflare's production data.
public sealed class SlidingWindowCounterAlgorithm : IRateLimitAlgorithm
{
    private readonly int _maxRequests;
    private readonly TimeSpan _windowSize;
    private readonly ConcurrentDictionary<string, WindowState> _states = new();
    private readonly TimeProvider _timeProvider;

    public SlidingWindowCounterAlgorithm(int maxRequests, TimeSpan windowSize, TimeProvider? timeProvider = null)
    {
        if (maxRequests <= 0) throw new ArgumentOutOfRangeException(nameof(maxRequests));
        if (windowSize <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(windowSize));

        _maxRequests = maxRequests;
        _windowSize = windowSize;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public bool TryAcquire(string clientId)
    {
        var now = _timeProvider.GetUtcNow();
        var state = _states.GetOrAdd(clientId, _ => new WindowState());
        return state.TryIncrement(now, _maxRequests, _windowSize);
    }

    // Per-client state. Lock scope is per-client.
    private sealed class WindowState
    {
        private long _currentWindowStart;
        private int _currentCount;
        private int _previousCount;
        private readonly object _lock = new();

        public bool TryIncrement(DateTimeOffset now, int maxRequests, TimeSpan windowSize)
        {
            lock (_lock)
            {
                var currentWindowStart = Quantize(now, windowSize);

                if (currentWindowStart != _currentWindowStart)
                {
                    if (currentWindowStart == _currentWindowStart + windowSize.Ticks)
                        _previousCount = _currentCount; // Slid one window forward.
                    else
                        _previousCount = 0; // Jumped multiple windows — stale data.

                    _currentWindowStart = currentWindowStart;
                    _currentCount = 0;
                }

                // Weighted estimate: current + previous * (% of previous window still in range).
                var elapsedInWindow = now.UtcTicks - currentWindowStart;
                var overlapRatio = 1.0 - ((double)elapsedInWindow / windowSize.Ticks);
                var estimate = _currentCount + (_previousCount * overlapRatio);

                if (estimate >= maxRequests)
                    return false;

                _currentCount++;
                return true;
            }
        }

        // Quantize timestamp to the nearest window boundary.
        private static long Quantize(DateTimeOffset now, TimeSpan windowSize)
        {
            var ticks = now.UtcTicks;
            return ticks - (ticks % windowSize.Ticks);
        }
    }
}
