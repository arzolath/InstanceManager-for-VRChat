using System;
using System.Threading;
using System.Threading.Tasks;

namespace InstanceManager.Core.RateLimiting;

public interface IRateLimiter
{
    Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct);
    Task RunAsync(Func<CancellationToken, Task> action, CancellationToken ct);
}
