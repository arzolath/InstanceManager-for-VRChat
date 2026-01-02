using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using InstanceManager.Core.Blocks;
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

    public async Task<IReadOnlyCollection<BlockedUser>> GetBlockedUsersAsync(CancellationToken ct)
    {
        var items = await _rateLimiter.RunAsync(
            _ => Task.Run(() => _pmApi.GetPlayerModerations(type: PlayerModerationType.Block), ct),
            ct
        ).ConfigureAwait(false);

        var entries = items
            .Where(x => !string.IsNullOrWhiteSpace(x.TargetUserId))
            .GroupBy(x => x.TargetUserId!, StringComparer.Ordinal)
            .Select(g =>
            {
                var name = g.Select(i => i.TargetDisplayName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));
                return new BlockedUser(g.Key, string.IsNullOrWhiteSpace(name) ? null : name);
            })
            .ToArray();

        return entries;
    }
}
