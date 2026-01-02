using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InstanceManager.Core.RateLimiting;
using VRChat.API.Api;
using VRChat.API.Model;

namespace InstanceManager.VRChat.Instances;

public sealed class VrchatInstanceApi
{
    private readonly IApiRateLimiter _rateLimiter;
    private readonly IVrchatApiContext _ctx;

    public VrchatInstanceApi(IApiRateLimiter rateLimiter, IVrchatApiContext ctx)
    {
        _rateLimiter = rateLimiter;
        _ctx = ctx;
    }

    private InstancesApi GetClient()
    {
        if (!_ctx.IsReady)
            throw new InvalidOperationException("VRChat client not ready. Please log in first.");

        return new InstancesApi(_ctx.Client, _ctx.Client, _ctx.Config);
    }

    public Task<IReadOnlyCollection<string>> GetRecentLocationsAsync(int? limit, int? offset, CancellationToken ct)
    {
        var client = GetClient();
        return _rateLimiter.RunAsync(
            _ => Task.Run(() => (IReadOnlyCollection<string>)client.GetRecentLocations(limit, offset), ct),
            ct
        );
    }

    public Task<Instance?> GetInstanceAsync(string worldId, string instanceId, CancellationToken ct)
    {
        var client = GetClient();
        return _rateLimiter.RunAsync(
            _ => Task.Run(() => client.GetInstance(worldId, instanceId), ct),
            ct
        );
    }
}
