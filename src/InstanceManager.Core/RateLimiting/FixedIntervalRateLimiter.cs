using System;
using System.Threading;
using System.Threading.Tasks;

namespace InstanceManager.Core.RateLimiting;

public sealed class FixedIntervalRateLimiter : IRateLimiter
{
    private readonly TimeSpan _interval;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTimeOffset _nextAllowed = DateTimeOffset.MinValue;

    public FixedIntervalRateLimiter(TimeSpan interval)
    {
        _interval = interval;
    }

    public async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        await WaitTurnAsync(ct).ConfigureAwait(false);
        return await action(ct).ConfigureAwait(false);
    }

    public async Task RunAsync(Func<CancellationToken, Task> action, CancellationToken ct)
    {
        await WaitTurnAsync(ct).ConfigureAwait(false);
        await action(ct).ConfigureAwait(false);
    }

    private async Task WaitTurnAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var now = DateTimeOffset.UtcNow;
            if (_nextAllowed > now)
            {
                var delay = _nextAllowed - now;
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }

            _nextAllowed = DateTimeOffset.UtcNow + _interval;
        }
        finally
        {
            _gate.Release();
        }
    }
}
