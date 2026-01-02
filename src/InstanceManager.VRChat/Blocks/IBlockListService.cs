using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace InstanceManager.Core.Blocks;

public interface IBlockListService
{
    Task<IReadOnlyCollection<BlockedUser>> GetVrchatBlockedUsersAsync(CancellationToken ct, bool useCache = true);
    Task<IReadOnlyCollection<string>> GetVrchatBlockedUserIdsAsync(CancellationToken ct, bool useCache = true);
    Task<IReadOnlyCollection<string>> GetCustomBlockedUserIdsAsync(string ownerUserId, CancellationToken ct);

    Task AddCustomBlockedUserIdAsync(string ownerUserId, string blockedUserId, CancellationToken ct);
    Task RemoveCustomBlockedUserIdAsync(string ownerUserId, string blockedUserId, CancellationToken ct);

    Task<IReadOnlyCollection<string>> GetEffectiveBlockedUserIdsAsync(string ownerUserId, BlockSourceMode mode, CancellationToken ct);
}
