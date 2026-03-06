# Rate Limiter — Design Document

## Problem Choice

I chose the **Rate Limiter** (Chapter 4) because it maximizes the ratio of technical depth to implementation scope:

- **Distinct algorithms** with clear trade-offs between accuracy, memory, and performance.
- **Concurrency is inherent**, not forced — rate limiters *must* be thread-safe.
- **No external dependencies** needed — the prototype works entirely in-memory, which is appropriate for a working prototype. In production, the counters would live in Redis for distribution across nodes.
- **The scope is manageable** — I can implement the complete system (algorithms, middleware, API, tests) without cutting corners or overengineering.

---

## Architecture

```
Client → ASP.NET Core Pipeline → RateLimiterMiddleware → API Endpoint
                                       ↓
                                 IRateLimitAlgorithm
                                  (per-client state,
                                   in-memory)
```

**Middleware approach**: I placed the rate limiter as an ASP.NET Core middleware because:

1. It intercepts requests *before* they reach any endpoint — no wasted work on rate-limited requests.
2. It's transparent to the API endpoints — they don't need to know about rate limiting.
3. It follows the single responsibility principle.

**Client identification**: The middleware extracts the client ID from `X-Forwarded-For` (for reverse proxy setups) or falls back to `RemoteIpAddress`.

---

## Algorithm Selection

The book describes 5 algorithms. I implemented **2** — the ones that represent the most useful points in the trade-off space:

| Algorithm | Accuracy | Memory | Burst Handling | Why I chose it |
|-----------|----------|--------|---------------|----------------|
| **Token Bucket** | Good | O(1) per client | Allows controlled bursts up to bucket size | Best general-purpose choice. Used by Amazon and Stripe. |
| **Sliding Window Counter** | ~99.997% accurate | O(1) per client | Smooths boundary spikes via weighted estimate | Best accuracy-to-memory ratio. Used by Cloudflare. |

### Why NOT the other three

| Algorithm | Why I excluded it |
|-----------|------------------|
| **Fixed Window Counter** | Its only advantage is simplicity, but it allows up to **2x the limit** at window boundaries. Sliding Window Counter is strictly better — same O(1) memory, far better accuracy, and only marginally more complex. |
| **Sliding Window Log** | Most accurate, but stores **every timestamp** — O(n) memory per client. Impractical at scale. The Sliding Window Counter achieves 99.997% accuracy with O(1) memory. |
| **Leaky Bucket** | Very similar to Token Bucket but enforces a constant output rate (no bursts). This is a niche use case — most APIs *want* to allow short bursts. Token Bucket is more broadly useful. |

This selection shows that I understand all five algorithms and can make **deliberate trade-off decisions** rather than implementing everything because I can.

---

## Concurrency Strategy

Every algorithm must handle concurrent requests from the same client without race conditions.

### Per-client locking via `lock`

Each client gets its own lock object (stored inside the per-client state class). This means:

- **No global locks** — clients don't block each other.
- **Fine-grained** — only concurrent requests from the *same* client contend.
- **Simple and correct** — `lock` is well-understood and provides mutual exclusion.

### Why not `Interlocked`?

`Interlocked` operations are faster but only work for single atomic operations. Rate limiting algorithms require *multiple* reads and writes in a single critical section (e.g., refill tokens → check balance → decrement). A lock is the correct tool here.

### Why not Redis + Lua scripts?

For a working prototype, in-memory is sufficient. In production, you'd replace the `ConcurrentDictionary<string, Bucket>` with Redis and use Lua scripts for atomicity. The algorithm logic remains the same — only the storage layer changes.

---

## Testability

All algorithms accept a `TimeProvider` parameter (.NET 8+ abstraction). In production, this defaults to `TimeProvider.System`. In tests, I inject a `FakeTimeProvider` that allows deterministic time control — no sleeps, no flaky tests.

### Test coverage

- **Unit tests**: Each algorithm is tested for allow/deny, window/bucket reset, time-based behavior, boundary conditions, and client isolation.
- **Concurrency tests**: 50 concurrent tasks × 20 requests = 1,000 total requests against each algorithm. Asserts the allowed count never exceeds the configured limit.
- **Integration tests**: Uses `WebApplicationFactory` to test the full middleware pipeline — HTTP 200, HTTP 429, response headers, and JSON error body.

---

## What I intentionally did NOT implement

| Feature | Why not |
|---------|---------|
| **3 additional algorithms** | Token Bucket + Sliding Window Counter cover the main trade-offs. Adding more would be overengineering. |
| **Redis backend** | Adds infrastructure complexity without demonstrating algorithm knowledge. |
| **Distributed synchronization** | Requires multiple nodes. Out of scope for a single-process prototype. |
| **Per-endpoint rules** | Global middleware is sufficient. Per-endpoint is straightforward to add but adds complexity without new concepts. |

---

## AI Usage

I used **Gemini** (via Antigravity IDE assistant) throughout this project:

- **Architecture planning**: AI helped analyze the book's chapters and design the project structure.
- **Code generation**: Each algorithm was generated with AI assistance, then reviewed for correctness. Key design choices (TimeProvider injection, per-client locking) were discussed before implementation.
- **Test generation**: Test scaffolding was AI-assisted. Test scenarios (boundary conditions, concurrency, window resets) were designed collaboratively.
- **Everything was reviewed**: Every line of code was reviewed. The algorithms are documented with comments explaining trade-offs and decisions.

---

## How to run

```bash
# Build
cd rate-limiter
dotnet build

# Run tests
dotnet test

# Run the API
dotnet run --project src/RateLimiter

# Hit the endpoints
curl http://localhost:5244/api/ping
curl http://localhost:5244/api/resource
curl http://localhost:5244/api/config
```

### Configuration (`appsettings.json`)

```json
{
  "RateLimiter": {
    "Algorithm": "TokenBucket",
    "MaxRequests": 10,
    "WindowSeconds": 60,
    "RatePerSecond": 1.0
  }
}
```

Supported `Algorithm` values: `TokenBucket`, `SlidingWindowCounter`.
