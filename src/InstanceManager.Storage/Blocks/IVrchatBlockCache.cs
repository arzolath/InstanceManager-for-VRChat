using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InstanceManager.Core.Blocks;

namespace InstanceManager.Storage.Blocks;

public interface IVrchatBlockCache
{
    Task SaveAsync(IReadOnlyCollection<BlockedUser> blocks, CancellationToken ct);
    Task<IReadOnlyCollection<BlockedUser>> LoadAsync(CancellationToken ct);
    Task ClearAsync(CancellationToken ct);
}
