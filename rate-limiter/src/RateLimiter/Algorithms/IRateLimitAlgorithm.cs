namespace RateLimiter.Algorithms;

public interface IRateLimitAlgorithm
{
    /// <returns>true if the request is allowed; false if rate-limited.</returns>
    bool TryAcquire(string clientId);
}
