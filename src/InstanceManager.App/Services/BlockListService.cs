using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using InstanceManager.Core.Blocks;
using InstanceManager.Storage.Blocks;
using InstanceManager.VRChat.Blocks;

namespace InstanceManager.App.Services;

public sealed class BlockListService : IBlockListService
{
    private readonly VrchatBlockApi _vrchat;
    private readonly ICustomBlockStore _store;
    private readonly IVrchatBlockCache _cache;
    private IReadOnlyCollection<BlockedUser>? _memoryCache;

    public BlockListService(VrchatBlockApi vrchat, ICustomBlockStore store, IVrchatBlockCache cache)
    {
        _vrchat = vrchat;
        _store = store;
        _cache = cache;
    }

    public async Task<IReadOnlyCollection<BlockedUser>> GetVrchatBlockedUsersAsync(CancellationToken ct, bool useCache = true)
    {
        if (useCache)
        {
            if (_memoryCache is { Count: > 0 })
                return _memoryCache;

            var cached = await _cache.LoadAsync(ct).ConfigureAwait(false);
            if (cached.Count > 0)
            {
                _memoryCache = cached;
                return cached;
            }
        }

        try
        {
            var live = await _vrchat.GetBlockedUsersAsync(ct).ConfigureAwait(false);
            _memoryCache = live;
            await _cache.SaveAsync(live, ct).ConfigureAwait(false);
            return live;
        }
        catch
        {
            // Fall back to whatever we already have so the UI isn't empty when offline/API fails
            if (_memoryCache is { Count: > 0 })
                return _memoryCache;

            var cached = await _cache.LoadAsync(ct).ConfigureAwait(false);
            if (cached.Count > 0)
            {
                _memoryCache = cached;
                return cached;
            }

            throw;
        }
    }

    public async Task<IReadOnlyCollection<string>> GetVrchatBlockedUserIdsAsync(CancellationToken ct, bool useCache = true)
    {
        var users = await GetVrchatBlockedUsersAsync(ct, useCache).ConfigureAwait(false);
        return users.Select(u => u.UserId).ToArray();
    }

    public Task<IReadOnlyCollection<string>> GetCustomBlockedUserIdsAsync(string ownerUserId, CancellationToken ct)
        => _store.LoadAsync(ownerUserId, ct);

    public async Task AddCustomBlockedUserIdAsync(string ownerUserId, string blockedUserId, CancellationToken ct)
    {
        var current = (await _store.LoadAsync(ownerUserId, ct).ConfigureAwait(false)).ToHashSet(StringComparer.Ordinal);
        current.Add(blockedUserId);
        await _store.SaveAsync(ownerUserId, current.ToArray(), ct).ConfigureAwait(false);
    }

    public async Task RemoveCustomBlockedUserIdAsync(string ownerUserId, string blockedUserId, CancellationToken ct)
    {
        var current = (await _store.LoadAsync(ownerUserId, ct).ConfigureAwait(false)).ToHashSet(StringComparer.Ordinal);
        current.Remove(blockedUserId);
        await _store.SaveAsync(ownerUserId, current.ToArray(), ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<string>> GetEffectiveBlockedUserIdsAsync(string ownerUserId, BlockSourceMode mode, CancellationToken ct)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);

        if (mode is BlockSourceMode.VrchatOnly or BlockSourceMode.Both)
        {
            foreach (var user in await GetVrchatBlockedUsersAsync(ct, useCache: true).ConfigureAwait(false))
                set.Add(user.UserId);
        }

        if (mode is BlockSourceMode.CustomOnly or BlockSourceMode.Both)
        {
            foreach (var id in await _store.LoadAsync(ownerUserId, ct).ConfigureAwait(false))
                set.Add(id);
        }

        return set.ToArray();
    }
}
