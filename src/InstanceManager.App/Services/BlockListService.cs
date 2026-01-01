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

    public BlockListService(VrchatBlockApi vrchat, ICustomBlockStore store)
    {
        _vrchat = vrchat;
        _store = store;
    }

    public Task<IReadOnlyCollection<string>> GetVrchatBlockedUserIdsAsync(CancellationToken ct)
        => _vrchat.GetBlockedUserIdsAsync(ct);

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
            foreach (var id in await _vrchat.GetBlockedUserIdsAsync(ct).ConfigureAwait(false))
                set.Add(id);
        }

        if (mode is BlockSourceMode.CustomOnly or BlockSourceMode.Both)
        {
            foreach (var id in await _store.LoadAsync(ownerUserId, ct).ConfigureAwait(false))
                set.Add(id);
        }

        return set.ToArray();
    }
}
