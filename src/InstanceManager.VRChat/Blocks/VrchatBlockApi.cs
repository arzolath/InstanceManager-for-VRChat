using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using InstanceManager.Core.RateLimiting;
using VRChat.API.Api;
using VRChat.API.Model;

namespace InstanceManager.VRChat.Blocks;

public sealed class VrchatBlockApi
{
    private readonly IApiRateLimiter _rateLimiter;
    private readonly PlayermoderationApi _pmApi;

    public VrchatBlockApi(IApiRateLimiter rateLimiter, PlayermoderationApi pmApi)
    {
        _rateLimiter = rateLimiter;
        _pmApi = pmApi;
    }

    public async Task<IReadOnlyCollection<string>> GetBlockedUserIdsAsync(CancellationToken ct)
    {
        // SDK method names vary. If yours differs, we’ll adjust.
        var items = await _rateLimiter.RunAsync(
            _ => Task.Run(() => _pmApi.GetPlayerModerations(type: PlayerModerationType.Block), ct),
            ct
        ).ConfigureAwait(false);


        var ids = items
            .Select(x => x.TargetUserId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return ids;
    }
}
