namespace RateLimiter.Tests;

/// <summary>
/// A controllable TimeProvider for deterministic tests.
/// Allows advancing time manually instead of relying on system clock.
/// </summary>
public sealed class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void Advance(TimeSpan duration)
    {
        _utcNow += duration;
    }

    public void SetUtcNow(DateTimeOffset value)
    {
        _utcNow = value;
    }
}
