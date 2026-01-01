using System;
using System.Threading;
using System.Threading.Tasks;

namespace InstanceManager.Core.RateLimiting;

public sealed class ApiRateLimiter : IApiRateLimiter
{
    private readonly FixedIntervalRateLimiter _inner = new(TimeSpan.FromSeconds(60));

    public Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
        => _inner.RunAsync(action, ct);

    public Task RunAsync(Func<CancellationToken, Task> action, CancellationToken ct)
        => _inner.RunAsync(action, ct);
}
